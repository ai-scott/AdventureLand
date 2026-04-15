using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// NPC interaction trigger. When the player is in range and presses interact,
/// starts dialogue from the attached DialogueData resource. Falls back to
/// legacy plain-text lines if no DialogueData is set.
/// </summary>
public partial class NpcInteract : Area2D
{
    [Export] public string NpcName = "Villager";

    /// <summary>Data-driven dialogue (Phase 3+). Set via Inspector or code.</summary>
    [Export] public DialogueData Dialogue;

    /// <summary>Legacy plain-text lines. Used if Dialogue is null.</summary>
    [Export] public string[] DialogueLines = System.Array.Empty<string>();

    private bool _playerInRange = false;
    private Label _interactLabel;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        _interactLabel = GetNodeOrNull<Label>("InteractLabel");
    }

    public override void _Process(double delta)
    {
        if (_playerInRange && Input.IsActionJustPressed("interact"))
        {
            var dialogueManager = GetTree().Root.FindChild("DialogueManager", true, false) as DialogueManager;
            if (dialogueManager == null || dialogueManager.IsActive) return;

            if (Dialogue != null)
            {
                dialogueManager.StartDialogue(Dialogue);
            }
            else if (DialogueLines.Length > 0)
            {
                dialogueManager.StartDialogue(NpcName, DialogueLines);
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is PlayerController)
        {
            _playerInRange = true;
            if (_interactLabel != null) _interactLabel.Visible = true;
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is PlayerController)
        {
            _playerInRange = false;
            if (_interactLabel != null) _interactLabel.Visible = false;
        }
    }
}
