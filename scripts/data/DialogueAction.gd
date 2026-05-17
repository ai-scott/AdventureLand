@tool
class_name DialogueAction extends Resource

# An action executed when a dialogue node is displayed or a response is
# chosen.

# Mirror of C# DialogueAction.ActionType. Integer values serialize into
# .tres files — NEVER reorder. Append new values at the end only.
enum ActionType {
	START_QUEST = 0,
	COMPLETE_QUEST = 1,
	SET_QUEST_STATUS = 2,
	GIVE_ITEM = 3,
	REMOVE_ITEM = 4,
	SET_FLAG = 5,
	SET_WORLD_FLAG = 6,
	SET_NPC_MEMORY = 7,
	DEPLOY_NPC = 8,
	PLAY_SOUND = 9,
	TELEPORT_PLAYER = 10,
	INPUT = 11,
	CUSTOM = 12,
	SPAWN_UNIQUE_ITEM = 13,
	SUMMON_SEA_MONSTER = 14,
	MAKE_SEA_MONSTER_HOSTILE = 15,
	SEA_MONSTER_ACCEPT_QUEST = 16,
	SEA_MONSTER_QUEST_COMPLETE = 17,
	SEA_MONSTER_RETREAT = 18,
}

@export var type: ActionType = ActionType.SET_QUEST_STATUS

@export_group("Quest")
@export var quest_id: String = ""
@export var status: String = ""

@export_group("Item")
@export var item_id: String = ""
@export var item_name: String = ""
@export var quantity: int = 1
@export var destroy_trigger: bool = false

@export_group("Flag/Memory")
@export var flag_key: String = ""
@export var flag_value: String = ""
@export var npc_id: String = ""
@export var memory_key: String = ""
@export var memory_value: String = ""

@export_group("Teleport")
@export var world_id: String = ""
@export var x: float = 0.0
@export var y: float = 0.0

@export_group("Other")
@export var sound_id: String = ""
@export var variable: String = ""
@export var custom_function: String = ""
@export var reason: String = ""
