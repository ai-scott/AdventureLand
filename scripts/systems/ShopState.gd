extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Global shop-mode flag. Mirrors C3's ShopMode global var: true when the
# player is standing in a shop layout (Blacksmith, Adventure Shop, General
# Store, Penny's House). Item pickups branch on this to decide between a
# free collect and a purchase prompt.
#
# Scene lifecycle:
#   - WorldMeta._ready sets is_active = is_shop on scene load.
#   - Scene unload clears it automatically (on_scene_leaving).
#
# Free-grant mechanic:
#   The `next_item_free` flag, set by a dialogue action (grantFreeItem),
#   lets the next purchase in a shop be free. Consumed by ItemTrigger on
#   pickup. One-shot, scene-scoped — resets on scene change so authors
#   can't grant a free item "for use in a different shop".
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/ShopState.gd
#   Name: ShopState

var is_active: bool = false

# True if a dialogue has granted the player a single free purchase.
# Consumed by the next successful pickup in a shop.
var next_item_free: bool = false

# Called by WorldMeta on scene load. is_shop=false also clears any pending
# next_item_free so the grant doesn't leak between shops.
func set_active(is_shop: bool) -> void:
	is_active = is_shop
	if not is_shop:
		next_item_free = false
