<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › **📚 Postmortems & Epistemic Retrospectives**

---

# ADCE Architecture & Implementation Postmortems

This directory contains retrospective postmortems and lessons learned generated across the milestone verification spikes and production integration phases of the **Active Desktop Context Engine (ADCE)**.

---

## Postmortem Index

| Document | Subsystem / Focus | Core Takeaway |
| :--- | :--- | :--- |
| [Hardware Acceleration & UIA Immunity](./LESSONS_LEARNED_HARDWARE_ACCELERATED_SCREENSHOTS_AND_UIA.md) | Visual Capture & Graphics Pipeline | Solves black screenshot bug via `PrintWindow(PW_RENDERFULLCONTENT)` and demonstrates why UIA COM accessibility trees are completely immune to GPU surface occlusion. |
| [Milestone 2 Retrospective](./LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md) | UIA Caching & FlaUI 5 Pipeline | Caching strategies (`CacheRequest`) vs live COM queries; bounds calculation and tree walker patterns. |
| [Milestone 4 Retrospective](./LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md) | SQLite State Storage & WAL | SQLite WAL mode, in-memory buffering, and schema migration for high-frequency desktop events. |
| [Milestone 4.5 Retrospective](./LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md) | Real-time Win32 Stimulus Driver | Verification of latency budgets under live simulated window events. |
| [Milestone 6 Retrospective](./LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md) | MCP Stdio & SSE Transport | High-concurrency async streaming for local AI agents and client disconnect handling. |
| [STA Threading & Caster HUD Integration](./STA_THREADING_AND_HUD_CASTER_INTEGRATION_POSTMORTEM.md) | COM Apartment State & Voice HUD | Resolving `MTA` vs `STA` threading deadlocks when integrating with Python/Qt Caster HUD. |
| [Unbounded DOM Traversal & Logging](./UNBOUNDED_DOM_TRAVERSAL_AND_DIAGNOSTIC_LOGGING_POSTMORTEM.md) | Web Document Traversal | Bounding recursion depth when walking rich client browser DOMs to preserve sub-15ms latency. |
| [Spikes Program Sprawl & Scratchpad Audit](./SPIKES_PROGRAM_SPRAWL_AND_SCRATCHPAD_AUDIT.md) | Architecture & Spikes Monolith | Quantitative audit of 3,600-line monolithic `Program.cs`, 19-commit churn, and scratchpad anti-pattern. |

---

## 4-Gate Protocol Lineage

In accordance with the mandatory [4-Gate Epistemic Protocol](../CONTEXT.md), each postmortem reflects:
1. **Gate 1:** Raw physical telemetry and baseline observation.
2. **Gate 2:** Red-team evaluation of 3 competing approaches.
3. **Gate 3:** Empirical micro-spike implementation (<50 lines).
4. **Gate 4:** Production deployment and regression prevention.
