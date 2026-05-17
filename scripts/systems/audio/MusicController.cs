using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Background-music controller. Two playback shapes:
///
///  - **Single track** via <see cref="StartTrack"/> — for worlds with one
///    composition (e.g. Town). One AudioStreamPlayer, looped.
///  - **3-layer crossfade mix** via <see cref="StartMix"/> — for worlds
///    that need mood shifts (e.g. Adventureland's Happy/Stress/Danger
///    layers). All three players are kept in lockstep at sample-accurate
///    sync; <see cref="SetDesiredMode"/> only fades volumes, never restarts
///    a layer, so transitions don't audibly hiccup.
///
/// VO ducking is a separate mechanism (<see cref="SetDuck"/> /
/// <see cref="ClearDuck"/>) that pulls the whole Music bus down by N dB
/// while VO plays, then restores. Keeps the inter-layer mix intact.
///
/// Register in Project → Autoload as:
///   Path: res://scripts/systems/audio/MusicController.cs
///   Name: MusicController
///
/// Bus: Music (configured in default_bus_layout.tres).
/// </summary>
public partial class MusicController : Node
{
    public static MusicController Instance { get; private set; }

    public enum Mode { Base, Mid, High }

    private const string BusName = "Music";
    private const string MusicRoot = "res://assets/audio/music/";
    private const string MusicExt = ".ogg";
    /// <summary>"Silent" target for the inactive layers. -80 dB is below
    /// human hearing on any reasonable playback chain — the layer is
    /// inaudible but the player keeps running so the layers stay in sync.</summary>
    private const float SilentDb = -80f;

    private AudioStreamPlayer _base, _mid, _high, _single;
    private string _baseName, _midName, _highName, _singleName;
    private Mode _mode = Mode.Base;
    private Tween _crossfadeTween;
    /// <summary>The Music bus's authored volume, captured once at boot. Duck
    /// values are subtracted from this; ClearDuck restores it. Keeping the
    /// reference symmetric prevents drift when SetDuck is called repeatedly.</summary>
    private float _nominalBusDb = 0f;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        int idx = AudioServer.GetBusIndex(BusName);
        if (idx >= 0) _nominalBusDb = AudioServer.GetBusVolumeDb(idx);
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Play a single-track world's music. Idempotent — re-calling
    /// with the same name while it's already playing is a no-op, so per-world
    /// _Ready hooks can call this freely without needing to know what's
    /// playing.</summary>
    public void StartTrack(string name)
    {
        if (_single != null && _singleName == name) return;

        StopMix();

        var stream = LoadStream(name);
        if (stream == null) return;

        _single = NewPlayer($"Music_Single_{name}");
        _single.Stream = stream;
        _single.VolumeDb = 0f;
        _single.Play();
        _singleName = name;
    }

    /// <summary>Start the layered mix. Loads all three streams up-front;
    /// if any fails, none start (avoids a partially-alive mix where mode
    /// transitions silently miss a layer). Idempotent on the same triplet.</summary>
    public void StartMix(string baseName, string midName, string highName)
    {
        if (_base != null
            && _baseName == baseName && _midName == midName && _highName == highName)
            return;

        StopMix();

        var baseStream = LoadStream(baseName);
        var midStream = LoadStream(midName);
        var highStream = LoadStream(highName);
        if (baseStream == null || midStream == null || highStream == null)
        {
            GD.PushWarning($"[MusicController] StartMix({baseName}/{midName}/{highName}) — one or more layers failed to load; mix not started");
            return;
        }

        _base = NewPlayer($"Music_Base_{baseName}");
        _mid = NewPlayer($"Music_Mid_{midName}");
        _high = NewPlayer($"Music_High_{highName}");

        _base.Stream = baseStream;
        _mid.Stream = midStream;
        _high.Stream = highStream;

        _base.VolumeDb = 0f;
        _mid.VolumeDb = SilentDb;
        _high.VolumeDb = SilentDb;

        // Three .Play() calls within the same _Ready tick — Godot starts
        // them on the same audio frame so the layers stay sample-aligned.
        _base.Play();
        _mid.Play();
        _high.Play();

        _baseName = baseName;
        _midName = midName;
        _highName = highName;
        _mode = Mode.Base;
    }

    /// <summary>Stop and free everything currently playing on the Music
    /// bus — both single and mix shapes. Safe to call when nothing is
    /// playing.</summary>
    public void StopMix()
    {
        _crossfadeTween?.Kill();
        _crossfadeTween = null;

        FreePlayer(ref _base);
        FreePlayer(ref _mid);
        FreePlayer(ref _high);
        FreePlayer(ref _single);

        _baseName = _midName = _highName = _singleName = null;
        _mode = Mode.Base;
    }

    /// <summary>Crossfade the layered mix to the requested mode. No-op if
    /// no mix is loaded (a single-track world ignores mode requests). Any
    /// in-flight crossfade is cancelled before the new one starts.</summary>
    public void SetDesiredMode(Mode mode, float fadeSec = 0.4f)
    {
        if (_base == null) return;
        if (_mode == mode) return;

        _crossfadeTween?.Kill();
        _crossfadeTween = CreateTween().SetParallel(true);

        // Tween LINEAR amplitude (0..1), not dB. A straight dB lerp from
        // 0 → -80 sounds like "drops fast, fades in late": dB is
        // logarithmic, so most of the perceived loudness change happens
        // in the first ~10 dB of the cut. Linear amplitude tweened in
        // parallel produces a true crossfade where outgoing and incoming
        // layers cross at roughly half-loudness mid-fade.
        TweenLayerGain(_base, mode == Mode.Base ? 1f : 0f, fadeSec);
        TweenLayerGain(_mid,  mode == Mode.Mid  ? 1f : 0f, fadeSec);
        TweenLayerGain(_high, mode == Mode.High ? 1f : 0f, fadeSec);

        _mode = mode;
    }

    /// <summary>Tween a player's linear amplitude (0..1) over `duration`,
    /// converting to volume_db each tick. Starting amplitude is read from
    /// the player's current volume_db so an in-flight fade interrupted
    /// mid-crossfade resumes from where it actually is, not from 0/1.</summary>
    private void TweenLayerGain(AudioStreamPlayer player, float targetGain, float duration)
    {
        float startGain = DbToLinear(player.VolumeDb);
        _crossfadeTween.TweenMethod(
            Callable.From<float>(g => player.VolumeDb = LinearToDb(g)),
            startGain,
            targetGain,
            duration);
    }

    private static float LinearToDb(float gain)
    {
        // Clamp the floor: anything below ~0.0001 maps to SilentDb so the
        // tween's tail doesn't push volume_db to -inf and audibly tick.
        if (gain <= 0.0001f) return SilentDb;
        return 20f * Mathf.Log(gain) / Mathf.Log(10f);
    }

    private static float DbToLinear(float db)
    {
        if (db <= SilentDb + 0.5f) return 0f;
        return Mathf.Pow(10f, db / 20f);
    }

    /// <summary>Duck the Music bus by `db` (positive number — magnitude of
    /// the cut). Symmetric with <see cref="ClearDuck"/>: both write
    /// absolute values relative to the boot-captured nominal, so calling
    /// SetDuck repeatedly doesn't accumulate drift.</summary>
    public void SetDuck(float db)
    {
        int idx = AudioServer.GetBusIndex(BusName);
        if (idx < 0) return;
        AudioServer.SetBusVolumeDb(idx, _nominalBusDb - Mathf.Abs(db));
    }

    public void ClearDuck()
    {
        int idx = AudioServer.GetBusIndex(BusName);
        if (idx < 0) return;
        AudioServer.SetBusVolumeDb(idx, _nominalBusDb);
    }

    private AudioStreamPlayer NewPlayer(string playerName)
    {
        var p = new AudioStreamPlayer { Name = playerName, Bus = BusName };
        AddChild(p);
        return p;
    }

    private static void FreePlayer(ref AudioStreamPlayer p)
    {
        if (p == null) return;
        p.Stop();
        p.QueueFree();
        p = null;
    }

    private static AudioStream LoadStream(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        using var _perf = PerfMonitor.Measure("music_load", name);
        string path = $"{MusicRoot}{name}{MusicExt}";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[MusicController] Missing music file: {path}");
            return null;
        }
        return GD.Load<AudioStream>(path);
    }
}
