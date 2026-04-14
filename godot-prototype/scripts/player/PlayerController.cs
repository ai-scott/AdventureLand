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
///   CharacterBody2D (this script, in group "player")
///   ├── SpriteLayers (Node2D, MSCAFarmerSpriteLayers.gd)
///   │   ├── AnimationPlayer
///   │   ├── AnimationTree  (StateMachine + BlendSpace2D per state)
///   │   └── 20+ Sprite2D layers (01body, 13hair, 14head, etc.)
///   ├── HealthSystem (Node with HealthSystem.cs) — required for damage intake
///   ├── AttackHitbox (Area2D with CollisionShape2D, layer=4, mask=8) — required for attacks
///   └── CostumeController (Node with CostumeController.cs)
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

    [ExportGroup("Combat")]
    /// <summary>Which MSCA attack state to Travel to. OverhandStrike is the default one-hand swing.</summary>
    [Export] public string AttackAnimName = "OverhandStrike";
    /// <summary>How far in front of the player to position the hitbox, in pixels.</summary>
    [Export] public float HitboxOffset = 14f;

    [ExportGroup("Debug")]
    /// <summary>Tick on, run once, inspect Output panel for row-by-row color dump, then tick off.</summary>
    [Export] public bool DebugDumpHairRamps = false;
    /// <summary>Set to a valid row (-1 = disabled). Recolors hair to that row from the ramps sheet on start.</summary>
    [Export] public int DebugRecolorHairToRow = -1;

    /// <summary>Dialogue sets this to freeze input without affecting facing.</summary>
    public bool InputLocked { get; set; } = false;

    /// <summary>True while mid-attack — blocks movement input, gates re-press.</summary>
    public bool Attacking { get; private set; } = false;

    private AnimationTree _tree;
    private AnimationNodeStateMachinePlayback _state;
    private Node _spriteLayers;
    private Area2D _attackHitbox;
    private HealthSystem _health;
    private Vector2 _facing = Vector2.Down;

    private const string AnimIdle = "Idle";
    private const string AnimWalk = "Walk";

    private const string HairRampsPath = "res://assets/sprites/player/farmer/palettes/mana seed hair ramps.png";
    private const string HairBaseRampPath = "res://assets/sprites/player/farmer/palettes/base ramps/hair color base ramp.png";

    public override void _Ready()
    {
        AddToGroup("player");

        var animPlayer = GetNode<AnimationPlayer>("SpriteLayers/AnimationPlayer");
        _tree = GetNode<AnimationTree>("SpriteLayers/AnimationTree");
        _spriteLayers = GetNode<Node>("SpriteLayers");

        // Rebind the AnimationTree to the correct AnimationPlayer. MSCA saves
        // an absolute NodePath that breaks if the scene was re-rooted or moved.
        // Setting this at runtime works regardless of the saved path.
        _tree.AnimPlayer = _tree.GetPathTo(animPlayer);
        _tree.Active = true;

        _state = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/playback");

        SetBlend(AnimIdle, _facing);
        _state.Travel(AnimIdle);

        // Combat wiring — best-effort; missing nodes log a warning but don't crash.
        _attackHitbox = GetNodeOrNull<Area2D>("AttackHitbox");
        if (_attackHitbox != null)
        {
            _attackHitbox.Monitoring = false;
            _attackHitbox.AreaEntered += OnAttackHitboxAreaEntered;
        }
        else
        {
            GD.PushWarning("[PlayerController] No AttackHitbox Area2D found — attacks will not deal damage.");
        }

        _health = GetNodeOrNull<HealthSystem>("HealthSystem");
        if (_health == null)
        {
            GD.PushWarning("[PlayerController] No HealthSystem found — player cannot take damage.");
        }

        // Subscribe to MSCA's animation_set_hitbox signal. This is a GDScript signal on the
        // SpriteLayers node (MSCAFarmerSpriteLayers.gd). Signal args from the plugin source:
        //   (counter, track, timer_value, direction)
        // We gate the hitbox Monitoring for timer_value seconds.
        if (_spriteLayers != null)
        {
            var err = _spriteLayers.Connect("animation_set_hitbox",
                new Callable(this, nameof(OnAnimationSetHitbox)));
            if (err != Error.Ok)
            {
                GD.PushWarning($"[PlayerController] Could not connect animation_set_hitbox: {err}");
            }
        }

        // MSCA animation signals to detect attack start/end for the Attacking gate.
        if (_spriteLayers != null)
        {
            _spriteLayers.Connect("animation_state_started", new Callable(this, nameof(OnAnimStateStarted)));
            _spriteLayers.Connect("animation_state_finished", new Callable(this, nameof(OnAnimStateFinished)));
        }

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
        // Input: always zero when locked or mid-attack, else read the action axis.
        var input = (InputLocked || Attacking)
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
            // Only return to Idle if not attacking (attack state is handled via Travel from _Input).
            if (!Attacking)
            {
                SetBlend(AnimIdle, _facing);
                _state.Travel(AnimIdle);
            }

            Velocity = Velocity.MoveToward(Vector2.Zero, Friction * Speed * (float)delta);
        }

        MoveAndSlide();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (InputLocked || Attacking) return;
        if (!@event.IsActionPressed("attack")) return;

        StartAttack();
    }

    private void StartAttack()
    {
        // MSCA BlendSpace2D uses facing direction for the strike variant.
        SetBlend(AttackAnimName, _facing);
        _state.Travel(AttackAnimName);
        Attacking = true;
        // Position the hitbox in front of the player for the duration of the strike.
        if (_attackHitbox != null)
        {
            _attackHitbox.Position = _facing * HitboxOffset;
        }
    }

    /// <summary>Receives damage from an enemy's Hitbox. Routes to the HealthSystem.</summary>
    public void TakeDamage(int amount)
    {
        _health?.TakeDamage(amount);
    }

    /// <summary>Turn player to face a world-space target (e.g., NPC during dialogue).</summary>
    public void FaceTarget(Vector2 globalTargetPos)
    {
        var delta = globalTargetPos - GlobalPosition;
        if (delta == Vector2.Zero) return;

        _facing = SnapToCardinal(delta.Normalized());
        SetBlend(AnimIdle, _facing);
    }

    // ----- MSCA signal handlers (SpriteLayers emits these from GDScript) -----

    private void OnAnimationSetHitbox(int counter, int track, float timerValue, int direction)
    {
        // Turn on the hitbox for `timerValue` seconds during an attack animation.
        if (_attackHitbox == null) return;
        _attackHitbox.Monitoring = true;

        var timer = GetTree().CreateTimer(Mathf.Max(0.05f, timerValue));
        timer.Timeout += () =>
        {
            if (_attackHitbox != null) _attackHitbox.Monitoring = false;
        };
    }

    private void OnAnimStateStarted(string stateName)
    {
        if (stateName == AttackAnimName) Attacking = true;
    }

    private void OnAnimStateFinished(string stateName, float duration)
    {
        if (stateName == AttackAnimName)
        {
            Attacking = false;
            if (_attackHitbox != null) _attackHitbox.Monitoring = false;
        }
    }

    private void OnAttackHitboxAreaEntered(Area2D other)
    {
        // The other area should be an enemy's Hitbox (layer=8). We use the parent chain to
        // reach the EnemyController + its HealthSystem.
        var enemyRoot = other.GetParent() as Node;
        var enemyHealth = enemyRoot?.GetNodeOrNull<HealthSystem>("HealthSystem");
        if (enemyHealth == null) return;

        enemyHealth.TakeDamage(1);

        // Knockback: push enemy away from player for ~0.2s.
        if (enemyRoot is CharacterBody2D enemyBody)
        {
            var dir = (enemyBody.GlobalPosition - GlobalPosition).Normalized();
            if (dir == Vector2.Zero) dir = _facing;
            enemyBody.Velocity = dir * 200f;
        }
    }

    // ----- helpers -----

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
