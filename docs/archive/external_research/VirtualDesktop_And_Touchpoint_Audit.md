<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 🔬 External Research ](README.md) › **VirtualDesktop & Touchpoint Ecosystem Audit**

---

# External Research: VirtualDesktop Implementations & Touchpoint MCP Audit

> **Document Status:** Historical Research Archive / API Ecosystem Audit
> **Epistemic Authority:** Tier 6 (External Research & Upstream Lineage — Non-Normative Background Context)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).
> **Scope:** Comparative audit of Windows Virtual Desktop libraries (`Slion` vs. `MSCholtes` vs. `pyvda`) and the `Touchpoint` cross-platform accessibility MCP server.
> **Context:** Architectural integration into ADCE's **Workspace Envelope** and MCP schema endpoints.

---

## 1. Executive Summary

To deliver comprehensive desktop awareness, the Active Desktop Context Engine (ADCE) must capture two distinct dimensions:
1. **Workspace Context (The Macro Layer):** Which virtual desktop GUID, name, and index is active, and which desktop owns the foreground window.
2. **MCP Accessibility Surface (The Tooling Layer):** How desktop context is queried and consumed by local AI agents and voice recognition frameworks.

This document audits the primary open-source projects in these two spaces:
* **Virtual Desktop Management:** `Slion/VirtualDesktop` (WinStasis lineage) vs. `MSCholtes/VirtualDesktop` vs. Python `pyvda`.
* **Accessibility MCP Servers:** `Touchpoint-Labs/touchpoint` ("Playwright for the OS") vs. ADCE.

---

## 2. Virtual Desktop Ecosystem: `Slion` vs. `MSCholtes` vs. `pyvda`

Windows Virtual Desktop COM interfaces (`IVirtualDesktopManagerInternal`, `IVirtualDesktopNotificationService`) are **undocumented and change internal GUIDs and vtable layouts across major Windows 10 and 11 builds** (e.g. 21H2, 22H2, 23H2, 24H2).

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        VIRTUAL DESKTOP ARCHITECTURE COMPARISON                         │
├──────────────────────────────┬──────────────────────────────┬──────────────────────────┤
│ Dimension                    │ Slion (WinStasis Lineage)    │ MSCholtes                │
├──────────────────────────────┼──────────────────────────────┼──────────────────────────┤
│ **Repository**               │ `Slion/VirtualDesktop`       │ `MSCholtes/VirtualDesktop│
│ **Package Form**             │ NuGet (`Slions.VirtualDesktop`)│ Standalone .cs / .exe  │
│ **Build Adaptability**       │ **Dynamic Roslyn Assembly**  │ **Separate Files**       │
│                              │ (compiles vtable at runtime) │ (VirtualDesktop11-24H2)  │
│ **Event Notifications**      │ `VirtualDesktop.CurrentChanged`│ Polling / CLI return   │
│                              │ (Native COM listener)        │ codes                    │
│ **Window Tracking**          │ `VirtualDesktop.FromHwnd()`  │ HWND CLI parameter       │
│ **Target Framework**         │ .NET 6 / 8 / 10 (C#)         │ .NET Framework 4.x / Csc │
│ **License**                  │ MIT                          │ MIT                      │
└──────────────────────────────┴──────────────────────────────┴──────────────────────────┘
```

### Detailed Breakdown:

#### 1. `Slion/VirtualDesktop` (Stéphane Lenclud — WinStasis Standard)
* **How It Works:** Uses dynamic runtime assembly compilation via Roslyn (`ComInterfaceAssemblyBuilder.cs`). When initialized, it detects `Environment.OSVersion.Version.Build` and compiles the exact COM interface definitions matching that Windows build into an in-memory assembly.
* **Key Capabilities:**
  * `VirtualDesktop.Current`: Returns active desktop (`Guid Id`, `string Name`, `string WallpaperPath`).
  * `VirtualDesktop.GetDesktops()`: Returns all active virtual desktops and their ordering.
  * `VirtualDesktop.FromHwnd(hwnd)`: Determines which virtual desktop owns a specific window.
  * `VirtualDesktop.CurrentChanged`: Fires instant event when user switches desktops (via `IVirtualDesktopNotificationService`).
  * `VirtualDesktop.IsPinnedWindow(hwnd)`: Checks if window is pinned across all desktops.
* **Verdict for ADCE:** 🟢 **Recommended for ADCE Production Implementation.** It is cleanly packaged on NuGet (`Slions.VirtualDesktop`), requires zero manual vtable patching, and provides event-driven desktop change notifications with zero CPU polling.

#### 2. `MSCholtes/VirtualDesktop` (Markus Scholtes)
* **How It Works:** Provides separate C# source files hardcoded for specific OS builds (`VirtualDesktop.cs` for Win10, `VirtualDesktop11.cs` for Win11 21H2–23H2, `VirtualDesktop11-24H2.cs` for Win11 24H2).
* **Pros & Cons:** Excellent standalone command-line tool, but requires manual build switching or bundling multiple binaries.
* **Verdict for ADCE:** 🟡 Useful as a secondary reference for raw COM GUID definitions and CLI testing.

#### 3. Python `pyvda`
* **How It Works:** Uses `comtypes` to bind to `IVirtualDesktopManagerInternal`.
* **Verdict for ADCE:** 🔴 Python-only; subject to GIL constraints and breakage when Windows 11 updates COM GUIDs.

---

## 3. Accessibility MCP Server Audit: `Touchpoint` vs. `ADCE`

`Touchpoint-Labs/touchpoint` is an open-source, cross-platform accessibility library and Model Context Protocol (MCP) server intended as "Playwright for the entire OS".

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                TOUCHPOINT vs. ADCE MATRIX                              │
├──────────────────────────────┬──────────────────────────────┬──────────────────────────┤
│ Architectural Trait          │ Touchpoint-Labs / touchpoint │ ADCE (Active Context)    │
├──────────────────────────────┼──────────────────────────────┼──────────────────────────┤
│ **Language & Runtime**       │ Python 3.10+ (`comtypes`)    │ C# 14 / .NET 10 (Native) │
│ **Primary Focus**            │ Full OS action automation    │ Low-latency context graph│
│                              │ (Click, Type, Find elements) │ (Tabs, Breadcrumbs, Focus)│
│ **Platform Scope**           │ Windows, macOS (AX), Linux   │ Windows-optimized (UIA3) │
│ **Electron / Monaco IDEs**   │ Falls back to CDP (DevTools) │ Direct UIA container query│
│ **Latency per Query**        │ `200 ms – 1,500 ms`          │ **`< 15 ms` (Multi-zone)**│
│ **Browser DOM Traversal**    │ Traverses full tree          │ **Strict DOM Pruning**   │
│ **Idle CPU Overhead**        │ Periodic / On-demand         │ **0.0% (WinEvent Hooks)**│
│ **Time-Series History**      │ ❌ None                      │ ✅ SQLite WAL / DuckDB   │
│ **Virtual Desktop Support**  │ ❌ None                      │ ✅ Slion VDesk COM       │
└──────────────────────────────┴──────────────────────────────┴──────────────────────────┘
```

### Key Insights from Touchpoint:

1. **The CDP Fallback Concession:**
   - In `touchpoint/backends/cdp/cdp.py`, Touchpoint connects to Chrome/Electron debugging ports (`--remote-debugging-port`) because standard Python `comtypes` UIA tree crawling is too slow on large Electron DOMs.
   - **ADCE Contrast:** ADCE avoids CDP entirely by using direct UIA container targeting (`tabs-container`, `monaco-breadcrumbs`) via `FlaUI.UIA3` batched `CacheRequest`, extracting 24 tabs in **10.17 ms** without needing remote debugging flags.

2. **Unified Role Taxonomy:**
   - Touchpoint defines a clean, cross-platform `Role` enum (e.g. `Role.BUTTON`, `Role.TAB_LIST`, `Role.TAB`, `Role.TEXT_FIELD`).
   - **ADCE Adoption:** We adopt a similar clean role mapping in [`docs/MCP_SCHEMA_SPEC.md`](../architecture/MCP_SCHEMA_SPEC.md) so LLMs receive standardized control classifications rather than raw Win32 integer IDs.

3. **Tool Design & Filtering:**
   - Touchpoint provides `elements(scope="window", app="Code")` and `find("Save")`.
   - **ADCE MCP Refinement:** ADCE focuses on high-density semantic snapshots (`get_desktop_context`, `desktop://current`) rather than fine-grained element clickers, giving AI coding assistants instant situational awareness in a single JSON payload.

---

## 4. Integration Blueprint for ADCE

Based on this audit, our implementation roadmap incorporates the following proven components:

1. **Workspace Envelope Layer:**
   - Reference `Slions.VirtualDesktop` (.NET 10) to power `ADCE.Core.WorkspaceManager`.
   - Subscribe to `VirtualDesktop.CurrentChanged` to emit `DesktopEvent.VirtualDesktopChanged` tokens into our `Channel<DesktopEvent>` with zero polling overhead.
2. **Context Schema Envelope Layer:**
   - Standardize focus and control output to match semantic MCP conventions.
3. **Execution Plane Layer:**
   - Continue leveraging `FlaUI.UIA3` batched `CacheRequest` with strict DOM pruning.

---

## 5. References & Local Clones

* **Cloned Reference Repositories (in `external/`):**
  * `external/Slion-VirtualDesktop/` — [Slion/VirtualDesktop](https://github.com/Slion/VirtualDesktop)
  * `external/MSCholtes-VirtualDesktop/` — [MSCholtes/VirtualDesktop](https://github.com/MSCholtes/VirtualDesktop)
  * `external/touchpoint/` — [Touchpoint-Labs/touchpoint](https://github.com/Touchpoint-Labs/touchpoint)
