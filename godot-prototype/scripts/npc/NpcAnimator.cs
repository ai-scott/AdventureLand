using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Builds NPC SpriteFrames at runtime from a spritesheet config.
/// Default layout matches the Penny sheet (128x256, 32x32 frames, 4 columns).
/// Adjust FrameWidth/FrameHeight/Columns in the inspector for other NPC sheets.
/// </summary>
public partial class NpcAnimator : Node
{
    [Export] public Texture2D Sheet;
    [Export] public int FrameWidth  = 32;
    [Export] public int FrameHeight = 32;
    [Export] public int Columns     = 4;

    // { name, row, startCol, frameCount, fps, loop }
    // Override in a subclass or config if NPC sheet layout differs.
    private static readonly (string Name, int Row, int StartCol, int FrameCount, float Fps, bool Loop)[] AnimDefs =
    {
        ("walk_down",  0, 0, 4, 8f, true),
        ("walk_right", 1, 0, 4, 8f, true),
        ("walk_up",    2, 0, 4, 8f, true),
        ("walk_left",  3, 0, 4, 8f, true),
        ("idle",       4, 1, 2, 2f, true),
    };

    private AnimatedSprite2D _sprite;

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<AnimatedSprite2D>("Sprite2D");
        BuildFrames();
        PlayIdle();
    }

    private void BuildFrames()
    {
        var texture = Sheet;
        if (texture == null)
        {
            GD.PrintErr("[NpcAnimator] No Sheet texture assigned in inspector!");
            return;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        var imgSize = texture.GetSize();

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
                    GD.PushWarning($"[NpcAnimator] '{name}' frame {i} out of bounds.");
                    break;
                }

                var atlas = new AtlasTexture();
                atlas.Atlas = texture;
                atlas.Region = new Rect2(x, y, FrameWidth, FrameHeight);
                frames.AddFrame(name, atlas);
            }
        }

        _sprite.SpriteFrames = frames;
    }

    public void PlayIdle() => SafePlay("idle");

    public void PlayWalk(string direction)
    {
        string anim = direction switch
        {
            "down"  or "down_right" or "down_left" => "walk_down",
            "up"    or "up_right"   or "up_left"   => "walk_up",
            "right" => "walk_right",
            "left"  => "walk_left",
            _ => "walk_down"
        };
        SafePlay(anim);
    }

    private void SafePlay(string anim)
    {
        if (_sprite.Animation == anim && _sprite.IsPlaying()) return;
        _sprite.Play(anim);
    }
}
