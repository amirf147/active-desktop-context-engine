<!--
SPDX-License-Identifier: Apache-2.0
Copyright (c) 2026 Amir Farhadi
-->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 🔬 External Research ](README.md) › **Synthesis & Wheel Reinvention Audit**

---

# Architectural Synthesis & Wheel Reinvention Audit: Roemer & Simon Mourier Ecosystems

> **Document Status:** Historical Research Synthesis / Justification Audit
> **Epistemic Authority:** Tier 6 (External Research & Upstream Lineage — Non-Normative Background Context)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).

## 1. Problem Framing & Strategic Objective

Before committing code to the Active Desktop Context Engine (ADCE), we performed an adversarial research investigation into the bodies of work authored by **Simon Mourier** (`github.com/smourier`, Windows systems/COM architect) and **Roman Baeriswyl** (`github.com/Roemer`, architect of `FlaUI` & `FlaUInspect`).

The goal is to answer three decisive epistemic questions:
1. **What concrete value, patterns, and code can we extract from these two ecosystems?**
2. **Are we reinventing the wheel?** Does an existing tool, framework, or daemon already solve the active desktop context problem for AI agents?
3. **Is our proposed ADCE architecture necessary and justified?** How do we synthesize the best of both worlds without redundant engineering?

---

## 2. Value Extraction Matrix

The table below maps both research ecosystems directly to our architecture layers:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            ADCE ENGINE LAYERS                               │
├──────────────────────────────┬──────────────────────────────────────────────┤
│ ADCE Architectural Layer     │ Extracted Pattern / Asset from Ecosystems    │
├──────────────────────────────┼──────────────────────────────────────────────┤
│ 1. Win32 Shallow Filter      │ • Win32Window OOP abstraction (HwndExplorer) │
│    (< 0.5 ms Window Gating)  │ • Style & Extended Style bitmasks (WS/WS_EX) │
│                              │ • Process-to-HWND fast lookup                │
├──────────────────────────────┼──────────────────────────────────────────────┤
│ 2. UIA Automation Plane      │ • FlaUI.UIA3 Custom COM wrappers & bindings  │
│    (Deep Context Extraction) │ • FlaUI CacheRequest DSL (Single-trip batch) │
│                              │ • SingleThreadTaskScheduler MTA (UInspect)   │
│                              │ • Structure changed event sinks (UInspect)   │
│                              │ • Retry.WhileNull resilience engine (FlaUI)  │
├──────────────────────────────┼──────────────────────────────────────────────┤
│ 3. Interop & Host Daemon     │ • RegfreeNetComServer NativeAOT COM Host     │
│                              │ • Win32Metadata P/Invoke builder             │
│                              │ • Blittable struct memory layouts            │
├──────────────────────────────┼──────────────────────────────────────────────┤
│ 4. Telemetry & User State    │ • Raw Input sink without LL-hooks (smourier) │
│                              │ • ETW EventProvider high-speed tracing       │
├──────────────────────────────┼──────────────────────────────────────────────┤
│ 5. Visual HUD (Optional)     │ • DirectNAot zero-allocation D2D overlays    │
│                              │ • FlaUInspect visual highlight bounding box  │
└────────────────────────────────┴──────────────────────────────────────────────┘
```

---

## 3. Adversarial Wheel Reinvention Audit

### Question: *Are we reinventing the wheel by building ADCE in C# .NET 10?*

To evaluate this rigorously, we audited all existing tools in this space across six categories:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             ECOSYSTEM SPECTRUM                              │
├──────────────────────────┬──────────────────────────────────────────────────┤
│ Category                 │ Existing Representatives                         │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ A. Diagnostic GUI Tools  │ FlaUInspect, UInspect, Windows SDK Inspect,      │
│                          │ HwndExplorer, Accessibility Insights, Spy++      │
│ B. UI Testing Frameworks │ FlaUI, WinAppDriver, White, pywinauto            │
│ C. Raw Interop Libraries │ DirectN, UIAutomationClient PIA, Win32Metadata   │
│ D. Accessibility Schema  │ AccessKit (Rust cross-platform unified tree)     │
│ E. AST & Vision Tooling  │ Tree-sitter (incremental parser), TIRG-DLL (CV)  │
│ F. Agent Context Engines │ [GAP IDENTIFIED] → ADCE                          │
└──────────────────────────┴──────────────────────────────────────────────────┘
```

### Detailed Gap Analysis:

1. **Diagnostic GUI Tools (`FlaUInspect`, `UInspect`, `Inspect.exe`, `HwndExplorer`):**
   - *What they do:* Provide human-facing tree views, property inspection grids, and live highlight rectangles.
   - *Why they cannot solve our problem:* They are graphical user applications meant for human eyes. They have no programmatic API, no semantic zone categorization (e.g. TabStrip vs Editor vs Output), no token-efficient summarization, and no MCP (Model Context Protocol) interface.

2. **UI Test Automation Frameworks (`FlaUI`, `pywinauto`):**
   - *What they do:* Automate synthetic clicking, typing, and assertion checks for QA testing.
   - *Why they cannot solve our problem:* Test frameworks assume the caller knows the exact `AutomationId` or XPath of target elements in advance. They perform slow, unoptimized, synchronous tree traversals that take 500ms–3000ms per screen scan when unguided. They do not maintain an incremental cache of active desktop state.

3. **Cross-Platform Accessibility Schema (`AccessKit`):**
   - *What it does:* Serves as an in-process *Accessibility Provider* for UI toolkits (egui, iced, flutter, slint) to expose native OS trees.
   - *Why it does not replace ADCE:* AccessKit publishes trees out to the OS; it does not crawl or consume existing heterogeneous 3rd-party Windows applications. However, its data schema (`Node`, `Role`, `TreeUpdate`) provides a blueprint for ADCE's long-term multi-platform vision.

4. **Syntactic & Spatial Primitives (`Tree-sitter`, `TIRG-DLL`):**
   - *What they do:* Tree-sitter provides sub-millisecond AST queries; TIRG-DLL provides sub-10ms raw RGB bounding box extraction.
   - *How they augment ADCE:* They act as specialized companion layers—Tree-sitter extends ADCE into intra-document syntactic voice scoping, while TIRG-DLL serves as a visual geometry fallback when UIA trees are obscured.

5. **Existing Python-based UIA Wrappers:**
   - *What they do:* Wrap `UIAutomationCore.dll` through `comtypes` or `ctypes`.
   - *Why they fail:* As proven in our empirical benchmarks ([`010_telemetry_benchmarks_and_live_findings.md`](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp/010_telemetry_benchmarks_and_live_findings.md)), Python cross-process COM marshaling suffers from severe GIL contention, high memory allocations, and 5x–10x latency penalties compared to native C# runtime execution.

### Verdict on Wheel Reinvention:
- **We are NOT reinventing low-level UIA or Win32 interop:** We do not write custom COM vtables, custom UIA client libraries, or new windowing APIs. We build directly on `FlaUI.UIA3`, proven Win32 patterns, and specialized AST/spatial primitives.
- **ADCE is a genuinely NEW abstraction:** An intelligent, dual-plane, caching background context engine that translates raw OS accessibility trees into compact, structured semantic context for LLMs via the Model Context Protocol.

---

## 4. Head-to-Head Synthesis: Roemer vs. Simon Mourier vs. Modern Primitives

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          SUPERPOSITIONS & DOMAINS                           │
├──────────────────────────────────────┬──────────────────────────────────────┤
│ Roman Baeriswyl (`Roemer`)           │ Simon Mourier (`smourier`)           │
│ SUPERSEDES SIMON IN:                 │ SUPERSEDES ROEMER IN:                │
├──────────────────────────────────────┼──────────────────────────────────────┤
│ 1. Ergonomic Control Patterns        │ 1. Registration-Free COM Server Host │
│    (Tabs, Grids, Trees, TextBoxes)   │    (NativeAOT / .NET 10 Out-of-Proc) │
│ 2. UIA2 vs UIA3 Polymorphic Engine   │ 2. Win32 Window Styles & Fast Gating │
│ 3. Expressive CacheRequest DSL       │    (HwndExplorer / WS_EX bitmasks)   │
│ 4. Deterministic Retry Polling Loops │ 3. Strict MTA Thread Isolation Queue │
│ 5. XPath Query & Navigation Engine   │ 4. Hardware Direct2D Overlay Engine  │
│ 6. Visual Element Highlighting Tool  │ 5. Zero-Overhead ETW Event Tracing   │
├──────────────────────────────────────┴──────────────────────────────────────┤
│ Specialized Primitives (`Tree-sitter`, `TIRG-DLL`, `AccessKit`):            │
│ • Tree-sitter: Incremental GLR AST parsing & intra-document syntactic scope │
│ • TIRG-DLL: Sub-10ms raw RGB bounding box extraction (visual fallback)      │
│ • AccessKit: Unified cross-platform accessibility data schema (Node/Role)   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Architectural Re-Evaluation & Synthesis

By standing on the shoulders of Simon Mourier's research, Roman Baeriswyl's FlaUI abstractions, and modern parsing/spatial primitives, ADCE's technical blueprint is refined into six core principles:

### Principle 1: Fast Win32 Shallow Gating (from `HwndExplorer`)
Before touching the UIA COM pipeline, ADCE uses `EnumWindows` and `GetWindowLongPtr` to filter out non-viable windows in `< 0.5ms`. Only the focused or target process window is ever passed to the UIA plane.

### Principle 2: MTA Thread Isolation (from `UInspect`)
All `FlaUI.UIA3` automation instance creation, cache requests, and tree queries must execute on a dedicated MTA thread to guarantee absolute immunity from COM re-entrancy deadlocks.

### Principle 3: Batch CacheRequests over Tree Crawling (from `FlaUI.UIA3`)
Never perform iterative child-by-child COM calls across process boundaries. Always dispatch a single `CacheRequest` requesting `Name`, `ControlType`, `BoundingRectangle`, and `SelectionItemPattern` in one round-trip.

### Principle 4: Spatial & Text Geometry Grounding (from `TIRG-DLL`)
Utilize lightweight pixel-matrix bounding box segmentation as a high-speed fallback for non-accessible canvas applications, ensuring continuous spatial awareness for voice click targets and visual HUD overlays.

### Principle 5: Syntactic Document Micro-Scoping (from `Tree-sitter`)
Augment UIA element zone envelopes with incremental concrete syntax trees to enable intra-editor micro-grammar scoping (code vs. docstring vs. comments) in downstream voice interfaces.

### Principle 6: High-Performance Telemetry & Standardized Transport
Integrate `EventProvider` ETW logging within the ADCE daemon and stream live context envelopes over standard Model Context Protocol (MCP JSON-RPC 2.0 / SSE) endpoints for universal client consumption.

---

## 6. Conclusion & Actionable Roadmap

The research confirms that our current architectural direction is optimal:
1. **Low-level components are reused:** We leverage `FlaUI.UIA3` for control parsing and caching, and adapt `HwndExplorer`'s Win32 window structures.
2. **System-level safety is hardened:** We adopt `UInspect`'s MTA thread scheduler pattern to eliminate STA deadlock risks.
3. **Core innovation is focused:** We concentrate our development on intelligent target zone extraction, dual-plane streaming, caching algorithms, and MCP tool endpoints.
