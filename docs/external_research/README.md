<!--
SPDX-License-Identifier: Apache-2.0
Copyright (c) 2024-2026 Amir Farhadi
-->

# External Research: Simon Mourier Ecosystem Deep Dive

This directory contains technical audits, architectural analyses, and reverse-engineering notes of Simon Mourier's open-source Windows platform tooling, COM frameworks, and UI Automation implementations.

## Analyzed Repositories

| Document | Target Repositories | Core Focus Areas |
| :--- | :--- | :--- |
| [`UInspect.md`](./UInspect.md) | `smourier/UInspect` | UIA COM interop, MTA thread scheduling, event listeners, tree walker vs. cache request performance. |
| [`HwndExplorer.md`](./HwndExplorer.md) | `smourier/HwndExplorer` | Win32 shallow tree discovery, window hierarchy, style bitmasking, process mapping, and z-order traversal. |
| [`RegfreeNetCom_Suite.md`](./RegfreeNetCom_Suite.md) | `smourier/RegfreeNetComServer`, `smourier/RefreeNetCom`, `smourier/OutOfProcessCOMServer`, `smourier/ActiveN`, `smourier/AotNetComHost` | Out-of-proc & in-proc registration-free COM servers in .NET 10 / NativeAOT, manifest marshaling with `OleAut32`, custom `IClassFactory`. |
| [`Interop_And_Telemetry_Tools.md`](./Interop_And_Telemetry_Tools.md) | `smourier/Win32InteropBuilder`, `smourier/TraceSpy`, `smourier/RawInputReader`, `smourier/DirectNAot` | Metadata-driven P/Invoke generation, zero-overhead ETW/debug tracing, raw HID input sinks, and GPU DirectX rendering. |
| [`SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md`](./SYNTHESIS_AND_WHEEL_REINVENTION_AUDIT.md) | **Global Synthesis** | Strategic evaluation: Are we reinventing the wheel? Value extraction matrix, standing on shoulders of existing primitives, and ADCE justification. |

---

## Architectural Mapping to ADCE & Caster

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        ADCE Architecture Layers                         │
├────────────────────────────────┬────────────────────────────────────────┤
│ Layer                          │ Leveraged smourier Patterns            │
├────────────────────────────────┼────────────────────────────────────────┤
│ 1. Win32 Shallow Filter        │ HwndExplorer (Win32Window & styles)    │
│ 2. Deep UIA Automation Plane   │ UInspect (MTA Task Scheduler & Events) │
│ 3. IPC & Daemon Host           │ RegfreeNetComServer & ActiveN Patterns │
│ 4. Telemetry & Input Plane     │ TraceSpy (ETW) & RawInputReader (HID)  │
│ 5. High-Performance Overlays   │ DirectNAot (Zero-alloc D2D/DComp)      │
└────────────────────────────────┴────────────────────────────────────────┘
```
