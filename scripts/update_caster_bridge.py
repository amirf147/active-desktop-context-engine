#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi

"""
Synchronizes ADCE bridge improvements into local Caster user content (%LOCALAPPDATA%/caster).
Updates adce_bridge.py with canonical zone, pane, view, section, and path predicates,
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
Enables instant voice-driven correction of active desktop controls, panes, views, and sections.
"""

from dragonfly import MappingRule, Function, Grammar
from caster_user_content.util.adce_bridge import adce


def tag_control(zone_name, pane=None, view=None, section=None, scope="element"):
    success = adce.tag_current_control(zone_name, target_pane=pane, target_view=view, target_section=section, scope=scope)
    if success:
        print(f"[Caster Voice] Tagged active control as [{zone_name}] (pane: {pane}, view: {view}, section: {section}, scope: {scope})")


class AdceTaggingRule(MappingRule):
    mapping = {
        # Fine-grained typing zones
        "tag zone commit [box]": Function(lambda: tag_control("GitCommitBox", pane="PrimarySidebar", view="SourceControl", section="CommitBox")),
        "tag zone editor": Function(lambda: tag_control("EditorBuffer", pane="MainContent", view="Editor")),
        "tag zone terminal": Function(lambda: tag_control("Terminal", pane="BottomPanel", view="Terminal")),
        "tag zone explorer": Function(lambda: tag_control("SidebarExplorer", pane="PrimarySidebar", view="Explorer")),
        "tag zone chat": Function(lambda: tag_control("ChatPrompt", pane="AuxiliarySidebar", view="Chat", section="ChatPrompt")),
        "tag zone timeline": Function(lambda: tag_control("Timeline", pane="PrimarySidebar", view="Explorer", section="Timeline")),
        "tag zone outline": Function(lambda: tag_control("Outline", pane="PrimarySidebar", view="Explorer", section="Outline")),
        "tag zone activity bar": Function(lambda: tag_control("ActivityBar", pane="ActivityBar", view="ActivityBar")),
        "tag zone tabs": Function(lambda: tag_control("TabBar", pane="TopBar")),
        "tag zone address bar": Function(lambda: tag_control("AddressBar", pane="TopBar")),
        "tag zone status bar": Function(lambda: tag_control("StatusBar", pane="StatusBar")),
        "tag zone quick open": Function(lambda: tag_control("QuickOpen", pane="OverlayModal", view="QuickOpen")),
        "tag zone dialog": Function(lambda: tag_control("SystemDialog", pane="OverlayModal")),

        # Macro panes
        "tag pane sidebar": Function(lambda: tag_control("SidebarExplorer", pane="PrimarySidebar", scope="container")),
        "tag pane auxiliary": Function(lambda: tag_control("ChatPrompt", pane="AuxiliarySidebar", scope="container")),
        "tag pane editor": Function(lambda: tag_control("EditorBuffer", pane="MainContent", scope="container")),
        "tag pane terminal": Function(lambda: tag_control("Terminal", pane="BottomPanel", scope="container")),

        # Views and sections
        "tag view explorer": Function(lambda: tag_control("SidebarExplorer", pane="PrimarySidebar", view="Explorer", scope="container")),
        "tag view git": Function(lambda: tag_control("GitCommitBox", pane="PrimarySidebar", view="SourceControl", scope="container")),
        "tag section timeline": Function(lambda: tag_control("Timeline", pane="PrimarySidebar", view="Explorer", section="Timeline")),
        "tag section outline": Function(lambda: tag_control("Outline", pane="PrimarySidebar", view="Explorer", section="Outline")),
    }


grammar = Grammar("adce_tagging_grammar")
grammar.add_rule(AdceTaggingRule())
grammar.load()
'''

HIERARCHY_BRIDGE_METHODS = '''
    def get_current_pane(self) -> str:
        """Returns the current window pane location (e.g. 'primary_sidebar', 'auxiliary_sidebar', 'main_content')."""
        snap = self.get_snapshot()
        if not snap:
            return "unknown"
        focus = snap.get("Focus") or snap.get("focus") or {}
        pane = focus.get("PaneLocation") or focus.get("pane_location") or "unknown"
        return str(pane).lower().replace("_", "")

    def get_current_view(self) -> str:
        """Returns the active view name (e.g. 'Explorer', 'SourceControl', 'Chat')."""
        snap = self.get_snapshot()
        if not snap:
            return ""
        focus = snap.get("Focus") or snap.get("focus") or {}
        return focus.get("ActiveView") or focus.get("active_view") or ""

    def get_current_section(self) -> str:
        """Returns the active section/accordion name (e.g. 'Timeline', 'Outline', 'CommitBox')."""
        snap = self.get_snapshot()
        if not snap:
            return ""
        focus = snap.get("Focus") or snap.get("focus") or {}
        return focus.get("SectionName") or focus.get("section_name") or ""

    def get_semantic_path(self) -> list:
        """Returns the hierarchical semantic path (e.g. ['PrimarySidebar', 'Explorer', 'Timeline'])."""
        snap = self.get_snapshot()
        if not snap:
            return []
        focus = snap.get("Focus") or snap.get("focus") or {}
        return focus.get("SemanticPath") or focus.get("semantic_path") or []

    def in_pane(self, pane_name: str) -> bool:
        """Returns True if the focused control is within the specified window pane."""
        norm_target = pane_name.lower().replace("_", "").replace("-", "")
        return self.get_current_pane() == norm_target

    def in_view(self, view_name: str) -> bool:
        """Returns True if the active view matches the specified view name."""
        norm_target = view_name.lower().replace("_", "").replace("-", "")
        return self.get_current_view().lower().replace("_", "").replace("-", "") == norm_target

    def in_section(self, section_name: str) -> bool:
        """Returns True if the active section matches the specified section name."""
        norm_target = section_name.lower().replace("_", "").replace("-", "")
        return self.get_current_section().lower().replace("_", "").replace("-", "") == norm_target

    def matches_path(self, path_spec: str) -> bool:
        """Returns True if the control's semantic path contains the given segment."""
        norm_spec = path_spec.lower().replace("_", "").replace("-", "")
        for segment in self.get_semantic_path():
            if norm_spec in segment.lower().replace("_", "").replace("-", ""):
                return True
        return False

    def tag_current_control(self, target_zone: str, target_pane: str = None, target_view: str = None, target_section: str = None, scope: str = "element", comment: str = None) -> bool:
        """Dispatches an MCP tool call to tag_active_control on the ADCE daemon with pane/view/section support."""
        try:
            args = {
                "target_zone": target_zone,
                "scope": scope,
                "comment": comment or f"Tagged via Caster voice command as {target_zone}"
            }
            if target_pane:
                args["target_pane"] = target_pane
            if target_view:
                args["target_view"] = target_view
            if target_section:
                args["target_section"] = target_section
            params = {
                "name": "tag_active_control",
                "arguments": args
            }
            self._send_mcp_request(method="tools/call", params=params)
            printer.out(f"ADCE: Tagged active control as [{target_zone}]")
            print(f"\\n[ADCE Tagging] Tagged active control as [{target_zone}] (pane: {target_pane}, view: {target_view}, section: {target_section}, scope: {scope})")
            return True
        except Exception as ex:
            _logger.error("Failed to tag active control: %s", ex)
            return False

    def tag_current_zone(self, target_zone: str, scope: str = "element", comment: str = None) -> bool:
        """Dispatches an MCP tool call to tag_active_control on the ADCE daemon."""
        return self.tag_current_control(target_zone, scope=scope, comment=comment)
'''

PREDICATES_CONTENT = '''

def is_in_pane(pane_name: str):
    """Dragonfly FuncContext predicate generator matching a window pane."""
    return lambda **kwargs: adce.in_pane(pane_name)


def is_in_view(view_name: str):
    """Dragonfly FuncContext predicate generator matching an active view."""
    return lambda **kwargs: adce.in_view(view_name)


def is_in_section(section_name: str):
    """Dragonfly FuncContext predicate generator matching an active section."""
    return lambda **kwargs: adce.in_section(section_name)


def is_path_matched(path_spec: str):
    """Dragonfly FuncContext predicate generator matching a semantic path."""
    return lambda **kwargs: adce.matches_path(path_spec)
'''


def update_caster():
    if not os.path.exists(BRIDGE_PATH):
        print(f"Error: Could not find Caster bridge at {BRIDGE_PATH}")
        sys.exit(1)

    with open(BRIDGE_PATH, "r", encoding="utf-8") as f:
        content = f.read()

    # 1. Update zone predicates
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

    # 2. Add or replace hierarchy bridge methods on AdceClient
    if "def get_current_pane" not in content:
        insertion_target = "    def is_ide_git_commit(self) -> bool:"
        if insertion_target in content:
            idx = content.find(insertion_target)
            next_def = content.find("\n\n# Global client", idx)
            content = content[:next_def] + HIERARCHY_BRIDGE_METHODS + content[next_def:]

    # 3. Add top-level Dragonfly FuncContext predicates if missing
    if "def is_in_pane(" not in content:
        content = content.rstrip() + PREDICATES_CONTENT + "\n"

    with open(BRIDGE_PATH, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Successfully updated {BRIDGE_PATH}")

    # 4. Write tagging rule
    os.makedirs(CASTER_RULES_DIR, exist_ok=True)
    with open(TAGGING_RULE_PATH, "w", encoding="utf-8") as f:
        f.write(TAGGING_RULE_CONTENT)
    print(f"Successfully created {TAGGING_RULE_PATH}")


if __name__ == "__main__":
    update_caster()
