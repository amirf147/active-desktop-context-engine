<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 🎯 ADCE.Extraction Deep-Dive ](../deep_dives/ADCE_EXTRACTION_DEEP_DIVE.md) › **Postmortem: Unbounded DOM Traversal, Continuous Ingestion Load, and Diagnostic Logging**

---

# Engineering Postmortem: Unbounded DOM Traversal and Diagnostic Telemetry

> **Target Subsystems:** `ADCE.Extraction`, `ADCE.Daemon`, `ADCE.Core`
> **Topic:** Dissecting the 100% CPU Fan Spin Regression, Spike vs Daemon Load Discrepancies, and Automated Architectural Invariant Enforcement
> **Related Docs:** [`docs/postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md`](LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md) | [`docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md`](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md)

---

## 1. Executive Summary

During live testing of the ADCE Daemon with the `--hud` flag, the engine generated continuous 100% CPU utilization, causing cooling fan spin and delaying UI initialization.

This postmortem investigates:
1. The exact mechanism that introduced unbounded DOM tree traversal (`FindAllDescendants`) into `MonacoIdeExtractor` and `GeckoBrowserExtractor`.
2. Why this anti-pattern passed single-shot CLI validation during Milestone 2 but failed catastrophically under continuous daemon event processing.
3. The concrete architectural safeguards and automated static analysis tests implemented to prevent future performance regressions.

---

## 2. Root Cause Analysis: The Band-Aid Lineage

### The Origin of the Fallback in Milestone 2
In Milestone 2, initial empirical testing against VS Code revealed that `FindFirstDescendant(cf.ByClassName("tabs-container"))` failed because Chromium accessibility elements frequently include compound dynamic class names (e.g. `tabs-container active`, `tabs-container scrollable`).

To quickly pass the Milestone 2 grab test, a fallback was introduced:
```csharp
var tabContainer = windowElement.FindFirstDescendant(cf.ByClassName("tabs-container")) ??
                   windowElement.FindAllDescendants(cf.ByControlType(ControlType.Tab))
                                .FirstOrDefault(t => (t.Properties.ClassName.ValueOrDefault ?? string.Empty).Contains("tabs-container", StringComparison.OrdinalIgnoreCase));
```

A matching fallback was later mirrored in `GeckoBrowserExtractor`:
```csharp
var tstContainer = windowElement.FindFirstDescendant(cf.ByClassName("tabs normal")) ??
                   windowElement.FindAllDescendants(cf.ByControlType(ControlType.List))
                                .FirstOrDefault(l => (l.Properties.ClassName.ValueOrDefault ?? string.Empty).Contains("tabs", StringComparison.OrdinalIgnoreCase));
```

### The Mechanism of Failure Under Daemon Ingestion
While running a single-shot CLI snapshot (`--grab`) executed the traversal once in approximately 140 ms, the operational profile changed entirely when integrated into `ADCE.Daemon`:

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                 THE CONTINUOUS COM RPC COLLISION TRAP                                  │
├────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                                        │
│   [WinEvent Hook] ────────► 50 ms Debounced Trigger                                                    │
│                                   │                                                                    │
│                                   ├──► ExtractForegroundSnapshotAsync(hwnd)                            │
│                                   │       │                                                            │
│                                   │       ├──► MonacoIdeExtractor.Extract(windowElement)               │
│                                   │       │       │                                                    │
│                                   │       │       ├──► FindFirstDescendant("tabs-container") -> null   │
│                                   │       │       │                                                    │
│                                   │       │       └──► FindAllDescendants(ControlType.Tab) ⚠️           │
│                                   │       │              │                                             │
│                                   │       │              ├── Out-of-Process COM Tree Walk              │
│                                   │       │              ├── 5,000+ Native DOM Elements Inspected       │
│                                   │       │              └── 140 ms CPU Saturation per Event           │
│                                   │                                                                    │
│   [Subsequent WinEvent] ──► Dispatches before prior COM walk finishes, keeping thread pool saturated. │
│                                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

When `FindFirstDescendant` returned `null`, `FindAllDescendants` initiated an exhaustive, recursive search of the entire Chromium/Gecko UI tree. For a modern IDE or browser containing thousands of DOM nodes, this resulted in thousands of synchronous cross-process COM calls. Because window focus and caret events arrived repeatedly, the pipeline remained in a permanent state of full CPU saturation.

---

## 3. Why the Lesson Was Overlooked

Three structural blind spots allowed this anti-pattern to persist:

1. **Spike Verification Bias:** Gate 3 micro-spikes measured functional correctness (whether tabs were extracted) rather than operational latency under sustained event throughput. A 140 ms latency is acceptable for a one-time command-line grab, but catastrophic for an event daemon with a 50 ms debounce interval.
2. **Permissive Fallbacks:** When a specific class query failed, the implementation defaulted to a global traversal rather than an early exit or a title-based fallback.
3. **Absence of Invariant Gating in Automated Tests:** The unit and integration test suites contained functional assertion tests, but lacked static analysis assertions prohibiting un-scoped subtree queries on root windows.

---

## 4. Corrective Architecture and Hardening

### 4.1 Bounded Scoped Queries
All extractor implementations now use strictly bounded queries. If a target container is not found among direct children or immediate descendants, extraction immediately exits and falls back to window title parsing:

```csharp
// Correct: Scoped, bounded query with immediate fallback
var tabContainer = windowElement.FindFirstDescendant(cf.ByClassName("tabs-container")) ??
                   windowElement.FindFirstDescendant(cf.ByControlType(ControlType.Tab));
```

### 4.2 Automated Architectural Invariant Tests
To permanently prevent regressions, `tests/ADCE.Extraction.Tests/Architecture/ExtractorInvariantTests.cs` uses Roslyn syntax analysis to verify during `dotnet test` that no extractor invokes `FindAllDescendants` on `windowElement` or un-scoped roots.

### 4.3 Production Diagnostic Logging (`AdceLogger`)
To ensure rapid triage of runtime bottlenecks without attaching a debugger, `AdceLogger` provides:
- File persistence at `LocalApplicationData/ADCE/logs/adce.log` with rolling retention.
- An in-memory circular buffer of the latest 500 records.
- The `ADCE.Daemon.exe --logs` CLI flag and System Tray menu integration.

---

## 5. Prevention Framework for Future Iterations

To prevent repeating previously documented pitfalls, all future milestones must apply the following 4-step checklist:

1. **Zero Un-scoped Traversal Rule:** Never call `FindAllDescendants` or unbounded `FindFirstDescendant` on a top-level window element. Query scopes must be clamped to direct children (`TreeScope.Children`) or localized container elements.
2. **Performance Budget Assertion:** Every extraction path must execute in $< 20\text{ ms}$. If a specific UIA query exceeds this budget, it must be replaced by a title parsing or caching strategy.
3. **Invariant Test Coverage:** Any architectural rule documented in a postmortem must be paired with an automated test in the `Architecture` test namespace.
4. **Daemon-Level Verification:** Features validated in single-shot CLI spikes must be verified under sustained event load in `ADCE.Daemon` before sign-off.
