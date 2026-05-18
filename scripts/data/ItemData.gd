class_name ItemData extends Resource

# Data resource for a single game item. Converted from ItemsLibrary.json
# via tools/items_to_tres.py. Covers weapons, armor, food, quest keys,
# hair.
#
# costume_id stores the raw C3 animation-frame string for traceability.
# costume_layer stores the extracted MSCA layer code (e.g., "14head",
# "05shrt") so CostumeController knows which sprite layer to swap at
# equip time.
#
# IMPORTANT: Never reorder enum values -- .tres files serialize them as
# ints. Always append new values at the end.

enum ItemCategory {
	WEAPON,   # 0
	FOOD,     # 1
	GENERAL,  # 2
	HEAD,     # 3
	NECK,     # 4
	BODY,     # 5
	HAND,     # 6
	LEGS,     # 7
	BOOT,     # 8
	MONEY,    # 9
	KEY,      # 10
	HAIR,     # 11
}

@export var id: int = 0
@export var name: String = ""
@export var description: String = ""
@export var category: ItemCategory = ItemCategory.GENERAL

@export_group("Stats")
@export var strength: int = 0
@export var cost: int = 0

@export_group("Icon")
# 16x16 item icon texture for inventory display.
@export var icon: Texture2D

@export_group("Costume")
# Raw C3 costume string (e.g., "51_fbas_14head_boaterhat_00d_straw_boat").
@export var costume_id: String = ""
# Extracted MSCA layer code (e.g., "14head", "05shrt", "04lwr1").
@export var costume_layer: String = ""
# For Weapon category: MSCA 1h weapon sheet number (1-7). Maps to
# farmer_1h_weapon sprite texture.
@export var weapon_sheet: int = 0

@export_group("Flags")
@export var stackable: bool = false
@export var quest_item: bool = false


# Whether this item can be equipped (has a costume layer or is a weapon/ring).
func is_equippable() -> bool:
	match category:
		ItemCategory.HEAD, ItemCategory.NECK, ItemCategory.BODY, \
		ItemCategory.HAND, ItemCategory.LEGS, ItemCategory.BOOT, \
		ItemCategory.HAIR, ItemCategory.WEAPON:
			return true
	return false


# Whether this item is consumable (food restores health).
func is_consumable() -> bool:
	return category == ItemCategory.FOOD


# Narrative-critical items the player can't equip, consume, or sell --
# keys, quest deliverables, etc. The inventory UI marks these with a
# leading star and suppresses every action chip so "this is a key
# item" reads at a glance. Both category==KEY and the quest_item flag
# count.
func is_key_item() -> bool:
	return category == ItemCategory.KEY or quest_item
