<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# ADCE Extraction Pipeline Specification

> **Document Status:** Active / Normative Extraction Pipeline Reference
> **Implementation Target:** `src/ADCE.Extraction/` (.NET 10 / C# 14 / FlaUI.UIA3)
> **Test Baseline:** 140/140 Passing Unit Tests in `tests/ADCE.Extraction.Tests/`

---

## 1. Pipeline Architecture & Gating Sequence

The extraction pipeline transforms raw Windows OS WinEvents and shell notifications into validated `DesktopContextSnapshot` objects. To prevent UI freezing and eliminate high CPU overhead, the pipeline enforces a strict four-stage gating sequence:

```
[ WinEvent: FOCUS / FOREGROUND / SELECTION / NAMECHANGE / DESKTOPSWITCH ]
                         │
                         ▼
┌────────────────────────────────────────────────────────┐
│ Stage 1: Win32 Shallow Gating (< 0.5 ms)               │
│ - GetForegroundWindow(), GetWindowThreadProcessId()    │
│ - DWM cloaking check: IsWindowCloaked()                │
│ - Self-filtering: Ignore ADCE.Daemon, ADCE.Mcp         │
│ - UIPI integrity check: Verify accessibility access    │
│ - Null/Ghost window rejection                          │
└────────────────────────┬───────────────────────────────┘
                         │ Pass
                         ▼
┌────────────────────────────────────────────────────────┐
│ Stage 2: Event Pipeline Debounce (50 ms quiet window)  │
│ - Clamps rapid focus bursts (e.g. keyboard navigation) │
│ - Deduplicates identical window/control transitions    │
│ - Enqueues background worker execution                 │
└────────────────────────┬───────────────────────────────┘
                         │ Stable Event
                         ▼
┌────────────────────────────────────────────────────────┐
│ Stage 3: FlaUI.UIA3 & Workspace Extraction (< 5 ms)    │
│ - AutomationElementMode.None batch property caching    │
│ - Focused element resolution with strict depth bound   │
│ - IWorkspaceManager transient COM desktop resolution   │
│ - Zero unbounded DOM tree traversal invariant          │
└────────────────────────┬───────────────────────────────┘
                         │ Element Properties
                         ▼
┌────────────────────────────────────────────────────────┐
│ Stage 4: Classification & Dynamic Rule Engine          │
│ - ArchetypeClassifier determines application model     │
│ - SemanticRuleEngine matches custom JSON rules         │
│ - Fallback heuristics resolve semantic zone & quadrant │
│ - ContextPrivacySanitizer scrubs sensitive content     │
└────────────────────────┬───────────────────────────────┘
                         │
                         ▼
             [ DesktopContextSnapshot ]
```

---

## 2. Invariants & Performance Bounds

### 2.1 Win32 Shallow Gating Invariants
* **Self-Exclusion:** The engine immediately drops events where the process ID matches `Environment.ProcessId` or known ADCE host processes.
* **Privilege Level Gating:** If the target window runs at a higher integrity level (UIPI restriction) without accessibility rights, extraction returns a bounded `WindowEnvelope` with `Focus = FocusedControlInfo.Empty` rather than hanging on COM errors.
* **Timing Guarantee:** Win32 gating operations execute in under 0.5 ms, avoiding WinEvent pump thread starvation.

### 2.2 UIA Traversal Bounds
* **Bounded Depth:** The engine inspects only the focused leaf element and traverses up the ancestor chain to a maximum depth of 8 parents.
* **Zero Child Crawling:** Under no circumstances does the engine call `FindAllChildren()` on browser viewport containers, editor text bodies, or tree views. Doing so on modern web views or IDEs causes catastrophic DOM traversal freezes.
* **Cached Properties:** The engine requests properties in a single cache request (`Name`, `AutomationId`, `ClassName`, `ControlType`, `BoundingRectangle`, `IsKeyboardFocusable`).

### 2.3 Virtual Desktop Invariants
* **Zero-Cached-State Sessions:** Out-of-process COM calls targeting `explorer.exe` execute inside call-scoped transient MTA sessions (`TransientMtaSession`). Interface pointers are acquired, invoked atomically, and released immediately to prevent dead proxy leaks across Explorer crashes.
* **Notification Callback Budget:** The `VirtualDesktopNotificationSink` executes synchronously on the Windows shell STA message pump. It pushes an `EVENT_SYSTEM_DESKTOPSWITCH` token to the bounded pipeline channel and returns `S_OK` within 0.1 ms without taking ownership of shell-owned `[In]` parameters.

---

## 3. Dynamic Rule Engine (`SemanticRuleEngine`)

Semantic classification operates on a two-tier evaluation strategy: dynamic user rules followed by built-in heuristic fallbacks.

### 3.1 Dynamic JSON Persistence
Dynamic rules are loaded from:
```
%LOCALAPPDATA%\ADCE\semantic_rules.json
```
If the file does not exist, the engine initializes default rule definitions for standard applications. When new rules are added via the MCP `tag_active_control` endpoint, they persist to this JSON configuration immediately without requiring daemon restarts.

### 3.2 Precedence Order
1. **Dynamic Exact Match:** Matching `ProcessName` + exact `AutomationId` or `ClassName`.
2. **Dynamic Archetype Match:** Matching `DesktopAppArchetype` + regex pattern.
3. **Built-in Extractor Heuristics:** Hardcoded selectors for Monaco (`monaco-editor`), Cascadia (`ConsoleWindowClass`), and Gecko (`urlbar`, `tabbrowser-tab`).
4. **Spatial Quadrant Fallback:** Default mapping derived from `WindowPaneLocation` when automation properties are generic or empty.
