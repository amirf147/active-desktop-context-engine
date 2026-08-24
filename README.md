<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

# Active Desktop Context Engine (ADCE)

> **High-Performance Desktop Context Graph, Time-Series Persistence & MCP Provider for Local AI Agents and Voice Interfaces**

---

## 1. Overview

The **Active Desktop Context Engine (ADCE)** is an always-on, high-performance Windows background service and [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) provider. It starts with Windows, resides silently in the system tray with **0% idle CPU**, and maintains an instant, live semantic graph of the user's active desktop:

* **Active Window & Focus:** Foreground application envelope, window title, process ID, Win32 class name, and focused control metadata (< 1.0 ms).
* **Multi-Zone Application Context:** Open editor tabs, active file path breadcrumbs, sidebar panels, and commit/prompt input buffers across IDEs (VS Code, Cursor, Antigravity) and browsers (Waterfox, Firefox, Edge, Chrome) in ~10–15 ms.
* **Workspace & Virtual Desktops:** Active Virtual Desktop GUID, friendly desktop name, and desktop index via COM interfaces.
* **Historical State Persistence:** Embedded time-series storage (SQLite / DuckDB) tracking focus transitions and tab states over time for AI temporal queries.
* **Universal MCP Streaming:** Exposes live desktop state and historical tools over local JSON-RPC / SSE / Stdio endpoints to AI agents and voice command engines (Caster).

---

## 2. Research Lineage & Single Source of Truth

This project evolved from extensive accessibility telemetry and COM reverse-engineering conducted within the **Caster** accessibility framework.

### Primary References in This Repository:
* 📑 **[UI Automation Structures Reference (SSOT)](docs/UI_AUTOMATION_STRUCTURES_REFERENCE.md)**: Definitive structural map of UIA node hierarchies, class names, and target zones for Antigravity IDE, Waterfox, and File Explorer.
* 📋 **[Requirements & Dynamic Discovery Specification](docs/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md)**: 5 Desktop Framework Archetypes, dynamic heuristic discovery pipeline, database tradeoffs, and performance SLAs.
* 📊 **[Empirical Telemetry Benchmarks](docs/benchmarks/)**:
  * [001: FlaUI UIA3 Telemetry](docs/benchmarks/001_micro_spike_1_flaui_telemetry.md) — 30 tabs in 10.17 ms with zero DOM crawling.
  * [002: Python Shallow vs C# Multi-Zone Telemetry](docs/benchmarks/002_micro_spike_2_python_shallow_telemetry.md) — Sub-millisecond envelope extraction.

### Upstream Caster Research Lineage:
Foundational research documents (001–018) live in the [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp) repository.

---

## 3. Technology Stack

* **Language & Framework:** C# 14 / .NET 10 (LTS) (`net10.0-windows`)
* **UI Automation Engine:** [FlaUI.UIA3](https://github.com/FlaUI/FlaUI) (v5.0.0+) over native `UIAutomationCore.dll`
* **Concurrency:** Native Win32 `WinEvent` hooks decoupled via `System.Threading.Channels` into MTA background workers
* **Persistence:** Embedded SQLite (WAL mode) / DuckDB for time-series state history
* **Protocol:** Model Context Protocol (MCP) C# SDK (SSE / HTTP / Stdio Minimal API)

---

## 4. Current Status: Phase 5 (Production Daemon Implementation)

Following the 4-Gate Epistemic Gating Protocol ([015](https://github.com/amirf147/caster-user-directory-and-notes/blob/master/docs/accessibility_mcp/015_recalibration_and_adversarial_architecture_review.md)):

* `[x]` **Gate 1 (Physical Observation):** Identified 6,800-node DOM COM crawling stalls in unpruned traversals.
* `[x]` **Gate 2 (Adversarial Red-Team):** Evaluated FlaUI C# vs. Win32 Python vs. WebExtensions with fatal flaws exposed.
* `[x]` **Gate 3 (Empirical Micro-Spikes):** Empirically verified that direct container targeting extracts 30 tabs in **10.17 ms** and shallow focus in **0.66 ms**.
* `[ ]` **Gate 4 / Phase 5 (Production Daemon):** Scaffold `ADCE.Daemon` with system tray UI, MCP server, and historical SQLite storage.

---

## 5. Building & Running Spikes

```powershell
# Build the solution
dotnet build src/ADCE.Spikes

# Execute live multi-zone diagnostic extractor against running browsers & IDEs
dotnet run --project src/ADCE.Spikes
```

---

## 6. License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.
