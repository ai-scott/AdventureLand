using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Sheet-based SpriteFrames builder for enemies. Modeled on NpcAnimator.
///
/// Configure via:
///   - [Export] Sheet — the spritesheet texture (e.g., assets/sprites/enemies/ooze/ooze.png)
///   - [Export] FrameWidth, FrameHeight — size of each cell in pixels
///   - [Export] AnimRows — array of AnimRowConfig entries: name + row + startCol + frameCount
///
/// Animation names follow the EnemyData convention: baseName_direction (e.g. "idle_down",
/// "hop_left", "hurt_up"). The EnemyController substitutes {direction} at Play() time.
///
/// Default config assumes Ooze layout (to be verified once the sheet is in hand):
///   rows 0-3 = idle_down/up/left/right
///   rows 4-7 = hop_down/up/left/right
///   rows 8-11 = hurt_down/up/left/right
/// If the actual layout differs, override AnimRows in the Inspector per-enemy.
/// </summary>
public partial class EnemyAnimator : Node
{
    [Export] public Texture2D Sheet;
    [Export] public int FrameWidth = 32;
    [Export] public int FrameHeight = 32;

    // One animation per row, or override in Inspector. Each tuple: (name, row, startCol, frameCount, fps, loop).
    // Kept as a serializable resource-like array via code defaults; user adjusts in EnemyAnimator.cs if layout
    // differs from Ooze. Keeping it as a static array rather than [Export] Array<> to avoid Resource subclass bloat
    // for Phase 1 — promote to a config resource if we need per-enemy overrides in Phase 6.
    public record struct AnimRow(string Name, int Row, int StartCol, int FrameCount, float Fps, bool Loop);

    private static readonly AnimRow[] DefaultAnimRows =
    {
        new("idle_down",  0, 0, 4, 4f, true),
        new("idle_up",    1, 0, 4, 4f, true),
        new("idle_left",  2, 0, 4, 4f, true),
        new("idle_right", 3, 0, 4, 4f, true),
        new("hop_down",   4, 0, 4, 8f, true),
        new("hop_up",     5, 0, 4, 8f, true),
        new("hop_left",   6, 0, 4, 8f, true),
        new("hop_right",  7, 0, 4, 8f, true),
        new("hurt_down",  8, 0, 2, 6f, true),
        new("hurt_up",    9, 0, 2, 6f, true),
        new("hurt_left", 10, 0, 2, 6f, true),
        new("hurt_right",11, 0, 2, 6f, true),
    };

    private AnimatedSprite2D _sprite;
    private string _currentAnim = "";

    public override void _Ready()
    {
        _sprite = GetParent().GetNodeOrNull<AnimatedSprite2D>("Sprite2D");
        if (_sprite == null)
        {
            GD.PrintErr("[EnemyAnimator] Parent must have an AnimatedSprite2D child named 'Sprite2D'");
            return;
        }

        if (Sheet == null)
        {
            GD.PrintErr("[EnemyAnimator] Sheet texture not assigned in Inspector");
            return;
        }

        BuildFrames();
    }

    private void BuildFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        var imgSize = Sheet.GetSize();

        foreach (var row in DefaultAnimRows)
        {
            frames.AddAnimation(row.Name);
            frames.SetAnimationSpeed(row.Name, row.Fps);
            frames.SetAnimationLoop(row.Name, row.Loop);

            for (int i = 0; i < row.FrameCount; i++)
            {
                float x = (row.StartCol + i) * FrameWidth;
                float y = row.Row * FrameHeight;

                if (x + FrameWidth > imgSize.X || y + FrameHeight > imgSize.Y)
                {
                    GD.PushWarning(
                        $"[EnemyAnimator] Frame out of bounds for '{row.Name}' frame {i} " +
                        $"(expected ≤ {imgSize.X}x{imgSize.Y}, computed {x + FrameWidth}x{y + FrameHeight}). " +
                        $"Check sheet dimensions + FrameWidth/Height/row mapping.");
                    break;
                }

                var atlas = new AtlasTexture
                {
                    Atlas = Sheet,
                    Region = new Rect2(x, y, FrameWidth, FrameHeight),
                };
                frames.AddFrame(row.Name, atlas);
            }
        }

        _sprite.SpriteFrames = frames;
    }

    /// <summary>
    /// Play a named animation. If the animation doesn't exist, logs and no-ops
    /// (so unhandled animation refs in EnemyData don't crash the game).
    /// </summary>
    public void Play(string animName)
    {
        if (_sprite == null || _sprite.SpriteFrames == null) return;
        if (animName == _currentAnim && _sprite.IsPlaying()) return;

        if (!_sprite.SpriteFrames.HasAnimation(animName))
        {
            GD.PushWarning($"[EnemyAnimator] Unknown animation '{animName}'. Falling back to 'idle_down'.");
            animName = "idle_down";
            if (!_sprite.SpriteFrames.HasAnimation(animName)) return;
        }

        _currentAnim = animName;
        _sprite.Play(animName);
    }

    public void Stop()
    {
        if (_sprite != null) _sprite.Stop();
    }
}
