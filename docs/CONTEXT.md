<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

# ADCE Domain Context & Reference Specification

> **Target System:** Active Desktop Context Engine (ADCE)  
> **Source Lineage:** Evolved from research in `caster/docs/accessibility_mcp/`  

---

## 1. Project Purpose & Scope

The **Active Desktop Context Engine (ADCE)** tracks active state across the Windows OS to supply local AI models and voice frameworks with instant, high-fidelity awareness of user focus and active tasks.

### Core Context Graph Nodes:
1. **Workspace:** Virtual Desktop ID, Virtual Desktop Name, Multi-monitor coordinates.
2. **Foreground Window:** Top-level HWND, Win32 Window Class, Title, Process ID, Process Path.
3. **Tabstrip / Documents:** Open tabs (Index, Title, IsActive, URL snippet) across Web Browsers (Waterfox, Chrome, Edge) and IDEs (VS Code, Cursor, Antigravity).
4. **Focused Control:** UIA ControlType (Edit, Document, Button, etc.), AutomationId, Bounding Rectangle, and current value snippet.

---

## 2. Pointers to Foundational Caster Research

For detailed telemetry, COM reverse-engineering history, and design rationales, refer to the Caster documentation repository:

* **Docs Directory:** `%LOCALAPPDATA%\caster\docs\accessibility_mcp\`
* **Key Reference Documents:**
  * **[008: Real-World Observations](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/008_real_world_observations_and_caching_architecture.md)** — Analysis of real-world DOM trees, 50-tab scenarios, and caching design.
  * **[010: Traversal Telemetry & Live Findings](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/010_telemetry_benchmarks_and_live_findings.md)** — Empirical benchmarks of COM recursion overhead in Python.
  * **[011: FlaUI Evaluation & Dual-Plane Architecture](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/011_flaui_evaluation_and_dual_plane_architecture.md)** — FlaUI capabilities and dual-plane architecture vision.
  * **[013: Empirical Post-Mortem & Event Diagnostics](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/013_v23_empirical_postmortem_and_event_diagnostics.md)** — WinEvent hook reliability and debouncing diagnostics.
  * **[014: C# Daemon Handover & Skill Specification](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/014_csharp_daemon_handover_and_skill_spec.md)** — Full specification of the C# daemon architecture, threading models, and schemas.
  * **[015: Epistemic Recalibration & Adversarial Review](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/015_recalibration_and_adversarial_architecture_review.md)** — The 4-gate verification protocol and adversarial red-team analysis.

---

## 3. Critical Architectural Rules

1. **Zero Browser DOM Crawling:**  
   Never recursively search or traverse descendant trees of `MozillaWindowClass` or `Chrome_WidgetWin_1`. Web browsers expose thousands of DOM elements over COM. Deep walks lead to 5–10 second freezes.
2. **Use UIA3 `CacheRequest` with Scoped Boundaries:**  
   Locate the specific tabstrip / tab container control directly, then execute a `CacheRequest` with `TreeScope.Children` requesting `NameProperty` and `SelectionItemPatternId`. This executes in a single batched IPC round-trip.
3. **Decouple WinEvent Hooks from UIA Queries:**  
   OS hooks (`SetWinEventHook`) must only post lightweight event tokens into an asynchronous `Channel<DesktopEvent>` and return immediately. UIA queries must run on a dedicated MTA thread pool worker.
4. **Trailing-Edge Focus Debouncing (50–75ms):**  
   Rapid window activations or focus transitions emit high-frequency bursts. Debouncing prevents intermediate query thrashing.
5. **Self-Monitoring Immunity:**  
   The daemon must filter out its own process ID, its console window handle (`GetConsoleWindow()`), and parent terminal processes (`WindowsTerminal.exe`, `conhost.exe`).

---

## 4. MCP Output JSON Schema

```json
{
  "timestamp": "2026-08-23T04:00:00.000Z",
  "workspace": {
    "virtual_desktop_id": "3f2a1b0c-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
    "virtual_desktop_name": "Development",
    "desktop_index": 1
  },
  "window": {
    "hwnd": "0x002A0B42",
    "title": "Waterfox - Technical Documentation",
    "process_name": "waterfox.exe",
    "pid": 35572,
    "class_name": "MozillaWindowClass"
  },
  "tabs": {
    "container_type": "TreeStyleTab",
    "total_count": 26,
    "active_tab": "Technical Documentation",
    "items": [
      { "index": 1, "title": "Developer Portal", "is_active": false },
      { "index": 2, "title": "Technical Documentation", "is_active": true },
      { "index": 3, "title": "Issue Tracker", "is_active": false }
    ]
  },
  "focus": {
    "control_type": "DocumentControl",
    "element_name": "Technical Documentation",
    "automation_id": "",
    "bounding_box": { "left": 1031, "top": 36, "width": 888, "height": 1131 },
    "value_snippet": "https://docs.example.org/spec"
  }
}
```
