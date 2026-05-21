extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Player inventory + equipment + item database. ItemData stays C# this
# cluster (deferred to Cluster 10 with InventoryUI per the strategy
# adjustment in commit 01571f3) — Inventory.gd loads .tres files as
# Resource and accesses fields via the runtime property dispatch.
# Property names stay PascalCase here (Name, Cost, Category, ...) to
# match the C# [Export] declarations; flip to snake_case when ItemData
# itself ports.
#
# Item database loaded once from assets/data/items/*.tres at startup.
# Inventory: 30 fixed slots (itemId + quantity pairs).
# Equipment: one item per ItemData.ItemCategory slot.
#
# Register in Project → Autoload:
#   Path: res://scripts/systems/Inventory.gd
#   Name: Inventory

# 30 slots: 6 columns × 5 rows. Was 25 (5×5) — bumped when the Collection
# grid grew a column to align with the description.
const SLOT_COUNT: int = 30

# Mirrors ItemData.ItemCategory by integer value. While ItemData stays
# C#, GDScript reads `item.category` as an int and compares against
# these. Don't reorder — .tres files have these baked as ints.
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

# Equipment category names — string form used as dict keys, matching
# the C# enum .ToString() output ("Weapon", "Food", "Head", ...).
const CATEGORY_NAMES: PackedStringArray = [
	"Weapon", "Food", "General", "Head", "Neck", "Body",
	"Hand", "Legs", "Boot", "Money", "Key", "Hair",
]

# Item database (id → Resource[ItemData]) and lookup by lowercase name.
static var _db: Dictionary = {}
static var _db_by_name: Dictionary = {}

# Inventory state — parallel arrays of item-id + quantity, 30 slots.
var _slot_item_ids: PackedInt32Array
var _slot_quantities: PackedInt32Array

# Equipment state — category name → item ID.
var _equipped: Dictionary = {}

# Signals — mirrored from the C# class, callers from C# subscribe via
# `node.Connect("inventory_changed", ...)`.
signal inventory_changed
signal item_equipped(item_id: int, category: String)
signal item_unequipped(category: String)

# Starter equipment IDs from the C3 SaveGameData.json defaults. Hair
# intentionally omitted — hair style + color are driven entirely by the
# inventory cyclers (HairStyleIndex / HairColorIndex on SaveData), not
# by item ownership.
const STARTER_ITEM_IDS: PackedInt32Array = [
	75,  # Body: Golden Tee-Shirt
	95,  # Legs: Brown Shorts
	101, # Boot: Blue Slippers
]

func _ready() -> void:
	_slot_item_ids = PackedInt32Array()
	_slot_item_ids.resize(SLOT_COUNT)
	_slot_quantities = PackedInt32Array()
	_slot_quantities.resize(SLOT_COUNT)
	_load_database()
	print("[Inventory] Database loaded: %d items" % _db.size())

# Grant starter equipment for a new game. Adds each starter item to
# inventory and auto-equips it in its category slot.
func grant_starter_equipment() -> void:
	# Clear current state so a fresh run doesn't keep stale gear.
	for i in range(SLOT_COUNT):
		_slot_item_ids[i] = 0
		_slot_quantities[i] = 0
	_equipped.clear()

	for id in STARTER_ITEM_IDS:
		var item := get_item(id)
		if item == null:
			push_warning("[Inventory] Starter item ID %d not found in database" % id)
			continue
		add_item(id, 1)
		var category_int: int = item.category
		_equipped[CATEGORY_NAMES[category_int]] = id

	print("[Inventory] Starter equipment granted: %d items equipped" % _equipped.size())
	inventory_changed.emit()

# ---- Database ----

# Statically-baked item DB file list. DirAccess.get_files() returns
# empty in web exports (source .tres files aren't in the .pck the
# way they are on desktop), so we ship the file list. Regenerate
# via: ls assets/data/items/*.tres | sed 's|.*/||' | sort
const ITEM_TRES_FILES: Array[String] = [
	"001_axe.tres", "002_sword.tres", "003_nail_bat.tres",
	"004_magic_trident.tres", "005_magic_wand.tres", "006_club.tres",
	"007_cutlass.tres", "008_mace.tres", "021_wild_mushroom.tres",
	"022_red_apple.tres", "023_fresh_strawberry.tres",
	"024_a_whole_chicken.tres", "051_yellow_boater_hat.tres",
	"052_blue_boater_hat.tres", "053_standard_boater.tres",
	"054_the_wrangler.tres", "055_lavender_cloud_hat.tres",
	"056_rosy_magical_hat.tres", "061_sunset_scarf.tres",
	"062_cloak_of_billowing.tres", "063_pink_mantle_cloak.tres",
	"071_green_waistcoat.tres", "072_emerald_tunic.tres",
	"073_sunset_vest_and_top.tres", "074_red_teeshirt.tres",
	"075_golden_teeshirt.tres", "076_purple_tunic.tres",
	"081_gold_purple_ring.tres", "085_silver_emerald_ring.tres",
	"091_black_pink_checkered_pants.tres",
	"092_purple_heart_skort.tres", "093_red_shorts.tres",
	"094_frilly_rose_dress.tres", "095_brown_shorts.tres",
	"096_bluejean_overalls.tres", "097_brown_jeans.tres",
	"098_blue_dress.tres", "101_blue_slippers.tres",
	"102_big_red_boots.tres", "103_plain_brown_shoes.tres",
	"104_ballet_slippers.tres", "105_green_flops.tres",
	"106_red_curlytoed_shoes.tres", "107_forest_green_boots.tres",
	"111_money_bag.tres", "121_sea_monster_key.tres",
	"122_rosie.tres", "123_pink_oyster_pearl.tres",
	"124_birthday_cake.tres", "126_fishing_rod.tres",
	"141_blonde_bob.tres", "142_brown_bob.tres",
	"143_purple_bob.tres", "144_brown_dapper_cut.tres",
	"145_long_wavy_blonde.tres", "146_long_wavy_brown.tres",
	"147_long_wavy_rainbow.tres", "148_blonde_spikes.tres",
	"149_purple_rain.tres", "150_tangerine_tails.tres",
]


func _load_database() -> void:
	_db.clear()
	_db_by_name.clear()

	for file_name in ITEM_TRES_FILES:
		var item: Resource = load("res://assets/data/items/%s" % file_name)
		# Skip non-ItemData resources (shouldn't be any, but guard).
		if item != null and item.get("name") != null and item.name != "":
			_db[int(item.id)] = item
			_db_by_name[String(item.name).to_lower()] = item

# Get an ItemData by integer ID, or null. Returns Resource (the
# underlying C# ItemData while it stays C#).
func get_item(id: int) -> Resource:
	return _db.get(id)

func get_item_by_name(item_name: String) -> Resource:
	if item_name.is_empty():
		return null
	return _db_by_name.get(item_name.to_lower())

# Whether the item is equippable based on its category. Mirrors
# ItemData.IsEquippable (computed property, not [Export], so we
# recompute here rather than property-read).
func _is_equippable(item: Resource) -> bool:
	if item == null:
		return false
	var c: int = item.category
	return c == ItemCategory.HEAD or c == ItemCategory.NECK \
		or c == ItemCategory.BODY or c == ItemCategory.HAND \
		or c == ItemCategory.LEGS or c == ItemCategory.BOOT \
		or c == ItemCategory.HAIR or c == ItemCategory.WEAPON

func _is_consumable(item: Resource) -> bool:
	return item != null and int(item.category) == ItemCategory.FOOD

# ---- Inventory Operations ----

# Add an item by ID. Returns true if successfully added.
func add_item(item_id: int, quantity: int = 1) -> bool:
	var item := get_item(item_id)
	if item == null:
		push_warning("[Inventory] Unknown item ID: %d" % item_id)
		return false

	# Try to stack on an existing slot first.
	if bool(item.stackable):
		for i in range(SLOT_COUNT):
			if _slot_item_ids[i] == item_id:
				_slot_quantities[i] += quantity
				print("[Inventory] Stacked %s x%d (now x%d)" % [item.name, quantity, _slot_quantities[i]])
				inventory_changed.emit()
				return true

	# Find an empty slot.
	for i in range(SLOT_COUNT):
		if _slot_item_ids[i] == 0:
			_slot_item_ids[i] = item_id
			_slot_quantities[i] = quantity
			print("[Inventory] Added %s to slot %d" % [item.name, i])
			inventory_changed.emit()
			return true

	print("[Inventory] Inventory full!")
	return false

# Add an item by name (for dialogue system integration).
func add_item_by_name(item_name: String, quantity: int = 1) -> bool:
	var item := get_item_by_name(item_name)
	if item == null:
		push_warning("[Inventory] Unknown item name: '%s'" % item_name)
		return false
	return add_item(int(item.id), quantity)

# Remove quantity of an item. Returns true if successfully removed.
func remove_item(item_id: int, quantity: int = 1) -> bool:
	for i in range(SLOT_COUNT):
		if _slot_item_ids[i] != item_id:
			continue
		_slot_quantities[i] -= quantity
		if _slot_quantities[i] <= 0:
			var item := get_item(item_id)
			var item_name: String = String(item.name) if item != null else str(item_id)
			print("[Inventory] Removed %s from slot %d" % [item_name, i])
			_slot_item_ids[i] = 0
			_slot_quantities[i] = 0
		inventory_changed.emit()
		return true
	return false

func remove_item_by_name(item_name: String, quantity: int = 1) -> bool:
	var item := get_item_by_name(item_name)
	return item != null and remove_item(int(item.id), quantity)

# Check if the player has at least one of this item.
func has_item(item_id: int) -> bool:
	for i in range(SLOT_COUNT):
		if _slot_item_ids[i] == item_id and _slot_quantities[i] > 0:
			return true
	return false

func has_item_by_name(item_name: String) -> bool:
	var item := get_item_by_name(item_name)
	return item != null and has_item(int(item.id))

# Get the item ID at a given slot (0 = empty).
func get_slot_item_id(slot: int) -> int:
	return _slot_item_ids[slot] if slot >= 0 and slot < SLOT_COUNT else 0

# Get the quantity at a given slot.
func get_slot_quantity(slot: int) -> int:
	return _slot_quantities[slot] if slot >= 0 and slot < SLOT_COUNT else 0

# Get the ItemData (Resource) at a given slot (null if empty).
func get_slot_item(slot: int) -> Resource:
	var id := get_slot_item_id(slot)
	return get_item(id) if id != 0 else null

# ---- Equipment ----

# Equip the item at inventory slot index. Returns true if equipped.
func equip(slot_index: int) -> bool:
	var item := get_slot_item(slot_index)
	if item == null or not _is_equippable(item):
		return false

	var category_int: int = item.category
	var cat_name: String = CATEGORY_NAMES[category_int]

	# If something is already equipped in this slot, unequip it first.
	if _equipped.has(cat_name):
		unequip(category_int)

	_equipped[cat_name] = int(item.id)
	print("[Inventory] Equipped %s (%s)" % [item.name, cat_name])
	item_equipped.emit(int(item.id), cat_name)
	return true

# Unequip the item in the given category slot. Accepts int (the
# ItemCategory enum value).
func unequip(category: int) -> bool:
	var cat_name: String = CATEGORY_NAMES[category]
	if not _equipped.has(cat_name):
		return false
	var item_id: int = _equipped[cat_name]
	_equipped.erase(cat_name)
	var item := get_item(item_id)
	var item_name: String = String(item.name) if item != null else str(item_id)
	print("[Inventory] Unequipped %s (%s)" % [item_name, cat_name])
	item_unequipped.emit(cat_name)
	return true

# Get the equipped item ID for a category (-1 if none). Accepts int.
func get_equipped_id(category: int) -> int:
	var cat_name: String = CATEGORY_NAMES[category]
	return _equipped.get(cat_name, -1)

# Get the equipped ItemData (Resource) for a category (null if none).
func get_equipped(category: int) -> Resource:
	var id := get_equipped_id(category)
	return get_item(id) if id > 0 else null

# Check if a specific item is currently equipped.
func is_equipped(item_id: int) -> bool:
	return item_id in _equipped.values()

# ---- Consumables ----

# Use a consumable item at the given slot. Returns true if consumed.
func use_item(slot_index: int) -> bool:
	var item := get_slot_item(slot_index)
	if item == null or not _is_consumable(item):
		return false

	# Food heals for Strength amount. HealthSystem is still C# this
	# cluster — call its Heal method via cross-language dispatch.
	if int(item.category) == ItemCategory.FOOD:
		var player := get_tree().get_first_node_in_group("player")
		var health := player.get_node_or_null("HealthSystem") if player != null else null
		if health != null:
			health.call("heal", int(item.strength))
			print("[Inventory] Used %s — healed %d HP" % [item.name, int(item.strength)])
			SFXController.play("potion")

	remove_item(int(item.id), 1)
	return true

# ---- Save/Load Integration ----

# Snapshot inventory state into SaveData. SaveData declares the
# arrays as Array[int]; we must hand .set() a typed Array[int] or
# Godot's strict-mode setter rejects the assignment silently and
# the field stays empty across save/load.
func save_to(data: Resource) -> void:
	var typed_ids: Array[int] = []
	var typed_qtys: Array[int] = []
	for i in range(SLOT_COUNT):
		typed_ids.append(_slot_item_ids[i])
		typed_qtys.append(_slot_quantities[i])
	data.set("inventory_item_ids", typed_ids)
	data.set("inventory_quantities", typed_qtys)

	var equipped_dict: Dictionary = {}
	for k in _equipped:
		equipped_dict[k] = _equipped[k]
	data.set("equipped_items", equipped_dict)

# Restore inventory state from SaveData (still C#).
func load_from(data: Resource) -> void:
	for i in range(SLOT_COUNT):
		_slot_item_ids[i] = 0
		_slot_quantities[i] = 0
	_equipped.clear()

	var saved_ids = data.get("inventory_item_ids")
	var saved_qtys = data.get("inventory_quantities")
	if saved_ids != null:
		var count: int = min(saved_ids.size(), SLOT_COUNT)
		for i in range(count):
			_slot_item_ids[i] = int(saved_ids[i])
			_slot_quantities[i] = int(saved_qtys[i]) if i < saved_qtys.size() else 1

	var saved_equipped = data.get("equipped_items")
	if saved_equipped != null:
		for k in saved_equipped:
			_equipped[String(k)] = int(saved_equipped[k])

	var live_count: int = 0
	for id in _slot_item_ids:
		if id != 0:
			live_count += 1
	print("[Inventory] Loaded: %d items, %d equipped" % [live_count, _equipped.size()])
	inventory_changed.emit()
