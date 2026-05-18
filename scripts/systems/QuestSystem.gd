extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Quest state tracker. Reads/writes quest statuses, world flags, and NPC
# memory to the active SaveData via SaveManager (still C#). Called
# directly by DialogueManager when evaluating conditions and executing
# actions.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/QuestSystem.gd
#   Name: QuestSystem

# Mirror of ItemData.ItemCategory (C#, still in Cluster 10 deferred).
# Match integer values exactly. NEVER reorder.
enum ItemCategory {
	WEAPON  = 0,
	FOOD    = 1,
	GENERAL = 2,
	HEAD    = 3,
	NECK    = 4,
	BODY    = 5,
	HAND    = 6,
	LEGS    = 7,
	BOOT    = 8,
	MONEY   = 9,
	KEY     = 10,
	HAIR    = 11,
}

# Mirror of DialogueCondition.ConditionType (C#, still in Cluster 8 prep).
# When DialogueCondition itself ports, this can defer to it.
enum ConditionType {
	QUEST_STATUS = 0,
	HAS_ITEM = 1,
	WORLD_FLAG = 2,
	NPC_MEMORY = 3,
	PLAYER_LEVEL = 4,
	CUSTOM = 5,
	EQUIPPED_CATEGORY = 6,
}

# Reach back into SaveManager for the active SaveData. Returns the
# SaveData Resource or null.
func _get_data() -> Resource:
	return SaveManager.current_data

# ---- Quest Status ----

func get_quest_status(quest_id: String) -> String:
	var data := _get_data()
	if data == null:
		return "Not_Started"
	var statuses: Dictionary = data.quest_statuses
	if not statuses.has(quest_id):
		return "Not_Started"
	return String(statuses[quest_id])

func set_quest_status(quest_id: String, status: String) -> void:
	var data := _get_data()
	if data == null:
		return
	var statuses: Dictionary = data.quest_statuses
	statuses[quest_id] = status
	print("[Quest] %s → %s" % [quest_id, status])

func start_quest(quest_id: String) -> void:
	set_quest_status(quest_id, "Active")

func complete_quest(quest_id: String) -> void:
	# Dialogue conditions check for Status == "Complete". An earlier
	# "Completed" mismatch silently kept post-quest node_complete and
	# node_post_complete branches from ever matching.
	set_quest_status(quest_id, "Complete")

# ---- World Flags ----

func get_world_flag(key: String) -> String:
	var data := _get_data()
	if data == null:
		return ""
	var flags: Dictionary = data.world_flags
	if not flags.has(key):
		return ""
	return String(flags[key])

func set_world_flag(key: String, value: String) -> void:
	var data := _get_data()
	if data == null:
		return
	var flags: Dictionary = data.world_flags
	flags[key] = value

func has_world_flag(key: String) -> bool:
	var data := _get_data()
	if data == null:
		return false
	var flags: Dictionary = data.world_flags
	return flags.has(key)

# ---- NPC Memory ----

func get_npc_memory(npc_id: String, key: String) -> String:
	var data := _get_data()
	if data == null:
		return ""
	var memory: Dictionary = data.npc_memory
	var combined: String = "%s:%s" % [npc_id, key]
	if not memory.has(combined):
		return ""
	return String(memory[combined])

func set_npc_memory(npc_id: String, key: String, value: String) -> void:
	var data := _get_data()
	if data == null:
		return
	var memory: Dictionary = data.npc_memory
	memory["%s:%s" % [npc_id, key]] = value

# ---- Unique Items ----

func has_unique_item(item_name: String) -> bool:
	if Inventory.has_item_by_name(item_name):
		return true
	# Fallback for pre-Phase 4 saves.
	return has_world_flag("UniqueItem_%s" % item_name)

func grant_unique_item(item_name: String) -> void:
	if Inventory.add_item_by_name(item_name):
		print("[Quest] Unique item granted via inventory: %s" % item_name)
		return
	# Fallback: store as world flag.
	set_world_flag("UniqueItem_%s" % item_name, "true")
	print("[Quest] Unique item granted via flag: %s" % item_name)

func remove_unique_item(item_name: String) -> void:
	Inventory.remove_item_by_name(item_name)
	# Also clean up the flag if it exists.
	var data := _get_data()
	if data == null:
		return
	var flags: Dictionary = data.world_flags
	var key: String = "UniqueItem_%s" % item_name
	if flags.has(key):
		flags.erase(key)

# ---- Inventory-backed condition helper ----

# True if the player has any item of the named ItemCategory currently
# equipped. Parses the string against ItemCategory; returns false on
# invalid category name.
func _has_equipped_category(category_name: String) -> bool:
	if category_name.is_empty():
		return false
	# Case-insensitive enum-name lookup.
	var upper: String = category_name.to_upper()
	var keys: Array = ItemCategory.keys()
	if not keys.has(upper):
		return false
	var cat: int = ItemCategory[upper]
	return Inventory.get_equipped_id(cat) > 0

# ---- Condition Evaluation ----

# c is a DialogueCondition Resource (still C# during Cluster 8 prep —
# PascalCase property access per Pattern C). When the full Cluster 8
# cutover lands, flip these to snake_case.
func evaluate_condition(c: Resource) -> bool:
	if c == null:
		return false
	var c_type: int = c.type
	var result: bool = false
	match c_type:
		ConditionType.QUEST_STATUS:
			result = get_quest_status(String(c.quest_id)) == String(c.status)
		ConditionType.HAS_ITEM:
			result = has_unique_item(String(c.item_id))  # Phase 4 will add inventory quantity checks
		ConditionType.WORLD_FLAG:
			result = get_world_flag(String(c.flag_key)) == String(c.flag_value)
		ConditionType.NPC_MEMORY:
			result = get_npc_memory(String(c.NpcId), String(c.MemoryKey)) == String(c.MemoryValue)
		ConditionType.PLAYER_LEVEL:
			result = false  # Phase 6 — no player levels yet
		ConditionType.CUSTOM:
			result = false  # Custom checks stubbed
		ConditionType.EQUIPPED_CATEGORY:
			result = _has_equipped_category(String(c.category))

	return not result if bool(c.negate) else result

# Evaluate all conditions on a node (AND logic). `conditions` is an
# Array<DialogueCondition> or null.
func all_conditions_met(conditions: Variant) -> bool:
	if conditions == null:
		return true
	var arr: Array = conditions
	if arr.is_empty():
		return true
	for c in arr:
		if c == null:
			continue
		if not evaluate_condition(c):
			return false
	return true
