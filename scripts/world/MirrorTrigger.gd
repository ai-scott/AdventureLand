class_name MirrorTrigger extends Area2D

# Interact-area in front of a mirror sprite (Player's Home, Penny's
# House). When the player stands in range and presses interact, opens
# the inventory UI. Reads as "the character checks themselves in the
# mirror" — a natural, world-coupled affordance for inventory access
# on top of the standard I/Tab keyboard toggle.

var _player_in_range: bool = false

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

func _exit_tree() -> void:
	InteractHintManager.unregister(self)

func _process(_delta: float) -> void:
	if not _player_in_range:
		return
	if Input.is_action_just_pressed("interact"):
		# Inventory's open() is idempotent so a double-fire wouldn't
		# break anything, but the hint should hide while the inventory
		# is up. InventoryUI is the GDScript autoload (Cluster 10g);
		# the autoload Node IS the singleton, so calling open() on it
		# directly works.
		InventoryUI.open()

func _on_body_entered(body: Node) -> void:
	# PlayerController is C# (still); duck-type via group.
	if not body.is_in_group("player"):
		return
	_player_in_range = true
	InteractHintManager.register(self, func() -> String: return "Look")

func _on_body_exited(body: Node) -> void:
	if not body.is_in_group("player"):
		return
	_player_in_range = false
	InteractHintManager.unregister(self)
