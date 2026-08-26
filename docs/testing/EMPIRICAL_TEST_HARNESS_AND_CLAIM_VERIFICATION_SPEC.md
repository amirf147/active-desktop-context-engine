<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **Empirical Test Harness & Claim Verification Specification**

---

# Empirical Test Harness & Claim Verification Specification

> **Target Systems:** `ADCE.Spikes`, `ADCE.Extraction`, `ADCE.Storage` (.NET 10 / C# 14 / FlaUI 5)
> **Status:** Architectural Specification & Roadmap Directive
> **Date:** August 2026
> **Core Objective:** Eliminate manual testing ambiguities, retrospective post-hoc rationalization, and ungrounded architectural claims by implementing a deterministic **Stimulus-Response UI Test Harness**.

---

## 1. Problem Statement: The Limits of Manual "Click-and-Watch" Testing

During early milestone spikes, testing relied on manual operator interaction (clicking around desktop applications while a 30-second logger recorded background events).

### Failure Modes of Manual Observation:
1. **Uncertain Stimulus Identity:** When a user clicks a visual control (e.g. a vertical tab inside Waterfox), the exact target control type, container hierarchy, and internal framework state are not logged at the instant of the click.
2. **Retrospective Rationalization (Post-Hoc Fallacy):** When telemetry logs reveal an unexpected zone (e.g., `[SidebarExplorer]` on a Waterfox document), an observer or AI assistant may guess an incorrect cause (e.g. "it matched `name.Contains('Explorer')`") instead of discovering the physical cause (Waterfox hosting Tree Style Tab inside the native `#sidebar-box` container).
3. **Non-Repeatability:** Race conditions (debouncing storms, focus switches under 20ms, D3D child window lifetimes) cannot be reproduced consistently by human hand.
4. **Ungrounded Documentation:** Claims entered into postmortems and lessons-learned documents risk becoming "received wisdom" without empirical trace anchors.

---

## 2. The Core Solution: Stimulus-Response Ground-Truth Test Harness

To achieve epistemic certainty, ADCE requires an automated **Ground-Truth Verification Harness**:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                      STIMULUS-RESPONSE VERIFICATION HARNESS                            │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│   1. KNOWN STIMULUS (The Driver)                                                       │
│   ┌────────────────────────────────────────────────────────────────────────────────┐   │
│   │ • Injects precise UI action (e.g., SetFocus on Monaco Editor line 42,          │   │
│   │   Click Tree Style Tab tab item in Waterfox, Switch to conhost pwsh).          │   │
│   │ • Logs Exact Ground Truth: { TargetPid, TargetHwnd, TargetControlType, Zone }  │   │
│   └────────────────────────────────────────────────────────────────────────────────┘   │
│                                       │                                                │
│                                       ▼                                                │
│   2. ACTIVE ENGINE UNDER TEST (Background Pipeline)                                    │
│   ┌────────────────────────────────────────────────────────────────────────────────┐   │
│   │ • SetWinEventHook ingests EVENT_OBJECT_FOCUS / EVENT_SYSTEM_FOREGROUND.        │   │
│   │ • Win32 Gating + DebouncedDesktopEventPipeline processes the stream.           │   │
│   │ • UiaExtractionEngine captures DesktopContextSnapshot.                         │   │
│   └────────────────────────────────────────────────────────────────────────────────┘   │
│                                       │                                                │
│                                       ▼                                                │
│   3. AUTOMATED ASSERTION & EVIDENCE LEDGER                                             │
│   ┌────────────────────────────────────────────────────────────────────────────────┐   │
│   │ • Snapshot.Focus.SemanticZone == GroundTruth.ExpectedZone                      │   │
│   │ • Snapshot.Window.Pid == GroundTruth.ExpectedPid (Zero Focus Bleed)             │   │
│   │ • Snapshot.ExtractionDurationMs <= GroundTruth.SlaLimit                        │   │
│   │ • Emits Structured Evidence JSON with timestamp and UIA property dump.         │   │
│   └────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Technology Evaluation for the UI Driver

We evaluate three technical architectures for building the automated driver:

| Dimension | Option A: Native C# FlaUI Test Harness (`ADCE.Spikes --verify`) | Option B: Python / PyWinAuto Test Runner | Option C: PowerShell / Windows Script Host |
| :--- | :--- | :--- | :--- |
| **Language & Runtime** | C# 14 / .NET 10 (In-Repo) | Python 3.12 + `pywinauto` + `uiautomation` | PowerShell 7 (`pwsh`) + Win32 Interop |
| **Dependency Footprint** | **Zero additional tools** (Uses existing `FlaUI.UIA3`) | Requires Python virtualenv, pip packages | Lightweight, but fragile script syntax |
| **Timing Synchronization** | **Exact in-memory sync** via `TaskCompletionSource` & Channels | Inter-process IPC (file/pipe/socket polling) | Process exit codes / pipe delays |
| **Property Fidelity** | 100% identical UIA3 COM interfaces as production engine | Python COM wrappers (occasional translation gaps) | Limited UIA access without custom assemblies |
| **Execution Command** | `dotnet run --project src/ADCE.Spikes -- --verify-suite focus` | `python scripts/test_harness.py --suite focus` | `pwsh scripts/verify.ps1` |
| **Recommendation** | ⭐ **(Recommended) Native C# in `ADCE.Spikes`** | Secondary (Useful for rapid exploratory scripts) | Not recommended for deep UIA automation |

### Why Native C# / FlaUI (`ADCE.Spikes --verify`) is Superior:
1. **Zero Divergence:** The test harness uses the exact same `FlaUI.UIA3` COM wrapper, memory management, and threading rules as `ADCE.Extraction`.
2. **Deterministic Synchronization:** The driver can launch a background pipeline task, programmatically focus a control, await the pipeline's `ChannelWriter<DesktopContextSnapshot>`, and assert results without arbitrary `sleep()` calls.
3. **CI/CD Integration:** Runs seamlessly via `dotnet test` or standalone `dotnet run --project src/ADCE.Spikes`.

---

## 4. Claim Verification Matrix (Ground-Truth Scenarios)

The following matrix maps every critical claim in the repository to a deterministic automated test scenario:

| Claim ID | Repository Claim | Target Application | Automated Stimulus | Expected Ground-Truth Response |
| :--- | :--- | :--- | :--- | :--- |
| **CLM-001** | **Global Focus Bleed Prevention:** Switching from GUI app to Win32 console (`pwsh.exe`) does not inherit GUI leaf focus. | Waterfox / Notepad + `pwsh.exe` (`conhost`) | 1. Focus Waterfox text input.<br>2. Switch foreground to `conhost.exe`. | `Snapshot.Window.ProcessName == "pwsh"`<br>`Snapshot.Focus.SemanticZone == Unknown / Window`<br>`Snapshot.Focus.ProcessId == pwshPid` (NOT Waterfox PID). |
| **CLM-002** | **Child HWND Normalization:** Clicking nested Electron sub-panels binds root window identity. | Antigravity IDE / VS Code | 1. Click child sub-panel (`Chat` input or `Source Control` tree). | `Snapshot.Window.Hwnd == TopLevelHwnd`<br>`Snapshot.Window.Title.Length > 0`<br>`Event was NOT dropped as empty-title noise.` |
| **CLM-003** | **IDE Semantic Zone Resolution:** Ancestor climbing identifies Monaco editor and Integrated Terminal. | Antigravity IDE / VS Code | 1. SetFocus on Monaco edit buffer.<br>2. SetFocus on Terminal xterm buffer. | 1. `Zone == EditorCodeBuffer`<br>2. `Zone == IntegratedTerminal` |
| **CLM-004** | **Browser Tab Sidebar vs. IDE Explorer:** Browser vertical tabs (Tree Style Tab) do not resolve to `SidebarExplorer`. | Waterfox / Firefox (`Gecko`) | 1. SetFocus inside Tree Style Tab sidebar panel.<br>2. SetFocus inside main web viewport. | 1. `Zone == TabBar` or `DocumentContent`<br>2. `Zone == DocumentContent`<br>(Neither resolves to `SidebarExplorer`). |
| **CLM-005** | **Burst Typing Debounce Clamping:** Continuous typing bursts trigger snapshot commits at $\le 250\text{ ms}$ intervals. | Any active editor (Notepad / Monaco) | Simulate 50 key events spaced 20ms apart (1,000ms continuous burst). | At least 4 snapshots committed during the burst (max delay $\le 250\text{ ms}$). |
| **CLM-006** | **Zero-Allocation Deduplication:** Identical consecutive focus states emit zero SQLite writes. | Any active window | Click the same text box 5 times consecutively. | Initial snapshot committed; next 4 events dropped as identical wavelets. |

---

## 5. Grounded Truth Protocol for Repository Documentation

To maintain absolute documentation integrity, the repository adheres to the **Grounded Truth Protocol**:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                             GROUNDED TRUTH PROTOCOL                                    │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. Telemetry Anchor Requirement:                                                       │
│    Every postmortem finding or performance claim MUST include a raw telemetry trace     │
│    with timestamps, PID, HWND, and AutomationId dumps.                                │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 2. Stimulus-Response Pairing:                                                          │
│    Documented observations must state both the STIMULUS (what action was executed)     │
│    and the RESPONSE (what the OS and ADCE emitted).                                    │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 3. Automated Regression Verification:                                                  │
│    Before a bug or misclassification is declared "fixed", an automated stimulus test   │
│    in ADCE.Spikes must execute and pass against the real or mock desktop target.      │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Implementation Plan: `ADCE.Spikes --verify`

### Phase 1: Test Driver Core (`ADCE.Spikes/Verification/`)
* **`IStimulusDriver`**: Interface for programmatically launching, finding, and driving UI elements via FlaUI and Win32 `SetForegroundWindow` / `SendMessage`.
* **`StimulusScenario`**: Record defining `{ Name, TargetApp, StimulusAction, ExpectedAssertion }`.
* **`EvidenceLedger`**: Generates an immutable markdown/JSON report at `docs/reports/claim_verification_<timestamp>.md`.

### Phase 2: Execution CLI
```powershell
# Run full automated claim verification suite across running applications:
dotnet run --project src/ADCE.Spikes -- --verify-all

# Run targeted scenario (e.g. Browser Sidebar vs IDE Explorer):
dotnet run --project src/ADCE.Spikes -- --verify CLM-004

# Run synthetic headless mock suite (CI/CD mode without external apps):
dotnet run --project src/ADCE.Spikes -- --verify-mocks
```

---

## 7. Summary & Next Steps

This specification establishes a permanent empirical framework for ADCE. By replacing manual guesswork with deterministic stimulus-response verification, we ensure every claim, performance metric, and architectural boundary is provably grounded in OS reality.
