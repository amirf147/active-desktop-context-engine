#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi

"""
Baseline Semantic Rules Seeder for ADCE.
Analyzes historical snapshots from %LOCALAPPDATA%/ADCE/adce_history.db and
generates %LOCALAPPDATA%/ADCE/semantic_rules.json with high-confidence rules
for developer environments (VS Code, Antigravity IDE, Waterfox, Windows Terminal).
"""

import json
import os
import sqlite3
import sys
from datetime import datetime, timezone

LOCAL_APPDATA = os.environ.get("LOCALAPPDATA") or os.path.expandvars("%LOCALAPPDATA%")
ADCE_DIR = os.path.join(LOCAL_APPDATA, "ADCE")
DB_PATH = os.path.join(ADCE_DIR, "adce_history.db")
RULES_PATH = os.path.join(ADCE_DIR, "semantic_rules.json")

SEED_RULES = [
    # --- Antigravity IDE / VS Code ---
    {
        "ruleId": "seed_ide_git_commit_scm",
        "targetZone": "GitCommitBox",
        "processPattern": "antigravity",
        "controlType": "Edit",
        "automationIdPattern": "scm.input",
        "priority": 60,
        "isUserOverride": False,
        "comment": "Antigravity IDE source control commit message input box",
    },
    {
        "ruleId": "seed_code_git_commit_scm",
        "targetZone": "GitCommitBox",
        "processPattern": "code",
        "controlType": "Edit",
        "automationIdPattern": "scm.input",
        "priority": 60,
        "isUserOverride": False,
        "comment": "VS Code source control commit message input box",
    },
    {
        "ruleId": "seed_ide_git_commit_name",
        "targetZone": "GitCommitBox",
        "processPattern": "antigravity",
        "elementNamePattern": "Message (Ctrl+Enter to commit",
        "priority": 60,
        "isUserOverride": False,
        "comment": "Antigravity IDE commit message prompt",
    },
    {
        "ruleId": "seed_code_git_commit_name",
        "targetZone": "GitCommitBox",
        "processPattern": "code",
        "elementNamePattern": "Message (Ctrl+Enter to commit",
        "priority": 60,
        "isUserOverride": False,
        "comment": "VS Code commit message prompt",
    },
    {
        "ruleId": "seed_ide_editor_monaco",
        "targetZone": "EditorBuffer",
        "processPattern": "antigravity",
        "classNamePattern": "monaco-editor",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Monaco editor code buffer in Antigravity IDE",
    },
    {
        "ruleId": "seed_code_editor_monaco",
        "targetZone": "EditorBuffer",
        "processPattern": "code",
        "classNamePattern": "monaco-editor",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Monaco editor code buffer in VS Code",
    },
    {
        "ruleId": "seed_ide_editor_edit_context",
        "targetZone": "EditorBuffer",
        "processPattern": "antigravity",
        "classNamePattern": "native-edit-context",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Monaco native edit context buffer",
    },
    {
        "ruleId": "seed_ide_terminal",
        "targetZone": "Terminal",
        "processPattern": "antigravity",
        "automationIdPattern": "terminal",
        "priority": 55,
        "isUserOverride": False,
        "comment": "Antigravity IDE integrated terminal",
    },
    {
        "ruleId": "seed_code_terminal",
        "targetZone": "Terminal",
        "processPattern": "code",
        "automationIdPattern": "terminal",
        "priority": 55,
        "isUserOverride": False,
        "comment": "VS Code integrated terminal",
    },
    {
        "ruleId": "seed_pwsh_console",
        "targetZone": "Terminal",
        "processPattern": "pwsh",
        "classNamePattern": "ConsoleWindowClass",
        "priority": 50,
        "isUserOverride": False,
        "comment": "PowerShell console window",
    },
    {
        "ruleId": "seed_windows_terminal",
        "targetZone": "Terminal",
        "processPattern": "windowsterminal",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Windows Terminal container",
    },
    {
        "ruleId": "seed_ide_chat_input",
        "targetZone": "ChatPrompt",
        "processPattern": "antigravity",
        "automationIdPattern": "chat-input",
        "priority": 60,
        "isUserOverride": False,
        "comment": "Antigravity AI chat interactive prompt",
    },
    {
        "ruleId": "seed_ide_chat_session",
        "targetZone": "ChatPrompt",
        "processPattern": "antigravity",
        "automationIdPattern": "interactive-session",
        "priority": 60,
        "isUserOverride": False,
        "comment": "Antigravity AI interactive session",
    },
    {
        "ruleId": "seed_ide_explorer_tree",
        "targetZone": "SidebarExplorer",
        "processPattern": "antigravity",
        "controlType": "TreeItem",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Project file explorer tree item",
    },
    {
        "ruleId": "seed_code_explorer_tree",
        "targetZone": "SidebarExplorer",
        "processPattern": "code",
        "controlType": "TreeItem",
        "priority": 50,
        "isUserOverride": False,
        "comment": "VS Code project file explorer tree item",
    },
    {
        "ruleId": "seed_ide_quick_input",
        "targetZone": "QuickOpen",
        "processPattern": "antigravity",
        "automationIdPattern": "quickInput",
        "priority": 60,
        "isUserOverride": False,
        "comment": "Quick open file / command picker",
    },
    {
        "ruleId": "seed_ide_tabs",
        "targetZone": "TabBar",
        "processPattern": "antigravity",
        "controlType": "TabItem",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Editor tab strip",
    },

    # --- Waterfox / Web Browsers ---
    {
        "ruleId": "seed_waterfox_urlbar",
        "targetZone": "AddressBar",
        "processPattern": "waterfox",
        "automationIdPattern": "urlbar",
        "priority": 60,
        "isUserOverride": False,
        "comment": "Waterfox URL address and search bar",
    },
    {
        "ruleId": "seed_waterfox_document",
        "targetZone": "WebDocument",
        "processPattern": "waterfox",
        "controlType": "Document",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Waterfox rendered web page body",
    },
    {
        "ruleId": "seed_waterfox_tab",
        "targetZone": "TabBar",
        "processPattern": "waterfox",
        "controlType": "TabItem",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Waterfox browser tab header",
    },

    # --- Windows Shell / Explorer ---
    {
        "ruleId": "seed_explorer_items_view",
        "targetZone": "ShellItemList",
        "processPattern": "explorer",
        "classNamePattern": "ItemsView",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Windows Explorer file list view",
    },
    {
        "ruleId": "seed_explorer_nav_tree",
        "targetZone": "SidebarExplorer",
        "processPattern": "explorer",
        "controlType": "TreeItem",
        "priority": 50,
        "isUserOverride": False,
        "comment": "Windows Explorer navigation tree",
    },
]


def seed_rules():
    now_utc = datetime.now(timezone.utc).isoformat()
    for r in SEED_RULES:
        r["createdAtUtc"] = now_utc

    existing_rules = []
    if os.path.exists(RULES_PATH):
        try:
            with open(RULES_PATH, "r", encoding="utf-8") as f:
                existing_rules = json.load(f)
            print(f"Loaded {len(existing_rules)} existing rules from {RULES_PATH}")
        except Exception as e:
            print(f"Warning reading existing rules: {e}")

    # Index existing rules by ruleId to preserve user overrides
    rule_map = {}
    for r in existing_rules:
        rule_map[r.get("ruleId")] = r

    # Insert or update seed rules without overwriting user overrides
    added_count = 0
    updated_count = 0
    for seed in SEED_RULES:
        rid = seed["ruleId"]
        if rid not in rule_map:
            rule_map[rid] = seed
            added_count += 1
        elif not rule_map[rid].get("isUserOverride", False):
            rule_map[rid] = seed
            updated_count += 1

    merged_rules = list(rule_map.values())
    merged_rules.sort(key=lambda r: (r.get("priority", 0), r.get("createdAtUtc", "")), reverse=True)

    os.makedirs(ADCE_DIR, exist_ok=True)
    with open(RULES_PATH, "w", encoding="utf-8") as f:
        json.dump(merged_rules, f, indent=2)

    print(f"\nSuccessfully seeded rules to {RULES_PATH}")
    print(f"Total Rules: {len(merged_rules)} (Added: {added_count}, Updated: {updated_count})")
    print("\nSample Active Rules:")
    for r in merged_rules[:10]:
        print(f"  [{r['targetZone']}] {r.get('processPattern', '*')} | {r.get('automationIdPattern') or r.get('classNamePattern') or r.get('controlType')} (P{r.get('priority')})")


if __name__ == "__main__":
    seed_rules()
