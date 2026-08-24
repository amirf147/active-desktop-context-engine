<!--
SPDX-License-Identifier: Apache-2.0
Copyright (c) 2024-2026 Amir Farhadi
-->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **External Research Hub**

---

# External Research: Windows Systems, COM & UI Automation Ecosystem

This directory contains technical audits, architectural analyses, and reverse-engineering notes across leading open-source Windows platform tooling, COM frameworks, and UI Automation implementations—specifically analyzing the bodies of work authored by **Simon Mourier** (`github.com/smourier`) and **Roman Baeriswyl** (`github.com/Roemer`, FlaUI).

## Analyzed Repositories & Tooling Ecosystems

| Document | Target Repositories / Authors | Core Focus Areas |
| :--- | :--- | :--- |
| [`FlaUI_And_Roemer_Ecosystem.md`](./FlaUI_And_Roemer_Ecosystem.md) | `FlaUI/FlaUI`, `FlaUI/FlaUInspect`, `Roemer/FlauLib` | FlaUI UIA3 custom COM interop, `CacheRequest` DSL, 40+ typed control patterns, XPath engine, head-to-head comparison vs. Simon Mourier. |
| [`UInspect.md`](./UInspect.md) | `smourier/UInspect` | UIA COM interop, dedicated MTA thread scheduling, structure change event listeners, comparison vs. `FlaUInspect`. |
| [`HwndExplorer.md`](./HwndExplorer.md) | `smourier/HwndExplorer` | Win32 shallow tree discovery, window hierarchy, style bitmasking (`WS`/`WS_EX`), process mapping, and z-order traversal. |
| [`RegfreeNetCom_Suite.md`](./RegfreeNetCom_Suite.md) | `smourier/RegfreeNetComServer`, `smourier/RefreeNetCom`, `smourier/OutOfProcessCOMServer`, `smourier/ActiveN`, `smourier/AotNetComHost` | Out-of-proc & in-proc registration-free COM servers in .NET 10 / NativeAOT, manifest marshaling with `OleAut32`, custom `IClassFactory`. |
| [`Interop_And_Telemetry_Tools.md`](./Interop_And_Telemetry_Tools.md) | `smourier/Win32InteropBuilder`, `smourier/TraceSpy`, `smourier/RawInputReader`, `smourier/DirectNAot` | Metadata-driven P/Invoke generation, zero-overhead ETW/debug tracing, raw HID input sinks, and GPU DirectX rendering. |
| [`SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md`](./SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md) | **Global Synthesis** | Strategic evaluation: Are we reinventing the wheel? Roemer vs. Simon supercedence matrix, standing on shoulders of existing primitives, and ADCE justification. |

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
│ 3. IPC & Daemon Host           │ • RegfreeNetComServer (NativeAOT RegFree)  │
│    (Cross-Process Context IPC) │ • ActiveN Out-of-Process COM Server Host   │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 4. Telemetry & Input Plane     │ • TraceSpy (ETW EventProvider Telemetry)   │
│    (Zero-Overhead Metrics)     │ • RawInputReader (Low-latency HID Sink)    │
├────────────────────────────────┼────────────────────────────────────────────┤
│ 5. High-Performance Overlays   │ • DirectNAot (Zero-alloc Direct2D / DComp) │
│    (Visual Context HUD)        │ • FlaUInspect (Visual Bounding Box Layout) │
└────────────────────────────────┴────────────────────────────────────────────┘
```
