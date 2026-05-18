extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Gem wallet. All mutations go through SaveManager.CurrentData.Gems
# (still C# Resource — Pattern C PascalCase property access) so
# saves/loads persist automatically.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/CurrencySystem.gd
#   Name: CurrencySystem

const MAX_GEMS: int = 9999

signal gems_changed(new_amount: int)

func _get_data() -> Resource:
	return SaveManager.current_data

func get_gems() -> int:
	var data := _get_data()
	if data == null:
		return 0
	return int(data.gems)

func can_afford(cost: int) -> bool:
	return cost <= 0 or get_gems() >= cost

# Grant gems (positive amount). Clamped to MAX_GEMS. Returns the amount
# actually added after clamp.
func add_gems(amount: int) -> int:
	if amount <= 0:
		return 0
	var data := _get_data()
	if data == null:
		return 0

	var before: int = int(data.gems)
	var after: int = min(MAX_GEMS, before + amount)
	data.set("gems", after)
	var added: int = after - before
	if added > 0:
		print("[Currency] +%d gems (→ %d)" % [added, after])
		gems_changed.emit(after)
	return added

# Spend gems (positive amount). Returns true if the player had enough;
# false otherwise and nothing is deducted.
func remove_gems(amount: int) -> bool:
	if amount <= 0:
		return true
	var data := _get_data()
	if data == null:
		return false
	var current: int = int(data.gems)
	if current < amount:
		return false

	data.set("gems", current - amount)
	print("[Currency] -%d gems (→ %d)" % [amount, current - amount])
	gems_changed.emit(current - amount)
	return true
