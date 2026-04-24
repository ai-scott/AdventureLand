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

	/// <summary>If set, the door only fires when QuestSystem.GetQuestStatus(RequiredQuestId) == RequiredQuestStatus.
	/// Used for quest-gated entries (e.g., Penny's House only opens after the cat quest).</summary>
	[Export] public string RequiredQuestId = "";
	[Export] public string RequiredQuestStatus = "";

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

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		_playerInRange = true;
		InteractHintManager.Instance?.Register(this, GetHintText);
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		_playerInRange = false;
		InteractHintManager.Instance?.Unregister(this);
	}

	/// <summary>Hint text — returns empty while the door is quest-gated so the
	/// player sees no prompt at all (the door is effectively invisible until
	/// the story calls for it).</summary>
	private string GetHintText() => IsUnlocked() ? PromptText : "";

	private bool IsUnlocked()
	{
		if (string.IsNullOrEmpty(RequiredQuestId)) return true;
		return QuestSystem.GetQuestStatus(RequiredQuestId) == RequiredQuestStatus;
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

		if (!IsUnlocked()) return;

		var wm = WorldManager.Instance;
		if (wm == null || wm.IsTransitioning) return;

		var dialogue = GetTree().Root.FindChild("DialogueManager", true, false) as DialogueManager;
		if (dialogue != null && dialogue.IsActive) return;

		InteractHintManager.Instance?.Unregister(this);
		GetViewport().SetInputAsHandled();
		_ = wm.GoToDoor(TargetScene, DoorId);
	}
}
