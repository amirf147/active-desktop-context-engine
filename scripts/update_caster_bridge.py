#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi

"""
Synchronizes ADCE bridge improvements into local Caster user content (%LOCALAPPDATA%/caster).
Updates adce_bridge.py with canonical zone predicates and tag_current_zone() MCP caller,
and installs adce_tagging.py Dragonfly voice grammar for hands-free semantic tagging.
"""

import os
import sys

LOCAL_APPDATA = os.environ.get("LOCALAPPDATA") or os.path.expandvars("%LOCALAPPDATA%")
CASTER_UTIL_DIR = os.path.join(LOCAL_APPDATA, "caster", "caster_user_content", "util")
CASTER_RULES_DIR = os.path.join(LOCAL_APPDATA, "caster", "caster_user_content", "rules", "apps")
BRIDGE_PATH = os.path.join(CASTER_UTIL_DIR, "adce_bridge.py")
TAGGING_RULE_PATH = os.path.join(CASTER_RULES_DIR, "adce_tagging.py")

TAGGING_RULE_CONTENT = '''# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi

"""
Dragonfly Voice Grammar for Runtime Semantic Zone Tagging in ADCE.
Enables instant voice-driven correction of active desktop controls.
"""

from dragonfly import MappingRule, Function, Grammar
from caster_user_content.util.adce_bridge import adce


def tag_zone(zone_name, scope="element"):
    success = adce.tag_current_zone(zone_name, scope=scope)
    if success:
        print(f"[Caster Voice] Tagged active control as [{zone_name}] (scope: {scope})")


class AdceTaggingRule(MappingRule):
    mapping = {
        "tag zone commit [box]": Function(lambda: tag_zone("GitCommitBox")),
        "tag zone editor": Function(lambda: tag_zone("EditorBuffer")),
        "tag zone terminal": Function(lambda: tag_zone("Terminal")),
        "tag zone explorer": Function(lambda: tag_zone("SidebarExplorer")),
        "tag zone chat": Function(lambda: tag_zone("ChatPrompt")),
        "tag zone tabs": Function(lambda: tag_zone("TabBar")),
        "tag zone address bar": Function(lambda: tag_zone("AddressBar")),
        "tag zone status bar": Function(lambda: tag_zone("StatusBar")),
        "tag zone quick open": Function(lambda: tag_zone("QuickOpen")),
        "tag zone dialog": Function(lambda: tag_zone("SystemDialog")),
        "tag container explorer": Function(lambda: tag_zone("SidebarExplorer", scope="container")),
    }


grammar = Grammar("adce_tagging_grammar")
grammar.add_rule(AdceTaggingRule())
grammar.load()
'''


def update_caster():
    if not os.path.exists(BRIDGE_PATH):
        print(f"Error: Could not find Caster bridge at {BRIDGE_PATH}")
        sys.exit(1)

    with open(BRIDGE_PATH, "r", encoding="utf-8") as f:
        content = f.read()

    # 1. Update predicates
    old_term = 'return is_ide_app and normalized_zone == "integratedterminal"'
    new_term = 'return is_ide_app and normalized_zone in ("terminal", "integratedterminal")'

    old_edit = 'return is_ide_app and normalized_zone == "editorcodebuffer"'
    new_edit = 'return is_ide_app and normalized_zone in ("editorbuffer", "editorcodebuffer")'

    old_git = 'return is_ide_app and normalized_zone == "gitcommitbox"'
    new_git = 'return is_ide_app and normalized_zone in ("gitcommitbox", "scm.input")'

    if old_term in content:
        content = content.replace(old_term, new_term)
    if old_edit in content:
        content = content.replace(old_edit, new_edit)
    if old_git in content:
        content = content.replace(old_git, new_git)

    # 2. Add tag_current_zone if not present
    if "def tag_current_zone" not in content:
        insertion_target = "    def is_ide_git_commit(self) -> bool:"
        if insertion_target in content:
            # Find end of is_ide_git_commit method
            idx = content.find(insertion_target)
            next_def = content.find("\n\n# Global client", idx)
            tag_method = '''

    def tag_current_zone(self, target_zone: str, scope: str = "element", comment: str = None) -> bool:
        """Dispatches an MCP tool call to tag_active_control on the ADCE daemon."""
        try:
            params = {
                "name": "tag_active_control",
                "arguments": {
                    "target_zone": target_zone,
                    "scope": scope,
                    "comment": comment or f"Tagged via Caster voice command as {target_zone}"
                }
            }
            self._send_mcp_request(method="tools/call", params=params)
            printer.out(f"ADCE: Tagged active control as [{target_zone}]")
            print(f"\\n[ADCE Tagging] Tagged active control as [{target_zone}] (scope: {scope})")
            return True
        except Exception as ex:
            _logger.error("Failed to tag active control: %s", ex)
            return False'''
            content = content[:next_def] + tag_method + content[next_def:]

    with open(BRIDGE_PATH, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Successfully updated {BRIDGE_PATH}")

    # 3. Write tagging rule
    os.makedirs(CASTER_RULES_DIR, exist_ok=True)
    with open(TAGGING_RULE_PATH, "w", encoding="utf-8") as f:
        f.write(TAGGING_RULE_CONTENT)
    print(f"Successfully created {TAGGING_RULE_PATH}")


if __name__ == "__main__":
    update_caster()
