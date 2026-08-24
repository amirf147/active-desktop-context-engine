<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

# ADCE Domain Context & Reference Specification

> **Target System:** Active Desktop Context Engine (ADCE)  
> **Source Lineage:** Evolved from research in [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp)  
> **Runtime:** .NET 10 (x64) + `FlaUI.UIA3 5.0.0`

---

## 1. Project Purpose & System Vision

The **Active Desktop Context Engine (ADCE)** is a high-performance, lightweight Windows background daemon / system tray service. It runs at Windows startup, maintains a live in-memory semantic graph of the user's active desktop state, and exposes it over the **Model Context Protocol (MCP)** to local AI agents, IDE assistants, and voice frameworks (Caster) with sub-millisecond query latency and near-zero idle CPU usage.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                ADCE SYSTEM ARCHITECTURE                                │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  [Windows OS Events] ──> SetWinEventHook (Foreground / Focus / Desktop Switches)       │
│                                    │                                                   │
│                                    ▼                                                   │
│               [Async Channel<DesktopEvent>] (0% Idle CPU Loop)                         │
│                                    │                                                   │
│                                    ▼ (MTA Worker Pool)                                 │
│        [Targeted Multi-Zone UIA3 Extractor] (10–50 ms, Zero DOM Crawling)              │
│        ├── Antigravity / VS Code: tabs-container, monaco-breadcrumbs, sidebar, edit    │
│        ├── Waterfox / Gecko: tabs normal, tabs pinned, urlbar-input                    │
│        ├── Windows 11 Explorer: TabView, PART_BreadcrumbBar, Items View                │
│        └── Virtual Desktops: IVirtualDesktopManager / pyvda COM                        │
│                                    │                                                   │
│                                    ▼                                                   │
│                   [Live Semantic Context Graph Engine]                                 │
│                     ├── Active State In-Memory Cache (< 1 ms MCP query)                │
│                     └── Historical Context Store (Embedded SQLite / DuckDB)            │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [MCP Server Endpoint (SSE / HTTP / Stdio)]                             │
│                 ├── AI Coding Agents (Antigravity / Gemini / Claude)                   │
│                 └── Voice Recognition Grammars (Caster)                                │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Foundational Research & Single Source of Truth References

* **UI Automation Hierarchy SSOT:** [`docs/UI_AUTOMATION_STRUCTURES_REFERENCE.md`](UI_AUTOMATION_STRUCTURES_REFERENCE.md)
* **Empirical Benchmarks:**
  * [001: FlaUI UIA3 Telemetry](benchmarks/001_micro_spike_1_flaui_telemetry.md)
  * [002: Python Shallow vs C# Multi-Zone Telemetry](benchmarks/002_micro_spike_2_python_shallow_telemetry.md)
* **Pointers to Upstream Research ([caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp)):**
  * [`010`: Traversal Telemetry & 6,800-node DOM cost analysis](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/010_telemetry_benchmarks_and_live_findings.md)
  * [`014`: C# Daemon Handover & Skill Specification](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/014_csharp_daemon_handover_and_skill_spec.md)
  * [`015`: Epistemic Recalibration & 4-Gate Protocol](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/015_recalibration_and_adversarial_architecture_review.md)
  * [`016`: Micro-Spike 2 Telemetry & Unified Architecture](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/016_micro_spike_2_win32_shallow_python_telemetry.md)
  * [`017`: Comprehensive UI Automation Tree Structures SSOT](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/017_ui_automation_tree_structures_and_target_zones_reference.md)

---

## 3. Core Architectural Principles

1. **Zero Browser DOM Crawling (Strict Pruning):**  
   Never recursively search descendant trees of `MozillaWindowClass` or `Chrome_WidgetWin_1`. Target specific container classes directly (`tabs-container`, `tabs normal`, `monaco-breadcrumbs`).
2. **Direct Container Targeting:**  
   Query specific named UI zones (e.g. `tabs-container` for editor tabs, `actions-container` for active sidebar view, `TabView` for File Explorer tabs).
3. **Decoupled WinEvent Dispatching:**  
   `SetWinEventHook` callbacks only push lightweight tokens into a `Channel<DesktopEvent>` and return instantly. UIA queries execute on dedicated MTA worker threads with 50–75 ms trailing-edge debouncing.
4. **Historical State Persistence:**  
   Persist state snapshots and focus transitions to an embedded high-performance database (SQLite / DuckDB) to enable historical queries ("what was open 15 minutes ago?").
5. **Universal Consumption via MCP:**  
   Expose both live current state and historical queries over standard Model Context Protocol resources and tools.

---

## 4. MCP Unified Desktop Context Schema

```json
{
  "timestamp": "2026-08-24T02:40:00.000Z",
  "workspace": {
    "virtual_desktop_id": "3f2a1b0c-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
    "virtual_desktop_name": "Development",
    "desktop_index": 1
  },
  "window": {
    "hwnd": "0x00DB083E",
    "title": "caster - Antigravity IDE - CacheRequest.cs",
    "process_name": "Antigravity.exe",
    "pid": 26420,
    "class_name": "Chrome_WidgetWin_1"
  },
  "ide_context": {
    "active_file_path": "C:\\Users\\<User>\\Documents\\repos\\FlaUI\\src\\FlaUI.Core\\CacheRequest.cs",
    "active_sidebar_view": "Explorer (Ctrl+Shift+E)",
    "open_editor_tabs": [
      { "title": "Preview 016_micro_spike_2.md", "is_active": false },
      { "title": "016_micro_spike_2.md", "is_active": false },
      { "title": "Walkthrough", "is_active": false },
      { "title": "CacheRequest.cs, preview", "is_active": true },
      { "title": "spike_win32_shallow_python.py", "is_active": false }
    ],
    "edit_buffer": "CacheRequest.cs, preview"
  },
  "browser_context": {
    "container_type": "TreeStyleTab",
    "total_count": 30,
    "active_tab": "Technical Documentation",
    "tabs": [
      { "index": 1, "title": "Technical Documentation", "is_active": true },
      { "index": 2, "title": "API Reference", "is_active": false }
    ]
  },
  "focus": {
    "control_type": "Edit",
    "element_name": "CacheRequest.cs, preview",
    "automation_id": "",
    "bounding_box": { "left": 400, "top": 120, "width": 1200, "height": 800 }
  }
}
```
