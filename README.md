<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

# Active Desktop Context Engine (ADCE)

> **High-Performance Desktop Context Graph & MCP Provider for Local AI Agents and Voice Interfaces**

---

## 1. Overview

The **Active Desktop Context Engine (ADCE)** is a high-performance Windows background service and [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) provider designed to maintain an instant, real-time semantic model of the active user desktop:

* **Active Window & Focus:** Foreground application envelope, window title, process ID, Win32 class name, and focused control metadata.
* **Workspace & Virtual Desktops:** Active Virtual Desktop GUID, friendly desktop name, and desktop index via COM interfaces.
* **Live Tab Extraction:** Zero-crawl, sub-millisecond extraction of open tabs across modern browsers (Waterfox, Firefox, Edge, Chrome) and code editors (VS Code, Cursor, Antigravity).
* **AI Tool & Resource Streaming:** Exposes live desktop state over local JSON-RPC / SSE endpoints (`get_desktop_context`, `desktop://current`) to AI pair programmers and voice command runtimes.

---

## 2. Research Lineage & Historical Documentation

This project evolved from extensive accessibility telemetry and COM reverse-engineering conducted within the **Caster** accessibility framework.

All foundational research, telemetry benchmarks, and architectural investigations are available in the Caster documentation:

* **Historical Docs Root:** `caster/docs/accessibility_mcp/` (Local path: `%LOCALAPPDATA%\caster\docs\accessibility_mcp\`)
* **Key Architecture & Telemetry Records:**
  * [008: Real-World Observations & Caching Architecture](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/008_real_world_observations_and_caching_architecture.md)
  * [010: Traversal Telemetry Benchmarks & Live Findings](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/010_telemetry_benchmarks_and_live_findings.md)
  * [011: FlaUI Evaluation & Dual-Plane Architecture](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/011_flaui_evaluation_and_dual_plane_architecture.md)
  * [013: Empirical Post-Mortem & Event Diagnostics](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/013_v23_empirical_postmortem_and_event_diagnostics.md)
  * [014: C# Daemon Handover & Skill Specification](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/014_csharp_daemon_handover_and_skill_spec.md)
  * [015: Epistemic Recalibration & Adversarial Architecture Review](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/015_recalibration_and_adversarial_architecture_review.md)

See [docs/CONTEXT.md](docs/CONTEXT.md) for domain concepts, data schemas, and architecture rules.

---

## 3. Technology Stack

* **Language & Framework:** C# 14 / .NET 10 (LTS) (`net10.0-windows`)
* **UI Automation Engine:** [FlaUI.UIA3](https://github.com/FlaUI/FlaUI) (v5.0.0+) over native `UIAutomationCore.dll`
* **Concurrency:** Native Win32 `WinEvent` hooks decoupled via `System.Threading.Channels` into MTA background workers
* **Protocol:** Model Context Protocol (MCP) C# SDK / ASP.NET Core SSE Minimal API

---

## 4. Current Status: 4-Gate Epistemic Protocol (Gate 3 Spikes)

In accordance with [Doc 015](file:///%LOCALAPPDATA%/caster/docs/accessibility_mcp/015_recalibration_and_adversarial_architecture_review.md), this codebase follows a strict 4-Gate Epistemic Gating Protocol to prevent premature architectural convergence:

1. **Gate 1 (Physical Observation):** Completed in Python PoC (identified DOM COM crawling stalls).
2. **Gate 2 (Adversarial Red-Team):** Completed in Doc 015 (evaluated FlaUI C# vs. Win32 Python vs. WebExtensions).
3. **Gate 3 (Empirical Micro-Spikes):** **ACTIVE** — Validating UIA3 `CacheRequest` latency in `src/ADCE.Spikes`.
4. **Gate 4 (Architectural Blueprint):** Formalizing daemon specs upon empirical verification.

---

## 5. Building & Running Spikes

```powershell
# Build the micro-spikes
dotnet build src/ADCE.Spikes

# Execute Micro-Spike 1 live against running browsers
dotnet run --project src/ADCE.Spikes
```

---

## 6. License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.
