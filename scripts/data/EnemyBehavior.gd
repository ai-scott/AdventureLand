class_name EnemyBehavior extends Resource

## One weighted behavior slot on an enemy. Mirrors C3's BehaviorConfig.
## duration is a [min, max] range — actual duration rolled at behavior start.
## cooldown=0 means no cooldown (matches the optional `cooldown?` in TypeScript).
## weight=0 means this behavior is NEVER selected randomly — only triggered by
## conditions (e.g., hurt/retreat behaviors that must match specific state
## rather than roll).

@export var name: String = ""

@export_group("Timing")
@export var duration_min: float = 1.0
@export var duration_max: float = 2.0
@export var weight: float = 1.0
@export var cooldown: float = 0.0

@export_group("Triggers")
@export var conditions: Array[BehaviorCondition] = []

@export_group("Effects")
@export var actions: Array[EnemyAction] = []
