<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Postmortems ](./README.md) › **Spikes Program Sprawl & Scratchpad Audit**

---

# ADCE.Spikes Program.cs Architectural Sprawl & Scratchpad Anti-Pattern Audit

> **Document Status:** Active Technical Debt Audit / Workflow Retrospective
> **Epistemic Authority:** Tier 5 (Historical Subsystem Audit & Anti-Pattern Ledger — Non-Normative)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).
> **Target Component:** `src/ADCE.Spikes/Program.cs`
> **Current Size:** 3,617 lines | 23 methods | 25 CLI flags | 19 Git commits
> **Scope:** Information gathering, quantitative inventory, and root-cause analysis

---

## 1. Problem Statement

The file `src/ADCE.Spikes/Program.cs` has evolved into an uncontrolled monolithic scratchpad. Rather than maintaining a lean entry point for running specific Gate 3 empirical micro-spikes, the file has accumulated code from every development phase since repository initialization.

When ad-hoc investigations or empirical studies are required, the recurring pattern has been to modify `Program.cs` directly. New CLI flags, P/Invoke wrappers, nested local functions, diagnostic queries, and full multi-hundred-line application profiling suites have all been appended into a single source file.

This pattern introduces distinct architectural liabilities:
1. **High Diff Churn:** 19 separate commits have modified `Program.cs`, causing ongoing noise in version control and frequent merge friction.
2. **Scratchpad Degradation:** Temporary diagnostic checks (such as probing a live window or inspecting an element tree) are written directly into production-tracked code instead of isolated scratch scripts.
3. **Loss of Separation of Concerns:** Core benchmarking, live stimulus drivers, daemon hosts, database analyzers, documentation generators, and one-off diagnostic tools all share the same class scope, global state, and P/Invoke definitions.
4. **Architectural Rule Violation:** The global repository rules specify that when a task requires compounding technical debt, implementation must pause to propose an architectural refactor. Accumulating code in `Program.cs` bypassed this rule.

---

## 2. Quantitative Code Inventory

The following table itemizes every method and structural block within `src/ADCE.Spikes/Program.cs` as of commit `0255e02`, sorted by line consumption:

| Method / Component | Line Range | Lines | Functional Domain | Primary Responsibility |
| :--- | :---: | :---: | :--- | :--- |
| `RunAntigravityEmpiricalStudyAsync` | 1738–2250 | 512 | App Profiling | 9-stop empirical UIA control stop verification for VS Code / Antigravity |
| `RunWaterfoxEmpiricalStudyAsync` | 1107–1524 | 417 | App Profiling | Empirical UIA control stop verification and screenshot generation for Waterfox |
| `GenerateWaterfoxHierarchyDoc` | 1525–1737 | 212 | Doc Authoring | Markdown documentation generator embedded in C# source |
| `RunMilestone1CoreDemo` | 329–534 | 205 | Milestone 1 Spike | Core model instantiation and serialization verification |
| `RunDeepAnalysisSpikeAsync` | 3424–3616 | 192 | Storage Analysis | Heuristic analysis of SQLite transitions and session boundary audits |
| `RunPaneInspectionSpike` | 2251–2441 | 190 | Diagnostics | Generic window pane and layout hierarchy probe across open applications |
| `RunStorageSpikeAsync` | 2524–2711 | 187 | Milestone 4 Spike | SQLite WAL throughput and in-memory L1 cache benchmark |
| `RunDatabaseTimelineSpikeAsync` | 3237–3423 | 186 | Storage Analysis | Terminal visualization of stored desktop event timelines |
| `BenchmarkTarget` | 588–769 | 181 | Performance Spike | Micro-benchmarking UIA3 cache requests vs live COM calls |
| `Main` | 150–328 | 178 | CLI Dispatcher | Unstructured string array parsing for 25 distinct CLI flags |
| `RunMcpTestSpikeAsync` | 2851–3022 | 171 | Milestone 5 Spike | Automated verification of MCP JSON-RPC endpoints |
| `RunListOpenApps` | 968–1106 | 138 | Diagnostics | Desktop window enumeration and archetype classification table |
| `PrintSnapshot` | 858–967 | 109 | Output Formatting | Console renderer for `DesktopContextSnapshot` objects |
| `RunDaemonSpikeAsync` | 3133–3236 | 103 | Milestone 6 Spike | In-process daemon pipeline test with synthetic message pump |
| `RunStandaloneGrabberAsync` | 770–857 | 87 | Milestone 2 Spike | Standalone foreground context extraction runner |
| `RunEventPipelineSpikeAsync` | 2442–2523 | 81 | Milestone 3 Spike | WinEvent hook event pipeline listener and noise measurement |
| `RunClaimVerificationSuiteAsync` | 2780–2850 | 70 | Milestone 4.5 Spike | Claim verification matrix driver (CLM-001 through CLM-006) |
| `RunGate3EmpiricalMicroSpikeAsync` | 2712–2779 | 67 | Gate 3 Spike | Micro-spike for Win32 gating and privilege validation |
| `RunMcpSseAsync` | 3071–3132 | 61 | MCP Transport | Minimal HTTP/SSE server host for local AI agent connections |
| `RunFlaUiBenchmark` | 535–587 | 52 | Performance Spike | Benchmark target discovery and runner dispatch |
| `RunMcpStdioAsync` | 3023–3070 | 47 | MCP Transport | Stdio pipe loop for JSON-RPC MCP clients |
| `ForceForegroundWindow` | 126–149 | 23 | Win32 Helper | Window elevation with thread input attachment |
| `SendKey` | 114–125 | 11 | Win32 Helper | Synthetic keyboard event dispatch via `keybd_event` |
| P/Invoke Declarations & Structs | 44–113 | 70 | Native Interop | User32 / Kernel32 function declarations and `RECT` layout |
| Auxiliary Records | Various | 86 | Data Contracts | `TargetWindow`, `TabInfo`, `WindowAppEntry`, `WaterfoxTelemetryStep`, etc. |
| **Total** | | **3,617** | | |

---

## 3. Categorization of Clustered Responsibilities

The inventory demonstrates that `src/ADCE.Spikes/Program.cs` is performing at least five mutually incompatible roles:

### Role 1: Historical Milestone Verification (1,152 lines)
Methods such as `RunMilestone1CoreDemo`, `RunFlaUiBenchmark`, `RunStandaloneGrabberAsync`, `RunEventPipelineSpikeAsync`, `RunStorageSpikeAsync`, and `RunDaemonSpikeAsync` represent point-in-time proof-of-concept tests. Once a milestone reached production status (such as `ADCE.Daemon` or `ADCE.Storage`), the corresponding spike code remained frozen in `Program.cs` without being transitioned to integration test suites or archived.

### Role 2: Application Archetype Profiling Suites (1,141 lines)
`RunWaterfoxEmpiricalStudyAsync`, `RunAntigravityEmpiricalStudyAsync`, and `GenerateWaterfoxHierarchyDoc` are complex empirical investigation suites. They manage hardware-accelerated screenshot capture, multi-stop focus traversal, and Markdown authoring. Combining these 1,100+ lines into `Program.cs` conflated operational spikes with documentation generation.

### Role 3: Ad-Hoc System Diagnostics & Utilities (706 lines)
`RunListOpenApps`, `RunPaneInspectionSpike`, `RunDatabaseTimelineSpikeAsync`, and `RunDeepAnalysisSpikeAsync` are administrative inspection utilities. They provide developers with visibility into active window HWNDs, hierarchy paths, and SQLite WAL contents. These are utility tools rather than verification spikes.

### Role 4: Verification Harness Entry Point (204 lines)
`ADCE.Spikes` contains a well-structured verification package under `src/ADCE.Spikes/Verification/` (including `ClaimVerificationRunner`, `EvidenceLedger`, `LiveWin32StimulusDriver`, and `MockStimulusDriver`). However, `Program.cs` only references this subsystem through thin adapter calls (`RunClaimVerificationSuiteAsync`), while the remaining 3,400 lines remain outside this framework.

### Role 5: Win32 P/Invoke Duplication (104 lines)
`Program.cs` declares its own local P/Invoke methods for `GetWindowText`, `GetClassName`, `GetWindowThreadProcessId`, `IsWindowVisible`, `PrintWindow`, and `keybd_event`. These declarations duplicate interop signatures that already exist in `src/ADCE.Extraction/Win32/NativeMethods.cs`.

---

## 4. Root-Cause Analysis: Why Does the File Keep Changing?

Four operational mechanisms drive this continuous sprawl:

### 1. The Shortcut Anti-Pattern (Path of Least Resistance)
When a quick test is needed, adding 15 lines directly to `Program.cs` requires touching only a single file. Creating a dedicated class file requires project updates, namespaces, and dependency wiring. Over multiple turns and tasks, this shortcut accumulates substantial technical debt.

### 2. Treating Program.cs as an Interactive REPL
When investigating system behavior (such as why Waterfox returned specific element hierarchies or why a window handle failed to bind), the impulse was to add a temporary `--probe-waterfox` block into `Program.cs`. Because the workspace lacked a dedicated, lightweight scratch runner, `Program.cs` functioned as an ad-hoc scratchpad.

### 3. Lack of a Formal Decommissioning Workflow for Spikes
Milestone spikes are created to satisfy Gate 3 empirical testing (<50 lines per the 4-Gate Epistemic Protocol). However, after Gate 4 implementation and production hardening in `ADCE.Extraction`, `ADCE.Storage`, or `ADCE.Daemon`, the spike code was never moved, cleaned up, or extracted into dedicated test fixtures.

### 4. Absence of Command Pattern in CLI Dispatch
The CLI parser in `Program.cs` consists of 25 procedural `args.Any(...)` checks spanning 178 lines. Adding any new capability requires editing the same `Main` method, creating perpetual modification churn in the primary entry point.

---

## 5. Commit History Analysis

The following 19 commits have modified `src/ADCE.Spikes/Program.cs`:

```text
0255e02 feat(extraction): implement antigravity ide hierarchy profile and interactive html diagrams
4d50248 feat(spikes): add dual-mode PrintWindow screenshot engine and Waterfox profile
092e61d docs(architecture): document window pane layouts and add uia inspection tool
10c188e feat(extraction): restore fine-grained semantic zones, seed rules from sqlite, and add runtime tagging
c051bdf feat(daemon): add collapsible DOM and structural tree view to floating HUD
05b9d13 chore(license): update copyright headers and instructions to 2026
bc79d90 feat(daemon): harden event pipeline, add SQLite timeline visualizer, and embed live demo GIFs
b2b7575 feat(daemon): implement Milestone 6 Windows system tray background host
8c60271 feat(mcp): implement Model Context Protocol JSON-RPC 2.0 server (ADCE.Mcp)
400641b feat(spikes): implement ground-truth stimulus test harness & claim verification matrix (CLM-001..CLM-006)
3af45cf feat(storage): implement SQLite WAL state store, L1 live cache, and Win32 focus boundary hardenings
42ca45b feat(extraction): add os noise filtering and semantic snapshot deduplication
8a9d264 feat(extraction): implement zero-cpu event pipeline with debounced channel and epoch supersession
0ceb287 feat(extraction): harden multi-zone discovery and formalize 4-gate verification workflow
9fed18d feat(extraction): implement standalone context grabber and zero-allocation core hardening
e7e58ad feat(core): implement ADCE.Core domain models, serialization, test suite, and architectural deep-dive
fa4b9fa ci: add GitHub Actions CI pipeline, path hygiene checker, and pre-commit hooks
0ad3748 feat: establish architecture specifications, telemetry benchmarks, and diagnostic spikes
1dd5ade feat(adce): initialize ADCE repository, context skills, and Gate 3 FlaUI micro-spike
```

Notice that almost every feature delivery touched `Program.cs`. This confirms that the file has served as the shared staging ground across all milestones rather than an isolated entry point.

---

## 6. Problem Summary

The problem can be stated plainly:
- `src/ADCE.Spikes/Program.cs` is overloaded with conflicting responsibilities.
- The repository currently lacks a strict boundary separating temporary scratch probes, persistent Gate 3 micro-spikes, administrative diagnostic utilities, and application hierarchy profiling suites.
- Taking implementation shortcuts by appending code directly to `Program.cs` has compounded technical debt, resulting in a 3,600+ line monolith that requires ongoing modifications for unrelated tasks.

Addressing this issue will require stopping further additions to `Program.cs` and re-evaluating how developer spikes, diagnostics, and exploratory routines are structured across the solution.
