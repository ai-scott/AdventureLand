using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Interior door / portal. Player must walk into the Area2D and press Enter
/// to transition. Shows a floating "Enter" prompt while in range.
///
/// Used for:
/// - Exterior entrance to interior (e.g., village → blacksmith)
/// - Interior exit back to exterior (same trigger, reverse direction)
///
/// Target scene must have a Marker2D named "SpawnFromDoor_{DoorId}"
/// at the desired spawn position.
///
/// We key specifically on the Enter keycode (not the `interact` action)
/// because that action also binds Space, which conflicts with `attack`.
/// Enter is a kid-friendly explicit confirmation that won't fire when the
/// player is mashing attack near a door.
/// </summary>
public partial class DoorTrigger : Area2D
{
	/// <summary>Scene file path, e.g., "res://scenes/worlds/World_00_Blacksmith.tscn"</summary>
	[Export(PropertyHint.File, "*.tscn")] public string TargetScene = "";

	/// <summary>Numeric ID matching the Marker2D "SpawnFromDoor_{DoorId}" in the target scene.</summary>
	[Export] public int DoorId = 1;

	/// <summary>Prompt text shown while the player is standing in the door's area.</summary>
	[Export] public string PromptText = "↵ Enter";

	private bool _playerInRange;
	private Label _prompt;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		BuildPrompt();
	}

	private void BuildPrompt()
	{
		_prompt = new Label
		{
			Text = PromptText,
			Size = new Vector2(80, 16),
			Position = new Vector2(-40, -26),
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false,
			ZIndex = 100,
		};
		_prompt.AddThemeColorOverride("font_color", Colors.White);
		_prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
		_prompt.AddThemeConstantOverride("outline_size", 3);
		AddChild(_prompt);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		_playerInRange = true;
		if (_prompt != null) _prompt.Visible = true;
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		_playerInRange = false;
		if (_prompt != null) _prompt.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange) return;
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
		if (key.Keycode != Key.Enter && key.Keycode != Key.KpEnter) return;

		if (string.IsNullOrEmpty(TargetScene))
		{
			GD.PushWarning($"[DoorTrigger] TargetScene not set on door {DoorId}");
			return;
		}

		var wm = WorldManager.Instance;
		if (wm == null || wm.IsTransitioning) return;

		var dialogue = GetTree().Root.FindChild("DialogueManager", true, false) as DialogueManager;
		if (dialogue != null && dialogue.IsActive) return;

		if (_prompt != null) _prompt.Visible = false;
		GetViewport().SetInputAsHandled();
		_ = wm.GoToDoor(TargetScene, DoorId);
	}
}
