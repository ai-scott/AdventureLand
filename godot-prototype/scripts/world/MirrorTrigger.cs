using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Interact-area in front of a mirror sprite (Player's Home, Penny's House).
/// When the player stands in range and presses interact, opens the inventory
/// UI. Reads as "the character checks themselves in the mirror" — a natural,
/// world-coupled affordance for inventory access on top of the standard I/Tab
/// keyboard toggle.
///
/// Scene structure:
///   MirrorTrigger (Area2D, this script)
///   └── CollisionShape2D (RectangleShape sized to the tile in front of mirror)
/// </summary>
public partial class MirrorTrigger : Area2D
{
    private bool _playerInRange;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _ExitTree()
    {
        InteractHintManager.Instance?.Unregister(this);
    }

    public override void _Process(double delta)
    {
        if (!_playerInRange) return;
        if (Input.IsActionJustPressed("interact"))
        {
            // Suppress further interact frames in the same press. Inventory's
            // Open() is idempotent so a double-fire wouldn't break anything,
            // but the hint should hide while the inventory is on-screen.
            InventoryUI.Instance?.Open();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not PlayerController) return;
        _playerInRange = true;
        InteractHintManager.Instance?.Register(this, () => "Look");
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is not PlayerController) return;
        _playerInRange = false;
        InteractHintManager.Instance?.Unregister(this);
    }
}
