<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › **📚 Architecture & Implementation Postmortems**

---

# ADCE Architecture & Implementation Postmortems

> **Document Status:** Active Index / Master Ledger of Milestone Postmortems
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).

This directory contains retrospective postmortems and lessons learned generated across the milestone verification spikes and production integration phases of the **Active Desktop Context Engine (ADCE)**.

---

## Postmortem Index

| Document | Subsystem / Focus | Core Takeaway |
| :--- | :--- | :--- |
| [Hardware Acceleration & UIA Immunity](./LESSONS_LEARNED_HARDWARE_ACCELERATED_SCREENSHOTS_AND_UIA.md) | Visual Capture & Graphics Pipeline | Solves black screenshot bug via `PrintWindow(PW_RENDERFULLCONTENT)` and demonstrates why UIA COM accessibility trees are completely immune to GPU surface occlusion. |
| [Milestone 2 Retrospective](./LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md) | UIA Caching & FlaUI 5 Pipeline | Caching strategies (`CacheRequest`) vs live COM queries; bounds calculation and tree walker patterns. |
| [Milestone 4 Retrospective](./LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md) | SQLite State Storage & WAL | SQLite WAL mode, in-memory buffering, and schema migration for high-frequency desktop events. |
| [Milestone 4.5 Retrospective](./LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md) | Real-time Win32 Stimulus Driver | Verification of latency budgets under live simulated window events. |
| [Milestone 5/6 Diagnostics Report](./MILESTONE_5_6_EMPIRICAL_FINDINGS_AND_DIAGNOSTICS_REPORT.md) | Telemetry & Profiling | Diagnostics report covering focus transitions, SQLite latency, and event pipeline metrics. |
| [Milestone 6 Retrospective](./LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md) | MCP Stdio & SSE Transport | High-concurrency async streaming for local AI agents and client disconnect handling. |
| [STA Threading & Caster HUD Integration](./STA_THREADING_AND_HUD_CASTER_INTEGRATION_POSTMORTEM.md) | COM Apartment State & Voice HUD | Resolving `MTA` vs `STA` threading deadlocks when integrating with Python/Qt Caster HUD. |
| [Unbounded DOM Traversal & Logging](./UNBOUNDED_DOM_TRAVERSAL_AND_DIAGNOSTIC_LOGGING_POSTMORTEM.md) | Web Document Traversal | Bounding recursion depth when walking rich client browser DOMs to preserve sub-15ms latency. |
| [Spikes Program Sprawl & Scratchpad Audit](./SPIKES_PROGRAM_SPRAWL_AND_SCRATCHPAD_AUDIT.md) | Architecture & Spikes Monolith | Quantitative audit of 3,600-line monolithic `Program.cs`, 19-commit churn, and scratchpad anti-pattern. |
| [Heuristic Name Matching & Menu Misclassification](./HEURISTIC_NAME_MATCHING_AND_MENU_ITEM_MISCLASSIFICATION.md) | Heuristic Classification & UIA Models | Evaluates why leaf-level name substring matching creates action-invoker collisions, misclassifying menu items as destination modal overlays. |
| [Claim Verifier Deprecation & Self-Confirmation Loop](./CLAIM_VERIFIER_DEPRECATION_AND_SELF_CONFIRMATION_LOOP_POSTMORTEM.md) | Verification Subsystem & Meta-Tooling | Deprecation of Milestone 4.5 claim harness; root-cause analysis of tautological mock loops and replacement with standard xUnit tests. |

---

## Engineering Verification Lineage

Milestone development and retrospectives reflect a four-stage engineering sequence:
1. **Physical Observation:** Raw OS telemetry and baseline metrics.
2. **Architecture Evaluation:** Comparative analysis of implementation options.
3. **Micro-Spike Verification:** Focused live test harness to validate COM/Win32 behaviors.
4. **Production Implementation:** Final code, automated xUnit tests, and regression prevention.
