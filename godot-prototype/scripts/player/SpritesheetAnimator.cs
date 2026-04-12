using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// Builds SpriteFrames at runtime from a spritesheet config.
/// No hand-building in the editor needed — works for any costume layer.
///
/// Mana Seed FBAS layout: 1024x1024, 64x64 frames, 16 columns per row.
/// Call SetTexture() to swap costume layers at runtime.
/// </summary>
public partial class SpritesheetAnimator : Node
{
    [Export] public Texture2D Sheet;
    [Export] public int FrameWidth  = 64;
    [Export] public int FrameHeight = 64;
    [Export] public int Columns     = 16;
    [Export] public float DefaultFps = 8.0f;

    // Each entry: { "name", row, startCol, frameCount, fps, loop }
    // Row/col are 0-indexed. fps = -1 means use DefaultFps.
    // Mana Seed FBAS standard layout — adjust if your sheet differs.
    private static readonly (string Name, int Row, int StartCol, int FrameCount, float Fps, bool Loop)[] AnimDefs =
    {
        // Walk (8 frames each)
        ("walk_south", 0, 0, 8, 8f, true),
        ("walk_west",  1, 0, 8, 8f, true),
        ("walk_east",  2, 0, 8, 8f, true),
        ("walk_north", 3, 0, 8, 8f, true),

        // Run (8 frames each)
        ("run_south",  4, 0, 8, 12f, true),
        ("run_west",   5, 0, 8, 12f, true),
        ("run_east",   6, 0, 8, 12f, true),
        ("run_north",  7, 0, 8, 12f, true),

        // Idle (2 frames, slow)
        ("idle_south", 8, 0, 2, 2f, true),
        ("idle_west",  8, 2, 2, 2f, true),
        ("idle_east",  8, 4, 2, 2f, true),
        ("idle_north", 8, 6, 2, 2f, true),

        // Combat stances
        ("slash_south", 9,  0, 6, 10f, false),
        ("slash_west",  10, 0, 6, 10f, false),
        ("slash_east",  11, 0, 6, 10f, false),
        ("slash_north", 12, 0, 6, 10f, false),

        // Hurt / death
        ("hurt",  13, 0, 3, 6f, false),
        ("death", 13, 3, 6, 6f, false),

        // Item use (bow, etc.)
        ("item_south", 14, 0, 13, 8f, false),
        ("item_west",  14, 0, 13, 8f, false),
        ("item_east",  14, 0, 13, 8f, false),
        ("item_north", 15, 0, 13, 8f, false),
    };

    private AnimatedSprite2D _sprite;

    public override void _Ready()
    {
        _sprite = GetParent().GetNode<AnimatedSprite2D>("Sprite2D");
        if (Sheet != null)
            BuildFrames(Sheet);
    }

    /// <summary>
    /// Swap to a different costume layer texture (same sheet layout).
    /// </summary>
    public void SetTexture(Texture2D texture)
    {
        Sheet = texture;
        BuildFrames(texture);
        _sprite.Play(_sprite.Animation); // resume current anim
    }

    private void BuildFrames(Texture2D texture)
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        var imgSize = texture.GetSize();

        foreach (var (name, row, startCol, frameCount, fps, loop) in AnimDefs)
        {
            frames.AddAnimation(name);
            frames.SetAnimationSpeed(name, fps < 0 ? DefaultFps : fps);
            frames.SetAnimationLoop(name, loop);

            for (int i = 0; i < frameCount; i++)
            {
                int col = startCol + i;
                float x = col * FrameWidth;
                float y = row * FrameHeight;

                // Skip frames that go out of bounds (handles partial sheets)
                if (x + FrameWidth > imgSize.X || y + FrameHeight > imgSize.Y)
                {
                    GD.PushWarning($"[SpritesheetAnimator] '{name}' frame {i} out of bounds — stopping at {i} frames.");
                    break;
                }

                var atlasTexture = new AtlasTexture();
                atlasTexture.Atlas = texture;
                atlasTexture.Region = new Rect2(x, y, FrameWidth, FrameHeight);
                frames.AddFrame(name, atlasTexture);
            }
        }

        _sprite.SpriteFrames = frames;
    }

    // --- Playback helpers ---

    public void PlayIdle(string direction = "south") =>
        SafePlay($"idle_{direction}");

    public void PlayWalk(string direction) =>
        SafePlay($"walk_{direction}");

    public void PlayRun(string direction) =>
        SafePlay($"run_{direction}");

    public void PlaySlash(string direction) =>
        SafePlay($"slash_{direction}");

    public void PlayHurt() => SafePlay("hurt");
    public void PlayDeath() => SafePlay("death");

    private void SafePlay(string anim)
    {
        if (!_sprite.SpriteFrames.HasAnimation(anim))
        {
            GD.PushWarning($"[SpritesheetAnimator] Animation '{anim}' not found.");
            return;
        }
        if (_sprite.Animation == anim && _sprite.IsPlaying()) return;
        _sprite.Play(anim);
    }
}
