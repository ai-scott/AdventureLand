using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Map-edge transition trigger. Placed as a thin Area2D strip just outside
/// the map on one edge. When player walks off that edge into this strip,
/// transitions to the adjacent world in the grid.
///
/// The player enters the target world at the opposite edge with perpendicular
/// coordinate preserved (matches C3 behavior from eGameRoom).
///
/// Placement convention: each world has up to 4 EdgeTriggers, one per cardinal
/// direction, positioned just outside the map bounds:
///   - East trigger:  at x = mapWidth, spanning full height
///   - West trigger:  at x = -8, spanning full height
///   - North trigger: at y = -16, spanning full width
///   - South trigger: at y = mapHeight, spanning full width
///
/// If an edge leads nowhere (grid boundary), omit the trigger — the player
/// will just walk into empty space (collision boundary to be added later).
/// </summary>
public partial class EdgeTrigger : Area2D
{
    /// <summary>Scene path to transition to, e.g., "res://scenes/worlds/World_01.tscn"</summary>
    [Export(PropertyHint.File, "*.tscn")] public string TargetScene = "";

    /// <summary>Which edge the player is EXITING through. The target scene places them on the OPPOSITE edge.</summary>
    [Export] public EdgeDirection ExitEdge = EdgeDirection.East;

    public enum EdgeDirection { East, West, North, South }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private async void OnBodyEntered(Node2D body)
    {
        if (!body.IsInGroup("player")) return;
        if (string.IsNullOrEmpty(TargetScene))
        {
            GD.PushWarning($"[EdgeTrigger] TargetScene not set");
            return;
        }

        var wm = WorldManager.Instance;
        if (wm == null || wm.IsTransitioning) return;

        // Don't fire during dialogue.
        var dialogue = GetTree().Root.FindChild("DialogueManager", true, false) as DialogueManager;
        if (dialogue != null && dialogue.IsActive) return;

        string edgeStr = ExitEdge.ToString().ToLowerInvariant();
        await wm.GoToEdge(TargetScene, edgeStr, body.GlobalPosition);
    }
}
