class_name BehaviorCondition extends Resource

## A gating condition on an enemy behavior. Mirrors C3's BehaviorCondition.
## Example: { type=DISTANCE, operator=LESS_THAN, value=150 } → only fires
## when player distance < 150.
## Multiple conditions on one behavior are ANDed together.

## Enum integer values must stay aligned across the C# ↔ GDScript boundary
## until EnemyController.cs ports in Phase 5. Don't reorder.
enum ConditionType {
	DISTANCE = 0,      ## Distance to player in pixels
	HEALTH = 1,        ## Current enemy health
	TIMER = 2,         ## Time since state entry (seconds)
	RANDOM = 3,        ## Random roll 0..1
	HURT = 4,          ## Currently in hurt state (0/1)
	INVULNERABLE = 5,  ## Currently invulnerable (0/1)
}

enum ComparisonOp {
	LESS_THAN = 0,        ## <
	GREATER_THAN = 1,     ## >
	LESS_OR_EQUAL = 2,    ## <=
	GREATER_OR_EQUAL = 3, ## >=
	EQUAL = 4,            ## ==
}

@export var type: ConditionType = ConditionType.DISTANCE
@export var operator: ComparisonOp = ComparisonOp.LESS_THAN
@export var value: float = 0.0
