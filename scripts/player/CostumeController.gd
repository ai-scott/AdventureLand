class_name CostumeController extends Node

# Paper-doll costume controller for the MSCA-generated player. Attach as
# a child of the CharacterBody2D (sibling of SpriteLayers).
#
# Each @export Texture2D is a costume slot; null means "hide that layer".
# Inspector edits apply on scene start; runtime swaps via the set_* methods
# or by assigning to the properties and calling apply_all().
#
# Layer names match the Mana Seed convention (ID 2 in the filename naming
# scheme): 00undr/01body/02sock/.../13hair/14head/15over.
#
# Per Seliel's convention: when a hat with "_e" in its ID is equipped,
# the 13hair layer should be hidden. apply_all() hides hair whenever any
# Hat is set if HatReplacesHair is true.

# ItemData is still C# this cluster; we mirror its ItemCategory enum
# values by int. Don't reorder.
const ITEM_CATEGORY_WEAPON: int = 0
const ITEM_CATEGORY_HEAD: int = 3
const ITEM_CATEGORY_NECK: int = 4
const ITEM_CATEGORY_BODY: int = 5
const ITEM_CATEGORY_HAND: int = 6
const ITEM_CATEGORY_LEGS: int = 7
const ITEM_CATEGORY_BOOT: int = 8
const ITEM_CATEGORY_HAIR: int = 11

@export_group("Hair (layer 13hair)")
@export var hair: Texture2D

@export_group("Hat (layer 14head)")
@export var hat: Texture2D
@export var hat_replaces_hair: bool = false

@export_group("Shirt (layer 05shrt)")
@export var shirt: Texture2D

@export_group("Pants (layer 04lwr1)")
@export var pants: Texture2D

@export_group("Shoes (layer 03fot1)")
@export var shoes: Texture2D

@export_group("Outerwear (layer 10outr)")
@export var outerwear: Texture2D

var _sprite_layers: Node

func _ready() -> void:
	_sprite_layers = get_parent().get_node_or_null("SpriteLayers")
	if _sprite_layers == null:
		push_error("[CostumeController] Parent has no SpriteLayers child. " +
				"Attach under the MSCA-generated CharacterBody2D.")
		return
	apply_all()

# Apply every costume slot to its matching layer. Call after bulk edits.
func apply_all() -> void:
	if _sprite_layers == null:
		return

	set_layer("13hair", hair)
	set_layer("14head", hat)
	set_layer("05shrt", shirt)
	set_layer("04lwr1", pants)
	set_layer("03fot1", shoes)
	set_layer("10outr", outerwear)

	if hat_replaces_hair and hat != null:
		var h := _sprite_layers.get_node_or_null("13hair") as Sprite2D
		if h != null:
			h.visible = false

# Generic layer setter. Pass null texture to hide the layer.
func set_layer(layer_name: String, texture: Texture2D) -> void:
	if _sprite_layers == null:
		return
	var node := _sprite_layers.get_node_or_null(layer_name) as Sprite2D
	if node == null:
		push_warning("[CostumeController] Layer '%s' not found in SpriteLayers." % layer_name)
		return

	if texture == null:
		node.visible = false
	else:
		node.texture = texture
		node.visible = true

# Convenience setters for runtime swaps (debug hotkeys, UI, shop purchases).
func set_hair(tex: Texture2D) -> void:
	hair = tex
	set_layer("13hair", tex)

func set_hat(tex: Texture2D) -> void:
	hat = tex
	apply_all()

func set_shirt(tex: Texture2D) -> void:
	shirt = tex
	set_layer("05shrt", tex)

func set_pants(tex: Texture2D) -> void:
	pants = tex
	set_layer("04lwr1", tex)

func set_shoes(tex: Texture2D) -> void:
	shoes = tex
	set_layer("03fot1", tex)

func set_outerwear(tex: Texture2D) -> void:
	outerwear = tex
	set_layer("10outr", tex)

# ---- Equipment Integration (Phase 4) ----

# Equip an item by resolving its CostumeLayer to a sprite sheet texture.
# Handles mutual exclusion for leg layers (dress hides pants/overalls).
# Item is a C# ItemData Resource — accessed via PascalCase properties.
func equip_item(item: Resource) -> void:
	if item == null:
		return

	# Weapons use a separate layer (farmer_1h_weapon) and the MSCA
	# weapon sheets.
	if int(item.category) == ITEM_CATEGORY_WEAPON:
		_equip_weapon(item)
		return

	var costume_layer: String = String(item.costume_layer)
	if costume_layer.is_empty():
		print("[Costume] Item '%s' has no costume layer — stats-only equip" % item.name)
		return

	var tex := _resolve_base_sheet(costume_layer, String(item.costume_id))
	if tex == null:
		push_warning("[Costume] Could not resolve texture for '%s' layer=%s" % [item.name, costume_layer])
		return

	# Handle mutual exclusion for leg-type and boot-type layers.
	_handle_leg_exclusion(costume_layer)
	_handle_boot_exclusion(costume_layer)

	set_layer(costume_layer, tex)
	_apply_costume_palette(item)
	print("[Costume] Equipped '%s' on layer %s" % [item.name, costume_layer])

	# If it's a hat, re-evaluate hair visibility.
	if costume_layer == "14head":
		hat = tex
		if hat_replaces_hair:
			var h := _sprite_layers.get_node_or_null("13hair") as Sprite2D if _sprite_layers != null else null
			if h != null:
				h.visible = false

# Equip a weapon by swapping the farmer_1h_weapon sprite's texture to
# the MSCA weapon sheet indicated by item.weapon_sheet (1-7).
func _equip_weapon(item: Resource) -> void:
	if _sprite_layers == null:
		return

	var weapon_sprite := _sprite_layers.get_node_or_null("farmer_1h_weapon") as Sprite2D
	if weapon_sprite == null:
		push_warning("[Costume] farmer_1h_weapon sprite not found")
		return

	var weapon_sheet: int = int(item.weapon_sheet)
	if weapon_sheet <= 0:
		print("[Costume] Weapon '%s' has no WeaponSheet — keeping default" % item.name)
		return

	# Weapon sheet filenames: "farmer 1hwpn 00N 32x32 v00.png"
	var path: String = "res://assets/sprites/player/farmer/effects/farmer 1hwpn 00%d 32x32 v00.png" % weapon_sheet
	if not ResourceLoader.exists(path):
		push_warning("[Costume] Weapon sheet not found: %s" % path)
		return

	weapon_sprite.texture = load(path) as Texture2D
	print("[Costume] Equipped weapon '%s' (sheet %d)" % [item.name, weapon_sheet])

# Unequip the visual for a given layer.
func unequip_layer(layer_name: String) -> void:
	if layer_name.is_empty():
		return
	set_layer(layer_name, null)
	_clear_layer_material(layer_name)

	# If unequipping hat, restore hair.
	if layer_name == "14head":
		hat = null
		if hair != null:
			set_layer("13hair", hair)

# Apply the baked palette swap for this item to its costume layer. If no
# palette is registered, clears any prior swap so the previously-equipped
# item's colors don't bleed through. Skin recoloring lives on layer
# 01body, never on a clothing layer, so resetting here is safe — it
# can't clobber the skin cycler's material.
func _apply_costume_palette(item: Resource) -> void:
	if _sprite_layers == null:
		return
	var layer_name: String = String(item.costume_layer)
	var layer := _sprite_layers.get_node_or_null(layer_name) as Sprite2D
	if layer == null:
		return

	var palette: Variant = CostumePaletteRegistry.get_palette(int(item.id))
	if palette == null:
		layer.material = null
		return
	var pair: Dictionary = palette
	layer.material = PaletteSwapper.create_material(pair["base"], pair["variant"])

func _clear_layer_material(layer_name: String) -> void:
	if _sprite_layers == null:
		return
	var layer := _sprite_layers.get_node_or_null(layer_name) as Sprite2D
	if layer != null:
		layer.material = null

# Restore all equipment visuals from the Inventory singleton. Clears
# every costume layer first so any @export Inspector defaults (e.g. a
# hat texture set in the player scene) don't bleed through for
# categories the inventory has unequipped.
func restore_equipment() -> void:
	# Wipe every clothing layer to a clean slate. Skin (01body) and the
	# body-shape layers (00undr, etc.) are intentionally not touched —
	# those are character identity, not equipment.
	var costume_layers: Array = [
		"13hair", "14head", "05shrt", "04lwr1", "06lwr2", "08lwr3",
		"03fot1", "07fot2", "09hand", "10outr", "11neck", "12face",
	]
	for layer in costume_layers:
		set_layer(layer, null)
		_clear_layer_material(layer)

	# Equip whatever the inventory currently has on. Inventory is a
	# GDScript autoload; ItemData is still C#, accessed via Resource.
	var categories: Array = [
		ITEM_CATEGORY_HEAD, ITEM_CATEGORY_NECK, ITEM_CATEGORY_BODY,
		ITEM_CATEGORY_HAND, ITEM_CATEGORY_LEGS, ITEM_CATEGORY_BOOT,
		ITEM_CATEGORY_HAIR, ITEM_CATEGORY_WEAPON,
	]
	for cat in categories:
		var item: Resource = Inventory.get_equipped(cat)
		if item != null:
			equip_item(item)

	# Apply persisted hair / hair-color / skin so the character looks
	# the way the player chose at NewGame (random) or last save.
	# SaveManager is still C# this cluster — the autoload NAME is the
	# Node instance (no .Instance indirection from GDScript; that's
	# a C# static accessor and isn't exposed via Variant). C# instance
	# properties ARE accessible via PascalCase.
	var save: Resource = SaveManager.CurrentData
	if save != null and _sprite_layers != null:
		CharacterCustomization.apply(
			_sprite_layers,
			int(save.hair_style_index),
			int(save.hair_color_index),
			int(save.skin_index),
		)

# Resolve a base sheet texture from the CostumeLayer and CostumeId. The
# base sheet lives at res://assets/sprites/player/farmer/sheets/{layer}/
# {baseFile}.png where baseFile is extracted from the C3 costume string.
func _resolve_base_sheet(layer: String, costume_id: String) -> Texture2D:
	if costume_id.is_empty():
		return null

	# Extract base filename from C3 costume string.
	var base_file := _extract_base_file_name(costume_id, layer)
	if base_file.is_empty():
		return null

	var path: String = "res://assets/sprites/player/farmer/sheets/%s/%s.png" % [layer, base_file]
	if ResourceLoader.exists(path):
		return load(path) as Texture2D

	# Sibling fallback — Mana Seed shape variants (00, 00a, 00b, 00c…)
	# share the same default ramp.
	var sibling := _find_sibling_base(layer, base_file)
	if sibling != "":
		print("[Costume] Using sibling base sheet '%s' for '%s'" % [sibling.get_file(), base_file])
		return load(sibling) as Texture2D

	push_warning("[Costume] Base sheet not found: %s" % path)
	return null

# For a missing base file like "fbas_11neck_cloakwithmantleplain_00",
# scan the layer dir for any "<stem>_00<letter?>.png" (no letter, a..f)
# and return the first hit. Returns "" if no sibling exists.
static func _find_sibling_base(layer: String, expected_base: String) -> String:
	var dir: String = "res://assets/sprites/player/farmer/sheets/%s/" % layer
	var regex := RegEx.new()
	regex.compile("^(.+?)_00[a-z]?$")
	var m := regex.search(expected_base)
	if m == null:
		return ""
	var stem: String = m.get_string(1)
	for letter in ["", "a", "b", "c", "d", "e", "f"]:
		var candidate: String = "%s%s_00%s.png" % [dir, stem, letter]
		if ResourceLoader.exists(candidate):
			return candidate
	return ""

# Extract the base filename from a C3 costume string. Examples:
#   "51_fbas_14head_boaterhat_00d_straw_boat" → "fbas_14head_boaterhat_00d"
#   "fbas_13hair_bob1_00_blonde_bob"          → "fbas_13hair_bob1_00"
#   "102_fbas_07fot2_fbas_07fot2_cuffedboots_00a_red"
#                                              → "fbas_07fot2_cuffedboots_00a"
static func _extract_base_file_name(costume_id: String, layer: String) -> String:
	# Strip numeric prefix (e.g., "51_", "102_").
	var prefix_re := RegEx.new()
	prefix_re.compile("^\\d+_")
	var stripped: String = prefix_re.sub(costume_id, "", false)

	# Handle doubled layer prefix (typo in data: "fbas_07fot2_fbas_07fot2_cuffedboots_00a_red").
	var layer_prefix: String = "fbas_%s_" % layer
	if stripped.begins_with(layer_prefix + layer_prefix):
		stripped = stripped.substr(layer_prefix.length())

	# Find the variant code (00, 00a, 00b, 00d, 01, 02, etc.) — this
	# ends the base filename. Pattern: _XX or _XXx where X is digit and
	# x is optional letter.
	var variant_re := RegEx.new()
	variant_re.compile("_(\\d{2}[a-d]?)(?:_|$)")
	var match_result := variant_re.search(stripped)
	if match_result == null:
		return stripped  # fallback: use the whole thing

	var end_idx: int = match_result.get_end()
	var result: String = stripped.substr(0, end_idx).rstrip("_")
	return result

# When equipping any leg-type layer, clear the OTHER leg layers so only
# one is visible at a time. Layers: 04lwr1 (pants/shorts), 06lwr2
# (overalls), 08lwr3 (dresses/skirts) — all three are in the "Legs"
# category.
#
# The early-return is load-bearing: without it, equipping any non-leg
# item would clear all three leg layers and the player would lose their
# pants on every shoe change.
func _handle_leg_exclusion(layer_name: String) -> void:
	var leg_layers: Array = ["04lwr1", "06lwr2", "08lwr3"]
	if not leg_layers.has(layer_name):
		return
	for layer in leg_layers:
		if layer != layer_name:
			set_layer(layer, null)

# The Boot category covers two sprite layers: 03fot1 (low shoes /
# slippers) and 07fot2 (boots over pant legs). Without this, swapping
# from a 07fot2 boot to a 03fot1 shoe (or vice versa) leaves the
# previous boot's layer visible.
func _handle_boot_exclusion(layer_name: String) -> void:
	var boot_layers: Array = ["03fot1", "07fot2"]
	if not boot_layers.has(layer_name):
		return
	for layer in boot_layers:
		if layer != layer_name:
			set_layer(layer, null)
			_clear_layer_material(layer)
