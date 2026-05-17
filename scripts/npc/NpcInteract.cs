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

	/// <summary>Verb shown in the floating hint ("Talk", "Look", "Read", …).
	/// Signs override to "Look" via the base TreeSign.tscn.</summary>
	[Export] public string InteractVerb = "Talk";

	/// <summary>Data-driven dialogue (Phase 3+). Set via Inspector or code.
	/// Resource type (was strong-typed DialogueData) — DialogueData is
	/// GDScript now (Cluster 8), so the C# strong-type is dropped.
	/// Inspector still accepts it as a Resource slot.</summary>
	[Export] public Resource Dialogue;

	/// <summary>Legacy plain-text lines. Used if Dialogue is null.</summary>
	[Export] public string[] DialogueLines = System.Array.Empty<string>();

	[ExportGroup("Quest Gating")]
	/// <summary>If set, the NPC is invisible and non-interactable until
	/// QuestSystem.GetQuestStatus(RequiredQuestId) == RequiredQuestStatus.
	/// Used for NPCs who "deploy" mid-quest (e.g., Rosie only appears after
	/// Penny's cat quest reaches "Rosie_Found"). Empty = always present.</summary>
	[Export] public string RequiredQuestId = "";
	[Export] public string RequiredQuestStatus = "";

	/// <summary>If set, the NPC is hidden once this world flag exists/is
	/// truthy. Used for one-way "NPC moved out" states — e.g., Village Penny
	/// vanishes once `penny_home` is set after the PennyOpensHome cutscene.</summary>
	[Export] public string HideWhenWorldFlag = "";

	/// <summary>If set, the NPC is hidden until this world flag exists/is
	/// truthy. Inverse of HideWhenWorldFlag — e.g., PennysHouse Penny appears
	/// only after `penny_home` is set.</summary>
	[Export] public string RequiredWorldFlag = "";

	private bool _playerInRange = false;
	/// <summary>Latched true once the player opens dialogue with this NPC.
	/// Stays true until they walk out of the Area2D, blocking a rapid-fire
	/// retrigger where every E press re-opens the just-closed dialogue.
	/// (Otherwise the player gets soft-stuck in a talk/close/talk loop while
	/// standing on the NPC.)</summary>
	private bool _suppressUntilExit = false;
	private StaticBody2D _body;
	private uint _bodyDefaultLayer;
	private bool? _lastUnlocked;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		// Optional blocking body for NPCs that should stop the player (Penny,
		// Rosie). Stored so we can disable its collision layer while the quest
		// gate is locked — flipping only Visible would still let the player
		// bump into an invisible body.
		_body = GetNodeOrNull<StaticBody2D>("Body");
		if (_body != null) _bodyDefaultLayer = _body.CollisionLayer;

		ApplyQuestGate();
	}

	public override void _Process(double delta)
	{
		// Re-evaluate the gate each tick — dialogue actions flip quest state
		// at runtime without reloading the scene, so a one-shot check in
		// _Ready would leave Rosie frozen in her locked state even after
		// Penny's cat quest advances to Rosie_Found.
		if (_lastUnlocked != IsUnlocked()) ApplyQuestGate();

		if (_playerInRange && !_suppressUntilExit && Input.IsActionJustPressed("interact"))
		{
			// DialogueManager is now a GDScript-backed static facade
			// (Cluster 8). IsActive returns false when no DialogueManager
			// is in the current scene, so the dual null/IsActive guard
			// collapses to a single IsActive check.
			if (DialogueManager.IsActive) return;

			if (Dialogue != null)
			{
				DialogueManager.StartDialogue(Dialogue, this);
				_suppressUntilExit = true;
			}
			else if (DialogueLines.Length > 0)
			{
				DialogueManager.StartDialogue(NpcName, DialogueLines, this);
				_suppressUntilExit = true;
			}
		}
	}

	private void ApplyQuestGate()
	{
		bool unlocked = IsUnlocked();
		bool wasLocked = _lastUnlocked != true;
		Visible = unlocked;
		Monitoring = unlocked;
		if (_body != null) _body.CollisionLayer = unlocked ? _bodyDefaultLayer : 0u;
		_lastUnlocked = unlocked;

		// Godot's Area2D doesn't fire body_entered retroactively when
		// Monitoring flips on with a body already overlapping. If the player
		// is standing on Rosie at the moment she "deploys" mid-quest, that
		// would leave _playerInRange = false and pressing E would do nothing.
		// Defer one frame so the physics state is settled, then probe for an
		// overlapping player and synthesise OnBodyEntered if needed.
		if (unlocked && wasLocked)
		{
			CallDeferred(nameof(CheckPostUnlockOverlap));
		}
	}

	private void CheckPostUnlockOverlap()
	{
		if (!IsInsideTree() || !Monitoring) return;
		foreach (var body in GetOverlappingBodies())
		{
			if (body is PlayerController player)
			{
				OnBodyEntered(player);
				break;
			}
		}
	}

	private bool IsUnlocked()
	{
		// Hide if any forbidding flag is set.
		if (!string.IsNullOrEmpty(HideWhenWorldFlag) && IsFlagTruthy(HideWhenWorldFlag))
			return false;
		// Must-have flag — hide until set.
		if (!string.IsNullOrEmpty(RequiredWorldFlag) && !IsFlagTruthy(RequiredWorldFlag))
			return false;
		// Quest-status gate (exact match). If no quest gate configured, we pass.
		if (string.IsNullOrEmpty(RequiredQuestId)) return true;
		return QuestSystem.GetQuestStatus(RequiredQuestId) == RequiredQuestStatus;
	}

	private static bool IsFlagTruthy(string key)
	{
		var v = QuestSystem.GetWorldFlag(key);
		return !string.IsNullOrEmpty(v) && v != "false" && v != "0";
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is PlayerController)
		{
			_playerInRange = true;
			InteractHintManager.Register(this, GetHintText);
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is PlayerController)
		{
			_playerInRange = false;
			_suppressUntilExit = false;
			InteractHintManager.Unregister(this);
		}
	}

	public override void _ExitTree()
	{
		InteractHintManager.Unregister(this);
	}

	/// <summary>Provider for the shared InteractHintManager. Suppressed while
	/// dialogue is already on-screen with this NPC so we don't stack a hint
	/// on top of the dialogue box.</summary>
	private string GetHintText()
	{
		return _suppressUntilExit ? "" : InteractVerb;
	}
}
