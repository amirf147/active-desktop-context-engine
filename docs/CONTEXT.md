<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# ADCE Domain Context & Master Documentation Hub

> **Target System:** Active Desktop Context Engine (ADCE)
> **Runtime:** .NET 10 (x64) + `FlaUI.UIA3 5.0.0`
> **Architecture:** Decoupled Layered Pipeline (Win32 Event Hooks + FlaUI.UIA3 + SQLite WAL + MCP Server)
> **Active Verification Baseline:** 262 Passing Unit Tests Across 5 Test Suites (`dotnet test`)
> **Canonical Architecture Specifications:** [`docs/architecture/`](./architecture/)

---

## 1. Project Purpose & System Topology

The **Active Desktop Context Engine (ADCE)** is a background Windows daemon and system tray application. It maintains an in-memory semantic snapshot of the active desktop state and exposes it over the **Model Context Protocol (MCP)** to local AI coding assistants and voice frameworks (Caster) with low latency and near-zero idle CPU usage.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                ADCE SYSTEM ARCHITECTURE                                │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  [Windows OS Events] ──▶ SetWinEventHook (Foreground / Focus Transitions)              │
│                                    │                                                   │
│                                    ▼ (STA WinEvent Pump in ADCE.Daemon)                │
│                 [Debounced Desktop Event Pipeline] (150 ms window)                     │
│                                    │                                                   │
│                                    ▼ (MTA Worker Thread)                               │
│        [FlaUI.UIA3 Targeted Extractor] (Zero Unbounded DOM Crawling)                   │
│        ├── VS Code / Antigravity: tabs, monaco-editor, chat-input, terminal            │
│        ├── Waterfox / Gecko: tabs, urlbar-input, web document body                     │
│        └── WinUI / Cascadia: Windows Terminal, console controls                        │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [Dynamic Rule Engine & Archetype Classifier]                           │
│                   ├── %LOCALAPPDATA%\ADCE\semantic_rules.json                          │
│                   └── Heuristic Selector Fallback Tree                                 │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [Dual-Tier State Storage Engine]                                       │
│                   ├── Tier 1: In-Memory Atomic Cache (< 1 µs reads)                    │
│                   └── Tier 2: SQLite WAL Time-Series Database                          │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [ADCE.Mcp Server Endpoints (Stdio / SSE / HTTP)]                       │
│                 ├── AI Coding Assistants (Antigravity IDE, Claude, Cline)              │
│                 └── Voice Navigation Engines (Caster Dynamic Grammars)                 │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Epistemic Document Hierarchy

Documentation is structured into strict tiers to keep human developers and AI assistants grounded in verified contracts:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        ADCE EPISTEMIC DOCUMENT HIERARCHY                               │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 1: VERIFIED PRODUCTION CODE & TEST SUITES (Supreme Authority)                     │
│ • Production Projects: src/ADCE.Core, ADCE.Extraction, ADCE.Storage, ADCE.Mcp,         │
│   ADCE.Daemon                                                                          │
│ • Automated Test Suites: tests/ (262 passing unit tests across 5 test assemblies)      │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 2: CANONICAL ARCHITECTURE SPECIFICATIONS (Normative Contracts)                   │
│ • docs/architecture/CORE_DOMAIN_MODEL.md (Domain models, 19 semantic zones)           │
│ • docs/architecture/EXTRACTION_PIPELINE.md (Gating sequence, traversal bounds, rules)  │
│ • docs/architecture/STORAGE_ARCHITECTURE.md (Dual-tier cache, SQLite WAL persistence) │
│ • docs/architecture/DAEMON_AND_CONSUMER_INTEGRATION.md (Tray host, MCP, Caster voice) │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 3: GUIDES & APPLICATION HIERARCHIES (Operational Reference)                       │
│ • docs/app_hierarchies/README.md (Waterfox, Antigravity IDE UI automation layouts)    │
│ • docs/guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md                          │
│ • docs/guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md                               │
│ • docs/guides/FIRST_REAL_WORLD_USE_CASE_CASTER_DYNAMIC_TERMINAL_GRAMMARS.md            │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 4: RETROSPECTIVE POSTMORTEMS (Anti-Pattern Ledgers)                               │
│ • docs/postmortems/README.md                                                           │
│ • docs/postmortems/CLAIM_VERIFIER_DEPRECATION_AND_SELF_CONFIRMATION_LOOP_POSTMORTEM.md  │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 5: HISTORICAL ARCHIVE (Non-Normative - Excluded from Active Agent Indexing)       │
│ • docs/archive/ (Legacy claim runs, exploratory research, deprecated specifications)   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Master Specification Directory

### 3.1 Normative Architecture Specifications
| Specification | Component | Primary Contracts Documented |
| :--- | :--- | :--- |
| [`architecture/CORE_DOMAIN_MODEL.md`](architecture/CORE_DOMAIN_MODEL.md) | `ADCE.Core` | `DesktopContextSnapshot`, `FocusedControlInfo`, 19 `DesktopSemanticZone` values, 6 `DesktopAppArchetype` categories, 9 `WindowPaneLocation` quadrants. |
| [`architecture/EXTRACTION_PIPELINE.md`](architecture/EXTRACTION_PIPELINE.md) | `ADCE.Extraction` | Win32 shallow gating (< 0.5 ms), UIPI privilege boundary checks, single-roundtrip `FlaUI.UIA3` batch caching, zero-unbounded DOM crawling invariant, dynamic JSON rules. |
| [`architecture/STORAGE_ARCHITECTURE.md`](architecture/STORAGE_ARCHITECTURE.md) | `ADCE.Storage` | Sub-microsecond L1 atomic memory cache, channel-decoupled SQLite WAL time-series store (`desktop_snapshots`), automated retention pruning. |
| [`architecture/DAEMON_AND_CONSUMER_INTEGRATION.md`](architecture/DAEMON_AND_CONSUMER_INTEGRATION.md) | `ADCE.Daemon`, `ADCE.Mcp` | STA WinEvent hook pump, single-instance mutex, non-activating floating HUD overlay, JSON-RPC 2.0 endpoints (`get_current_snapshot`), Caster dynamic voice grammars. |

### 3.2 Application Hierarchies & Layouts
| Profile | Target Application | Documented Automation Structure |
| :--- | :--- | :--- |
| [`app_hierarchies/README.md`](app_hierarchies/README.md) | General Overview | Hierarchy topologies and container classifications. |
| [`app_hierarchies/01_waterfox.md`](app_hierarchies/01_waterfox.md) | Waterfox (Gecko) | Address bar (`urlbar-input`), tab bar (`tabbrowser-tab`), web document viewport. |
| [`app_hierarchies/02_antigravity_ide.md`](app_hierarchies/02_antigravity_ide.md) | Antigravity IDE (Electron) | Activity bar, explorer tree, editor tabs, Monaco text buffer, integrated terminal. |

### 3.3 Active Retrospective Postmortems
| Postmortem Ledger | Focus Area | Key Architectural Lesson |
| :--- | :--- | :--- |
| [`postmortems/README.md`](postmortems/README.md) | Master Ledger | Complete index of development retrospectives. |
| [`postmortems/CLAIM_VERIFIER_DEPRECATION_AND_SELF_CONFIRMATION_LOOP_POSTMORTEM.md`](postmortems/CLAIM_VERIFIER_DEPRECATION_AND_SELF_CONFIRMATION_LOOP_POSTMORTEM.md) | Verification | Deprecation of bespoke claim runner; analysis of tautological mock loops; enforcement of standard xUnit tests. |
| [`postmortems/LESSONS_LEARNED_HARDWARE_ACCELERATED_SCREENSHOTS_AND_UIA.md`](postmortems/LESSONS_LEARNED_HARDWARE_ACCELERATED_SCREENSHOTS_AND_UIA.md) | UIPI / DirectX | Dual-mode capture, DirectX rendering surfaces, and UIA semantic immunity. |
| [`postmortems/STA_THREADING_AND_HUD_CASTER_INTEGRATION_POSTMORTEM.md`](postmortems/STA_THREADING_AND_HUD_CASTER_INTEGRATION_POSTMORTEM.md) | Threading | STA WinForms pump isolation and non-activating floating HUD window styles. |

### 3.4 Quarantined Historical Archive
All legacy test run dumps, exploratory research into unused third-party libraries, and deprecated custom claim verification matrices are archived in [`docs/archive/`](archive/README.md). They are retained strictly for retrospective auditability and must not be used as active specifications.

---

## 4. Core Engineering Invariants

1. **Win32 Shallow Gating Before UIA:** Always check HWND validity, process identity, and UIPI privilege boundaries with Win32 APIs (< 0.5 ms) before initializing or querying UI Automation.
2. **Strict Depth Bounds:** Inspect only the focused leaf element and climb a maximum of 5 ancestor levels. Never invoke child discovery (`FindAllChildren`) on complex containers like web documents or code buffers.
3. **Dedicated Apartment Isolation:** Run `SetWinEventHook` on an STA message pump thread and offload all `FlaUI.UIA3` inspection to background worker threads.
4. **Standard Testing Frameworks:** Verify real behavioral invariants exclusively with standard xUnit unit tests. Never build custom verification runners or self-asserting mock drivers.
