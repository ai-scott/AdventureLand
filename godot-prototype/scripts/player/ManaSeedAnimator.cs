using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Mana Seed FBAS walk animations — 3 directions + mirrored left.
/// Walk cycles: A, B, C, A(flip), B(flip), C(flip) for down/up.
/// Walk right: 6 unique frames (64-69), walk_left mirrors via FlipH.
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
    private bool _directionFlip = false;
    private bool _playing = false;

    private readonly struct AnimFrame
    {
        public readonly int CellId;
        public readonly int DurationMs;
        public readonly bool FlipH;

        public AnimFrame(int cellId, int durationMs, bool flipH = false)
        {
            CellId = cellId;
            DurationMs = durationMs;
            FlipH = flipH;
        }
    }

    private static readonly Dictionary<string, AnimFrame[]> Animations = new()
    {
        // Idle — dedicated stand poses matching C3 (frames 0, 16, 32 in the sheet)
        ["idle_down"]  = new[] { new AnimFrame(0,  1000) },
        ["idle_up"]    = new[] { new AnimFrame(16, 1000) },
        ["idle_right"] = new[] { new AnimFrame(32, 1000) },

        // Walk down: cells 48-50, then mirrored
        ["walk_down"] = new[]
        {
            new AnimFrame(48, WalkFrameMs),
            new AnimFrame(49, WalkFrameMs),
            new AnimFrame(50, WalkFrameMs),
            new AnimFrame(48, WalkFrameMs, flipH: true),
            new AnimFrame(49, WalkFrameMs, flipH: true),
            new AnimFrame(50, WalkFrameMs, flipH: true),
        },

        // Walk up: cells 52-54, then mirrored
        ["walk_up"] = new[]
        {
            new AnimFrame(52, WalkFrameMs),
            new AnimFrame(53, WalkFrameMs),
            new AnimFrame(54, WalkFrameMs),
            new AnimFrame(52, WalkFrameMs, flipH: true),
            new AnimFrame(53, WalkFrameMs, flipH: true),
            new AnimFrame(54, WalkFrameMs, flipH: true),
        },

        // Walk right: 6 unique frames, no per-frame flip needed
        ["walk_right"] = new[]
        {
            new AnimFrame(64, WalkFrameMs),
            new AnimFrame(65, WalkFrameMs),
            new AnimFrame(66, WalkFrameMs),
            new AnimFrame(67, WalkFrameMs),
            new AnimFrame(68, WalkFrameMs),
            new AnimFrame(69, WalkFrameMs),
        },
    };

    // Left variants mirror right via direction flip
    private static readonly Dictionary<string, string> MirrorMap = new()
    {
        ["idle_left"]  = "idle_right",
        ["walk_left"]  = "walk_right",
    };

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<Sprite2D>("Sprite2D");
        _sprite.RegionEnabled = true;
        _sprite.RegionRect = GetCellRect(0);
    }

    public void Play(string animName)
    {
        bool dirFlip = false;
        string resolved = animName;

        if (MirrorMap.TryGetValue(animName, out string mirror))
        {
            resolved = mirror;
            dirFlip = true;
        }

        if (!Animations.ContainsKey(resolved))
        {
            GD.PrintErr($"[ManaSeedAnimator] Animation not found: {animName}");
            return;
        }

        if (resolved == _currentAnim && dirFlip == _directionFlip && _playing)
            return;

        _currentAnim = resolved;
        _directionFlip = dirFlip;
        _currentFrame = 0;
        _frameTimer = 0;
        _playing = true;
        ApplyFrame();
    }

    public override void _Process(double delta)
    {
        if (!_playing || !Animations.ContainsKey(_currentAnim)) return;

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
        var frame = frames[_currentFrame];
        _sprite.RegionRect = GetCellRect(frame.CellId);
        _sprite.FlipH = frame.FlipH ^ _directionFlip;
    }

    private static Rect2 GetCellRect(int cellId)
    {
        int col = cellId % SheetColumns;
        int row = cellId / SheetColumns;
        return new Rect2(col * CellSize, row * CellSize, CellSize, CellSize);
    }
}
