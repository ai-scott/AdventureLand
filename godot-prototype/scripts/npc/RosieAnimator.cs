using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Rosie-specific animator. Idle = tail flick (row 6, frames 0-1) at random intervals.
/// Row 8 (frames 0-2) is her sleeping animation for Penny's house (Phase 5).
/// </summary>
public partial class RosieAnimator : Node
{
    [Export] public Texture2D Sheet;
    [Export] public int FrameWidth = 32;
    [Export] public int FrameHeight = 32;
    [Export] public int Columns = 4;

    // Row 6 = sitting idle with tail flick (2 frames).
    // Row 8 = sleeping/lying down (3 frames) — used in Penny's house later.
    private static readonly (string Name, int Row, int StartCol, int FrameCount, float Fps, bool Loop)[] AnimDefs =
    {
        ("idle",     5, 0, 2, 2f, true),   // row 6 (0-indexed = 5), frames 0-1, slow
        ("sleeping", 7, 0, 3, 1.5f, true), // row 8 (0-indexed = 7), frames 0-2
    };

    private AnimatedSprite2D _sprite;
    private double _nextFlickTime;

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<AnimatedSprite2D>("Sprite2D");
        BuildFrames();
        _sprite.Play("idle");
        _nextFlickTime = GD.RandRange(2.0, 5.0);
    }

    public override void _Process(double delta)
    {
        // Random tail flick timing — pause between flicks.
        if (_sprite == null || !_sprite.IsPlaying()) return;

        _nextFlickTime -= delta;
        if (_nextFlickTime <= 0)
        {
            _sprite.Frame = 0;
            _sprite.Play("idle");
            _nextFlickTime = GD.RandRange(1.5, 4.0);
        }
    }

    private void BuildFrames()
    {
        if (Sheet == null)
        {
            GD.PrintErr("[RosieAnimator] No Sheet texture assigned");
            return;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        var imgSize = Sheet.GetSize();

        foreach (var (name, row, startCol, frameCount, fps, loop) in AnimDefs)
        {
            frames.AddAnimation(name);
            frames.SetAnimationSpeed(name, fps);
            frames.SetAnimationLoop(name, loop);

            for (int i = 0; i < frameCount; i++)
            {
                float x = (startCol + i) * FrameWidth;
                float y = row * FrameHeight;

                if (x + FrameWidth > imgSize.X || y + FrameHeight > imgSize.Y)
                {
                    GD.PushWarning($"[RosieAnimator] Frame out of bounds for '{name}' frame {i}");
                    break;
                }

                var atlas = new AtlasTexture
                {
                    Atlas = Sheet,
                    Region = new Rect2(x, y, FrameWidth, FrameHeight),
                };
                frames.AddFrame(name, atlas);
            }
        }

        _sprite.SpriteFrames = frames;
    }
}
