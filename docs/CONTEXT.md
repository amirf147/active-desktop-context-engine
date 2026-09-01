<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../README.md) › **ADCE Domain Context & Master Documentation Hub**

---

# ADCE Domain Context & Master Documentation Hub

> **Target System:** Active Desktop Context Engine (ADCE)
> **Source Lineage:** Evolved from research in [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp)
> **Runtime:** .NET 10 (x64) + `FlaUI.UIA3 5.0.0`
> **Architecture:** Decoupled Layered Pipeline (Win32 Event Hooks + FlaUI.UIA3 + SQLite WAL + MCP Server)

---

## 1. Project Purpose & System Vision

The **Active Desktop Context Engine (ADCE)** is a lightweight, privacy-first Windows background daemon and system tray service. It maintains a live in-memory semantic graph of the user's active desktop state and exposes it over the **Model Context Protocol (MCP)** to local AI agents, IDE assistants, and voice frameworks (Caster) with low latency and near-zero idle CPU usage.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                ADCE SYSTEM ARCHITECTURE                                │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  [Windows OS Events] ──> SetWinEventHook (Foreground / Focus / Desktop Switches)       │
│                                    │                                                   │
│                                    ▼                                                   │
│               [Async Channel<DesktopEvent>] (0% Idle CPU Loop)                         │
│                                    │                                                   │
│                                    ▼ (MTA Dedicated Worker Pool)                       │
│        [Targeted Multi-Zone UIA3 Extractor] (Zero DOM Crawling)                        │
│        ├── Antigravity / VS Code: tabs-container, monaco-breadcrumbs, sidebar, edit    │
│        ├── Waterfox / Gecko: tabs normal, tabs pinned, urlbar-input                    │
│        ├── Windows 11 Explorer: TabView, PART_BreadcrumbBar, Items View                │
│        └── Virtual Desktops: IVirtualDesktopManager / pyvda COM                        │
│                                    │                                                   │
│                                    ▼                                                   │
│                   [Live Semantic Context Graph Engine]                                 │
│                     ├── Active State In-Memory Cache (Live fast queries)               │
│                     └── Historical Context Store (Embedded SQLite WAL)                 │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [MCP Server Endpoint (SSE / HTTP / Stdio)]                             │
│                 ├── AI Coding Agents (Antigravity / Gemini / Claude)                   │
│                 └── Voice Recognition Grammars (Caster)                                │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Epistemic Document Hierarchy (Single Source of Truth)

To ensure consistency and ground human developers and local AI agents in verified facts, ADCE strictly enforces a **6-Tier Epistemic Ordering of Truth**:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        ADCE EPISTEMIC DOCUMENT HIERARCHY (SSOT)                        │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 1: EMPIRICAL GROUND TRUTH & EXECUTABLE CODE (Supreme Authority)                   │
│ • Active C# Solution: src/ (ADCE.Core, Extraction, Storage, Mcp, Daemon, Spikes)       │
│ • Automated Test Suite: tests/ (136 unit tests)                                        │
│ • Verified Telemetry: docs/reports/LATEST_CLAIM_VERIFICATION.md                        │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 2: ARCHITECTURAL SSOT & STRUCTURAL SPECIFICATIONS (Active Master Blueprints)      │
│ • docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md (Target node SSOT)          │
│ • docs/architecture/ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md (Work Package SSOT)│
│ • docs/architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md (Archetype Spec)        │
│ • docs/architecture/MCP_SCHEMA_SPEC.md (Protocol Schema Spec)                          │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 3: SUBSYSTEM ARCHITECTURAL DEEP DIVES (Implementation Mechanics)                 │
│ • docs/deep_dives/ADCE_CORE_DEEP_DIVE.md                                               │
│ • docs/deep_dives/ADCE_EXTRACTION_DEEP_DIVE.md                                         │
│ • docs/deep_dives/ADCE_EVENT_PIPELINE_DEEP_DIVE.md                                     │
│ • docs/deep_dives/ADCE_STORAGE_DEEP_DIVE.md                                            │
│ • docs/deep_dives/ADCE_MCP_DEEP_DIVE.md                                                │
│ • docs/deep_dives/ADCE_DAEMON_DEEP_DIVE.md                                             │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 4: EDUCATIONAL GUIDES & VISUAL EXPLAINERS (Concepts & Pedagogy)                   │
│ • docs/guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md                          │
│ • docs/guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md                               │
│ • docs/guides/EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md                 │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 5: MILESTONE POSTMORTEMS & EMPIRICAL SPIKE EVIDENCE (Milestone Ledgers)           │
│ • docs/postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2, 4, 4_5, 6.md     │
│ • docs/benchmarks/001_micro_spike_1_flaui_telemetry.md, 002_...                       │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 6: HISTORICAL RESEARCH LINEAGE & ECOSYSTEM AUDITS (Context & Upstream)            │
│ • docs/external_research/ (FlaUI, Roemer, Simon Mourier, Touchpoint audits)            │
│ • Upstream Caster Docs 001–018 (Research handover lineage in caster-user-directory)   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Master Documentation Index & Research Ledger

### 🏛️ Tier 2: Architecture & System Specifications
| Document | Description |
| :--- | :--- |
| 🏗️ [`architecture/ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md`](architecture/ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md) | 5-project solution architecture, decoupled work packages, and phased execution milestones. |
| 📋 [`architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md`](architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md) | 5 Desktop Framework Archetypes, dynamic heuristic discovery pipeline, database tradeoffs, and performance targets. |
| 🔌 [`architecture/MCP_SCHEMA_SPEC.md`](architecture/MCP_SCHEMA_SPEC.md) | Evolving JSON schema specifications, decoupled envelope definitions, and MCP tool endpoint definitions. |
| 📑 [`architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md`](architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md) | Definitive structural map of UIA node hierarchies, class names, and target zones across major applications. |
| ⚔️ [`architecture/HOSTILE_ARCHITECTURE_REVIEW.md`](architecture/HOSTILE_ARCHITECTURE_REVIEW.md) | Adversarial systems review evaluating COM apartment deadlocks, GC allocation churn, UIPI barriers, and race conditions. |

### 🧠 Tier 3: Subsystem Architectural Deep Dives
| Subsystem / Project | Deep Dive Reference | Architectural Focus |
| :--- | :--- | :--- |
| `ADCE.Core` | [`deep_dives/ADCE_CORE_DEEP_DIVE.md`](deep_dives/ADCE_CORE_DEEP_DIVE.md) | Domain models, immutable state envelopes, sequence equality mechanics, failure mode analysis. |
| `ADCE.Extraction` | [`deep_dives/ADCE_EXTRACTION_DEEP_DIVE.md`](deep_dives/ADCE_EXTRACTION_DEEP_DIVE.md) | Win32 shallow gating, UIPI filtering, single-roundtrip batch caching, privacy redaction. |
| `ADCE.EventPipeline` | [`deep_dives/ADCE_EVENT_PIPELINE_DEEP_DIVE.md`](deep_dives/ADCE_EVENT_PIPELINE_DEEP_DIVE.md) | Dedicated STA message pump, WinEvent hooks, trailing-edge debouncing, monotonic epoch supersession. |
| `ADCE.Storage` | [`deep_dives/ADCE_STORAGE_DEEP_DIVE.md`](deep_dives/ADCE_STORAGE_DEEP_DIVE.md) | Dual-tier storage architecture, L1 in-memory atomic cache, channel-decoupled SQLite WAL time-series store. |
| `ADCE.Mcp` | [`deep_dives/ADCE_MCP_DEEP_DIVE.md`](deep_dives/ADCE_MCP_DEEP_DIVE.md) | JSON-RPC 2.0 protocol implementation, Stdio / SSE / HTTP transports, MCP tool execution handlers. |
| `ADCE.Daemon` | [`deep_dives/ADCE_DAEMON_DEEP_DIVE.md`](deep_dives/ADCE_DAEMON_DEEP_DIVE.md) | Windows system tray hosting, non-activating floating DevTools HUD overlay, background worker lifecycle. |

### 📘 Tier 4: Educational Guides & Visual Explainers
| Guide | Description |
| :--- | :--- |
| 🎧 [`guides/audiobooks/chapter_1/AUDIOBOOK_CHAPTER_1_WIN32_AND_COM_FOUNDATIONS.md`](guides/audiobooks/chapter_1/AUDIOBOOK_CHAPTER_1_WIN32_AND_COM_FOUNDATIONS.md) | Educational Audio Script: Win32 Architecture, Window Handles, Message Pumps, and Component Object Model (COM). |
| 🎧 [`guides/audiobooks/chapter_2/AUDIOBOOK_CHAPTER_2_WIN32K_AND_USER32_INTERNALS.md`](guides/audiobooks/chapter_2/AUDIOBOOK_CHAPTER_2_WIN32K_AND_USER32_INTERNALS.md) | Educational Audio Script: Kernel-User GUI Substrate (`win32k.sys`, `user32.dll`), System Calls, and Desktop Introspection. |
| 📘 [`guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md`](guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md) | Plain-English walkthrough of UI Automation, Win32 systems programming, FlaUI caching, and the Dual-Engine synthesis. |
| 🚀 [`guides/FIRST_REAL_WORLD_USE_CASE_CASTER_DYNAMIC_TERMINAL_GRAMMARS.md`](guides/FIRST_REAL_WORLD_USE_CASE_CASTER_DYNAMIC_TERMINAL_GRAMMARS.md) | First verified production use case: Caster dynamic sub-window terminal grammar activation via low-latency SSE streaming. |
| 👁️ [`guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md`](guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md) | Plain-English visual guide to Windows focus mechanics, parent-chain climbing, and how ADCE detects semantic zones. |
| 🧪 [`guides/EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md`](guides/EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md) | Detailed educational guide on empirical stimulus testing, claim verification, and telemetry methodology. |

### 🧪 Tier 5: Testing Specifications, Empirical Postmortems & Telemetry
| Document | Description |
| :--- | :--- |
| 🧪 [`testing/EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md`](testing/EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md) | Deterministic stimulus-response testing framework, ground-truth verification matrix, and empirical protocols. |
| 🛡️ [`testing/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md`](testing/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md) | Physical observations from live verification and scheduled hardening across future work packages. |
| 🔬 [`postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md`](postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md) | Dissecting empty snapshot failures, Win32 desktop sessions, compound class names, and architectural hardening. |
| 🔬 [`postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md`](postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md) | Physical analysis of child HWNDs in Electron, desktop-wide global UIA focus bleeding, and archetype-scoped zone isolation. |
| 🔬 [`postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md`](postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md) | Ground-Truth stimulus test harness findings, automated assertion engines, and empirical evidence logging. |
| 🔬 [`postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md`](postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md) | Milestone 6 verification, System Tray lifecycle integration, non-activating DevTools HUD overlay mechanics. |
| 📋 [`reports/LATEST_CLAIM_VERIFICATION.md`](reports/LATEST_CLAIM_VERIFICATION.md) | Canonical Ground-Truth Claim Evidence Matrix (CLM-001 through CLM-006). |
| 📊 [`benchmarks/`](benchmarks/) | Empirical Telemetry Benchmarks ([FlaUI UIA3](benchmarks/001_micro_spike_1_flaui_telemetry.md) / [Win32 Shallow](benchmarks/002_micro_spike_2_python_shallow_telemetry.md)). |

### 🔬 Tier 6: External Research & Upstream Lineage
* 🔬 **[External Research Hub](external_research/README.md)**
  * [Tree-sitter & Syntactic Scoping Audit](external_research/TreeSitter_And_Syntax_Scoping_Audit.md)
  * [TIRG-DLL & Text Geometry Audit](external_research/TirgDll_And_Text_Geometry_Audit.md)
  * [AccessKit & Accessibility Primitives Audit](external_research/AccessKit_And_Accessibility_Primitives_Audit.md)
  * [Roman Baeriswyl (Roemer) & FlaUI Ecosystem](external_research/FlaUI_And_Roemer_Ecosystem.md)
  * [Simon Mourier Ecosystem & Systems Tools](external_research/README.md)
  * [Simon Mourier: UInspect Deep Dive](external_research/UInspect.md)
  * [Simon Mourier: HwndExplorer Deep Dive](external_research/HwndExplorer.md)
  * [Simon Mourier: RegFree COM & NativeAOT Suite](external_research/RegfreeNetCom_Suite.md)
  * [Simon Mourier: Interop, Telemetry & Input Tools](external_research/Interop_And_Telemetry_Tools.md)
  * [VirtualDesktop & Touchpoint Ecosystem Audit](external_research/VirtualDesktop_And_Touchpoint_Audit.md)
  * [Synthesis & Wheel Reinvention Audit](external_research/SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md)
* 🔗 **[Upstream Caster Research Lineage](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp)** (Documents 001–018)

---

## 4. Core Architectural Principles (Production Pipeline)

1. **Fast Win32 Shallow Gating (< 0.5 ms):**
   Filter candidate windows rapidly via native Win32 `EnumWindows` and `GetWindowLongPtr` style bitmasks before engaging UI Automation.
2. **Zero Browser DOM Crawling (Strict Container Pruning):**
   Never recursively search descendant trees of `MozillaWindowClass` or `Chrome_WidgetWin_1`. Target specific container classes directly (`tabs-container`, `tabs normal`, `monaco-breadcrumbs`).
3. **MTA Thread Scheduler Isolation:**
   Execute all `FlaUI.UIA3` instance creation and COM queries on dedicated background MTA worker threads to eliminate cross-process COM reentrancy deadlocks.
4. **Single-Roundtrip Batch Caching (`FlaUI.UIA3`):**
   Dispatch scoped `CacheRequest.Activate()` requests with `AutomationElementMode.None` to fetch names, patterns, and rectangles in 1 single OS call without spawning active COM proxies.
5. **Decoupled WinEvent Channel Dispatching:**
   `SetWinEventHook` callbacks only push lightweight tokens into a `Channel<DesktopEvent>` and return instantly. UIA queries execute with responsive trailing-edge debouncing (50 ms) and max-burst delay clamping (250 ms).
6. **Dual-Tier In-Memory & Time-Series Persistence:**
   Maintain an atomic L1 cache for sub-microsecond live queries and an embedded SQLite WAL repository for temporal agent reasoning.
7. **Universal Model Context Protocol (MCP) Transport:**
   Expose live state snapshots and historical queries over standard JSON-RPC 2.0 endpoints (Stdio and SSE/HTTP).

---

## 5. MCP Context Envelope & Progressive Disclosure

ADCE exposes desktop context as a 4-part semantic snapshot partitioned into decoupled envelopes:
* **Workspace Envelope:** Virtual desktop GUID, friendly name, and multi-monitor index.
* **Window Envelope:** Process metadata, HWND, window title, and Win32 class.
* **App Semantic Context:** Extracted tabs, breadcrumbs, sidebar views, or document paths.
* **Focus & Control Context:** Focused control type, name, automation ID, and screen bounding box.

> 📑 **Full Specification & Draft JSON Schema:** See [`architecture/MCP_SCHEMA_SPEC.md`](architecture/MCP_SCHEMA_SPEC.md) for complete field dictionaries, JSON schemas, and MCP tool endpoint definitions.
