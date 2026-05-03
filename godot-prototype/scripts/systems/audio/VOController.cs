using Godot;
using System.Collections.Generic;
using System.Text;

namespace AdventureLandPrototype;

/// <summary>
/// Voice-over playback for dialogue nodes. One stream player on the VO bus —
/// a new line cuts the previous one (NPCs talking over each other never reads
/// well). Files live at
///   res://assets/audio/vo/{speaker}/{speaker}__{node_id}.ogg
/// with both speaker and node id lowered + snake-cased on lookup. Missing
/// files are a silent no-op so unrecorded nodes (player "You" responses, NPC
/// stragglers) don't spam the log.
///
/// Register in Project → Autoload as:
///   Path: res://scripts/systems/audio/VOController.cs
///   Name: VOController
/// </summary>
public partial class VOController : Node
{
    public static VOController Instance { get; private set; }

    private const string BusName = "VO";
    private const string MusicBusName = "Music";
    private const string VoRoot = "res://assets/audio/vo/";
    private const string VoExt = ".ogg";

    /// <summary>Music bus attenuation while VO is active. -6 dB ≈ 50% perceived
    /// volume — quiet enough that the VO sits clearly on top, loud enough that
    /// the score doesn't disappear and snap back jarringly when the line ends.</summary>
    private const float MusicDuckDb = -6f;

    private AudioStreamPlayer _player;
    private int _musicBusIdx = -1;
    private float _musicBusBaseDb = 0f;
    private bool _musicDucked = false;
    private static readonly Dictionary<string, AudioStream> _streamCache = new();

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        _player = new AudioStreamPlayer
        {
            Name = "VOPlayer",
            Bus = BusName,
        };
        // Restore music level the moment VO ends, regardless of how it ended
        // (natural end, Stop(), or being cut by another Play call).
        _player.Finished += UnduckMusic;
        AddChild(_player);

        _musicBusIdx = AudioServer.GetBusIndex(MusicBusName);
        if (_musicBusIdx < 0)
        {
            GD.PushWarning($"[VOController] '{MusicBusName}' bus not found — ducking disabled");
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Play VO for a dialogue node. Looks up
    /// {speaker.lower}/{speaker.lower}__{nodeId.snake}.ogg. If the file
    /// doesn't exist (most nodes), this is a silent no-op. A new call cuts
    /// any in-flight VO so dialogue auto-advance doesn't double-talk.</summary>
    public void Play(string speaker, string nodeId)
    {
        if (string.IsNullOrEmpty(speaker) || string.IsNullOrEmpty(nodeId)) return;
        // "You" is the player — never has VO. Cheap early exit so the cache
        // doesn't fill with player-response misses.
        if (speaker.Equals("You", System.StringComparison.OrdinalIgnoreCase)) return;

        // Speaker is just lowercased (existing files use "seamonster", not
        // "sea_monster" — the C3 webm names had no separator there). Node id
        // is PascalCase→snake_case so "WelcomeToAdventureLand" finds
        // "al__welcome_to_adventure_land.ogg".
        string folder = speaker.ToLowerInvariant();
        string node = ToSnakeCaseLower(nodeId);
        string key = $"{folder}__{node}";

        if (!_streamCache.TryGetValue(key, out var stream))
        {
            string path = $"{VoRoot}{folder}/{key}{VoExt}";
            // ResourceLoader.Exists is cheaper than Load + null-check and
            // doesn't print a [res] error for the unrecorded ~80% of nodes.
            stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
            _streamCache[key] = stream;
        }
        if (stream == null) return;

        if (_player.Playing) _player.Stop();
        _player.Stream = stream;
        DuckMusic();
        _player.Play();
    }

    public void Stop()
    {
        if (_player != null && _player.Playing) _player.Stop();
        UnduckMusic();
    }

    /// <summary>Drop the Music bus by MusicDuckDb. Captures the current bus
    /// level on the first duck so the user-set music volume (mute toggle,
    /// settings slider) is preserved when we restore. Idempotent.</summary>
    private void DuckMusic()
    {
        if (_musicBusIdx < 0 || _musicDucked) return;
        _musicBusBaseDb = AudioServer.GetBusVolumeDb(_musicBusIdx);
        AudioServer.SetBusVolumeDb(_musicBusIdx, _musicBusBaseDb + MusicDuckDb);
        _musicDucked = true;
    }

    private void UnduckMusic()
    {
        if (_musicBusIdx < 0 || !_musicDucked) return;
        AudioServer.SetBusVolumeDb(_musicBusIdx, _musicBusBaseDb);
        _musicDucked = false;
    }

    /// <summary>Convert "WelcomeToAdventureLand" → "welcome_to_adventure_land"
    /// and leave already-snake "greeting" / "accept_quest" alone. Matches the
    /// snake-case convention the converted .ogg files were named with.</summary>
    private static string ToSnakeCaseLower(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            // Treat boundary as: prev=lower/digit and current=upper, OR
            // current is the start of a run of uppers ending at a lower.
            // E.g. "WelcomeToAdventureLand" -> w_e_l_c_o_m_e_t_o_a_... wait we want the WORD boundary, not every cap.
            // Simpler: insert "_" before any upper that's NOT the first char and follows a lower or digit.
            bool isUpper = c >= 'A' && c <= 'Z';
            if (isUpper && i > 0)
            {
                char prev = s[i - 1];
                bool prevLower = (prev >= 'a' && prev <= 'z') || (prev >= '0' && prev <= '9');
                if (prevLower) sb.Append('_');
            }
            sb.Append(isUpper ? (char)(c + 32) : c);
        }
        return sb.ToString();
    }
}
