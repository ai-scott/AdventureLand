using Godot;
using System.Threading.Tasks;

namespace AdventureLandPrototype;

/// <summary>
/// Scene-local sea-monster orchestrator. Owns the rise/retreat tweens and
/// gates the SM dialogue behind a fully-risen state. Lives on the
/// SeaMonster node in World_10 — DialogueManager finds it by walking the
/// current scene when SummonSeaMonster / SeaMonsterAcceptQuest /
/// SeaMonsterQuestComplete / MakeSeaMonsterHostile actions fire.
///
/// Simplified port of scripts/systems/npc/sea-monster-controller.ts:
///   Hidden ─Summon()─▶ Rising ─(rise tween done)─▶ NPC ─dialogue actions─▶ Retreating
///                                                                          │
///                                                              (retreat tween done)
///                                                                          ▼
///                                                                       Hidden
///
/// The "rise out of water" effect is driven by a shader on the Sprite2D —
/// pixels with UV.y > local_water_y go transparent. As the SM tweens up,
/// the controller writes local_water_y so the lake surface appears to be a
/// fixed world-Y line cutting through the sprite. No mask rectangle, no
/// blend-mode juggling — closer to "hide what's below the surface" than
/// C3's destination-in compositing trick, but reads identically in motion.
/// </summary>
public partial class SeaMonsterController : Node2D
{
    public enum State { Hidden, Rising, NPC, Hostile, Retreating }

    [Export] public NodePath SpritePath;
    [Export] public DialogueData Dialogue;
    [Export] public Texture2D AttackTexture;

    /// <summary>Optional CPUParticles2D node — the bubble swirl burst at the
    /// SM's base on summon and retreat. C3 used FX_WaterSwirl (a Particles
    /// object with the fx_waterswirl 9×7 PNG). When wired, the controller
    /// briefly emits on Summon and Retreat. Leave unset to skip the FX.</summary>
    [Export] public NodePath BubblesPath;

    /// <summary>World-Y of the lake surface. Pixels above (smaller Y) show,
    /// below get clipped by the shader. C3's lake water surface sits around
    /// y=210 in world coords on World_10 — adjust per-scene if the placed
    /// shell + SM aren't lining up.</summary>
    [Export] public float WaterLineWorldY = 210f;

    /// <summary>Underwater spawn Y — SM starts here, rises to the resting Y.</summary>
    [Export] public float SubmergedY = 320f;

    /// <summary>Resting Y when fully risen (final position from C3:
    /// scripts/systems/npc/sea-monster-controller.ts:185 ≈ 224).</summary>
    [Export] public float SurfaceY = 224f;

    [Export] public float RiseSeconds = 2.2f;
    [Export] public float RetreatSeconds = 2.0f;

    /// <summary>Water-ball projectile scene shot at the player while hostile.
    /// Optional — without it, hostile mode is purely visual.</summary>
    [Export] public PackedScene WaterBallScene;

    /// <summary>Hostile auto-retreat threshold. When the player walks beyond
    /// this radius (off the bridge / island), the SM submerges and waits
    /// for the next shell touch. Mirrors the C3 isPlayerOnIsland check.</summary>
    [Export] public float HostileLeashRadius = 240f;

    [Export] public float WaterBallInterval = 1.4f;

    private Sprite2D _sprite;
    private Texture2D _idleTexture;
    private ShaderMaterial _shaderMat;
    private CpuParticles2D _bubbles;
    private State _state = State.Hidden;
    private float _restingX;
    private double _waterBallTimer;
    private bool _pearlDeployed;

    /// <summary>True while a tween or active dialogue is in progress —
    /// PinkShell consults this to suppress repeat-summon presses.</summary>
    public bool IsBusy => _state == State.Rising || _state == State.NPC || _state == State.Retreating;

    public State GetState() => _state;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>(SpritePath);
        if (_sprite == null)
        {
            GD.PushWarning("[SeaMonster] SpritePath not set or wrong type — water-line shader disabled");
        }
        else
        {
            _idleTexture = _sprite.Texture;
            // Wrap the existing texture with our shader. The shader caches
            // the original alpha; setting local_water_y = 0 keeps it fully
            // hidden until Summon() begins the rise.
            var shader = GD.Load<Shader>("res://assets/sprites/npc/sea-monster/water_line.gdshader");
            if (shader != null)
            {
                _shaderMat = new ShaderMaterial { Shader = shader };
                _shaderMat.SetShaderParameter("local_water_y", 0.0f);
                _sprite.Material = _shaderMat;
            }
            else
            {
                GD.PushWarning("[SeaMonster] water_line.gdshader missing — falling back to plain visibility toggle");
            }
        }

        _restingX = Position.X;
        // Start hidden underwater — the Summon() call sets visibility back on.
        Position = new Vector2(_restingX, SubmergedY);
        Visible = false;

        _bubbles = BubblesPath != null && !BubblesPath.IsEmpty
            ? GetNodeOrNull<CpuParticles2D>(BubblesPath)
            : null;
        if (_bubbles != null)
        {
            // Authored-emitting on the scene side would fire on world load.
            // Force-disable until Summon/Retreat triggers a one-shot burst.
            _bubbles.Emitting = false;
        }
    }

    /// <summary>Rise from the depths. No-op if already risen or rising.</summary>
    public async void Summon()
    {
        if (_state == State.Rising || _state == State.NPC || _state == State.Hostile) return;
        if (_state == State.Retreating)
        {
            // Player came back fast — snap to hidden, then re-summon.
            Visible = false;
            _state = State.Hidden;
        }

        _state = State.Rising;
        Visible = true;
        Position = new Vector2(_restingX, SubmergedY);
        UpdateWaterLineUniform();

        SFXController.Instance?.Play("seamonster_rise");
        EmitBubbleBurst();

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(this, "position:y", SurfaceY, RiseSeconds);
        // Drive the shader uniform on every frame of the tween. We can't
        // bind a method-tween cleanly to a shader uniform in C#, so a
        // simple polling loop fed by the same SceneTree timer covers it.
        // The loop terminates on tween.IsRunning() rather than awaiting
        // the Finished signal afterward — Finished fires the same frame
        // the position lands, and an `await ToSignal(tween, Finished)`
        // AFTER the signal has already been emitted hangs forever in
        // Godot 4 (the awaiter never resumes). That's what was leaving
        // the SeaMonster silent + the pearl undeployed: this method
        // deadlocked here and never ran StartDialogue.
        while (_state == State.Rising && IsInstanceValid(this)
            && tween.IsValid() && tween.IsRunning())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            UpdateWaterLineUniform();
        }
        // Snap to final position in case the loop exited a frame early.
        if (IsInstanceValid(this))
        {
            Position = new Vector2(_restingX, SurfaceY);
            UpdateWaterLineUniform();
        }
        _state = State.NPC;
        // Pearl deploys after the very first interaction regardless of
        // dialogue branch. C3's flow only spawned it via accept/refuse
        // actions, but the user's design has it surface unconditionally
        // so even a hostile-on-greeting can come back, find the pearl,
        // and complete the loop.
        DeployPearlIfNeeded();
        StartDialogue();
    }

    private void DeployPearlIfNeeded()
    {
        if (_pearlDeployed) return;
        var scene = GetTree().CurrentScene;
        if (scene == null) return;
        var pickup = FindFirstItemTrigger(scene, "Pink Oyster Pearl");
        if (pickup == null) return;
        pickup.Visible = true;
        pickup.Monitoring = true;
        pickup.CollisionMask = 1; // re-enable player overlap (was zeroed in scene)
        _pearlDeployed = true;
    }

    private static ItemTrigger FindFirstItemTrigger(Node from, string itemName)
    {
        if (from is ItemTrigger t && t.Data?.Name == itemName) return t;
        foreach (var c in from.GetChildren())
        {
            var r = FindFirstItemTrigger(c, itemName);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>Submerge and hide. Triggered from dialogue actions
    /// (sea_monster_accept_quest, sea_monster_quest_complete) when the line
    /// resolves peacefully — the SM dives back, the controller resets to
    /// Hidden so the next shell touch can re-summon.</summary>
    public async void Retreat()
    {
        if (_state == State.Hidden || _state == State.Retreating) return;
        // Wipe danger music whenever the SM submerges, regardless of
        // whether it's a peaceful retreat or a hostile-leash retreat —
        // music shouldn't keep blaring "danger" once the threat is gone.
        MusicController.Instance?.SetDesiredMode(MusicController.Mode.Base);
        _state = State.Retreating;
        SFXController.Instance?.Play("seamonster_rise"); // same bubble cue
        EmitBubbleBurst();

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(this, "position:y", SubmergedY, RetreatSeconds);
        // Same async-deadlock fix as Summon — terminate on tween.IsRunning()
        // and snap-to-final, never await Finished after the fact.
        while (_state == State.Retreating && IsInstanceValid(this)
            && tween.IsValid() && tween.IsRunning())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            UpdateWaterLineUniform();
        }
        if (IsInstanceValid(this))
        {
            Position = new Vector2(_restingX, SubmergedY);
            UpdateWaterLineUniform();
        }
        Visible = false;
        _state = State.Hidden;
    }

    /// <summary>Switch to hostile combat mode: attack pose, danger music,
    /// and water-ball volleys aimed at the player. Auto-retreats when the
    /// player walks past HostileLeashRadius (drives them off the island
    /// per C3's isPlayerOnIsland behavior).</summary>
    public void MakeHostile()
    {
        _state = State.Hostile;
        if (AttackTexture != null && _sprite != null)
        {
            _sprite.Texture = AttackTexture;
        }
        MusicController.Instance?.SetDesiredMode(MusicController.Mode.High);
        _waterBallTimer = 0.6; // small delay so the first ball doesn't
                               // overlap the dialogue's last line audibly.
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_state != State.Hostile) return;

        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player == null) return;

        // Distance-based retreat — when the player gets clear of the island
        // (over the bridge), submerge and reset to peaceful music. The next
        // shell touch will re-summon and the dialogue's quest_status check
        // routes back into hostile_encounter_summon if the player still
        // doesn't have the pearl.
        float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
        if (dist > HostileLeashRadius)
        {
            MusicController.Instance?.SetDesiredMode(MusicController.Mode.Base);
            Retreat();
            return;
        }

        _waterBallTimer -= delta;
        if (_waterBallTimer <= 0)
        {
            _waterBallTimer = WaterBallInterval;
            FireWaterBall(player);
        }
    }

    private void FireWaterBall(Node2D player)
    {
        if (WaterBallScene == null) return;
        var ball = WaterBallScene.Instantiate<WaterBall>();
        // Spawn just in front of the SM's mouth — middle-ish of the sprite.
        Vector2 spawn = GlobalPosition + new Vector2(0, -16);
        var to = (player.GlobalPosition - spawn);
        if (to.Length() <= 0.001f) return;
        ball.Direction = to.Normalized();
        // Add to the world root so the ball persists if the player darts
        // around the SM and the leash kicks in mid-flight.
        GetTree().CurrentScene?.AddChild(ball);
        ball.GlobalPosition = spawn;
    }

    private void UpdateWaterLineUniform()
    {
        if (_shaderMat == null || _sprite == null) return;
        var tex = _sprite.Texture;
        if (tex == null) return;
        var size = tex.GetSize();
        if (size.Y <= 0) return;

        // World-Y of the sprite's top-left in pixels. Sprite2D's `offset`
        // shifts the texture relative to the node origin (see scene config:
        // offset = (0, -32)), so include it. The visible portion goes from
        // sprite_top → water_line, mapped to UV [0..local_water_y].
        float worldTop = GlobalPosition.Y + (_sprite.Offset.Y - size.Y * 0.5f);
        float visiblePx = WaterLineWorldY - worldTop;
        float uv = Mathf.Clamp(visiblePx / size.Y, 0f, 1f);
        _shaderMat.SetShaderParameter("local_water_y", uv);
    }

    /// <summary>One-shot bubble swirl at the SM's surface waterline. C3
    /// destroys + respawns FX_WaterSwirl on each event; we just toggle
    /// Emitting on the scene-authored CPUParticles2D (one_shot = true so
    /// the burst auto-stops). Both summon and retreat use the same effect
    /// — it's the "something's happening at the water surface" cue.</summary>
    private void EmitBubbleBurst()
    {
        if (_bubbles == null) return;
        // Restart cleanly — Emitting = false → true forces the one-shot
        // sequence to play even if a previous burst is still trailing off.
        _bubbles.Emitting = false;
        _bubbles.Restart();
    }

    private void StartDialogue()
    {
        if (Dialogue == null)
        {
            GD.PushWarning("[SeaMonster] No dialogue assigned — staying silent at the surface");
            return;
        }
        var dm = GetTree().Root.FindChild("DialogueManager", true, false) as DialogueManager;
        if (dm == null || dm.IsActive) return;
        dm.StartDialogue(Dialogue);
    }
}
