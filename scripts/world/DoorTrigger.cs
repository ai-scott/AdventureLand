using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Interior door / portal. Player must walk into the Area2D and press the
/// interact action (Space or Enter) to transition. Shows a floating
/// "Enter" prompt while in range.
///
/// Used for:
/// - Exterior entrance to interior (e.g., village → blacksmith)
/// - Interior exit back to exterior (same trigger, reverse direction)
///
/// Target scene must have a Marker2D named "SpawnFromDoor_{DoorId}"
/// at the desired spawn position.
///
/// Listens on the `interact` action — InteractHintManager.IsHintVisible
/// suppresses PlayerController's attack swing while a door prompt is up,
/// so Space goes to the door instead of the sword.
/// </summary>
public partial class DoorTrigger : Area2D
{
	/// <summary>Scene file path, e.g., "res://scenes/worlds/World_00_Blacksmith.tscn"</summary>
	[Export(PropertyHint.File, "*.tscn")] public string TargetScene = "";

	/// <summary>Numeric ID matching the Marker2D "SpawnFromDoor_{DoorId}" in the target scene.</summary>
	[Export] public int DoorId = 1;

	/// <summary>Prompt text shown while the player is standing in the door's area.</summary>
	[Export] public string PromptText = "Enter";

	/// <summary>If set, the door only fires when QuestSystem.GetQuestStatus(RequiredQuestId) == RequiredQuestStatus.
	/// Used for quest-gated entries (e.g., Penny's House only opens after the cat quest).</summary>
	[Export] public string RequiredQuestId = "";
	[Export] public string RequiredQuestStatus = "";

	/// <summary>If set, the door only fires when QuestSystem.HasWorldFlag(RequiredWorldFlag).
	/// World flags survive save/load reliably (set in cutscenes via
	/// SetWorldFlag actions) — preferred over the quest gate for "one-way
	/// unlock" doors.</summary>
	[Export] public string RequiredWorldFlag = "";

	private bool _playerInRange;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _ExitTree()
	{
		InteractHintManager.Unregister(this);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		_playerInRange = true;
		InteractHintManager.Register(this, GetHintText);
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		_playerInRange = false;
		InteractHintManager.Unregister(this);
	}

	/// <summary>Hint text — returns empty while the door is quest-gated so the
	/// player sees no prompt at all (the door is effectively invisible until
	/// the story calls for it).</summary>
	private string GetHintText() => IsUnlocked() ? PromptText : "";

	private bool IsUnlocked()
	{
		// World flag gate — most-reliable post-cutscene unlock check.
		if (!string.IsNullOrEmpty(RequiredWorldFlag)
			&& !QuestSystem.HasWorldFlag(RequiredWorldFlag))
			return false;
		if (string.IsNullOrEmpty(RequiredQuestId)) return true;
		return QuestSystem.GetQuestStatus(RequiredQuestId) == RequiredQuestStatus;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange) return;
		if (!@event.IsActionPressed("interact")) return;

		if (string.IsNullOrEmpty(TargetScene))
		{
			GD.PushWarning($"[DoorTrigger] TargetScene not set on door {DoorId}");
			return;
		}

		if (!IsUnlocked()) return;

		if (WorldManager.IsTransitioning) return;

		if (DialogueManager.IsActive) return;

		InteractHintManager.Unregister(this);
		GetViewport().SetInputAsHandled();
		_ = WorldManager.GoToDoor(TargetScene, DoorId);
	}
}
