using Godot;
using System;
using System.Text;

namespace AdventureLandPrototype;

public enum PerfMarkType : byte { Event, Begin, End }

public struct PerfMark
{
    public ulong TimeMs;
    public string Category;
    public string Detail;
    public int ElapsedMs;
    public PerfMarkType Type;
}

/// <summary>
/// Disposable scope returned by <see cref="PerfMonitor.Measure"/>. Records a
/// BEGIN mark on construction and an END mark with elapsed milliseconds on
/// dispose, so spike dumps can attribute stalls to in-flight operations.
/// </summary>
public readonly struct PerfScope : IDisposable
{
    private readonly string _category;
    private readonly string _detail;
    private readonly ulong _startMs;

    internal PerfScope(string category, string detail)
    {
        _category = category;
        _detail = detail;
        _startMs = Godot.Time.GetTicksMsec();
        PerfMonitor.Instance?.RecordMark(category, detail, PerfMarkType.Begin, 0);
    }

    public void Dispose()
    {
        var inst = PerfMonitor.Instance;
        if (inst == null) return;
        int elapsed = (int)(Godot.Time.GetTicksMsec() - _startMs);
        inst.RecordMark(_category, _detail, PerfMarkType.End, elapsed);
    }
}

/// <summary>
/// Always-on performance monitor with auto-spike capture and event marker API.
///
/// Press F3 in-game to toggle the live HUD. Frames whose duration exceeds
/// <c>SpikeThresholdMs</c> are recorded to <c>user://perf_log.csv</c> with the
/// prior 30 frames of context, the next 10 frames of recovery, and the most
/// recent event marks (so spikes can be attributed to in-flight operations).
///
/// Other systems instrument themselves via:
/// <code>using var _ = PerfMonitor.Measure("scene_transition", scenePath);</code>
/// or the lighter one-shot:
/// <code>PerfMonitor.Mark("npc_init", "Penny");</code>
///
/// Log path on macOS: ~/Library/Application Support/Godot/app_userdata/AdventureLandPrototype/perf_log.csv
/// </summary>
public partial class PerfMonitor : Node
{
    public static PerfMonitor Instance { get; private set; }

    [Export] public float SpikeThresholdMs { get; set; } = 18f;
    [Export] public int HistoryFrames { get; set; } = 600;
    [Export] public int SpikeContextFrames { get; set; } = 30;
    [Export] public int SpikePostFrames { get; set; } = 10;

    private const string LogPath = "user://perf_log.csv";
    private const int MarkRingSize = 32;
    private const int MarksDumpedPerSpike = 12;

    private double[] _frameMs;
    private double[] _processMs;
    private double[] _physicsMs;
    private byte[] _gen0Delta;
    private byte[] _gen1Delta;
    private byte[] _gen2Delta;
    private int _writeIdx;
    private long _totalFrames;

    private int _lastGen0, _lastGen1, _lastGen2;

    private int _spikeCount;
    private double _lastSpikeMs;

    private StringBuilder _activeDump;
    private int _postCaptureRemaining;

    private CanvasLayer _layer;
    private Label _label;
    private bool _overlayVisible;
    private double _refreshTimer;

    private readonly PerfMark[] _marks = new PerfMark[MarkRingSize];
    private int _markIdx;
    private long _totalMarks;

    /// <summary>One-shot marker. Use for instantaneous events (e.g. trigger entered).</summary>
    public static void Mark(string category, string detail) =>
        Instance?.RecordMark(category, detail, PerfMarkType.Event, 0);

    /// <summary>Disposable scope that brackets a region of code with BEGIN/END marks.
    /// Pattern: <c>using var _ = PerfMonitor.Measure("scene_transition", path);</c></summary>
    public static PerfScope Measure(string category, string detail) =>
        new PerfScope(category, detail);

    internal void RecordMark(string category, string detail, PerfMarkType type, int elapsedMs)
    {
        int idx = _markIdx;
        _markIdx = (idx + 1) % MarkRingSize;
        _totalMarks++;
        _marks[idx] = new PerfMark
        {
            TimeMs = Godot.Time.GetTicksMsec(),
            Category = category,
            Detail = detail,
            Type = type,
            ElapsedMs = elapsedMs
        };
    }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = int.MaxValue;

        _frameMs = new double[HistoryFrames];
        _processMs = new double[HistoryFrames];
        _physicsMs = new double[HistoryFrames];
        _gen0Delta = new byte[HistoryFrames];
        _gen1Delta = new byte[HistoryFrames];
        _gen2Delta = new byte[HistoryFrames];

        _lastGen0 = GC.CollectionCount(0);
        _lastGen1 = GC.CollectionCount(1);
        _lastGen2 = GC.CollectionCount(2);

        BuildOverlay();
        StartLogFile();

        GD.Print($"[PerfMonitor] armed. F3 toggles overlay. Spike threshold={SpikeThresholdMs}ms. Log: {ProjectSettings.GlobalizePath(LogPath)}");
    }

    private void BuildOverlay()
    {
        _layer = new CanvasLayer { Layer = 1000, Visible = false };
        AddChild(_layer);

        var bg = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.65f),
            AnchorLeft = 1, AnchorRight = 1,
            AnchorTop = 0, AnchorBottom = 0,
            OffsetLeft = -240, OffsetTop = 8,
            OffsetRight = -8, OffsetBottom = 152
        };
        _layer.AddChild(bg);

        _label = new Label
        {
            AnchorLeft = 1, AnchorRight = 1,
            AnchorTop = 0, AnchorBottom = 0,
            OffsetLeft = -232, OffsetTop = 14,
            OffsetRight = -16, OffsetBottom = 146
        };
        _label.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _label.AddThemeFontSizeOverride("font_size", 12);
        _label.Text = "Perf: warming up...";
        _layer.AddChild(_label);
    }

    private void StartLogFile()
    {
        using var f = FileAccess.Open(LogPath, FileAccess.ModeFlags.Write);
        if (f == null)
        {
            GD.PushWarning($"[PerfMonitor] could not open {LogPath} for writing");
            return;
        }
        f.StoreLine($"# PerfMonitor started {Godot.Time.GetDatetimeStringFromSystem()}");
        f.StoreLine($"# threshold={SpikeThresholdMs}ms history={HistoryFrames}f context={SpikeContextFrames}f post={SpikePostFrames}f");
        f.StoreLine($"# .NET GC server={System.Runtime.GCSettings.IsServerGC} latencyMode={System.Runtime.GCSettings.LatencyMode}");
        f.StoreLine("frame,frame_ms,proc_ms,phys_ms,gc0,gc1,gc2,note");
    }

    public override void _Process(double delta)
    {
        double frameMs = delta * 1000.0;
        double procMs = (double)Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
        double physMs = (double)Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;

        int g0 = GC.CollectionCount(0);
        int g1 = GC.CollectionCount(1);
        int g2 = GC.CollectionCount(2);
        byte d0 = (byte)Math.Min(g0 - _lastGen0, 255);
        byte d1 = (byte)Math.Min(g1 - _lastGen1, 255);
        byte d2 = (byte)Math.Min(g2 - _lastGen2, 255);
        _lastGen0 = g0; _lastGen1 = g1; _lastGen2 = g2;

        int idx = _writeIdx;
        _frameMs[idx] = frameMs;
        _processMs[idx] = procMs;
        _physicsMs[idx] = physMs;
        _gen0Delta[idx] = d0;
        _gen1Delta[idx] = d1;
        _gen2Delta[idx] = d2;
        _writeIdx = (idx + 1) % HistoryFrames;
        _totalFrames++;

        if (_postCaptureRemaining > 0 && _activeDump != null)
        {
            AppendCsvRow(_activeDump, _totalFrames, frameMs, procMs, physMs, d0, d1, d2,
                frameMs >= SpikeThresholdMs ? "SPIKE+POST" : "POST");
            _postCaptureRemaining--;
            if (_postCaptureRemaining == 0)
            {
                FlushDump();
            }
        }

        if (frameMs >= SpikeThresholdMs && _postCaptureRemaining == 0)
        {
            _spikeCount++;
            _lastSpikeMs = frameMs;
            BeginSpikeDump(idx, frameMs, d0, d1, d2);
        }

        _refreshTimer += delta;
        if (_overlayVisible && _refreshTimer >= 0.25)
        {
            _refreshTimer = 0;
            UpdateOverlay(frameMs, procMs);
        }
    }

    private void BeginSpikeDump(int spikeIdx, double frameMs, byte d0, byte d1, byte d2)
    {
        _activeDump = new StringBuilder(4096);
        _activeDump.Append("# SPIKE #").Append(_spikeCount)
                   .Append(" t=").Append(Godot.Time.GetTicksMsec()).Append("ms ")
                   .Append(frameMs.ToString("F2")).Append("ms");
        if (d0 + d1 + d2 > 0)
            _activeDump.Append(" GC[").Append(d0).Append(',').Append(d1).Append(',').Append(d2).Append(']');
        _activeDump.AppendLine();

        AppendRecentMarks(_activeDump);

        int available = (int)Math.Min(_totalFrames, HistoryFrames);
        int count = Math.Min(SpikeContextFrames, available);
        int start = (spikeIdx - count + 1 + HistoryFrames) % HistoryFrames;
        long firstFrame = _totalFrames - count + 1;

        for (int i = 0; i < count; i++)
        {
            int j = (start + i) % HistoryFrames;
            string note = (j == spikeIdx) ? "SPIKE" : "";
            AppendCsvRow(_activeDump, firstFrame + i, _frameMs[j], _processMs[j], _physicsMs[j],
                _gen0Delta[j], _gen1Delta[j], _gen2Delta[j], note);
        }

        _postCaptureRemaining = SpikePostFrames;
        if (_postCaptureRemaining == 0) FlushDump();
    }

    private void AppendRecentMarks(StringBuilder sb)
    {
        if (_totalMarks == 0) return;
        int count = (int)Math.Min(_totalMarks, MarksDumpedPerSpike);
        // Walk back from most-recent
        int newest = (_markIdx - 1 + MarkRingSize) % MarkRingSize;
        sb.Append("# RECENT MARKS (last ").Append(count).Append("):").AppendLine();
        // Print oldest of the window first for readability.
        int oldest = (newest - count + 1 + MarkRingSize) % MarkRingSize;
        for (int i = 0; i < count; i++)
        {
            int j = (oldest + i) % MarkRingSize;
            ref var m = ref _marks[j];
            sb.Append("#   t=").Append(m.TimeMs).Append("ms [").Append(m.Category).Append("] ")
              .Append(m.Detail);
            switch (m.Type)
            {
                case PerfMarkType.Begin: sb.Append(" (BEGIN)"); break;
                case PerfMarkType.End: sb.Append(" (END +").Append(m.ElapsedMs).Append("ms)"); break;
            }
            sb.AppendLine();
        }
    }

    private static void AppendCsvRow(StringBuilder sb, long frame, double frameMs, double procMs,
        double physMs, byte d0, byte d1, byte d2, string note)
    {
        sb.Append(frame).Append(',')
          .Append(frameMs.ToString("F2")).Append(',')
          .Append(procMs.ToString("F2")).Append(',')
          .Append(physMs.ToString("F2")).Append(',')
          .Append(d0).Append(',').Append(d1).Append(',').Append(d2).Append(',')
          .Append(note).AppendLine();
    }

    private void FlushDump()
    {
        if (_activeDump == null) return;
        using var f = FileAccess.Open(LogPath, FileAccess.ModeFlags.ReadWrite);
        if (f != null)
        {
            f.SeekEnd();
            f.StoreString(_activeDump.ToString());
            f.StoreLine("");
        }
        _activeDump = null;
    }

    private void UpdateOverlay(double frameMs, double procMs)
    {
        double fps = 1000.0 / Math.Max(frameMs, 0.001);
        _label.Text =
            $"FPS: {fps:F0}\n" +
            $"Frame: {frameMs:F2}ms\n" +
            $"Process: {procMs:F2}ms\n" +
            $"Spikes (>={SpikeThresholdMs:F0}ms): {_spikeCount}\n" +
            $"Last: {_lastSpikeMs:F1}ms\n" +
            $"GC 0/1/2: {_lastGen0}/{_lastGen1}/{_lastGen2}";
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is not InputEventKey key) return;
        if (!key.Pressed || key.Echo) return;
        if (key.Keycode != Key.F3) return;
        _overlayVisible = !_overlayVisible;
        if (_layer != null) _layer.Visible = _overlayVisible;
        if (_overlayVisible)
        {
            int last = (_writeIdx - 1 + HistoryFrames) % HistoryFrames;
            UpdateOverlay(_frameMs[last], _processMs[last]);
        }
    }
}
