<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../README.md) › **ADCE Domain Context & Reference Specification**

---

# ADCE Domain Context & Reference Specification

> **Target System:** Active Desktop Context Engine (ADCE)
> **Source Lineage:** Evolved from research in [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp)
> **Runtime:** .NET 10 (x64) + `FlaUI.UIA3 5.0.0`
> **Architecture:** Dual-Engine Synthesis (Roman Baeriswyl / FlaUI + Simon Mourier Systems Patterns)

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
│                                    ▼ (MTA Dedicated Worker Pool)                       │
│        [Targeted Multi-Zone UIA3 Extractor] (10–15 ms, Zero DOM Crawling)              │
│        ├── Antigravity / VS Code: tabs-container, monaco-breadcrumbs, sidebar, edit    │
│        ├── Waterfox / Gecko: tabs normal, tabs pinned, urlbar-input                    │
│        ├── Windows 11 Explorer: TabView, PART_BreadcrumbBar, Items View                │
│        └── Virtual Desktops: IVirtualDesktopManager / pyvda COM                        │
│                                    │                                                   │
│                                    ▼                                                   │
│                   [Live Semantic Context Graph Engine]                                 │
│                     ├── Active State In-Memory Cache (< 1 ms MCP query)                │
│                     └── Historical Context Store (Embedded SQLite WAL / DuckDB)        │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [MCP Server Endpoint (SSE / HTTP / Stdio)]                             │
│                 ├── AI Coding Agents (Antigravity / Gemini / Claude)                   │
│                 └── Voice Recognition Grammars (Caster)                                │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Foundational Research & Single Source of Truth References

* **Architecture & Modular Implementation Plan:** [`docs/ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md`](ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md)
* **Gate 2 Hostile Architecture & Systems Review:** [`docs/HOSTILE_ARCHITECTURE_REVIEW.md`](HOSTILE_ARCHITECTURE_REVIEW.md)
* **ADCE.Core Deep-Dive & Architecture Reference:** [`docs/ADCE_CORE_DEEP_DIVE.md`](ADCE_CORE_DEEP_DIVE.md)
* **ADCE.Extraction Deep-Dive & Architecture Reference:** [`docs/ADCE_EXTRACTION_DEEP_DIVE.md`](ADCE_EXTRACTION_DEEP_DIVE.md)
* **ADCE Event Pipeline Deep-Dive & Systems Reference:** [`docs/ADCE_EVENT_PIPELINE_DEEP_DIVE.md`](ADCE_EVENT_PIPELINE_DEEP_DIVE.md)
* **ADCE.Storage Deep-Dive & Architecture Reference:** [`docs/ADCE_STORAGE_DEEP_DIVE.md`](ADCE_STORAGE_DEEP_DIVE.md)
* **ADCE.Mcp Deep-Dive & Systems Reference:** [`docs/ADCE_MCP_DEEP_DIVE.md`](ADCE_MCP_DEEP_DIVE.md)
* **ADCE.Daemon Deep-Dive & Systems Architecture:** [`docs/ADCE_DAEMON_DEEP_DIVE.md`](ADCE_DAEMON_DEEP_DIVE.md)
* **Reviewer Observations & Systems Hardening Roadmap:** [`docs/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md`](REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md)
* **Milestone 2 Engineering Postmortem & Analysis:** [`docs/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md`](LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md)
* **Milestone 4 Engineering Postmortem & Systems Analysis:** [`docs/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md`](LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md)
* **Milestone 4.5 Engineering Postmortem & Claim Verification Ledger:** [`docs/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md`](LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md)
* **Milestone 6 Engineering Postmortem & Systems Verification Ledger:** [`docs/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md`](LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md)
* **Ground-Truth Claim Verification Evidence Ledger:** [`docs/reports/LATEST_CLAIM_VERIFICATION.md`](reports/LATEST_CLAIM_VERIFICATION.md)
* **ADCE Focus & Zone Detection Explained:** [`docs/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md`](ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md)
* **Empirical Test Harness & Claim Verification Spec:** [`docs/EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md`](EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md)
* **UI Automation Hierarchy SSOT:** [`docs/UI_AUTOMATION_STRUCTURES_REFERENCE.md`](UI_AUTOMATION_STRUCTURES_REFERENCE.md)
* **Requirements & Dynamic Discovery Spec:** [`docs/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md`](REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md)
* **MCP Schema & Tool Specification:** [`docs/MCP_SCHEMA_SPEC.md`](MCP_SCHEMA_SPEC.md)
* **Educational Guide & Architecture Refresher:** [`docs/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md`](EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md)
* **Educational Guide: Test Harness & Claim Verification:** [`docs/EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md`](EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md)
* **External Research & Wheel Reinvention Audit Hub:** [`docs/external_research/README.md`](external_research/README.md)
  * [Roman Baeriswyl (Roemer) & FlaUI Ecosystem](external_research/FlaUI_And_Roemer_Ecosystem.md)
  * [Simon Mourier Ecosystem & Systems Tools](external_research/README.md)
  * [Synthesis & Wheel Reinvention Audit](external_research/SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md)
* **Empirical Benchmarks:**
  * [001: FlaUI UIA3 Telemetry](benchmarks/001_micro_spike_1_flaui_telemetry.md)
  * [002: Python Shallow vs C# Multi-Zone Telemetry](benchmarks/002_micro_spike_2_python_shallow_telemetry.md)
* **Pointers to Upstream Research ([caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp)):**
  * [`010`: Traversal Telemetry & 6,800-node DOM cost analysis](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/010_telemetry_benchmarks_and_live_findings.md)
  * [`014`: C# Daemon Handover & Skill Specification](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/014_csharp_daemon_handover_and_skill_spec.md)
  * [`015`: Epistemic Recalibration & 4-Gate Protocol](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/015_recalibration_and_adversarial_architecture_review.md)
  * [`016`: Micro-Spike 2 Telemetry & Unified Architecture](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/016_micro_spike_2_win32_shallow_python_telemetry.md)
  * [`017`: Comprehensive UI Automation Tree Structures SSOT](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/017_ui_automation_tree_structures_and_target_zones_reference.md)
  * [`018`: Epistemic Gaps, Dynamic App Discovery & Requirements](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/018_epistemic_gaps_dynamic_app_discovery_and_requirements.md)

---

## 3. Core Architectural Principles (The Dual-Engine Model)

1. **Dual-Plane Discovery (Fast Win32 Gating — *from HwndExplorer*):**
   Filter candidate windows in `< 0.5 ms` via native Win32 `EnumWindows` and `GetWindowLongPtr` before engaging the heavy UI Automation plane.
2. **Zero Browser DOM Crawling (Strict Pruning):**
   Never recursively search descendant trees of `MozillaWindowClass` or `Chrome_WidgetWin_1`. Target specific container classes directly (`tabs-container`, `tabs normal`, `monaco-breadcrumbs`).
3. **MTA Thread Scheduler Isolation (*from UInspect*):**
   Execute all `FlaUI.UIA3` instance creation and COM queries on an isolated Multi-Threaded Apartment (`ApartmentState.MTA`) worker to eliminate cross-process COM reentrancy deadlocks.
4. **Single-Roundtrip Batch Caching (*from FlaUI.UIA3*):**
   Dispatch scoped `CacheRequest.Activate()` requests with `AutomationElementMode.None` to fetch names, patterns, and rectangles in 1 single OS call without spawning active COM proxies.
5. **Decoupled WinEvent Dispatching:**
   `SetWinEventHook` callbacks only push lightweight tokens into a `Channel<DesktopEvent>` and return instantly. UIA queries execute with 50–75 ms trailing-edge debouncing.
6. **Historical State Persistence:**
   Persist state snapshots and focus transitions to an embedded high-performance database (SQLite WAL mode / DuckDB) to enable historical queries ("what was open 15 minutes ago?").
7. **Registration-Free COM & Universal MCP Transport:**
   Expose both live current state and historical queries over standard Model Context Protocol resources/tools and NativeAOT COM endpoints.

---

## 4. MCP Context Envelope & Progressive Disclosure

ADCE exposes desktop context as a 4-part semantic snapshot partitioned into decoupled envelopes:
* **Workspace Envelope:** Virtual desktop GUID, friendly name, and multi-monitor index.
* **Window Envelope:** Process metadata, HWND, window title, and Win32 class.
* **App Semantic Context:** Extracted tabs, breadcrumbs, sidebar views, or document paths.
* **Focus & Control Context:** Focused control type, name, automation ID, and screen bounding box.

> 📑 **Full Specification & Draft JSON Schema:** See [`docs/MCP_SCHEMA_SPEC.md`](MCP_SCHEMA_SPEC.md) for complete field dictionaries, JSON schemas, and MCP tool endpoint definitions.
