<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../README.md) › **ADCE Domain Context & Master Documentation Hub**

---

# ADCE Domain Context & Master Documentation Hub

> **Target System:** Active Desktop Context Engine (ADCE)
> **Source Lineage:** Evolved from research in [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp)
> **Runtime:** .NET 10 (x64) + `FlaUI.UIA3 5.0.0`
> **Architecture:** Dual-Engine Synthesis (Roman Baeriswyl / FlaUI + Simon Mourier Systems Patterns)

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

## 2. Master Documentation Index & Research Ledger

### 🏛️ Architecture & System Specifications
| Document | Description |
| :--- | :--- |
| 🏗️ [`architecture/ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md`](architecture/ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md) | 5-project solution architecture, decoupled work packages, and phased execution milestones. |
| 📋 [`architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md`](architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md) | 5 Desktop Framework Archetypes, dynamic heuristic discovery pipeline, database tradeoffs, and performance targets. |
| 🔌 [`architecture/MCP_SCHEMA_SPEC.md`](architecture/MCP_SCHEMA_SPEC.md) | Evolving JSON schema specifications, decoupled envelope definitions, and MCP tool endpoint definitions. |
| 📑 [`architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md`](architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md) | Definitive structural map of UIA node hierarchies, class names, and target zones across major applications. |
| ⚔️ [`architecture/HOSTILE_ARCHITECTURE_REVIEW.md`](architecture/HOSTILE_ARCHITECTURE_REVIEW.md) | Adversarial systems review evaluating COM apartment deadlocks, GC allocation churn, UIPI barriers, and race conditions. |

### 🧠 Subsystem Deep Dives
| Subsystem / Project | Deep Dive Reference | Architectural Focus |
| :--- | :--- | :--- |
| `ADCE.Core` | [`deep_dives/ADCE_CORE_DEEP_DIVE.md`](deep_dives/ADCE_CORE_DEEP_DIVE.md) | Domain models, immutable state envelopes, sequence equality mechanics, failure mode analysis. |
| `ADCE.Extraction` | [`deep_dives/ADCE_EXTRACTION_DEEP_DIVE.md`](deep_dives/ADCE_EXTRACTION_DEEP_DIVE.md) | Win32 shallow gating, UIPI filtering, single-roundtrip batch caching, privacy redaction. |
| `ADCE.EventPipeline` | [`deep_dives/ADCE_EVENT_PIPELINE_DEEP_DIVE.md`](deep_dives/ADCE_EVENT_PIPELINE_DEEP_DIVE.md) | Dedicated STA message pump, WinEvent hooks, trailing-edge debouncing, monotonic epoch supersession. |
| `ADCE.Storage` | [`deep_dives/ADCE_STORAGE_DEEP_DIVE.md`](deep_dives/ADCE_STORAGE_DEEP_DIVE.md) | Dual-tier storage architecture, L1 in-memory atomic cache, channel-decoupled SQLite WAL time-series store. |
| `ADCE.Mcp` | [`deep_dives/ADCE_MCP_DEEP_DIVE.md`](deep_dives/ADCE_MCP_DEEP_DIVE.md) | JSON-RPC 2.0 protocol implementation, Stdio / SSE / HTTP transports, MCP tool execution handlers. |
| `ADCE.Daemon` | [`deep_dives/ADCE_DAEMON_DEEP_DIVE.md`](deep_dives/ADCE_DAEMON_DEEP_DIVE.md) | Windows system tray hosting, non-activating floating DevTools HUD overlay, background worker lifecycle. |

### 📘 Educational Guides & Visual Explainers
| Guide | Description |
| :--- | :--- |
| 📘 [`guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md`](guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md) | Plain-English walkthrough of UI Automation, Win32 systems programming, FlaUI caching, and the Dual-Engine synthesis. |
| 👁️ [`guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md`](guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md) | Plain-English visual guide to Windows focus mechanics, parent-chain climbing, and how ADCE detects semantic zones. |
| 🧪 [`guides/EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md`](guides/EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md) | Detailed educational guide on empirical stimulus testing, claim verification, and telemetry methodology. |

### 🧪 Testing Specifications & Hardening Roadmaps
| Document | Description |
| :--- | :--- |
| 🧪 [`testing/EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md`](testing/EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md) | Deterministic stimulus-response testing framework, ground-truth verification matrix, and empirical protocols. |
| 🛡️ [`testing/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md`](testing/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md) | Physical observations from live verification and scheduled hardening across future work packages. |

### 🔬 Milestone Engineering Postmortems & Spikes
| Milestone Postmortem | Core Focus & Findings |
| :--- | :--- |
| 🔬 [`postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md`](postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md) | Dissecting empty snapshot failures, Win32 desktop sessions, compound class names, and architectural hardening. |
| 🔬 [`postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md`](postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md) | Physical analysis of child HWNDs in Electron, desktop-wide global UIA focus bleeding, and archetype-scoped zone isolation. |
| 🔬 [`postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md`](postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md) | Ground-Truth stimulus test harness findings, automated assertion engines, and empirical evidence logging. |
| 🔬 [`postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md`](postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md) | Milestone 6 verification, System Tray lifecycle integration, non-activating DevTools HUD overlay mechanics. |

### 📊 Telemetry, Benchmarks & Evidence Ledgers
* 📊 **Empirical Benchmarks:**
  * [`benchmarks/001_micro_spike_1_flaui_telemetry.md`](benchmarks/001_micro_spike_1_flaui_telemetry.md) — FlaUI UIA3 cache request batching telemetry.
  * [`benchmarks/002_micro_spike_2_python_shallow_telemetry.md`](benchmarks/002_micro_spike_2_python_shallow_telemetry.md) — Win32 shallow gating vs C# multi-zone extraction.
* 📋 **Canonical Claim Evidence:** [`reports/LATEST_CLAIM_VERIFICATION.md`](reports/LATEST_CLAIM_VERIFICATION.md)
* 📑 **Milestones 5 & 6 Diagnostic Report:** [`reports/MILESTONE_5_6_EMPIRICAL_FINDINGS_AND_DIAGNOSTICS_REPORT.md`](reports/MILESTONE_5_6_EMPIRICAL_FINDINGS_AND_DIAGNOSTICS_REPORT.md)
* 🎨 **Interactive Diagrams:** [`diagrams/adce_core_architecture_uml.html`](diagrams/adce_core_architecture_uml.html)

### 🔬 External Research & Ecosystem Audits
* 🔬 **[External Research Hub](external_research/README.md)**
  * [Roman Baeriswyl (Roemer) & FlaUI Ecosystem](external_research/FlaUI_And_Roemer_Ecosystem.md)
  * [Simon Mourier Ecosystem & Systems Tools](external_research/README.md)
  * [Simon Mourier: UInspect Deep Dive](external_research/UInspect.md)
  * [Simon Mourier: HwndExplorer Deep Dive](external_research/HwndExplorer.md)
  * [Simon Mourier: RegFree COM & NativeAOT Suite](external_research/RegfreeNetCom_Suite.md)
  * [Simon Mourier: Interop, Telemetry & Input Tools](external_research/Interop_And_Telemetry_Tools.md)
  * [VirtualDesktop & Touchpoint Ecosystem Audit](external_research/VirtualDesktop_And_Touchpoint_Audit.md)
  * [Synthesis & Wheel Reinvention Audit](external_research/SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md)

### 🔗 Upstream Caster Research Lineage
Foundational accessibility research documents (001–018) live in the [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp) repository:
* [`010`: Traversal Telemetry & 6,800-node DOM cost analysis](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/010_telemetry_benchmarks_and_live_findings.md)
* [`014`: C# Daemon Handover & Skill Specification](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/014_csharp_daemon_handover_and_skill_spec.md)
* [`015`: Epistemic Recalibration & 4-Gate Protocol](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/015_recalibration_and_adversarial_architecture_review.md)
* [`016`: Micro-Spike 2 Telemetry & Unified Architecture](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/016_micro_spike_2_win32_shallow_python_telemetry.md)
* [`017`: Comprehensive UI Automation Tree Structures SSOT](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/017_ui_automation_tree_structures_and_target_zones_reference.md)
* [`018`: Epistemic Gaps, Dynamic App Discovery & Requirements](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/018_epistemic_gaps_dynamic_app_discovery_and_requirements.md)

---

## 3. Core Architectural Principles (The Dual-Engine Model)

1. **Dual-Plane Discovery (Fast Win32 Gating — *from HwndExplorer*):**
   Filter candidate windows rapidly via native Win32 `EnumWindows` and `GetWindowLongPtr` before engaging the heavy UI Automation plane.
2. **Zero Browser DOM Crawling (Strict Pruning):**
   Never recursively search descendant trees of `MozillaWindowClass` or `Chrome_WidgetWin_1`. Target specific container classes directly (`tabs-container`, `tabs normal`, `monaco-breadcrumbs`).
3. **MTA Thread Scheduler Isolation (*from UInspect*):**
   Execute all `FlaUI.UIA3` instance creation and COM queries on an isolated Multi-Threaded Apartment (`ApartmentState.MTA`) worker to eliminate cross-process COM reentrancy deadlocks.
4. **Single-Roundtrip Batch Caching (*from FlaUI.UIA3*):**
   Dispatch scoped `CacheRequest.Activate()` requests with `AutomationElementMode.None` to fetch names, patterns, and rectangles in 1 single OS call without spawning active COM proxies.
5. **Decoupled WinEvent Dispatching:**
   `SetWinEventHook` callbacks only push lightweight tokens into a `Channel<DesktopEvent>` and return instantly. UIA queries execute with responsive trailing-edge debouncing (50 ms).
6. **Historical State Persistence:**
   Persist state snapshots and focus transitions to an embedded high-performance database (SQLite WAL mode) to enable historical queries ("what was open 15 minutes ago?").
7. **Registration-Free COM & Universal MCP Transport:**
   Expose live state and historical queries over standard Model Context Protocol resources/tools and NativeAOT COM endpoints.

---

## 4. MCP Context Envelope & Progressive Disclosure

ADCE exposes desktop context as a 4-part semantic snapshot partitioned into decoupled envelopes:
* **Workspace Envelope:** Virtual desktop GUID, friendly name, and multi-monitor index.
* **Window Envelope:** Process metadata, HWND, window title, and Win32 class.
* **App Semantic Context:** Extracted tabs, breadcrumbs, sidebar views, or document paths.
* **Focus & Control Context:** Focused control type, name, automation ID, and screen bounding box.

> 📑 **Full Specification & Draft JSON Schema:** See [`architecture/MCP_SCHEMA_SPEC.md`](architecture/MCP_SCHEMA_SPEC.md) for complete field dictionaries, JSON schemas, and MCP tool endpoint definitions.
