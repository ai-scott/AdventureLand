using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Player controller. Drives the MSCA-generated AnimationTree via
/// StateMachine.Travel() + BlendSpace2D blend_position vectors.
///
/// Attach to the CharacterBody2D root produced by MSCA's "Create Player Node"
/// (replacing the GDScript MSCAPlayer.gd). Keep MSCAFarmerSpriteLayers.gd
/// on the SpriteLayers child node — animations have keyframe function calls
/// into it for signals (hitbox, sound, state lifecycle).
///
/// Expected scene structure:
///   CharacterBody2D (this script)
///   └── SpriteLayers (Node2D, MSCAFarmerSpriteLayers.gd)
///       ├── AnimationPlayer
///       ├── AnimationTree  (StateMachine + BlendSpace2D per state)
///       └── 20+ Sprite2D layers (01body, 13hair, 14head, etc.)
///
/// MSCA state names use PascalCase (Idle, Walk). Direction vectors:
///   (0, 1)=Down  (1, 0)=Right  (0,-1)=Up  (-1, 0)=Left
/// BlendSpace2D is BLEND_MODE_DISCRETE; input snapped to cardinal to
/// avoid ambiguity at exact diagonals.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
	[Export] public float Speed = 80f;
	[Export] public float Acceleration = 10f;
	[Export] public float Friction = 10f;

	[ExportGroup("Debug")]
	/// <summary>Tick on, run once, inspect Output panel for row-by-row color dump, then tick off.</summary>
	[Export] public bool DebugDumpHairRamps = false;
	/// <summary>Set to a valid row (-1 = disabled). Recolors hair to that row from the ramps sheet on start.</summary>
	[Export] public int DebugRecolorHairToRow = -1;

	/// <summary>Dialogue sets this to freeze input without affecting facing.</summary>
	public bool InputLocked { get; set; } = false;

	private AnimationTree _tree;
	private AnimationNodeStateMachinePlayback _state;
	private Vector2 _facing = Vector2.Down;

	private const string AnimIdle = "Idle";
	private const string AnimWalk = "Walk";

	private const string HairRampsPath = "res://assets/sprites/player/_supporting files/palettes/mana seed hair ramps.png";
	private const string HairBaseRampPath = "res://assets/sprites/player/_supporting files/palettes/base ramps/hair color base ramp.png";

	public override void _Ready()
	{
		var animPlayer = GetNode<AnimationPlayer>("SpriteLayers/AnimationPlayer");
		_tree = GetNode<AnimationTree>("SpriteLayers/AnimationTree");

		// Rebind the AnimationTree to the correct AnimationPlayer. MSCA saves
		// an absolute NodePath that breaks if the scene was re-rooted or moved.
		// Setting this at runtime works regardless of the saved path.
		_tree.AnimPlayer = _tree.GetPathTo(animPlayer);
		_tree.Active = true;

		_state = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/playback");

		SetBlend(AnimIdle, _facing);
		_state.Travel(AnimIdle);

		// Debug: dump hair ramps so the user can pick a row
		if (DebugDumpHairRamps)
		{
			var rampsSheet = GD.Load<Texture2D>(HairRampsPath);
			PaletteSwapper.DumpRampSheet(rampsSheet, "hair ramps");
		}

		// Debug: recolor hair to a chosen row
		if (DebugRecolorHairToRow >= 0)
		{
			RecolorHair(DebugRecolorHairToRow);
		}
	}

	private void RecolorHair(int row)
	{
		var hair = GetNodeOrNull<Sprite2D>("SpriteLayers/13hair");
		var baseRamp = GD.Load<Texture2D>(HairBaseRampPath);
		var rampsSheet = GD.Load<Texture2D>(HairRampsPath);
		if (hair == null || baseRamp == null || rampsSheet == null)
		{
			GD.Print($"[Palette] missing asset: hair={hair != null} base={baseRamp != null} sheet={rampsSheet != null}");
			return;
		}
		var original = PaletteSwapper.ReadRampFromTexture(baseRamp);
		var replace = PaletteSwapper.ReadRampRow(rampsSheet, row);
		hair.Material = PaletteSwapper.CreateMaterial(original, replace);
		GD.Print($"[Palette] hair recolored: row {row}, {replace.Length} colors");
	}

	public override void _PhysicsProcess(double delta)
	{
		var input = InputLocked
			? Vector2.Zero
			: Input.GetVector("move_left", "move_right", "move_up", "move_down");

		if (input != Vector2.Zero)
		{
			input = input.Normalized();
			_facing = SnapToCardinal(input);

			SetBlend(AnimWalk, _facing);
			_state.Travel(AnimWalk);

			var target = input * Speed;
			Velocity = Velocity.MoveToward(target, Acceleration * Speed * (float)delta);
		}
		else
		{
			SetBlend(AnimIdle, _facing);
			_state.Travel(AnimIdle);

			Velocity = Velocity.MoveToward(Vector2.Zero, Friction * Speed * (float)delta);
		}

		MoveAndSlide();
	}

	/// <summary>Turn player to face a world-space target (e.g., NPC during dialogue).</summary>
	public void FaceTarget(Vector2 globalTargetPos)
	{
		var delta = globalTargetPos - GlobalPosition;
		if (delta == Vector2.Zero) return;

		_facing = SnapToCardinal(delta.Normalized());
		SetBlend(AnimIdle, _facing);
	}

	private void SetBlend(string animName, Vector2 direction)
	{
		_tree.Set($"parameters/{animName}/blend_position", direction);
	}

	private static Vector2 SnapToCardinal(Vector2 input)
	{
		// Horizontal dominates ties so east/west reads first for diagonal input.
		if (Mathf.Abs(input.X) >= Mathf.Abs(input.Y))
			return new Vector2(Mathf.Sign(input.X), 0);
		return new Vector2(0, Mathf.Sign(input.Y));
	}
}
