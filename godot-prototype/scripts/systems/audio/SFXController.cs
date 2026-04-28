using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Fire-and-forget sound-effect player. Pools a fixed set of
/// AudioStreamPlayer children so concurrent SFX (sword swing while taking
/// damage, etc.) don't cut each other off, and so we avoid per-call
/// allocation churn from Godot's play_one_shot pattern.
///
/// Register in Project → Autoload as:
///   Path: res://scripts/systems/audio/SFXController.cs
///   Name: SFXController
///
/// Convention: a Play(name) call resolves to
/// res://assets/audio/sfx/{name}.webm. Keep filenames in sync with this API
/// (lowercase_snake_case) so the call sites read the same as the assets.
///
/// Bus: SFX. Configured in default_bus_layout.tres alongside Music + VO.
/// </summary>
public partial class SFXController : Node
{
    public static SFXController Instance { get; private set; }

    /// <summary>Pool size. Eight is plenty for a top-down ARPG — if we
    /// exhaust it, we're spamming.</summary>
    private const int PoolSize = 8;

    private const string BusName = "SFX";
    private const string SfxRoot = "res://assets/audio/sfx/";

    private readonly Queue<AudioStreamPlayer> _free = new();
    private readonly Dictionary<AudioStreamPlayer, string> _activeName = new();

    /// <summary>Stream cache — lazy-loaded on first Play(name). Saves the
    /// disk-load cost on every subsequent Play of the same clip without
    /// preloading all clips at boot (most never play in a given session).</summary>
    private static readonly Dictionary<string, AudioStream> _streamCache = new();

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        for (int i = 0; i < PoolSize; i++)
        {
            var player = new AudioStreamPlayer
            {
                Name = $"SFXPlayer{i}",
                Bus = BusName,
            };
            player.Finished += () => OnPlayerFinished(player);
            AddChild(player);
            _free.Enqueue(player);
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Play a one-shot SFX by short name (e.g. "player_sword").
    /// Volume is in decibels; 0 is the file's authored level. If the pool
    /// is exhausted, logs a warning and skips — better than queuing,
    /// because a queued late-firing SFX would feel broken.</summary>
    public void Play(string name, float volumeDb = 0f)
    {
        if (string.IsNullOrEmpty(name)) return;

        var stream = LoadStream(name);
        if (stream == null) return;

        if (_free.Count == 0)
        {
            GD.PushWarning($"[SFXController] Pool exhausted ({PoolSize} concurrent SFX) — skipping '{name}'");
            return;
        }

        var player = _free.Dequeue();
        player.Stream = stream;
        player.VolumeDb = volumeDb;
        _activeName[player] = name;
        player.Play();
    }

    /// <summary>Stop every player currently sounding the given name. Used
    /// when a held sound (e.g. an enemy charge cue) needs to cut as the
    /// state machine leaves that branch.</summary>
    public void Stop(string name)
    {
        if (string.IsNullOrEmpty(name)) return;

        // Snapshot the keys — we mutate _activeName via OnPlayerFinished
        // when player.Stop() emits the Finished signal, so we can't iterate
        // the dict directly.
        var toStop = new List<AudioStreamPlayer>();
        foreach (var (player, activeName) in _activeName)
        {
            if (activeName == name) toStop.Add(player);
        }
        foreach (var player in toStop) player.Stop();
    }

    /// <summary>Stop all SFX. Called on layout transitions and game over so
    /// in-flight one-shots don't bleed into the next scene.</summary>
    public void StopAll()
    {
        var toStop = new List<AudioStreamPlayer>(_activeName.Keys);
        foreach (var player in toStop) player.Stop();
    }

    private void OnPlayerFinished(AudioStreamPlayer player)
    {
        _activeName.Remove(player);
        _free.Enqueue(player);
    }

    private static AudioStream LoadStream(string name)
    {
        if (_streamCache.TryGetValue(name, out var cached)) return cached;

        string path = $"{SfxRoot}{name}.webm";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[SFXController] Missing SFX file: {path}");
            // Cache the miss as null so we don't hammer the loader on every
            // call — a missing file isn't going to materialize at runtime.
            _streamCache[name] = null;
            return null;
        }

        var stream = GD.Load<AudioStream>(path);
        _streamCache[name] = stream;
        return stream;
    }
}
