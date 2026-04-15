#!/usr/bin/env python3
"""
Convert TypeScript dialogue files to Godot .tres Resources.

Usage:
    python3 tools/dialogue_to_tres.py

Reads from:  ../scripts/external/quest-dialogue/*-dialogue.ts
Writes to:   assets/data/dialogue/<npc>.tres

Each .tres mirrors the existing EnemyData pattern — one [GlobalClass] Resource
per file, with sub-resources for nested types (DialogueNode, DialogueCondition,
DialogueAction, DialogueResponse).
"""

import re
import sys
import json
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
DIALOGUE_SRC = PROJECT_ROOT.parent / "scripts" / "external" / "quest-dialogue"
OUTPUT_DIR = PROJECT_ROOT / "assets" / "data" / "dialogue"

# C# enum integer values — must match declaration order in the .cs files.
ACTION_TYPES = [
    "StartQuest", "CompleteQuest", "SetQuestStatus", "GiveItem", "RemoveItem",
    "SetFlag", "SetWorldFlag", "SetNpcMemory", "DeployNpc", "PlaySound",
    "TeleportPlayer", "Input", "Custom", "SpawnUniqueItem", "SummonSeaMonster",
    "MakeSeaMonsterHostile", "SeaMonsterAcceptQuest", "SeaMonsterQuestComplete",
]

CONDITION_TYPES = [
    "QuestStatus", "HasItem", "WorldFlag", "NpcMemory", "PlayerLevel", "Custom",
]

TS_ACTION_MAP = {
    "start_quest": "StartQuest", "complete_quest": "CompleteQuest",
    "set_quest_status": "SetQuestStatus", "give_item": "GiveItem",
    "remove_item": "RemoveItem", "take_item": "RemoveItem",
    "set_flag": "SetFlag", "set_world_flag": "SetWorldFlag",
    "set_npc_memory": "SetNpcMemory", "deploy_npc": "DeployNpc",
    "play_sound": "PlaySound", "teleport_player": "TeleportPlayer",
    "input": "Input", "custom": "Custom", "spawn_unique_item": "SpawnUniqueItem",
    "summon_sea_monster": "SummonSeaMonster",
    "make_sea_monster_hostile": "MakeSeaMonsterHostile",
    "sea_monster_accept_quest": "SeaMonsterAcceptQuest",
    "sea_monster_quest_complete": "SeaMonsterQuestComplete",
}

TS_CONDITION_MAP = {
    "quest_status": "QuestStatus", "has_item": "HasItem",
    "world_flag": "WorldFlag", "npc_memory": "NpcMemory",
    "player_level": "PlayerLevel", "custom": "Custom",
}


def strip_ts_to_json(content):
    """Convert a TypeScript dialogue export to JSON."""
    content = re.sub(r'import\s+.*?;\s*\n', '', content)
    content = re.sub(r'export\s+const\s+\w+\s*:\s*\w+\s*=\s*', '', content)
    content = re.sub(r'export\s+default\s+', '', content)
    content = re.sub(r';\s*$', '', content.strip())
    content = re.sub(r'\s+as\s+\w+', '', content)
    content = re.sub(r'//.*?\n', '\n', content)
    content = re.sub(r'/\*.*?\*/', '', content, flags=re.DOTALL)
    content = re.sub(r"'(\w+)'", r'"\1"', content)
    content = re.sub(r',\s*([\]}])', r'\1', content)
    content = re.sub(r'(?<=[{,\n])\s*(\w+)\s*:', r' "\1":', content)
    return content


def parse_dialogue_file(filepath):
    content = filepath.read_text(encoding="utf-8")
    if "npcId" not in content:
        return None
    json_str = strip_ts_to_json(content)
    try:
        return json.loads(json_str)
    except json.JSONDecodeError as e:
        lines = json_str.split('\n')
        ln = e.lineno - 1
        print(f"  ERROR parsing {filepath.name}: {e}")
        for i in range(max(0, ln-2), min(len(lines), ln+3)):
            print(f"  {'>>>' if i == ln else '   '} {i+1}: {lines[i][:120]}")
        return None


def escape(text):
    """Escape for .tres string values."""
    return text.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n')


class TresWriter:
    def __init__(self):
        self.sub_resources = []  # (id, props_list)
        self.counter = 0

    def next_id(self, prefix):
        self.counter += 1
        return f"{prefix}_{self.counter}"

    def ref(self, sid):
        return f'SubResource("{sid}")'

    def refs_array(self, ext_id, sids):
        refs = ", ".join(self.ref(s) for s in sids)
        return f'Array[ExtResource("{ext_id}")]([{refs}])'

    def write_condition(self, c):
        sid = self.next_id("cond")
        ctype = TS_CONDITION_MAP.get(c.get("type", "quest_status"), "QuestStatus")
        idx = CONDITION_TYPES.index(ctype)

        props = [f'script = ExtResource("3")', f'Type = {idx}']

        negate = c.get("negate", False) or c.get("inverted", False)
        if negate:
            props.append('Negate = true')

        str_fields = [("questId", "QuestId"), ("status", "Status"), ("itemId", "ItemId"),
                      ("flagKey", "FlagKey"), ("flagValue", "FlagValue"),
                      ("npcId", "NpcId"), ("memoryKey", "MemoryKey"), ("memoryValue", "MemoryValue"),
                      ("customCheck", "CustomCheck")]
        int_fields = [("quantity", "Quantity"), ("level", "Level")]

        for ts_key, cs_key in str_fields:
            v = c.get(ts_key)
            if v is not None and v != "":
                props.append(f'{cs_key} = "{escape(str(v))}"')
        for ts_key, cs_key in int_fields:
            v = c.get(ts_key)
            if v is not None:
                props.append(f'{cs_key} = {v}')

        self.sub_resources.append((sid, props))
        return sid

    def write_action(self, a):
        sid = self.next_id("act")
        atype = TS_ACTION_MAP.get(a.get("type", "set_quest_status"), "SetQuestStatus")
        idx = ACTION_TYPES.index(atype)

        props = [f'script = ExtResource("4")', f'Type = {idx}']

        str_fields = [("questId", "QuestId"), ("status", "Status"),
                      ("itemId", "ItemId"), ("itemName", "ItemName"),
                      ("flagKey", "FlagKey"), ("flagValue", "FlagValue"),
                      ("npcId", "NpcId"), ("memoryKey", "MemoryKey"), ("memoryValue", "MemoryValue"),
                      ("worldId", "WorldId"), ("soundId", "SoundId"), ("variable", "Variable"),
                      ("customFunction", "CustomFunction"), ("reason", "Reason")]
        int_fields = [("quantity", "Quantity")]
        float_fields = [("x", "X"), ("y", "Y")]
        bool_fields = [("destroyTrigger", "DestroyTrigger")]

        for ts_key, cs_key in str_fields:
            v = a.get(ts_key)
            if v is not None and v != "":
                props.append(f'{cs_key} = "{escape(str(v))}"')
        for ts_key, cs_key in int_fields:
            v = a.get(ts_key)
            if v is not None:
                props.append(f'{cs_key} = {v}')
        for ts_key, cs_key in float_fields:
            v = a.get(ts_key)
            if v is not None:
                props.append(f'{cs_key} = {float(v)}')
        for ts_key, cs_key in bool_fields:
            v = a.get(ts_key)
            if v:
                props.append(f'{cs_key} = true')

        self.sub_resources.append((sid, props))
        return sid

    def write_response(self, r):
        sid = self.next_id("resp")
        props = [f'script = ExtResource("5")',
                 f'Text = "{escape(r.get("text", ""))}"',
                 f'LeadsTo = "{r.get("leads_to", "")}"']

        conds = r.get("conditions", [])
        if conds:
            cond_ids = [self.write_condition(c) for c in conds]
            props.append(f'Conditions = {self.refs_array("3", cond_ids)}')

        acts = r.get("actions", [])
        if acts:
            act_ids = [self.write_action(a) for a in acts]
            props.append(f'Actions = {self.refs_array("4", act_ids)}')

        self.sub_resources.append((sid, props))
        return sid

    def write_node(self, n):
        sid = self.next_id("node")
        props = [f'script = ExtResource("2")',
                 f'Id = "{n.get("id", "")}"',
                 f'Text = "{escape(n.get("text", ""))}"',
                 f'Speaker = "{n.get("speaker", "")}"',
                 f'Priority = {n.get("priority", 50)}']

        auto = n.get("autoAdvance", "")
        if auto:
            props.append(f'AutoAdvance = "{auto}"')
        if n.get("endsDialogue", False):
            props.append('EndsDialogue = true')

        conds = n.get("conditions", [])
        if conds:
            cond_ids = [self.write_condition(c) for c in conds]
            props.append(f'Conditions = {self.refs_array("3", cond_ids)}')

        resps = n.get("responses", [])
        if resps:
            resp_ids = [self.write_response(r) for r in resps]
            props.append(f'Responses = {self.refs_array("5", resp_ids)}')

        acts = n.get("actions", [])
        if acts:
            act_ids = [self.write_action(a) for a in acts]
            props.append(f'Actions = {self.refs_array("4", act_ids)}')

        self.sub_resources.append((sid, props))
        return sid


def write_tres(npc_data, output_path):
    w = TresWriter()

    nodes = npc_data.get("nodes", [])
    node_ids = [w.write_node(n) for n in nodes]

    npc_id = npc_data.get("npcId", "unknown")
    display_name = npc_data.get("name", npc_id)
    default_node = npc_data.get("defaultNode", "node_000")
    world_id = npc_data.get("worldId", "")
    quest_relations = npc_data.get("questRelations", [])

    lines = []
    lines.append('[gd_resource type="Resource" script_class="DialogueData" format=3]')
    lines.append('')
    lines.append('[ext_resource type="Script" path="res://scripts/data/DialogueData.cs" id="1"]')
    lines.append('[ext_resource type="Script" path="res://scripts/data/DialogueNode.cs" id="2"]')
    lines.append('[ext_resource type="Script" path="res://scripts/data/DialogueCondition.cs" id="3"]')
    lines.append('[ext_resource type="Script" path="res://scripts/data/DialogueAction.cs" id="4"]')
    lines.append('[ext_resource type="Script" path="res://scripts/data/DialogueResponse.cs" id="5"]')
    lines.append('')

    for sid, props in w.sub_resources:
        lines.append(f'[sub_resource type="Resource" id="{sid}"]')
        for p in props:
            lines.append(p)
        lines.append('')

    lines.append('[resource]')
    lines.append('script = ExtResource("1")')
    lines.append(f'NpcId = "{npc_id}"')
    lines.append(f'DisplayName = "{display_name}"')
    lines.append(f'DefaultNode = "{default_node}"')
    lines.append(f'WorldId = "{world_id}"')

    if quest_relations:
        qr = ", ".join(f'"{q}"' for q in quest_relations)
        lines.append(f'QuestRelations = PackedStringArray({qr})')

    if node_ids:
        lines.append(f'Nodes = {w.refs_array("2", node_ids)}')

    lines.append('')

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text('\n'.join(lines), encoding='utf-8')
    return len(nodes)


def main():
    if not DIALOGUE_SRC.exists():
        print(f"ERROR: Dialogue source not found at {DIALOGUE_SRC}")
        sys.exit(1)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    dialogue_files = sorted(DIALOGUE_SRC.glob("*-dialogue.ts"))
    print(f"Found {len(dialogue_files)} dialogue files in {DIALOGUE_SRC}")

    success = 0
    failed = 0

    for filepath in dialogue_files:
        name = filepath.stem.replace("-dialogue", "")
        print(f"\n  Converting {filepath.name}...")

        data = parse_dialogue_file(filepath)
        if data is None:
            print(f"    SKIPPED")
            failed += 1
            continue

        output_path = OUTPUT_DIR / f"{name}.tres"
        node_count = write_tres(data, output_path)
        print(f"    OK → {output_path.name} ({node_count} nodes)")
        success += 1

    print(f"\nDone: {success} converted, {failed} failed/skipped")


if __name__ == "__main__":
    main()
