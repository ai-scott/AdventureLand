using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Animates a Sprite2D using the Mana Seed cell-based system.
/// The spritesheet is a 16x16 grid of 64x64 cells (1024x1024 total).
/// Animations reference cells by ID (0-255) with per-frame timing.
/// Some frames are horizontally flipped to create left-facing variants.
/// </summary>
public partial class ManaSeedAnimator : Node
{
    private const int CellSize = 64;
    private const int SheetColumns = 16;

    private Sprite2D _sprite;
    private double _frameTimer = 0;
    private int _currentFrame = 0;
    private string _currentAnim = "";
    private bool _playing = false;

    // Animation definitions: name -> array of (cellId, durationMs)
    // ---------------------------------------------------------------
    // IMPORTANT: These cell IDs are read from the Mana Seed animation
    // guide. If animations look wrong, cross-reference your guide's
    // WALK section and update the cell IDs below.
    // ---------------------------------------------------------------
    private static readonly Dictionary<string, AnimFrame[]> Animations = new()
    {
        // === IDLE (from guide's IDLE section) ===
        // Cell 000 = front-facing standing pose
        ["idle_down"]       = new[] { new AnimFrame(000, 1000) },
        ["idle_up"]         = new[] { new AnimFrame(003, 1000) },
        ["idle_right"]      = new[] { new AnimFrame(002, 1000) },
        ["idle_down_right"] = new[] { new AnimFrame(001, 1000) },
        ["idle_up_right"]   = new[] { new AnimFrame(004, 1000) },

        // === WALK DOWN (from guide top-left, row 1) ===
        // Guide reads: 043(130) 049(130) 050(130) 055(130) 049(130) 050(115)
        ["walk_down"] = new[]
        {
            new AnimFrame(043, 130),
            new AnimFrame(049, 130),
            new AnimFrame(050, 130),
            new AnimFrame(055, 130),
            new AnimFrame(049, 130),
            new AnimFrame(050, 115),
        },

        // === WALK RIGHT (from guide, ~row 3) ===
        // TODO: Verify these cell IDs against your animation guide's
        // WALK section, right-facing row. Update if walk looks wrong.
        ["walk_right"] = new[]
        {
            new AnimFrame(052, 130),
            new AnimFrame(058, 130),
            new AnimFrame(059, 130),
            new AnimFrame(064, 130),
            new AnimFrame(058, 130),
            new AnimFrame(059, 115),
        },

        // === WALK UP (from guide, ~row 5) ===
        // TODO: Verify these cell IDs against your animation guide's
        // WALK section, up-facing row.
        ["walk_up"] = new[]
        {
            new AnimFrame(065, 130),
            new AnimFrame(070, 130),
            new AnimFrame(071, 130),
            new AnimFrame(076, 130),
            new AnimFrame(070, 130),
            new AnimFrame(071, 115),
        },

        // === WALK DIAGONALS ===
        // Down-Right uses its own row in the guide (~row 2)
        // TODO: Verify cell IDs
        ["walk_down_right"] = new[]
        {
            new AnimFrame(048, 130),
            new AnimFrame(054, 130),
            new AnimFrame(055, 130),
            new AnimFrame(060, 130),
            new AnimFrame(054, 130),
            new AnimFrame(055, 115),
        },

        // Up-Right uses its own row (~row 4)
        // TODO: Verify cell IDs
        ["walk_up_right"] = new[]
        {
            new AnimFrame(053, 130),
            new AnimFrame(059, 130),
            new AnimFrame(065, 130),
            new AnimFrame(071, 130),
            new AnimFrame(059, 130),
            new AnimFrame(065, 115),
        },
    };

    // Left-facing animations are mirrors of right-facing
    // down_left mirrors down_right, left mirrors right, up_left mirrors up_right
    private static readonly Dictionary<string, string> MirrorMap = new()
    {
        ["idle_left"]       = "idle_right",
        ["idle_down_left"]  = "idle_down_right",
        ["idle_up_left"]    = "idle_up_right",
        ["walk_left"]       = "walk_right",
        ["walk_down_left"]  = "walk_down_right",
        ["walk_up_left"]    = "walk_up_right",
    };

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<Sprite2D>("Sprite2D");

        // Enable region mode to show one cell at a time
        _sprite.RegionEnabled = true;
        _sprite.RegionRect = GetCellRect(0);
    }

    public void Play(string animName)
    {
        // Check if this is a mirrored animation
        bool shouldFlip = false;
        string resolvedName = animName;

        if (MirrorMap.TryGetValue(animName, out string mirrorSource))
        {
            resolvedName = mirrorSource;
            shouldFlip = true;
        }

        if (resolvedName == _currentAnim && _playing)
            return;

        if (!Animations.ContainsKey(resolvedName))
        {
            GD.PrintErr($"Animation not found: {animName} (resolved: {resolvedName})");
            return;
        }

        _currentAnim = resolvedName;
        _currentFrame = 0;
        _frameTimer = 0;
        _playing = true;
        _sprite.FlipH = shouldFlip;

        ApplyFrame();
    }

    public override void _Process(double delta)
    {
        if (!_playing || !Animations.ContainsKey(_currentAnim))
            return;

        var frames = Animations[_currentAnim];
        _frameTimer += delta * 1000; // Convert to ms

        if (_frameTimer >= frames[_currentFrame].DurationMs)
        {
            _frameTimer -= frames[_currentFrame].DurationMs;
            _currentFrame = (_currentFrame + 1) % frames.Length;
            ApplyFrame();
        }
    }

    private void ApplyFrame()
    {
        var frames = Animations[_currentAnim];
        int cellId = frames[_currentFrame].CellId;
        _sprite.RegionRect = GetCellRect(cellId);
    }

    private static Rect2 GetCellRect(int cellId)
    {
        int col = cellId % SheetColumns;
        int row = cellId / SheetColumns;
        return new Rect2(col * CellSize, row * CellSize, CellSize, CellSize);
    }

    private readonly struct AnimFrame
    {
        public readonly int CellId;
        public readonly int DurationMs;

        public AnimFrame(int cellId, int durationMs)
        {
            CellId = cellId;
            DurationMs = durationMs;
        }
    }
}
