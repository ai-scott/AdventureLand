using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Tiny autoload that runs once at game launch to set <see cref="UiStyles.IsMobile"/>
/// from the platform feature flag and runtime touchscreen probe. Lives in the
/// autoload list ahead of HUD/InventoryUI so any UI that branches on IsMobile
/// already has the answer when its own _Ready fires.
///
/// Debug shortcut: <b>Shift+M</b> toggles mobile mode at runtime — useful for
/// previewing the touch UI on a desktop without exporting. The toggle reloads
/// the current scene; HUD reacts each <c>_Process</c> tick to add/remove the
/// dpad as needed, so other autoloaded UI catches up immediately.
/// </summary>
public partial class MobileBoot : Node
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        bool mobile = UiStyles.DetectMobile();
        GD.Print($"[MobileBoot] IsMobile={mobile} (HasFeature(mobile)={OS.HasFeature("mobile")}, HasFeature(web)={OS.HasFeature("web")}, Touchscreen={DisplayServer.IsTouchscreenAvailable()})");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        // Shift+M (capital "M") — debug-only toggle for previewing touch UI
        // on a desktop. Plain M is the mute shortcut, so the shift modifier
        // gives us a sibling key for the related "switch input mode" action.
        if (key.Keycode != Key.M || !key.ShiftPressed) return;

        bool next = !UiStyles.IsMobile;
        UiStyles.SetMobileOverride(next);
        GD.Print($"[MobileBoot] IsMobile toggled → {next} (Shift+M)");
        // No scene reload — that would wipe the player's equipped items /
        // appearance / position. UiStyles.SetMobileOverride fires
        // MobileChanged; HUD watches IsMobile each _Process and the
        // InteractHintManager / DialogueManager autoloads subscribe to
        // MobileChanged to rebuild their boot-time UI in place.
    }
}
