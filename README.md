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

> **Research Status:** This codebase is actively exploring low-level COM performance and UI tree traversal boundaries. Capabilities, heuristics, and transport architectures reflect empirical benchmarks from our research spikes.

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
