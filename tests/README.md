# Tests

GUT (Godot Unit Test) specs for the GDScript port. Kept minimal — only the highest-regression-risk paths get coverage.

## Setup

1. Install GUT via Godot AssetLib (Project Settings → Plugins → Enable)
2. Tests live in this directory as `*_test.gd`

## Specs

Per [docs/PORT_PLAN.md](../docs/PORT_PLAN.md) verification protocol, the three planned specs are:

- `save_manager_test.gd` — SaveData round-trip with sample state (added at Phase 7 exit)
- `inventory_test.gd` — Add / remove / equip flow (added at Phase 9 exit)
- `dialogue_traversal_test.gd` — Walk a DialogueData end-to-end (added at Phase 2 exit)

Until those land, validation is manual smoke-test via the golden path.
