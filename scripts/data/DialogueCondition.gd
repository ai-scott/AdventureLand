@tool
class_name DialogueCondition extends Resource

# A condition that must be true for a node to be selected.
# All conditions on a node are AND'd together.

# Mirror of C# DialogueCondition.ConditionType. Integer values serialize
# into .tres files — NEVER reorder. Append new values at the end only.
enum ConditionType {
	QUEST_STATUS = 0,
	HAS_ITEM = 1,
	WORLD_FLAG = 2,
	NPC_MEMORY = 3,
	PLAYER_LEVEL = 4,
	CUSTOM = 5,
	EQUIPPED_CATEGORY = 6,
}

@export var type: ConditionType = ConditionType.QUEST_STATUS
@export var negate: bool = false

@export_group("Quest")
@export var quest_id: String = ""
@export var status: String = ""

@export_group("Item")
@export var item_id: String = ""
@export var quantity: int = 1
# For EQUIPPED_CATEGORY: ItemData.ItemCategory name, e.g. "Weapon", "Hat",
# "Body". True if the player has any item of that category currently
# equipped.
@export var category: String = ""

@export_group("Flag")
@export var flag_key: String = ""
@export var flag_value: String = ""

@export_group("NPC Memory")
@export var npc_id: String = ""
@export var memory_key: String = ""
@export var memory_value: String = ""

@export_group("Other")
@export var level: int = 0
@export var custom_check: String = ""
