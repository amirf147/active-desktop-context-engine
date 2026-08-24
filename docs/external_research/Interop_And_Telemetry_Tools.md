<!--
SPDX-License-Identifier: Apache-2.0
Copyright (c) 2024-2026 Amir Farhadi
-->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 🔬 External Research ](README.md) › **Simon Mourier: Interop, Telemetry & Input Tools**

---

# Codebase Deep Dive: Interop, Telemetry & Input Tools

This document analyzes Simon Mourier's specialized Windows systems tooling:
1. [`smourier/Win32InteropBuilder`](https://github.com/smourier/Win32InteropBuilder)
2. [`smourier/TraceSpy`](https://github.com/smourier/TraceSpy)
3. [`smourier/RawInputReader`](https://github.com/smourier/RawInputReader)
4. [`smourier/DirectNAot`](https://github.com/smourier/DirectNAot)

---

## 1. `Win32InteropBuilder` (Metadata-Driven P/Invoke Generation)

### Overview
`Win32InteropBuilder` is a tool that reads Microsoft's official `Microsoft.Windows.SDK.Win32Metadata` packages and generates clean, NativeAOT-friendly C# P/Invoke signatures, structs, unions, and enums.

### Key Architectural Features:
- **No Heavy Runtime Marshalling:** Generates blittable structs and raw function pointers (`calli` / `DllImport` with exact memory layouts) to avoid GC transition overhead.
- **Configurable Patching:** Uses JSON configuration files (`BuilderConfiguration.cs`, `BuilderPatches.cs`) to customize type names, map pointers to friendly C# types, and inject custom marshaling rules.
- **AOT Compatibility:** Guaranteed 100% NativeAOT compatibility with zero reliance on legacy runtime reflection-based COM interop.

---

## 2. `TraceSpy` (Real-Time ETW & OutputDebugString Diagnostics)

### Overview
`TraceSpy` is a pure .NET, zero-dependency alternative to SysInternals `DebugView`. It captures high-throughput diagnostic telemetry without impacting application execution speed.

### Key Mechanisms:
- **`OutputDebugString` Capturing:** Uses Win32 memory-mapped files (`DBWIN_BUFFER`) and event synchronization (`DBWIN_BUFFER_READY`, `DBWIN_DATA_READY`) to stream debug messages across processes with sub-microsecond latency.
- **Event Tracing for Windows (ETW):** Implements `EventProvider` and `EventProviderLoggerProvider` in C#, allowing ADCE or any daemon process to emit structured high-frequency event traces that can be analyzed without attaching a debugger.

---

## 3. `RawInputReader` (Low-Level Input Telemetry)

### Overview
`RawInputReader` demonstrates high-performance, non-intrusive keyboard, mouse, and HID hardware input capture using the Windows Raw Input API.

### Comparison with Global Hooks:

| Dimension | Global Hook (`SetWindowsHookEx` / `WH_KEYBOARD_LL`) | Raw Input API (`RegisterRawInputDevices` / `WM_INPUT`) |
| :--- | :--- | :--- |
| **Execution Context** | Blocks the OS UI input pipeline on every keystroke/mouse move | Asynchronous input sink (`RIDEV_INPUTSINK`) |
| **System Impact** | Can freeze mouse/keyboard if callback takes >5ms | Zero risk of lagging user input |
| **Elevated Windows** | Fails silently on UAC / elevated windows unless running as SYSTEM | Captures hardware-level events uniformly |
| **Relevance for ADCE** | Anti-pattern for background context tracking | **Ideal pattern for detecting user idle / activity transitions** |

---

## 4. `DirectNAot` (Zero-Overhead GPU Rendering & DComp)

### Overview
`DirectNAot` is a 9,500+ file interop library providing exhaustive, AOT-compatible bindings for:
- Direct2D, DirectWrite, DirectComposition
- DXGI, DirectX 9 to 12
- Windows Imaging Component (WIC)
- Media Foundation, WASAPI, GDI

### Strategic Application for ADCE / Caster:
If the Accessibility MCP or ADCE introduces a **Visual Overlay HUD** (e.g., drawing numbered bounding boxes, target zone highlights, or visual anchors on top of target windows like *Hunt and Peck* or *Warpd*), `DirectNAot` enables hardware-accelerated, transparent DirectComposition overlays with zero GC allocations.
