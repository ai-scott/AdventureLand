using Godot;

namespace AdventureLandPrototype;

public partial class NpcInteract : Area2D
{
	[Export] public string NpcName = "Villager";
	[Export] public string[] DialogueLines = new string[]
	{
		"Hello, traveler! Welcome to Leafwood Village.",
		"It's a peaceful place... mostly.",
        "Watch out for the forest to the east!"
	};

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
			if (dialogueManager != null && !dialogueManager.IsActive)
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
