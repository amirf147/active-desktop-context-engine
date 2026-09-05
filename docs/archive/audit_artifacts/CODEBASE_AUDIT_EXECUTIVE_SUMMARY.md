<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 📋 Reports ](./LATEST_CLAIM_VERIFICATION.md) › **Codebase Audit & Architecture Executive Summary**

---

# ADCE Architecture & Codebase Audit: Executive Summary

> **Document Status:** Active / Master Architecture Executive Summary
> **Epistemic Authority:** Tier 2 (Canonical Synthesis of Active Architecture & Audit Outcomes)
> **Verification Baseline:** 263/263 Passing Unit Tests Across Solution (.NET 10 / C# 14)
> **Audit Reference Ledgers:** [Stage 1 Baseline](./AUDIT_STAGE_1_GROUND_TRUTH_BASELINE.md) | [Stage 2 Drift Audit](./AUDIT_STAGE_2_SPECIFICATION_DRIFT.md)

---

## 1. Executive Overview: Why Was This Audit Conducted?

As the Active Desktop Context Engine (ADCE) evolved across Milestones 1 through 6, the codebase accumulated over 60 markdown files, 5 C# projects, and extensive empirical spike research. When developers or AI assistants navigate a codebase of this scale after working on other projects, two dangerous failure modes emerge:

1. **AI Hallucinations & Historical Confusion:** Early postmortems and external research documents contained exploratory ideas (e.g. an obsolete 3-table normalized database proposal or legacy flat zone selectors). Without explicit labeling, AI agents would treat past retrospective notes as active work specifications.
2. **Specification Drift:** Production code had evolved significantly beyond earlier specifications—implementing a 3-level focus hierarchy, 19 semantic typing anchors, a dynamic self-healing rule engine, and 263 passing unit tests (while older docs still cited 136 tests).
3. **The MSBuild Daemon Lock Trap:** `ADCE.Extraction.Tests` had an unnecessary dependency on `ADCE.Spikes`, which transitively pulled in `ADCE.Daemon`. Because `ADCE.Daemon` runs as a live background service, `dotnet test` was crashing with MSBuild file lock errors.

To eliminate technical debt and anchor the repository in unshakeable ground truth, we executed the **Multi-Stage Codebase Audit Protocol**.

---

## 2. The Active Architecture Today (Ground Truth)

ADCE is organized into 5 decoupled C# 14 / .NET 10 projects and an automated test suite:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                ADCE PRODUCTION SOLUTION                                │
├───────────────────┬────────────────────────────────────────────────────────────────────┤
│ Project           │ Architectural Responsibility & Active Capabilities                 │
├───────────────────┼────────────────────────────────────────────────────────────────────┤
│ **`ADCE.Core`**   │ Domain contracts & immutable records (`DesktopContextSnapshot`,    │
│                   │ `FocusedControlInfo`). Defines 19 `DesktopSemanticZone` anchors,   │
│                   │ 6 `DesktopAppArchetype` categories, and 9 `WindowPaneLocation`s.   │
├───────────────────┼────────────────────────────────────────────────────────────────────┤
│ **`ADCE.Extraction`**│ Win32 shallow gating (< 0.5 ms), UIPI privilege boundary checks, │
│                   │ `FlaUI.UIA3` batch caching (`AutomationElementMode.None`), 4       │
│                   │ targeted extractors (Monaco, Gecko, WinUI, Cascadia), and dynamic   │
│                   │ JSON rule persistence (`%LOCALAPPDATA%\ADCE\semantic_rules.json`). │
├───────────────────┼────────────────────────────────────────────────────────────────────┤
│ **`ADCE.Storage`**│ High-throughput dual-tier storage: sub-microsecond L1 atomic cache │
│                   │ for live queries + channel-decoupled SQLite WAL time-series store  │
│                   │ (`desktop_snapshots` table) with automated periodic pruning.       │
├───────────────────┼────────────────────────────────────────────────────────────────────┤
│ **`ADCE.Mcp`**    │ Standard JSON-RPC 2.0 Model Context Protocol endpoints exposing    │
│                   │ live snapshots and historical queries over Stdio and SSE/HTTP.     │
├───────────────────┼────────────────────────────────────────────────────────────────────┤
│ **`ADCE.Daemon`** │ Windows background system tray host (`TrayIconFactory`), dedicated │
│                   │ STA WinEvent hook pump (`SetWinEventHook`), single-instance named  │
│                   │ Mutex, and non-activating floating HUD overlay (`FloatingHudForm`).│
├───────────────────┼────────────────────────────────────────────────────────────────────┤
│ **`tests/`**      │ 263 passing automated unit tests across 5 test suites. Enforces    │
│                   │ zero unbounded DOM crawling at compile time via invariant tests.   │
└───────────────────┴────────────────────────────────────────────────────────────────────┘
```

---

## 3. What Was Reconciled in the Audit

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              RECONCILIATION SUMMARY MATRIX                             │
├────┬────────────────────────────┬─────────────────────────────┬────────────────────────┤
│ ID │ Area                       │ Before Audit                │ Reconciled Resolution  │
├────┼────────────────────────────┼─────────────────────────────┼────────────────────────┤
│ 01 │ Test Dependencies          │ Tests depended on Spikes;   │ Verification mocks     │
│    │                            │ failed on live daemon lock  │ moved into test suite; │
│    │                            │                             │ tests run in 1s flat   │
├────┼────────────────────────────┼─────────────────────────────┼────────────────────────┤
│ 02 │ Focus Context Model        │ Earlier specs described a   │ Reconciled to 3-level  │
│    │                            │ flat single `TargetZone`    │ hierarchy (`Pane` ->   │
│    │                            │                             │ `View` -> `Zone`)      │
├────┼────────────────────────────┼─────────────────────────────┼────────────────────────┤
│ 03 │ Semantic Zones             │ 10–12 ad-hoc zone strings   │ 19 canonical enum items│
│    │                            │                             │ with `ToMacroZone()`   │
├────┼────────────────────────────┼─────────────────────────────┼────────────────────────┤
│ 04 │ Dynamic App Discovery      │ Spec 018 warned of an open  │ Documented production  │
│    │                            │ hardcoded selector crisis   │ `SemanticRuleEngine`   │
│    │                            │                             │ and 6 archetypes       │
├────┼────────────────────────────┼─────────────────────────────┼────────────────────────┤
│ 05 │ Time-Series Storage        │ Spec 018 proposed 3 tables  │ Documented actual      │
│    │                            │ and debated DuckDB          │ SQLite WAL denormalized│
│    │                            │                             │ `desktop_snapshots`    │
├────┼────────────────────────────┼─────────────────────────────┼────────────────────────┤
│ 06 │ Epistemic Labeling         │ Historical docs lacked      │ Added status banners   │
│    │                            │ status headers              │ across 25+ markdown    │
│    │                            │                             │ files in Tiers 4, 5, 6 │
├────┼────────────────────────────┼─────────────────────────────┼────────────────────────┤
│ 07 │ Test Suite Metrics         │ Docs cited 136 tests        │ Reconciled to 263      │
│    │                            │                             │ verified unit tests    │
└────┴────────────────────────────┴─────────────────────────────┴────────────────────────┘
```

---

## 4. How to Navigate Documentation (The 6-Tier Hierarchy)

When reviewing documentation or directing AI assistants, rely on the **6-Tier Epistemic Hierarchy**:

* **Tier 1 (Supreme Authority):** Active C# Code (`src/`) and passing tests (`tests/`). What the code does is the ground truth.
* **Tier 2 (Active Blueprints):** [`UI_AUTOMATION_STRUCTURES_REFERENCE.md`](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md), [`REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md`](../architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md), and [`MCP_SCHEMA_SPEC.md`](../architecture/MCP_SCHEMA_SPEC.md). Normative architectural contracts.
* **Tier 3 (Subsystem Deep Dives):** Detailed engineering mechanics for Core, Extraction, Event Pipeline, Storage, Mcp, and Daemon (`docs/deep_dives/`).
* **Tier 4 (Educational Guides):** Pedagogical walkthroughs for humans and pair programmers (`docs/guides/`). Subordinate to Tier 1 and 2.
* **Tier 5 (Milestone Postmortems & Spikes):** Historical retrospectives, spike telemetry, and lessons learned (`docs/postmortems/`, `docs/benchmarks/`). Non-normative historical ledgers.
* **Tier 6 (External Research):** Ecosystem audits of Roemer, Simon Mourier, AccessKit, Tree-sitter, and Touchpoint (`docs/external_research/`). Non-normative background context.

---

## 5. What Comes Next?

With the documentation and architectural baseline fully synchronized, the immediate next engineering priorities are:

1. **Decomposing the `ADCE.Spikes` Monolith:**
   As audited in [`docs/postmortems/SPIKES_PROGRAM_SPRAWL_AND_SCRATCHPAD_AUDIT.md`](../postmortems/SPIKES_PROGRAM_SPRAWL_AND_SCRATCHPAD_AUDIT.md), `src/ADCE.Spikes/Program.cs` accumulated 3,600 lines of procedural scratchpad code. The next step is to slim down `ADCE.Spikes` into a clean, thin CLI dispatcher strictly for live diagnostics (`--grab`, `--events`, `--timeline`, `--verify-claims`).
2. **Downstream Voice Integration (Caster):**
   Expanding dynamic sub-window grammar switching using low-latency SSE streaming from the ADCE daemon.
