class_name EnemyAnimatorBase extends Node

# Abstract base for enemy animators. Subclasses build SpriteFrames from
# either a spritesheet (EnemySheetAnimator) or a folder of per-frame PNGs
# (EnemyFolderAnimator). EnemyController references this base type so the
# two strategies are interchangeable.

# Play a named animation (e.g. "idle_down", "hop_left").
# Implementations should no-op gracefully for unknown names.
func play(_anim_name: String) -> void:
	push_warning("[EnemyAnimatorBase] play() not overridden")

# Stop the current animation.
func stop() -> void:
	push_warning("[EnemyAnimatorBase] stop() not overridden")
