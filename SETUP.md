# Adventure Land — Setup

## Prerequisites
- **Godot 4.6.3** (standard build, not .NET). GDScript only.
- **Tiled 1.10+** for map editing.
- **Python 3 + Pillow** (`pip3 install pillow`) for the bake/pack tools in `tools/`.

## Open the project
1. Clone the repo somewhere **outside iCloud Drive** (e.g. `~/Developer/AdventureLand`).
   iCloud's file provider makes Godot's file reads crawl and the game hangs on "Loading...".
2. Godot → **Import** → select `project.godot` → **Import & Edit**. The first import takes a minute.
3. Run with **Cmd+B**. The game boots to the title screen.

## Map authoring (Tiled)
Install the autobake extension so saving a TMX regenerates the CSV + trigger `.tres`
files Godot reads — see `tools/README.md`. Without it, run `python3 tools/bake_all.py`
after editing any map.

## Web export (itch.io)
1. Editor → **Manage Export Templates** → Download and Install (once per Godot version).
2. **Project → Export → Web → Export Project** into `exports/` (gitignored).
3. Zip the contents of `exports/` and upload to itch.io.

See `CLAUDE.md` for architecture, conventions, and gotchas.
