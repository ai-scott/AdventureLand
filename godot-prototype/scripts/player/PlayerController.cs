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

    /// <summary>Dialogue sets this to freeze input without affecting facing.</summary>
    public bool InputLocked { get; set; } = false;

    private AnimationTree _tree;
    private AnimationNodeStateMachinePlayback _state;
    private Vector2 _facing = Vector2.Down;

    private const string AnimIdle = "Idle";
    private const string AnimWalk = "Walk";

    public override void _Ready()
    {
        _tree = GetNode<AnimationTree>("SpriteLayers/AnimationTree");
        _state = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/playback");
        _tree.Active = true;

        SetBlend(AnimIdle, _facing);
        _state.Travel(AnimIdle);
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
