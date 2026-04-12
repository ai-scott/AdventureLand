using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Animates a Sprite2D using the Mana Seed cell-based system.
/// Spritesheet: 16x16 grid of 64x64 cells (1024x1024 total).
/// Cells referenced by ID (0-255) with per-frame timing in ms.
/// Left-facing directions mirror their right-facing counterparts.
///
/// Cell IDs read from "farmer base animation guide.png" WALK section:
///   Row 1 (Down):        048, 049, 050, 048, 051
///   Row 2 (Down-Right):  052, 053, 054, 052, 055
///   Row 3 (Right):       064, 065, 066, 064, 067
///   Row 4 (Up-Right):    080, 081, 082, 080, 083
///   Row 5 (Up):          096, 097, 098, 096, 099
/// Walk cycle pattern per row: neutral, step-R, extend-R, neutral, step-L
/// </summary>
public partial class ManaSeedAnimator : Node
{
    private const int CellSize = 64;
    private const int SheetColumns = 16;
    private const int WalkFrameMs = 130;

    private Sprite2D _sprite;
    private double _frameTimer = 0;
    private int _currentFrame = 0;
    private string _currentAnim = "";
    private bool _playing = false;

    private static readonly Dictionary<string, AnimFrame[]> Animations = new()
    {
        // === IDLE ===
        // idle_down uses cell 000 (clean standing pose, top-left of sheet).
        // Other idle directions use the neutral pose from their walk cycle.
        ["idle_down"]       = new[] { new AnimFrame(0, 1000) },
        ["idle_down_right"] = new[] { new AnimFrame(52, 1000) },
        ["idle_right"]      = new[] { new AnimFrame(64, 1000) },
        ["idle_up_right"]   = new[] { new AnimFrame(80, 1000) },
        ["idle_up"]         = new[] { new AnimFrame(96, 1000) },

        // === WALK DOWN (front-facing) ===
        ["walk_down"] = new[]
        {
            new AnimFrame(48, WalkFrameMs),
            new AnimFrame(49, WalkFrameMs),
            new AnimFrame(50, WalkFrameMs),
            new AnimFrame(48, WalkFrameMs),
            new AnimFrame(51, WalkFrameMs),
        },

        // === WALK DOWN-RIGHT (3/4 front view) ===
        ["walk_down_right"] = new[]
        {
            new AnimFrame(52, WalkFrameMs),
            new AnimFrame(53, WalkFrameMs),
            new AnimFrame(54, WalkFrameMs),
            new AnimFrame(52, WalkFrameMs),
            new AnimFrame(55, WalkFrameMs),
        },

        // === WALK RIGHT (profile / side view) ===
        ["walk_right"] = new[]
        {
            new AnimFrame(64, WalkFrameMs),
            new AnimFrame(65, WalkFrameMs),
            new AnimFrame(66, WalkFrameMs),
            new AnimFrame(64, WalkFrameMs),
            new AnimFrame(67, WalkFrameMs),
        },

        // === WALK UP-RIGHT (3/4 back view) ===
        ["walk_up_right"] = new[]
        {
            new AnimFrame(80, WalkFrameMs),
            new AnimFrame(81, WalkFrameMs),
            new AnimFrame(82, WalkFrameMs),
            new AnimFrame(80, WalkFrameMs),
            new AnimFrame(83, WalkFrameMs),
        },

        // === WALK UP (back-facing) ===
        ["walk_up"] = new[]
        {
            new AnimFrame(96, WalkFrameMs),
            new AnimFrame(97, WalkFrameMs),
            new AnimFrame(98, WalkFrameMs),
            new AnimFrame(96, WalkFrameMs),
            new AnimFrame(99, WalkFrameMs),
        },
    };

    // Left-facing animations are mirrors of right-facing.
    // Mana Seed convention: flip right-facing sprites horizontally for left.
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
        _sprite.RegionEnabled = true;
        _sprite.RegionRect = GetCellRect(0);
    }

    public void Play(string animName)
    {
        bool shouldFlip = false;
        string resolvedName = animName;

        if (MirrorMap.TryGetValue(animName, out string mirrorSource))
        {
            resolvedName = mirrorSource;
            shouldFlip = true;
        }

        if (!Animations.ContainsKey(resolvedName))
        {
            GD.PrintErr($"Animation not found: {animName} (resolved: {resolvedName})");
            return;
        }

        // If already playing this animation with same flip, do nothing
        if (resolvedName == _currentAnim && _sprite.FlipH == shouldFlip && _playing)
            return;

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
        _frameTimer += delta * 1000;

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
        _sprite.RegionRect = GetCellRect(frames[_currentFrame].CellId);
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
