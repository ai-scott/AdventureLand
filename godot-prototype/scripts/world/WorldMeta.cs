using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Per-world metadata node. Add as a child of the world scene root (or attach
/// the script to the root). Provides camera bounds and display name for banners.
///
/// Example placement:
///   World_01 (Node2D)
///   ├── WorldMeta (Node, this script)
///   │     MapSize = (720, 480)
///   │     WorldDisplayName = "Leafwood Forest"
///   └── ...
/// </summary>
public partial class WorldMeta : Node
{
    /// <summary>Map size in pixels. Used for camera bounds and edge-walking clamp.</summary>
    [Export] public Vector2I MapSize { get; set; } = new Vector2I(720, 480);

    /// <summary>Display name shown on the first-visit banner (e.g., "Leafwood Forest").</summary>
    [Export] public string WorldDisplayName { get; set; } = "";

    /// <summary>True for shop layouts (Blacksmith, Adventure Shop, General Store,
    /// Penny's House). Flips ShopState.IsActive on load so ItemTrigger routes
    /// item pickups through the purchase flow instead of collecting them free.</summary>
    [Export] public bool IsShop { get; set; } = false;

    public override void _Ready()
    {
        ShopState.SetActive(IsShop);
    }
}
