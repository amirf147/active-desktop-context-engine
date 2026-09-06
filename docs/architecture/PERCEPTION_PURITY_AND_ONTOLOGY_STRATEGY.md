<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **Perception Purity & Ontology Strategy**

---

# ADCE Architectural Strategy: Perception Purity vs. Domain Ontology

> **Document Status:** Active Canonical Strategy Specification
> **Epistemic Authority:** Tier 2 (Canonical Architecture & Boundary Definition)
> **Target Solution:** `ADCE.Core`, `ADCE.Extraction`, `ADCE.Storage`, `ADCE.Mcp`, `ADCE.Daemon`
> **Target Consumers:** AI Agents (Antigravity, Claude), Voice Engines (Caster), Automation Frameworks
> **Date:** September 2026

---

## 1. Executive Summary & Core Identity

The Active Desktop Context Engine (ADCE) is a **real-time desktop context and window topology provider**.

ADCE is not restricted to tracking a single focused control. It maintains an in-memory, structured model of the entire active desktop environment. It serves this context with low latency (< 15 ms) and near-zero idle CPU to downstream consumers (voice engines like Caster, AI assistants over MCP, and automation drivers).

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                          ADCE DESKTOP CONTEXT ARCHITECTURE                             │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│  [ PILLAR 1: WINDOW TOPOLOGY & PROCESS IDENTITY ]                                      │
│  • Foreground HWND, PID, ProcessName, WindowTitle, ClassName, Window Bounds            │
│  • Minimized / Maximized window state tracking                                         │
│  • Enables instantaneous window switching and target window discovery                  │
│                                                                                        │
│  [ PILLAR 2: VIRTUAL DESKTOP & WORKSPACE AWARENESS ]                                   │
│  • Virtual Desktop GUID, Desktop Name, Desktop Index (via IVirtualDesktopManager COM)  │
│  • Multi-monitor display bounds and monitor index mapping                              │
│                                                                                        │
│  [ PILLAR 3: APPLICATION ENVELOPES & CONTAINER TAB ROSTERS ]                           │
│  • Web Browsers (Gecko / Waterfox, Chrome, Edge):                                      │
│    - Sanitized URL, address bar content, total tab count, open tab titles              │
│  • Code Editors / IDEs (VS Code, Antigravity IDE, Cursor):                             │
│    - Workspace root folder, active file path, open editor tabs, Monaco breadcrumbs     │
│  • Terminals (Windows Terminal, conhost):                                              │
│    - Active shell title, terminal tab titles, command buffer snippet                   │
│  • File Explorer: Active directory path, selected items                                │
│                                                                                        │
│  [ PILLAR 4: KEYBOARD FOCUS & UI CONTROL TELEMETRY ]                                   │
│  • ControlType, AutomationId, ClassName, ElementName, Screen Bounding Box              │
│  • Ancestor container chain (ContainerPath, ContainerClasses)                          │
│  • Active text value snippets (ValuePattern / TextPattern)                             │
│                                                                                        │
│  [ PILLAR 5: TIME-SERIES PERSISTENCE & TEMPORAL HISTORY ]                              │
│  • SQLite WAL database (desktop_snapshots table) tracking chronological transitions     │
│  • Enables historical queries: "what file was open 5 minutes ago?"                     │
│                                                                                        │
│  [ PILLAR 6: PRIVACY FIREWALL & ZERO-LATENCY DISTRIBUTION ]                            │
│  • Inline password redaction ([REDACTED_PASSWORD]) and sensitive file path masking     │
│  • Sub-microsecond RAM cache, HTTP Server-Sent Events (SSE :8424), and MCP endpoints   │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Complete Functional Breakdown: What ADCE Does

ADCE aggregates desktop state across five distinct telemetry domains:

### 2.1 Window Topology & Process Management
* **Native HWND Extraction:** Captures the active Win32 window handle (`WindowEnvelope.Hwnd`). Downstream tools (such as window focusers or voice switchers) can immediately target the window without enumerating top-level windows through the OS.
* **Process Metadata:** Tracks the executable name (`ProcessName`), OS Process ID (`Pid`), window class (`ClassName`), and window title (`Title`).
* **Window Placement & State:** Records screen bounds (`Bounds`), minimized state (`IsMinimized`), and maximized state (`IsMaximized`).

### 2.2 Virtual Desktop & Multi-Monitor Workspace Tracking
* **Virtual Desktop GUID & Name:** Queries Windows Shell COM interfaces (`IVirtualDesktopManagerInternal`) to identify the active workspace GUID and friendly name (e.g., "Development", "Browser").
* **Desktop Index & Monitor Bounds:** Tracks zero-indexed desktop order and the specific display monitor coordinates hosting the active context.

### 2.3 Application Envelopes & Tab Inventory
ADCE performs targeted tab and container extraction without triggering unpruned DOM crawling stalls:
* **Browser Context (`BrowserContext`):** Enumerates all open tab titles (`Tabs`), total tab count (`TotalCount`), active tab title (`ActiveTab`), and sanitized address bar URL (`UrlAddress`).
* **IDE Context (`IdeContext`):** Extracts the workspace root directory (`WorkspaceRoot`), active file path (`ActiveFilePath`), open editor tabs (`OpenEditorTabs`), active sidebar view ID (`ActiveSidebarView`), and breadcrumb path (`Breadcrumbs`).
* **Terminal Context (`TerminalContext`):** Extracts shell titles (`ShellTitle`), open terminal tab titles (`Tabs`), and active buffer snippets.
* **Explorer Context (`ExplorerContext`):** Extracts current directory paths and selected items.

### 2.4 Focus & Interactive Element Telemetry
* **Focused Control Properties:** Captures the leaf control under active keyboard focus (`ControlType`, `AutomationId`, `ClassName`, `ElementName`, `BoundingBox`).
* **Structural Ancestor Chain:** Climbs a bounded ancestor hierarchy (depth $\le 5$) to harvest container IDs (`ContainerPath`) and container classes (`ContainerClasses`).
* **Value Extraction:** Safely extracts text snippets from editable fields and code buffers via UIA Value and Text patterns.

### 2.5 Historical Time-Series Persistence
* **SQLite WAL Event Store:** Persists focus transitions, tab switches, and window shifts to a local SQLite database (`desktop_snapshots`).
* **Temporal Querying:** Supports queries across time windows (e.g., retrieving recent focus transitions to give AI agents chronological workflow context).

---

## 3. Perception Purity vs. Prescriptive Domain Ontology

The distinction between **Perception Purity** and **Prescriptive Ontology** defines what belongs inside the ADCE daemon versus external consumer tools:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              ADCE ARCHITECTURAL LAYERS                                 │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│  [ LAYER 0: CORE CONTEXT & TOPOLOGY PROVIDER (Deterministic Ground Truth) ]            │
│  • Window handles (HWND), process IDs, titles, window states                           │
│  • Virtual desktop workspaces and monitor geometry                                     │
│  • Application envelopes (URLs, open tab lists, file paths, workspace roots)           │
│  • Focused control UIA properties, ancestor chains, bounding boxes, text snippets      │
│  • SQLite WAL historical timeline                                                      │
│  • Fast (< 15 ms), zero idle CPU, maintenance-free across all applications             │
│                                                                                        │
│                                           │ (Emits Full Telemetry Snapshot)            │
│                                           ▼                                            │
│                                                                                        │
│  [ LAYER 1: RUNTIME CLASSIFIERS & ALIASING (Pluggable / Optional) ]                    │
│  • Dynamic JSON rule matches (%LOCALAPPDATA%\ADCE\semantic_rules.json)                 │
│  • Maps known containers to high-level tags (e.g. workbench.parts.panel -> BottomPanel)│
│  • Non-blocking: unknown apps still emit complete Layer 0 telemetry                    │
│                                                                                        │
│                                           │ (Enriched Snapshot)                        │
│                                           ▼                                            │
│                                                                                        │
│  [ LAYER 2: DOWNSTREAM CONSUMERS & ACTORS (External User Space) ]                      │
│  • Caster Dynamic Voice Grammars (Python Dragonfly rules switching on HWND/properties) │
│  • Window Switchers & Automation Drivers (Activating windows via HWND, clicking UIA)   │
│  • Local AI Assistants (Antigravity IDE, Claude, Cline via MCP context tools)          │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. How Downstream Consumers Use Full Context

### 4.1 Window Switching and Process Targeting (Caster / Window Switchers)
Because ADCE maintains the active `Hwnd`, `ProcessName`, and `Title`:
* A voice command or automation script does not need to perform slow Win32 `EnumWindows` scans.
* It can query ADCE directly over SSE or local IPC to obtain the current target HWND and issue Win32 `SetForegroundWindow(hwnd)` or `ShowWindow(hwnd, SW_RESTORE)` commands.

### 4.2 Dynamic Voice Grammars (Caster / Dragonfly)
Voice rules can bind to any level of context granularity:
* **By Process & Window:** `snapshot.process_name == "Code.exe"`
* **By Tab / Document:** `"test" in snapshot.ide_context.active_file_path`
* **By Container Hierarchy:** `"workbench.parts.panel" in snapshot.focused_control.container_path`
* **By Control Identifier:** `snapshot.focused_control.automation_id == "urlbar-input"`

### 4.3 AI Coding Assistants (MCP Server)
AI assistants querying `get_desktop_context` or `get_recent_transitions` receive:
* Active project path and open files.
* Active browser documentation URL and open tabs.
* Focused control and recent terminal commands.
* Chronological history of user focus transitions over the last session.

---

## 5. Repository & Architecture Guidelines

1. **Keep Layer 0 Authoritative:**
   * Extraction of window metadata, workspaces, tab collections, and UIA control hierarchies must always succeed independently of semantic classification rules.
2. **Pluggable Rules for Ontologies:**
   * Higher-level semantic labels remain in configurable rule files (`semantic_rules.json`) or consumer scripts rather than hardcoded into C# extractors.
3. **No Branch Fragmentation:**
   * Maintain the single master branch with clean modular boundaries (`ADCE.Core`, `ADCE.Extraction`, `ADCE.Storage`, `ADCE.Mcp`, `ADCE.Daemon`).
