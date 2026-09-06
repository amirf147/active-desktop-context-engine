<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **ADCE Philosophy & Scope Boundaries**

---

# ADCE Philosophy & Scope Boundaries (Perception vs. Action)

> **Document Status:** Active Canonical Philosophy & Architectural Scope Specification
> **Epistemic Authority:** Tier 2 (Canonical Architecture & Boundary Definition)
> **Target Solution:** `ADCE.Core`, `ADCE.Extraction`, `ADCE.Storage`, `ADCE.Mcp`, `ADCE.Daemon`
> **Target Consumers:** AI Agents, Voice Engines (Caster), External Planners, Downstream Tools
> **Date:** September 2026

---

## 1. Executive Summary & The Core Thesis

The **Active Desktop Context Engine (ADCE)** is a **pure perception engine**. It is the desktop sensory nervous system for AI coding assistants and voice navigation tools.

It answers one fundamental question with extreme speed (< 15 ms), zero idle CPU, and absolute accuracy:
> **"What is the user currently looking at, focused on, and interacting with on the Windows desktop right now?"**

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                ADCE SYSTEM BOUNDARY                                    │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│   ┌──────────────────────────────────────────────┐                                     │
│   │        ADCE: THE PERCEPTION SENSOR           │                                     │
│   │ • Passive WinEvent Hook Listener             │                                     │
│   │ • Single-Roundtrip UIA Ancestor Inspection   │                                     │
│   │ • High-Level Semantic Zoning & Macro Panes   │                                     │
│   │ • Sub-Microsecond Dual-Tier State Cache      │                                     │
│   │ • Model Context Protocol (MCP) Publisher     │                                     │
│   └──────────────────────┬───────────────────────┘                                     │
│                          │                                                             │
│                          ▼ Emits: DesktopContextSnapshot (Read-Only Telemetry)         │
│                                                                                        │
│   ┌──────────────────────────────────────────────┐                                     │
│   │      DOWNSTREAM CONSUMERS & ACTORS           │                                     │
│   │ • AI Coding Assistants (Antigravity, Claude) │ (Decision Making, Action Planning)  │
│   │ • Voice Command Engines (Caster)             │ (Dynamic Grammars, Voice Actions)   │
│   │ • Task Planners & Automation Plugins         │ (State Graphs, UI Reveal Recipes)   │
│   │ • UI Automation Drivers (PyAutoGUI, FlaUI)   │ (Mouse Clicks, Keystrokes)          │
│   └──────────────────────────────────────────────┘                                     │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. In Scope vs. Out of Scope (The Clear Boundary)

To prevent scope creep and keep the engine lean, robust, and maintainable, ADCE enforces strict boundary separation:

### 2.1 In Scope for ADCE (The Core Perception Responsibilities)
1. **Passive Event Ingress:** Hooking Windows focus and foreground events (`SetWinEventHook`) on an isolated STA message pump with near-zero idle CPU.
2. **Focused Element Extraction:** Inspecting the focused leaf control and climbing a bounded parent ancestor chain (max depth $\le 8$) in a single COM cache request.
3. **Macro Pane & Zone Classification:** Assigning canonical, deterministic semantic tags (`MacroPane`, `SemanticZone`, `ActiveView`, `SectionName`) based on closed structural container topologies (e.g. `workbench.parts.panel` $\implies$ `BottomPanel`, `#navigator-toolbox` $\implies$ `TopBar`, web canvas $\implies$ `MainContent`).
4. **Context Envelopes:** Extracting active IDE file paths (`IdeContext.ActiveFilePath`), browser URLs (`BrowserContext.CurrentUrl`), Explorer paths, and terminal titles.
5. **State Storage & Distribution:** Maintaining an atomic in-memory L1 cache (< 1 µs read) and publishing snapshots over JSON-RPC 2.0 (MCP stdio/HTTP) and local IPC.

### 2.2 Out of Scope for ADCE (Belongs to External Agents / Plugins)
1. **Action & Automation (RPA):**
   * ADCE **never** clicks buttons, presses keys, expands menus, or manipulates UI elements.
   * *Why:* Driving UI requires state machines, permissions, retries, and error recovery—this is the job of an automation driver (Playwright, AutoHotkey, FlaUI drivers), not a passive context sensor.
2. **Interaction State Transition Graphs & UI Path Planning:**
   * ADCE does **not** maintain multi-step click recipes or navigation planners (e.g. *"how to click 3 menus to reach settings"*).
   * *Why:* The agent or external tool owns the planning. The agent decides what it wants to do, executes its action, and uses ADCE's real-time sensory feedback to verify whether the action succeeded.
3. **Deep DOM Crawling / Full Page Tree Scraping:**
   * ADCE **never** recursively crawls entire web page DOMs or window visual trees (`FindAllChildren`).
   * *Why:* Deep crawling violates the $< 15\text{ ms}$ extraction SLA and wastes CPU.
4. **Natural Language Understanding / Intent Guessing:**
   * ADCE provides raw semantic facts (`ControlType: "Edit"`, `ElementName: "Find in Settings"`, `Zone: EditorBuffer`, `Pane: MainContent`). It does not interpret human intent.

---

## 3. How ADCE Interacts with the AI Ecosystem

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                       THE PERCEPTION-ACTION AGENTIC LOOP                               │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│  1. Agent Receives Goal: "Search for proxy settings in Waterfox"                       │
│                                                                                        │
│  2. Agent Queries Sensor (ADCE):                                                       │
│     MCP: `get_current_snapshot` ──▶ State: [Waterfox | MainContent > WebDocument]     │
│                                                                                        │
│  3. Agent / External Plugin Plans Action:                                              │
│     Agent knows Waterfox shortcut for settings is `Ctrl+,` or `about:preferences`      │
│                                                                                        │
│  4. Agent Executes Action:                                                             │
│     Agent issues shortcut or navigates to `about:preferences`                          │
│                                                                                        │
│  5. ADCE Passively Detects Transition:                                                 │
│     Focus shifts ──▶ WinEvent hook fires ──▶ ADCE extracts state (< 10 ms)             │
│                                                                                        │
│  6. Agent Queries Sensor to Verify:                                                    │
│     MCP: `get_current_snapshot` ──▶ State: [Waterfox | MainContent > Settings > Search]│
│                                            Control: Edit ("Find in Settings")          │
│                                                                                        │
│  7. Agent Confirms Success & Proceeds to Next Step.                                    │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Guiding Design Principles for ADCE

1. **Be a Sensor, Not an Actor:** Always maintain read-only passive observation. Never try to drive or automate the desktop from within ADCE.
2. **Structural Ground Truth Over Speculative Guessing:** Always rely on explicit container IDs (`AncestorChain`) and closed archetype topologies. Never guess semantic names from raw screen pixel coordinates.
3. **Stay Fast & Lightweight:** Zero idle CPU, $< 15\text{ ms}$ extraction latency, single-roundtrip batch COM caching. If a proposed feature requires unbounded tree scanning or multi-second processing, it does not belong in ADCE.
4. **Simple, Modular Taxonomy:** A concise, universal taxonomy (`19 Semantic Zones`, `9 Macro Panes`, `6 Archetypes`) that cleanly maps across all desktop applications without application-specific bloat.
