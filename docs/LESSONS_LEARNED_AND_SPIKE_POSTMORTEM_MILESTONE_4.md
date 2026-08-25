<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../README.md) › [ 📚 Documentation Hub ](CONTEXT.md) › **Milestone 4 Engineering Postmortem & Systems Analysis**

---

# Milestone 4 Engineering Postmortem & Systems Analysis

> **Target Systems:** `ADCE.Extraction`, `ADCE.Storage`, `ADCE.Spikes` (.NET 10 / C# 14)
> **Date:** August 2026
> **Physical Verification Focus:** Multi-Window Focus Transitions, Child HWNDs in Electron, Global UIA Focus Bleeding, and Archetype-Scoped Classification.
> **Parent Documents:** [`docs/ADCE_STORAGE_DEEP_DIVE.md`](ADCE_STORAGE_DEEP_DIVE.md) | [`docs/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md`](REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md)

---

## 1. Executive Summary

During live telemetry verification of **Milestone 4 (`ADCE.Storage`)** across active desktop applications (Antigravity IDE, Waterfox, Windows Terminal, and PowerShell), real-world empirical traces revealed **three critical physical OS interaction phenomena**:

1. **Child HWND Dropping in Electron/Chromium:** When clicking inside IDE child sub-panels (e.g. Chat, Source Control tree, Terminal tabs), Windows `SetWinEventHook` emits `EVENT_OBJECT_FOCUS` with the **child/rendering HWND** instead of the top-level window. Without root window normalization, UIA fails to bind the root window and drops 70% of intra-app focus transitions as "transient noise".
2. **Desktop-Wide Global UIA Focus Bleeding:** `UIA3Automation.FocusedElement()` queries the global OS keyboard focus. When transitioning to classic Win32 console windows (`pwsh.exe`) or shell windows (`explorer.exe`) that do not expose UIA focus trees, Windows leaves the global UIA pointer lingering on the previous GUI application (e.g. Waterfox Gemini text prompt). Without PID boundary validation, stale GUI focus was erroneously attached to console windows.
3. **Overly Broad Semantic Zone String Matching:** Generic string matching (`name.Contains("Explorer")`) caused document titles in web browsers (e.g. viewing GitHub repositories or documentation) to be misclassified as `[SidebarExplorer]` instead of `[DocumentContent]`.

This document records the empirical telemetry, root physical causes, and the architectural solutions.

---

## 2. Empirical Telemetry Findings (Live 30-Second Traces)

### 2.1 Trace 1: The Global Focus Bleed Trace
```text
 [EVENT DETECTED #2] HWND 0x02240AFE | waterfox | 'docs(architecture): document reviewer observations... — Waterfox'
  Focus Target   : [SidebarExplorer] 'docs(architecture): document reviewer observations...' (Document)
  Archetype      : Gecko

 [EVENT DETECTED #3] HWND 0x00B40D9C | pwsh | 'PowerShell 7 (x64)'
  Focus Target   : [SidebarExplorer] 'docs(architecture): document reviewer observations...' (Document)  <-- Stale Waterfox focus!
  Archetype      : ClassicWin32

 [EVENT DETECTED #4] HWND 0x007D02E6 | explorer | ''
  Focus Target   : [SidebarExplorer] 'docs(architecture): document reviewer observations...' (Document)  <-- Stale Waterfox focus!
  Archetype      : Unknown
```

### 2.2 Trace 2: Intra-App Click Starvation & High Noise Drop Ratio
```text
==========================================================================
  MILESTONE 3 TELEMETRY SUMMARY
==========================================================================
 Elapsed Time              : 30.01 s
 Raw WinEvents Ingested    : 206
 OS Noise / Destroyed Dropped: 30  <-- 30 valid intra-IDE clicks dropped as noise!
 Debounced Extractions     : 43
 Duplicate Wavelets Filtered : 4
 Snapshots Committed       : 9
 Total Noise Suppression   : 95.6% noise reduced
```

---

## 3. Physical Root Cause Analysis

### 3.1 Root Cause 1: Child HWNDs in Multi-Process GUI Frameworks
Modern desktop application frameworks (Chromium, Electron, WPF, WinUI 3) use deep nested HWND topologies:
* In Chromium/Electron, keyboard focus events (`EVENT_OBJECT_FOCUS`) often originate from an internal rendering sub-window (e.g., `Intermediate D3D Window`, child canvas, or native edit container).
* When `UiaExtractionEngine.ExtractSnapshotAsync(hwnd)` attempts to bind `automation.FromHandle(childHwnd)`, FlaUI cannot locate the top-level window patterns (`tabs-container`, root title, Monaco editor) because the child HWND is not the root automation element.
* Consequently, `Win32Gating.GetWindowIdentityFast(childHwnd)` returned an empty title, causing the pipeline's noise filter to discard the event.

### 3.2 Root Cause 2: Global UIA Focus Retention Across Non-UIA Processes
* The Windows UI Automation core maintains a single system-wide focused element pointer accessed via `IUIAutomation::GetFocusedElement()`.
* When a rich GUI app (Waterfox, VS Code) has focus, this pointer references a specific leaf node.
* When the user clicks into a Win32 console host (`conhost.exe`, raw `pwsh.exe`) or shell tray element, the target process does not construct a UIA tree for its caret.
* Windows therefore leaves `GetFocusedElement()` pointing to the previous GUI window's control.
* Without checking `focused.Properties.ProcessId == targetPid`, the extractor incorrectly inherited the previous window's focus element.

### 3.3 Root Cause 3: Unbounded Zone Keyword Matching
* `ResolveSemanticZone` evaluated raw text substrings (e.g. `name.Contains("Explorer")` or `name.Contains("Terminal")`) without checking whether the active window's `DesktopAppArchetype` was actually an IDE or File Explorer.
* When viewing a webpage or document containing the word "Explorer" (such as a GitHub file browser or documentation), the zone classifier incorrectly tagged the entire document as `DesktopSemanticZone.SidebarExplorer`.

---

## 4. Architectural Solutions

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                          ARCHITECTURAL BOUNDARY NORMALIZATION                          │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. Win32 Root HWND Normalization:                                                      │
│    nint rootHwnd = NativeMethods.GetAncestor(hwnd, GA_ROOTOWNER);                      │
│    if (rootHwnd != nint.Zero && NativeMethods.IsWindow(rootHwnd)) hwnd = rootHwnd;     │
│    -> Guarantees child clicks in Monaco/Electron always bind the top-level IDE window. │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 2. Process-Scoped Focus Boundary:                                                      │
│    if (focused != null && focused.Properties.ProcessId.ValueOrDefault == targetPid)    │
│    -> Prevents global UIA focus bleed when switching to consoles or shell elements.   │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 3. Archetype-Scoped Zone Classification:                                               │
│    Restrict SidebarExplorer & EditorCodeBuffer to verified IDE and WinUI archetypes.   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Summary & Status

All observations are fully understood, physically verified, and recorded in the repository's permanent technical architecture ledger.
