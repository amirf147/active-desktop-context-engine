<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

# Active Desktop Context Engine (ADCE)

> **High-Performance Desktop Context Graph, Time-Series Persistence & MCP Provider for Local AI Agents and Voice Interfaces**

---

## 1. Overview

The **Active Desktop Context Engine (ADCE)** is an experimental, privacy-first Windows background daemon and [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server.

Instead of relying on resource-heavy screenshot OCR, periodic screen polling, or cloud telemetry, ADCE integrates directly with native Win32 `WinEvent` hooks and UI Automation (UIA) caching to deliver a deterministic, structured desktop state in **< 20 ms** with near-zero CPU and memory overhead:

* **100% Local Data Sovereignty:** Complete on-device execution. Active window titles, document contents, and application states never leave localhost—ensuring enterprise privacy and zero telemetry leakage.
* **Deterministic Focus & Window Topology:** Instant tracking of the foreground application envelope, process metadata, window hierarchies, and focused UI controls (< 1.0 ms) via decoupled asynchronous event channels.
* **Virtual Desktop & Workspace Awareness:** Direct extraction of active Virtual Desktop GUIDs, friendly names, and desktop indices via native COM interop.
* **Container-Aware Tab Discovery:** Efficient tab enumeration across modern browsers (Waterfox, Firefox, Chrome, Edge) and code editors (VS Code, Antigravity) without triggering unpruned DOM crawling stalls (~10–15 ms).
* **Token-Efficient MCP Streaming:** Compact, high-density JSON context snapshots over local MCP endpoints (`get_desktop_context`, `desktop://current`), giving local LLMs actionable workflow awareness without wasting context tokens on raw visual screen dumps.
* **Historical State Persistence:** Embedded time-series storage (SQLite / DuckDB) tracking focus transitions and tab history for temporal agent reasoning.

---

## 2. Live Telemetry & DevTools HUD Demos

| System Tray & Non-Activating Live HUD | Antigravity IDE (Monaco Editor & Zones) |
| :---: | :---: |
| ![ADCE System Tray & HUD Demo](docs/media/adce_tray_and_hud_demo.gif) | ![ADCE Antigravity IDE Telemetry](docs/media/adce_antigravity_ide_telemetry.gif) |

| Waterfox Browser (Gecko Tabs & Links) | SQLite Time-Series Transition Timeline |
| :---: | :---: |
| ![ADCE Waterfox Browser Telemetry](docs/media/adce_waterfox_browser_telemetry.gif) | <pre>============================================================<br/>#   &#124; TIME (UTC)   &#124; PROCESS        &#124; SEMANTIC ZONE<br/>------------------------------------------------------------<br/>323 &#124; 13:34:19.604 &#124; Antigravity ID &#124; [ChatAssistant]<br/>324 &#124; 13:36:26.553 &#124; Antigravity ID &#124; [EditorCodeBuffer]<br/>329 &#124; 13:36:31.583 &#124; waterfox       &#124; [DocumentContent]<br/>336 &#124; 13:36:38.140 &#124; Antigravity ID &#124; [GitCommitBox]<br/>============================================================</pre> |

---

## 3. Architecture: The Dual-Engine Model

Synthesized from our research across the **Roman Baeriswyl (`Roemer` / FlaUI)** and **Simon Mourier (`smourier`)** ecosystems:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                ADCE DUAL-ENGINE ARCHITECTURE                           │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  INFRASTRUCTURE & HOST PLANE (Leveraging Simon Mourier Patterns)                       │
│  ├── Win32 Shallow Filter (< 0.5 ms): Fast HWND, process & WS/WS_EX bitmask gating     │
│  ├── Concurrency Plane: Dedicated MTA SingleThreadTaskScheduler queue (No STA locks)   │
│  ├── Daemon Host & IPC: RegfreeNetComServer / NativeAOT COM Endpoint                   │
│  └── Telemetry & Diagnostics: TraceSpy ETW event provider for zero-overhead metrics    │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  EXECUTION & CONTEXT EXTRACTION PLANE (Leveraging Roman Baeriswyl / FlaUI Patterns)    │
│  ├── UIA Automation Engine: FlaUI.UIA3 (Direct UIAutomationClient vtable interop)      │
│  ├── Batch Context Extraction: FlaUI.Core CacheRequest DSL (< 15 ms multi-tab batch)   │
│  ├── Strongly-Typed Multi-Zone Parsing: 40+ typed controls (Tab, Edit, Text, Grid)     │
│  └── Asynchronous Retry Resilience: Retry.WhileNull for lazy Chromium/Monaco buffers   │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  PERSISTENCE & MODEL CONTEXT PROTOCOL (MCP) INTERFACE                                  │
│  ├── In-Memory Semantic Context Graph: Sub-millisecond pre-cached query responses      │
│  ├── Embedded Time-Series Store: SQLite WAL / DuckDB for focus and tab history         │
│  └── MCP Server Endpoint (Stdio / SSE / HTTP): Universal agent & Caster integration    │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Public Research Ledger & Architecture Hub

This repository functions as an **evolving public research ledger** and implementation hub, tracking low-level COM experiments, UI Automation latency benchmarks, and architectural design decisions.

### Primary References & Specifications:
* 🏗️ **[Architecture & Modular Implementation Plan](docs/ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md)**: 5-project solution architecture, decoupled work packages, and phased execution milestones.
* ⚔️ **[Gate 2 Hostile Architecture & Systems Review](docs/HOSTILE_ARCHITECTURE_REVIEW.md)**: Adversarial systems review evaluating COM apartment deadlocks, GC allocation churn, UIPI barriers, and lifecycle race conditions.
* 🛡️ **[Reviewer Observations & Systems Hardening Roadmap](docs/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md)**: Physical observations from Milestone 3 live verification and scheduled hardening across future work packages (parent-chain climbing, debounce starvation clamps, Electron latency SLAs, Per-Monitor V2 DPI).
* 🧠 **[ADCE.Core Deep-Dive & Architecture Reference](docs/ADCE_CORE_DEEP_DIVE.md)**: Plain-English architectural breakdown, end-to-end dataflow sequence diagrams, file-by-file failure mode analysis, and sequence equality mechanics.
* 🎯 **[ADCE.Extraction Deep-Dive & Architecture Reference](docs/ADCE_EXTRACTION_DEEP_DIVE.md)**: Plain-English architectural breakdown, Win32 shallow gating, UIPI filtering, single-roundtrip batch caching, and privacy redaction.
* ⚡ **[ADCE Event Pipeline Deep-Dive & Systems Reference](docs/ADCE_EVENT_PIPELINE_DEEP_DIVE.md)**: Dedicated STA message pump, `ManualResetEventSlim` barrier sync, noise filtering, 50ms trailing-edge debouncer, monotonic epoch supersession, and live multi-window telemetry postmortem.
* 💾 **[ADCE.Storage Deep-Dive & Architecture Reference](docs/ADCE_STORAGE_DEEP_DIVE.md)**: Dual-tier storage architecture, sub-microsecond L1 atomic cache (13.4 ns), channel-decoupled SQLite WAL time-series engine, and non-blocking temporal querying.
* 🔬 **[Milestone 2 Engineering Postmortem & Analysis](docs/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md)**: Dissecting the empty snapshot failure, Win32 desktop sessions, compound class names, and architectural hardening.
* 🔬 **[Milestone 4 Engineering Postmortem & Systems Analysis](docs/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md)**: Physical analysis of child HWNDs in Electron, desktop-wide global UIA focus bleeding, and archetype-scoped zone isolation.
* 🧪 **[Empirical Test Harness & Claim Verification Spec](docs/EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md)**: Deterministic stimulus-response testing framework, ground-truth verification matrix, and empirical protocol to eliminate post-hoc guesswork.
* 👁️ **[ADCE Focus & Zone Detection Explained](docs/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md)**: Plain-English visual guide to Windows focus mechanics, parent-chain climbing, and how ADCE detects code vs terminal vs search zones.
* 📘 **[Educational Refresher & Architecture Guide](docs/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md)**: Plain-English walkthrough of UI Automation, Win32 systems programming, FlaUI caching, and the Dual-Plane architecture.
* 📑 **[UI Automation Structures Reference (SSOT)](docs/UI_AUTOMATION_STRUCTURES_REFERENCE.md)**: Definitive structural map of UIA node hierarchies, class names, and target zones for Antigravity IDE, Waterfox, and File Explorer.
* 📋 **[Requirements & Dynamic Discovery Specification](docs/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md)**: 5 Desktop Framework Archetypes, dynamic heuristic discovery pipeline, database tradeoffs, and performance SLAs.
* 🔌 **[Model Context Protocol (MCP) Schema Spec](docs/MCP_SCHEMA_SPEC.md)**: Evolving draft JSON schema, decoupled envelope definitions, and MCP tool endpoint specifications.
* 🔬 **[External Research & Ecosystem Audit](docs/external_research/README.md)**: Comprehensive deep dive and comparative audit of **Roman Baeriswyl (`Roemer` / FlaUI)** and **Simon Mourier (`smourier`)** Windows systems/COM repositories, supercedence matrix, and wheel reinvention analysis.
* 📊 **[Empirical Telemetry Benchmarks](docs/benchmarks/)**:
  * [001: FlaUI UIA3 Telemetry](docs/benchmarks/001_micro_spike_1_flaui_telemetry.md) — 30 tabs in 10.17 ms with zero DOM crawling.
  * [002: Python Shallow vs C# Multi-Zone Telemetry](docs/benchmarks/002_micro_spike_2_python_shallow_telemetry.md) — Sub-millisecond envelope extraction.

### Upstream Caster Research Lineage:
Foundational accessibility research documents (001–018) live in the [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp) repository.

---

## 5. Engineering Roadmap & Phase Status

Following our **4-Gate Epistemic Protocol**:

| Phase | Description | Status | Deliverables & Artifacts |
| :--- | :--- | :--- | :--- |
| **Phase 1: Physical Observation & Problem Isolation** | Identify DOM traversal traps and latency bottlenecks across real-world apps. | `[x]` Complete | • [Doc 010: DOM Traversal Telemetry](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/010_telemetry_benchmarks_and_live_findings.md)<br/>• Exposed 6,800-node DOM COM stall (5,897 ms). |
| **Phase 2: Adversarial Evaluation & Micro-Spikes** | Gate 2 & Gate 3 empirical tests validating container targeting and Win32 gating. | `[x]` Complete | • [Micro-Spike 1 Telemetry (FlaUI UIA3)](docs/benchmarks/001_micro_spike_1_flaui_telemetry.md) (10.17 ms)<br/>• [Micro-Spike 2 Telemetry (Win32 Shallow)](docs/benchmarks/002_micro_spike_2_python_shallow_telemetry.md) (0.66 ms) |
| **Phase 3: Ecosystem Audit & Wheel Reinvention** | Deep-dive audits of leading open-source Windows/COM/UIA tooling catalogs. | `[x]` Complete | • [Simon Mourier Ecosystem Suite](docs/external_research/README.md)<br/>• [Roman Baeriswyl (Roemer / FlaUI) Deep Dive](docs/external_research/FlaUI_And_Roemer_Ecosystem.md)<br/>• [Synthesis & Wheel Reinvention Audit](docs/external_research/SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md) |
| **Phase 4: Architectural Specifications & SSOT** | Formalize ground-truth target zones, heuristic discovery archetypes, and MCP schemas. | `[x]` Complete | • [UI Automation SSOT Reference](docs/UI_AUTOMATION_STRUCTURES_REFERENCE.md)<br/>• [Dynamic Discovery & Requirements Spec](docs/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md)<br/>• [MCP JSON Schema Specification](docs/MCP_SCHEMA_SPEC.md) |
| **Phase 5: Production Daemon Implementation** | Build modular multi-project solution (`ADCE.slnx`), event pipeline, storage, and MCP server. | `[x]` Complete | • **Milestone 1:** `ADCE.Core` domain models, events, serialization & unit tests (`[x]` Complete)<br/>• **Milestone 2:** `ADCE.Extraction` standalone context grabber (`[x]` Complete)<br/>• **Milestone 3:** Zero-CPU event pipeline (`SetWinEventHook` + channel debouncer) (`[x]` Complete)<br/>• **Milestone 4:** SQLite WAL store & in-memory live cache (`[x]` Complete)<br/>• **Milestone 4.5:** Ground-Truth Stimulus Test Harness (`[x]` Complete)<br/>• **Milestone 5:** High-Performance MCP Server (Stdio & SSE/HTTP) (`[x]` Complete)<br/>• **Milestone 6:** Windows System Tray Daemon & Live DevTools HUD (`[x]` Complete) |
| **Phase 6: Voice & AI Client Integration** | Connect Caster Dragonfly grammars and local AI assistants to the live MCP endpoint. | `[ ]` Planned | • Caster MCP client bindings<br/>• Live active window context streaming to Antigravity/Claude |

---

## 6. Technology Stack

* **Language & Framework:** C# 14 / .NET 10 (LTS) (`net10.0-windows`)
* **UI Automation Engine:** [FlaUI.UIA3](https://github.com/FlaUI/FlaUI) (v5.0.0+) over native `UIAutomationCore.dll`
* **Concurrency:** Native Win32 `WinEvent` hooks decoupled via `System.Threading.Channels` into MTA background workers
* **Persistence:** Embedded SQLite (WAL mode) with single-writer asynchronous queue & L1 atomic cache
* **Protocol:** Model Context Protocol (MCP) JSON-RPC 2.0 (Stdio / SSE / HTTP Minimal API)
* **DevTools HUD:** WinForms Non-Activating overlay (`WS_EX_NOACTIVATE | WS_EX_TOPMOST`)

---

## 7. Building & Running

### Running the System Tray Daemon & Live HUD
```powershell
# Launch System Tray Daemon with live non-activating floating HUD overlay
dotnet run --project src/ADCE.Daemon -- --hud

# Launch System Tray Daemon with MCP server on HTTP/SSE port 8424
dotnet run --project src/ADCE.Daemon

# Launch as headless MCP server over Stdio (for IDE/Agent integration)
dotnet run --project src/ADCE.Daemon -- --stdio
```

### Inspecting SQLite Time-Series History
```powershell
# Visualize recent context transitions and application time distribution
dotnet run --project src/ADCE.Spikes -- --timeline 20
```

### Running Test Suite & Spikes
```powershell
# Run full automated unit test suite across all 5 projects (136 tests)
dotnet test

# Run Milestone 6 Daemon & End-to-End integration verification spike
dotnet run --project src/ADCE.Spikes -- --spike6

# Run Milestone 4 SQLite WAL store & L1 in-memory live cache verification spike
dotnet run --project src/ADCE.Spikes -- --storage

# Run Milestone 3 live zero-CPU event pipeline spike (listening for foreground & focus transitions)
dotnet run --project src/ADCE.Spikes -- --events -d 15

# Run Milestone 2 live standalone context grabber against active foreground window
dotnet run --project src/ADCE.Spikes -- --grab
```

---

## 8. License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.
