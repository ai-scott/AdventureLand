@tool
class_name DialogueNode extends Resource

# A single dialogue node — text, speaker, conditions, actions, responses.
# Priority determines evaluation order (highest first).

@export var id: String = ""
@export var text: String = ""
@export var speaker: String = ""
@export var priority: int = 50

@export_group("Flow")
@export var auto_advance: String = ""
@export var ends_dialogue: bool = false

@export_group("Logic")
@export var conditions: Array[DialogueCondition] = []
@export var responses: Array[DialogueResponse] = []
@export var actions: Array[DialogueAction] = []
