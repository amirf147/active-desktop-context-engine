<!--
SPDX-License-Identifier: Apache-2.0
Copyright (c) 2026 Amir Farhadi
-->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **External Research Hub**

---

# External Research: Windows Systems, COM & UI Automation Ecosystem

> **Document Status:** Active Index / Historical Research Archive
> **Epistemic Authority:** Tier 6 (External Research & Upstream Lineage — Non-Normative Background Context)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).

This directory contains technical audits, architectural analyses, and reverse-engineering notes across leading open-source Windows platform tooling, COM frameworks, and UI Automation implementations—specifically analyzing the bodies of work authored by **Simon Mourier** (`github.com/smourier`) and **Roman Baeriswyl** (`github.com/Roemer`, FlaUI).

## Analyzed Repositories & Tooling Ecosystems

| Document | Target Repositories / Authors | Core Focus Areas |
| :--- | :--- | :--- |
| [`TreeSitter_And_Syntax_Scoping_Audit.md`](./TreeSitter_And_Syntax_Scoping_Audit.md) | `tree-sitter/tree-sitter` (Max Brunsfeld) | Incremental GLR parsing (< 1 ms), concrete syntax trees (CST/AST), query engine (S-expressions), caret-to-AST resolution, and intra-editor micro-grammar voice scoping in Caster/Dragonfly. |
| [`TirgDll_And_Text_Geometry_Audit.md`](./TirgDll_And_Text_Geometry_Audit.md) | `LexiconCode/tirg-dll` (LexiconCode) | TRG computer-vision algorithm: ITU-R luma transform, adaptive regional thresholding, 8-neighbor connectivity, sub-10ms raw RGB text bounding box extraction, and spatial visual grounding. |
| [`AccessKit_And_Accessibility_Primitives_Audit.md`](./AccessKit_And_Accessibility_Primitives_Audit.md) | `AccessKit/accesskit` (Matt Campbell et al.) | Cross-platform accessibility abstraction layer in Rust, `Node`/`Role`/`TreeUpdate` schema, Windows UIA provider COM adapter, and accessibility provider vs. consumer continuum. |
| [`FlaUI_And_Roemer_Ecosystem.md`](./FlaUI_And_Roemer_Ecosystem.md) | `FlaUI/FlaUI`, `FlaUI/FlaUInspect`, `Roemer/FlauLib` | FlaUI UIA3 custom COM interop, `CacheRequest` DSL, 40+ typed control patterns, XPath engine, head-to-head comparison vs. Simon Mourier. |
| [`UInspect.md`](./UInspect.md) | `smourier/UInspect` | UIA COM interop, dedicated MTA thread scheduling, structure change event listeners, comparison vs. `FlaUInspect`. |
| [`HwndExplorer.md`](./HwndExplorer.md) | `smourier/HwndExplorer` | Win32 shallow tree discovery, window hierarchy, style bitmasking (`WS`/`WS_EX`), process mapping, and z-order traversal. |
| [`RegfreeNetCom_Suite.md`](./RegfreeNetCom_Suite.md) | `smourier/RegfreeNetComServer`, `smourier/RefreeNetCom`, `smourier/OutOfProcessCOMServer`, `smourier/ActiveN`, `smourier/AotNetComHost` | Out-of-proc & in-proc registration-free COM servers in .NET 10 / NativeAOT, manifest marshaling with `OleAut32`, custom `IClassFactory`. |
| [`Interop_And_Telemetry_Tools.md`](./Interop_And_Telemetry_Tools.md) | `smourier/Win32InteropBuilder`, `smourier/TraceSpy`, `smourier/RawInputReader`, `smourier/DirectNAot` | Metadata-driven P/Invoke generation, zero-overhead ETW/debug tracing, raw HID input sinks, and GPU DirectX rendering. |
| [`VirtualDesktop_And_Touchpoint_Audit.md`](./VirtualDesktop_And_Touchpoint_Audit.md) | `Slion/VirtualDesktop`, `MSCholtes/VirtualDesktop`, `Touchpoint-Labs/touchpoint` | Virtual Desktop COM version negotiation across Windows 10/11 builds, WinStasis lineage, and cross-platform accessibility MCP server architecture. |
| [`SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md`](./SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md) | **Global Synthesis** | Strategic evaluation: Are we reinventing the wheel? Roemer vs. Simon vs. AccessKit supercedence matrix, standing on shoulders of existing primitives, and ADCE justification. |

---

## Architectural Mapping to ADCE & Caster

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          ADCE ARCHITECTURE LAYERS                           │
├────────────────────────────────┬────────────────────────────────────────────┤
│ Layer                          │ Leveraged Patterns & Open-Source Primitives│
├────────────────────────────────┼────────────────────────────────────────────┤
│ 1. Win32 Shallow Filter        │ • HwndExplorer (Win32Window & styles)      │
│    (< 0.5 ms Window Gating)    │ • User32 EnumWindows + GetWindowLongPtr    │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 2. Deep UIA Automation Plane   │ • FlaUI.UIA3 (CacheRequest DSL & Patterns) │
│    (10–15 ms Multi-Zone State) │ • UInspect (Dedicated MTA Worker Scheduler)│
│                                │ • FlaUI Resilience (Retry.WhileNull)       │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 3. Spatial & Text Geometry     │ • TIRG-DLL (Sub-10ms RGB Text Bounding Box)│
│    (Visual Grounding Fallback) │ • Zero-OCR Pixel Matrix Segmentation       │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 4. Syntactic AST Navigation    │ • Tree-sitter (Incremental GLR CST Parser) │
│    (Intra-Editor Micro-Context)│ • S-Expression AST Queries for Voice Rules │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 5. Cross-Platform Primitives   │ • AccessKit (Unified Node/Role/Action)     │
│    (Long-Term Multi-OS Schema) │ • Chromium AXTree Lineage & UIA Providers  │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 6. IPC & Daemon Host           │ • RegfreeNetComServer (NativeAOT RegFree)  │
│    (Cross-Process Context IPC) │ • ActiveN Out-of-Process COM Server Host   │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 7. Telemetry & Input Plane     │ • TraceSpy (ETW EventProvider Telemetry)   │
│    (Zero-Overhead Metrics)     │ • RawInputReader (Low-latency HID Sink)    │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 8. High-Performance Overlays   │ • DirectNAot (Zero-alloc Direct2D / DComp) │
│    (Visual Context HUD)        │ • FlaUInspect (Visual Bounding Box Layout) │
└────────────────────────────────┴────────────────────────────────────────────┘
```
