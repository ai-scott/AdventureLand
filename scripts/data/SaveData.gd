class_name SaveData extends Resource

# Persisted player state. Saved as .tres via ResourceSaver to
# user://saves/slot_N.tres.
#
# PORT NOTE (Cluster 9): All fields are snake_case now. Existing save
# files written with the prior C# (PascalCase) shape will fail to load
# -- testers' user://saves/* should be deleted before resuming. Repo
# does not ship any .tres referencing this script.

@export var schema_version: int = 3

@export_group("Player")
@export var player_name: String = ""
@export var health: int = 10
@export var max_health: int = 10
@export var position_x: float = 149.0
@export var position_y: float = 164.0

@export_group("World")
@export var current_world: String = "res://scenes/worlds/World_00.tscn"

@export_group("Quest State")
@export var quest_statuses: Dictionary = {}
@export var world_flags: Dictionary = {}
@export var npc_memory: Dictionary = {}

@export_group("Inventory")
@export var inventory_item_ids: Array[int] = []
@export var inventory_quantities: Array[int] = []

@export_group("Equipment")
@export var equipped_items: Dictionary = {}

@export_group("Customization")
# Index into the hair-style roster (sheets/13hair). -1 means "use the
# player scene's default hair", which lets brand-new saves inherit
# whatever the Inspector authored.
@export var hair_style_index: int = -1

# Index into the hair color ramp roster (mana seed hair ramps). -1
# means no palette swap applied.
@export var hair_color_index: int = -1

# Index into the skin color ramp roster (mana seed skin ramps). -1
# means no palette swap applied.
@export var skin_index: int = -1

@export_group("Currency")
# Gems -- the game's single currency. Earned from pickups/sales, spent
# in shops. CurrencySystem reads/writes this through SaveManager.
@export var gems: int = 0

@export_group("Worlds")
# Scene paths of worlds the player has visited. Used to show the
# world-name banner only on first visit.
@export var visited_worlds: Array[String] = []
