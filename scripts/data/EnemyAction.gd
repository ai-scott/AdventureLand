class_name EnemyAction extends Resource

## A single action executed when a behavior fires. Mirrors C3's ActionConfig.
## Flat parameter layout: set only the fields relevant to the chosen Type.
## The runtime EnemyAI reads type and dispatches to the matching handler,
## reading only the fields that apply. Unused fields are harmless defaults.
##
## Not every action type uses every field. Reference:
##  - Move:         pattern, speed
##  - Animate:      anim_name (supports "{direction}" placeholder)
##  - Sound:        sound, volume
##  - Invulnerable: duration
##  - SetEffect:    effect, parameter, value, enabled

## Enum integer values must stay aligned with C# EnemyController until that
## file ports in Phase 5. Don't reorder.
enum ActionType {
	MOVE = 0,
	ANIMATE = 1,
	SOUND = 2,
	INVULNERABLE = 3,
	SET_EFFECT = 4,
}

enum MovePattern {
	NONE = 0,
	TOWARD_PLAYER = 1,
	AWAY_FROM_PLAYER = 2,
	RANDOM = 3,
	STOP = 4,
	SIDEWAYS_LEFT = 5,
	SIDEWAYS_RIGHT = 6,
	CRAB_TOWARD_PLAYER = 7,
	SWOOP_TO_PLAYER = 8,
	FLEE_TO_NEAREST_TREE = 9,
	IDLE_IN_TREE = 10,
}

@export var type: ActionType = ActionType.ANIMATE

@export_group("Movement")
@export var pattern: MovePattern = MovePattern.NONE
@export var speed: float = 0.0

@export_group("Animation")
@export var anim_name: String = ""

@export_group("Sound")
@export var sound: String = ""
@export var volume: float = 0.0

@export_group("Invulnerability / Duration")
@export var duration: float = 0.0

@export_group("Effect")
@export var effect: String = ""
@export var parameter: String = ""
@export var value: float = 0.0
@export var enabled: bool = false
