#!/usr/bin/env python3
"""
Analyze eGlobal.json to extract trigger system architecture
"""

import json
import sys
from pathlib import Path

def analyze_event(event, depth=0):
    """Recursively analyze an event and its sub-events"""
    indent = "  " * depth

    # Extract basic event info
    event_type = event.get("eventType", "unknown")

    if event_type == "block":
        conditions = event.get("conditions", [])
        actions = event.get("actions", [])

        # Look for trigger-related conditions
        trigger_conditions = []
        for cnd in conditions:
            cnd_type = cnd.get("type", "")
            params = cnd.get("parameters", [])

            # Check for dialogue-related conditions
            if any(keyword in str(cnd).lower() for keyword in ["indialogue", "currentaction", "buttonmgr", "dialogueresult"]):
                trigger_conditions.append({
                    "type": cnd_type,
                    "params": params,
                    "is_inverted": cnd.get("isInverted", False)
                })

        # Look for trigger-related actions
        trigger_actions = []
        for act in actions:
            act_type = act.get("type", "")
            params = act.get("parameters", [])

            # Check for function calls
            if "callFunction" in act_type.lower():
                func_name = params[0] if params else "unknown"
                if any(keyword in str(func_name).lower() for keyword in ["dialogue", "character", "scene", "trigger"]):
                    trigger_actions.append({
                        "type": act_type,
                        "function": func_name,
                        "params": params
                    })

        return {
            "conditions": trigger_conditions,
            "actions": trigger_actions,
            "subevents": [analyze_event(e, depth+1) for e in event.get("subEvents", [])]
        }

    elif event_type == "group":
        return {
            "type": "group",
            "name": event.get("title", "Unnamed Group"),
            "subevents": [analyze_event(e, depth+1) for e in event.get("subEvents", [])]
        }

    return {}

def find_trigger_events(events):
    """Find all events related to trigger system"""
    trigger_events = []

    for event in events:
        # Look for groups with trigger-related names
        if event.get("eventType") == "group":
            title = event.get("title", "").lower()
            if any(keyword in title for keyword in ["trigger", "interaction", "dialogue", "character", "check"]):
                trigger_events.append({
                    "group": event.get("title"),
                    "analysis": analyze_event(event)
                })

    return trigger_events

def main():
    # Read eGlobal.json
    eglobal_path = Path(__file__).parent.parent / "eventSheets" / "eGlobal.json"

    with open(eglobal_path, 'r') as f:
        data = json.load(f)

    events = data.get("events", [])

    # Find trigger-related events
    trigger_events = find_trigger_events(events)

    # Output analysis
    print("# Trigger System Analysis")
    print(f"\nTotal event groups analyzed: {len(events)}")
    print(f"Trigger-related groups found: {len(trigger_events)}")
    print("\n" + "="*80 + "\n")

    for trigger in trigger_events:
        print(f"## Group: {trigger['group']}")
        print(json.dumps(trigger['analysis'], indent=2))
        print("\n" + "-"*80 + "\n")

if __name__ == "__main__":
    main()
