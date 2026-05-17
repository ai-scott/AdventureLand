using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Player-interaction trigger for the lake's pink shell. Pressing the
/// interact key while the player overlaps fires the sea monster's
/// summon/dialogue sequence — the shell is the canonical way the player
/// invokes the SM in C3 (per scripts/systems/npc/sea-monster-controller.ts).
///
/// Mirrors NpcInteract's range/suppress pattern but skips the InteractHint
/// because the shell already reads as obviously interactable, and skips the
/// per-NPC quest-gate scaffolding (the dialogue's own quest_status priority
/// rules pick the right branch — first-time vs has-pearl vs hostile-return).
/// </summary>
public partial class PinkShellInteract : Area2D
{
    /// <summary>Path to the SeaMonster node — typically "../SeaMonster".
    /// Optional: if unset, scan the current scene for a child of type
    /// SeaMonsterController.</summary>
    [Export] public NodePath SeaMonsterPath;

    private bool _playerInRange;
    private bool _suppressUntilExit;

    public override void _Ready()
    {
        BodyEntered += b => { if (b is PlayerController) { _playerInRange = true; InteractHintManager.Instance?.Register(this, () => _suppressUntilExit ? "" : "Touch"); } };
        BodyExited += b => { if (b is PlayerController) { _playerInRange = false; _suppressUntilExit = false; InteractHintManager.Instance?.Unregister(this); } };
    }

    public override void _Process(double delta)
    {
        if (!_playerInRange || _suppressUntilExit) return;
        if (!Input.IsActionJustPressed("interact")) return;

        var sm = GetSeaMonster();
        if (sm == null)
        {
            GD.PushWarning("[PinkShell] No SeaMonsterController found in scene — interact ignored");
            return;
        }
        // Block re-summon while a sequence is already in flight. The
        // controller itself also guards this, but bailing here keeps the
        // hint suppressed until the player walks off the shell.
        if (sm.IsBusy) return;

        sm.Summon();
        _suppressUntilExit = true;
    }

    public override void _ExitTree()
    {
        InteractHintManager.Instance?.Unregister(this);
    }

    private SeaMonsterController GetSeaMonster()
    {
        if (SeaMonsterPath != null && !SeaMonsterPath.IsEmpty)
        {
            return GetNodeOrNull<SeaMonsterController>(SeaMonsterPath);
        }
        // Fallback: walk the scene root for the first SeaMonsterController.
        var root = GetTree().CurrentScene;
        return root?.FindChild("*", true, false) is SeaMonsterController smc
            ? smc
            : FindFirstByType<SeaMonsterController>(root);
    }

    private static T FindFirstByType<T>(Node from) where T : Node
    {
        if (from == null) return null;
        if (from is T match) return match;
        foreach (var c in from.GetChildren())
        {
            var r = FindFirstByType<T>(c);
            if (r != null) return r;
        }
        return null;
    }
}
