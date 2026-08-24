<!--
SPDX-License-Identifier: Apache-2.0
Copyright (c) 2024-2026 Amir Farhadi
-->

# Codebase Deep Dive: Registration-Free COM & NativeAOT Suite

This document analyzes the collection of COM server architectures authored by Simon Mourier:
1. [`smourier/RegfreeNetComServer`](file:///C:/Users/Amir/Documents/repos/RegfreeNetComServer)
2. [`smourier/RefreeNetCom`](file:///C:/Users/Amir/Documents/repos/RefreeNetCom)
3. [`smourier/OutOfProcessCOMServer`](file:///C:/Users/Amir/Documents/repos/OutOfProcessCOMServer)
4. [`smourier/ActiveN`](file:///C:/Users/Amir/Documents/repos/ActiveN)
5. [`smourier/AotNetComHost`](file:///C:/Users/Amir/Documents/repos/AotNetComHost)

---

## 1. Architectural Taxonomy of the Suite

```
                               ┌─────────────────────────────────────────┐
                               │       Simon Mourier COM Ecosystem       │
                               └────────────────────┬────────────────────┘
                                                    │
                 ┌──────────────────────────────────┴──────────────────────────────────┐
                 │                                                                     │
                 ▼                                                                     ▼
   ┌───────────────────────────┐                                         ┌───────────────────────────┐
   │  Out-of-Process Servers   │                                         │    In-Process Servers     │
   ├───────────────────────────┤                                         ├───────────────────────────┤
   │ • RegfreeNetComServer     │                                         │ • RefreeNetCom (.NET 10)  │
   │   (.NET 10 / Reg-Free)    │                                         │ • ActiveN (NativeAOT)     │
   │ • OutOfProcessCOMServer   │                                         │ • AotNetComHost (Thunk)   │
   │   (Native C++ ATL)        │                                         └───────────────────────────┘
   └───────────────────────────┘
```

---

## 2. In-Depth Analysis by Repository

### 2.1 `RegfreeNetComServer` (.NET 10 Out-Of-Process Reg-Free COM)
- **Goal:** Host an out-of-process COM server in modern .NET 10 without registry writes or administrator rights.
- **Key Pattern:**
  - Uses `CoRegisterClassObject` with `CLSCTX_LOCAL_SERVER` and `REGCLS_MULTIPLEUSE`.
  - Client side uses `app.manifest` referencing a Type Library (`server.tlb`).
  - Automatically redirects marshaling to `OleAut32.dll` standard marshaler `{00020424-0000-0000-C000-000000000046}` via `comInterfaceExternalProxyStub`.
  - Cross-architecture compatible: 32-bit native clients and 64-bit .NET servers communicate transparently without custom proxy/stub DLLs.

### 2.2 `RefreeNetCom` (.NET 10 In-Process Reg-Free COM)
- **Goal:** Enable .NET 10 classes to be loaded in-process by native clients (C++, VB6, VBA, VBScript, .NET Framework) without registration.
- **Key Pattern:**
  - Exposes `[ComVisible(true)]` class inheriting from `IServer` and explicit `IDispatch`.
  - Client manifest specifies `<file name="RegfreeNetCom.dll"><comClass clsid="..." /></file>`.
  - IDL is compiled via custom MSBuild target invoking Windows SDK `midl.exe`.

### 2.3 `ActiveN` (Modern COM / ActiveX Framework for NativeAOT)
- **Goal:** Author full OLE/ActiveX controls and classic COM components in .NET NativeAOT without WinForms or WPF dependencies.
- **Key Pattern:**
  - Uses modern C# source generators (`[GeneratedComClass]`).
  - Implements full OLE control lifecycle: `IOleObject`, `IOleControl`, `IOleInPlaceActiveObject`, `IPersistStreamInit`, `IConnectionPointContainer`, and `IDataObject`.
  - Supports aggregation (`IAggregable`), enabling host integration with strict hosts like Microsoft Excel.
  - Native rendering via Direct2D / DirectComposition (`DirectNAot`).

### 2.4 `AotNetComHost` (Development-Time Thunk for NativeAOT)
- **Goal:** Solve the slow development loop in .NET NativeAOT COM authoring (where recompiling AOT release binaries is slow).
- **Key Pattern:**
  - Native C++ thunk DLL that proxies COM registration and exports `DllGetClassObject` / `DllRegisterServer` to a non-AOT debug .NET assembly via `nethost.dll`.
  - Supports per-user registration (`HKCU`), allowing registration without administrative elevation (`regsvr32 /i:user`).

### 2.5 `OutOfProcessCOMServer` (C++ ATL Out-Of-Process Reference)
- **Goal:** Canonical ATL implementation of out-of-process COM server with custom interface marshaling.
- **Key Pattern:**
  - Shows the distinction between automation dual interfaces (`IMouse` using standard marshaler) and custom non-automation interfaces (`IKeyboard` using custom proxy/stub DLL `OutOfProcessCOMServerPS.dll`).

---

## 3. Comparative Evaluation: COM IPC vs Named Pipes / JSON-RPC for ADCE

| Criterion | Reg-Free COM IPC (`RegfreeNetComServer`) | Named Pipes + JSON-RPC / MCP (ADCE) | Strategic Verdict |
| :--- | :--- | :--- | :--- |
| **Protocol Compatibility** | Windows COM Clients Only (C++, C#, VB) | Standard JSON-RPC (Claude, Caster, Python, Web) | **Named Pipes / Stdio** required for MCP compatibility. |
| **Zero-Config Deployment** | Requires sidecar `.manifest` + `.tlb` files | Single self-contained `.exe` or stdio process | **Named Pipes** has zero manifest dependency. |
| **Bitness Crossing (32/64)**| Automatic via `OleAut32.dll` proxy | Universal stream format (JSON byte stream) | **Both support 32/64 seamlessly.** |
| **Throughput & Latency** | ~0.05ms per call (binary COM vtable) | ~0.2ms per call (JSON serialization) | **COM is ~4x faster**, but JSON-RPC latency is negligible for context extraction. |
| **Native C# AOT Support** | Excellent via `ActiveN` patterns | Native via `System.Text.Json` source generation | **Both support full NativeAOT.** |

---

## 4. Architectural Lessons for ADCE

1. **Keep ADCE Daemon on Named Pipes / Stdio for MCP:**
   The Model Context Protocol (MCP) requires stdio or SSE/HTTP JSON-RPC streams. Pure COM IPC is too Windows-specific to expose directly to standard MCP hosts.
2. **Use Reg-Free COM for In-Process Extensions:**
   If ADCE ever needs an in-process hook DLL injected into target processes (e.g. for extracting rich editor buffers or terminal contents directly), `RefreeNetCom` and `ActiveN` provide the exact blueprint for building lightweight, NativeAOT in-proc DLLs.
3. **TypeLib and IDL Generation Mastery:**
   The MSBuild target patterns in `GetWindowsSDKPaths.targets` provide a robust mechanism for invoking Windows SDK tools dynamically inside modern .NET `.csproj` builds.
