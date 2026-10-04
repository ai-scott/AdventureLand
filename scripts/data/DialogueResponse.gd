@tool
class_name DialogueResponse extends Resource

# A player response option in a dialogue node.

@export var text: String = ""
@export var leads_to: String = ""
@export var conditions: Array[DialogueCondition] = []
@export var actions: Array[DialogueAction] = []
