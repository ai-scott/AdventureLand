using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Per-device preferences (not per-save-slot) — UI mode, audio toggles, etc.
/// Persisted to <c>user://prefs.cfg</c> via Godot's ConfigFile so changes survive
/// across launches and across save slots.
/// </summary>
public static class UserPrefs
{
    private const string Path = "user://prefs.cfg";
    private const string SectionUi = "ui";
    private const string KeyMobile = "mobile";
    private const string SectionAudio = "audio";
    private const string KeyMuted = "muted";

    /// <summary>Read the persisted mobile-mode preference, or <c>null</c> if
    /// the user has never made a choice on this device. <c>null</c> tells the
    /// caller to fall back to auto-detection.</summary>
    public static bool? GetMobileOverride()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return null;
        if (!cfg.HasSectionKey(SectionUi, KeyMobile)) return null;
        return (bool)cfg.GetValue(SectionUi, KeyMobile);
    }

    /// <summary>Persist the mobile-mode choice. Called once on title screen
    /// after auto-detection, or anytime the user toggles it from a settings
    /// screen.</summary>
    public static void SetMobileOverride(bool isMobile)
    {
        var cfg = new ConfigFile();
        cfg.Load(Path); // ignore failure — first launch creates the file
        cfg.SetValue(SectionUi, KeyMobile, isMobile);
        cfg.Save(Path);
    }

    /// <summary>Read the persisted mute state. Defaults to false (unmuted)
    /// if no preference has ever been written.</summary>
    public static bool GetMuted()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return false;
        if (!cfg.HasSectionKey(SectionAudio, KeyMuted)) return false;
        return (bool)cfg.GetValue(SectionAudio, KeyMuted);
    }

    public static void SetMuted(bool muted)
    {
        var cfg = new ConfigFile();
        cfg.Load(Path);
        cfg.SetValue(SectionAudio, KeyMuted, muted);
        cfg.Save(Path);
    }
}
