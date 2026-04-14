using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Abstract base for enemy animators. Subclasses build SpriteFrames from
/// either a spritesheet (EnemySheetAnimator) or a folder of per-frame PNGs
/// (EnemyFolderAnimator). EnemyController references this base type so the
/// two strategies are interchangeable.
/// </summary>
public abstract partial class EnemyAnimatorBase : Node
{
    /// <summary>
    /// Play a named animation (e.g. "idle_down", "hop_left").
    /// Implementations should no-op gracefully for unknown names.
    /// </summary>
    public abstract void Play(string animName);

    /// <summary>Stop the current animation.</summary>
    public abstract void Stop();
}
