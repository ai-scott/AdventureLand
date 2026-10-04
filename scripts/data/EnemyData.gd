class_name EnemyData extends Resource

## Top-level enemy config resource. Mirrors C3's EnemyConfig
## (scripts/systems/enemy/enemy-configs.ts).
## One .tres per enemy type lives in assets/data/enemies/. Edit stats in the
## Inspector. Enemy.tscn reads this resource and drives AI accordingly — no
## code change per enemy type.

@export var type: String = ""

@export_group("Base Stats")
@export var health: int = 1
@export var speed: float = 20.0
@export var view_distance: float = 120.0
@export var attack_distance: float = 0.0

## Soft leash for Random-pattern wander. When > 0, the enemy steers back
## toward its spawn whenever it drifts farther than this many pixels from
## home. 0 = unbounded (default). Chase patterns (TOWARD_PLAYER etc.) are
## not affected.
@export var wander_radius: float = 0.0

@export_group("Audio")
## SFX key played when this enemy dies. Defaults to the generic
## enemy_destroy.ogg; override per-enemy for varied death cues (e.g. bat
## uses bat_destroy.ogg). Looked up via SFXController.Play, so the file
## must exist at assets/audio/sfx/{death_sound}.ogg.
@export var death_sound: String = "enemy_destroy"

## SFX key played when this enemy takes a hit. Defaults to the generic
## enemy_hurt.ogg.
@export var hurt_sound: String = "enemy_hurt"

@export_group("Behaviors")
@export var behaviors: Array[EnemyBehavior] = []
