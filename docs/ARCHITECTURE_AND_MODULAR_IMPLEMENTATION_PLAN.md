<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../README.md) › [ 📚 Documentation Hub ](CONTEXT.md) › **Architecture & Modular Implementation Plan**

---

# ADCE Architecture & Modular Implementation Plan

> **Target Solution:** Active Desktop Context Engine (ADCE)
> **Runtime Target:** .NET 10 (`net10.0-windows`) / C# 14
> **Architecture Pattern:** Decoupled Event-Driven Pipeline & Modular Multi-Project Solution
> **Consumers:** Accessibility Frameworks, Dynamic Voice Grammar Engines & Local AI Agents (via MCP)

---

## 1. System Vision & Design Philosophy

The **Active Desktop Context Engine (ADCE)** is a lightweight, privacy-first Windows context daemon and accessibility primitive. It maintains a live, in-memory semantic graph of active desktop applications, window topologies, virtual desktop workspaces, open editor/browser tabs, and UI focus states—operating with **sub-15ms extraction latencies and 0.0% idle CPU overhead**.

### Core Design Principles:
1. **Unopinionated Accessibility Primitives:** ADCE does not dictate how downstream speech recognition frameworks (e.g. Caster, Dragonfly, Talon) or accessibility tools consume context. It provides raw, high-speed, structured primitives (window identity, container tabs, active buffers, workspace IDs) that tools can use for window switching, contextual grammar activation, or dynamic rule generation.
2. **Dual-Consumer Architecture:** A single high-performance engine serves both **local accessibility/voice tools** (direct C#/.NET or IPC bindings) and **local AI agents** (universal Model Context Protocol JSON-RPC 2.0 endpoints).
3. **Strict Modularity & Testability:** The engine is split into isolated class libraries. Each component can be instantiated, tested, and verified in isolation via minimal console spikes before being composed into the long-running daemon.
4. **Non-Invasive Execution:** 100% out-of-process execution using official Windows accessibility (`FlaUI.UIA3`), Win32 hooks (`SetWinEventHook`), and Virtual Desktop COM interfaces (`Slions.VirtualDesktop`), requiring zero DLL injection or kernel hooks.

---

## 2. The 5-Project Solution Architecture

The repository solution (`ADCE.slnx`) is organized into 5 decoupled projects with clear unidirectional dependency flows:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                ADCE SOLUTION DEPENDENCY GRAPH                          │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│                                   ┌─────────────────┐                                  │
│                                   │   ADCE.Daemon   │  (System Tray Host Application)  │
│                                   └────────┬────────┘                                  │
│                                            │                                           │
│                      ┌─────────────────────┼─────────────────────┐                     │
│                      ▼                     ▼                     ▼                     │
│             ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐            │
│             │ ADCE.Extraction │   │  ADCE.Storage   │   │    ADCE.Mcp     │            │
│             │ (UIA3 / Win32)  │   │ (SQLite / Cache)│   │ (JSON-RPC MCP)  │            │
│             └────────┬────────┘   └────────┬────────┘   └────────┬────────┘            │
│                      │                     │                     │                     │
│                      └─────────────────────┼─────────────────────┘                     │
│                                            ▼                                           │
│                                   ┌─────────────────┐                                  │
│                                   │    ADCE.Core    │  (Domain Models & Interfaces)    │
│                                   └─────────────────┘                                  │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### Project Responsibilities:

#### 1. `ADCE.Core` (Domain Models, Events & Interfaces)
* **Dependencies:** None (Pure .NET 10 / BCL).
* **Contents:**
  * **Data Records:** `DesktopContextSnapshot`, `WindowEnvelope`, `WorkspaceEnvelope`, `FocusedControlInfo`, `TabItemInfo`.
  * **Semantic Enums:** `DesktopSemanticZone` (`EditorCodeBuffer`, `IntegratedTerminal`, `GitCommitBox`, `SidebarExplorer`, `AddressBar`, `DocumentContent`, `Unknown`).
  * **Event Tokens:** `DesktopEvent` records (`ForegroundChanged`, `FocusChanged`, `VirtualDesktopSwitched`, `StructureChanged`).
  * **Abstractions:** `IExtractionEngine`, `IDesktopStateStore`, `IWorkspaceManager`, `IEventHookProvider`.

#### 2. `ADCE.Extraction` (Win32 Gating & FlaUI.UIA3 Context Engine)
* **Dependencies:** `ADCE.Core`, `FlaUI.UIA3` (5.0.0+), `Slions.VirtualDesktop`.
* **Contents:**
  * **Win32 Layer:** High-speed `EnumWindows`, `GetWindowLongPtr` (`WS`/`WS_EX`) shallow pre-filtering (`< 0.5 ms`).
  * **UIA Extraction Engine:** Scoped `CacheRequest.Activate()` multi-zone container extractors with strict browser DOM pruning (`< 15 ms`).
  * **Archetype Resolvers:** Specialized container recipes for Monaco IDEs (Antigravity, VS Code), Gecko/Chromium browsers (Waterfox, Chrome), WinUI 3 (File Explorer), and Terminal (`Cascadia`).
  * **Workspace Resolver:** `Slions.VirtualDesktop` runtime COM adapter.

#### 3. `ADCE.Storage` (In-Memory Graph & Time-Series Persistence)
* **Dependencies:** `ADCE.Core`, `Microsoft.Data.Sqlite`.
* **Contents:**
  * **In-Memory Cache:** Lock-free, thread-safe live snapshot cache for `< 1.0 ms` instant reads.
  * **SQLite WAL Store:** Embedded time-series repository tracking window transitions, focus history, and tab timelines for temporal queries (*"What was I editing 15 minutes ago?"*).

#### 4. `ADCE.Mcp` (Model Context Protocol JSON-RPC Server)
* **Dependencies:** `ADCE.Core`, `ADCE.Storage`.
* **Contents:**
  * **MCP Protocol Transports:** Standard I/O (Stdio) and Server-Sent Events (SSE / HTTP Minimal API).
  * **Endpoints:**
    * `get_desktop_context` (Tool: returns structured snapshot with optional app filter).
    * `desktop://current` (Resource: live current state snapshot).
    * `desktop://history?minutes=15` (Resource: recent temporal transitions).

#### 5. `ADCE.Daemon` (Windows System Tray Host)
* **Dependencies:** `ADCE.Core`, `ADCE.Extraction`, `ADCE.Storage`, `ADCE.Mcp`.
* **Contents:**
  * **Hosting Runtime:** Windows background service with tray icon menu (Status, Pause/Resume, Exit).
  * **MTA Thread Isolation:** Dedicated worker thread hosting the `Channel<DesktopEvent>` consumer to eliminate COM apartment deadlocks.
  * **Event Pipeline:** Bridges `SetWinEventHook` + `VirtualDesktop.CurrentChanged` ➔ `Channel<DesktopEvent>` ➔ MTA Extractor ➔ Storage ➔ MCP.

---

## 3. Modular Phased Milestones

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                 EXECUTION MILESTONES                                   │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ Milestone 1: Solution Scaffolding & Core Domain Models (ADCE.Core) [COMPLETE]          │
│ • Scaffold ADCE.slnx with net10.0-windows multi-project structure.                     │
│ • Implement immutable data records, semantic zone enums, and interfaces.               │
│ • [Verification]: 18/18 Unit tests passing (equality, JSON serialization, boundary).   │
│ • [Documentation]: docs/ADCE_CORE_DEEP_DIVE.md architecture and sequence equality.     │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ Milestone 2: Standalone Context Grabber (ADCE.Extraction)                              │
│ • Encapsulate FlaUI.UIA3 CacheRequest recipes into modular extractor classes.          │
│ • Integrate Slions.VirtualDesktop workspace envelope resolution.                       │
│ • [Verification]: Standalone CLI tool that extracts foreground tabs/zones in < 15 ms.  │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ Milestone 3: Zero-CPU Event Pipeline (ADCE.Extraction & ADCE.Core.Events)              │
│ • Implement SetWinEventHook listeners for foreground, focus, and desktop switches.     │
│ • Pipe events into System.Threading.Channels with 50ms trailing-edge debouncing.       │
│ • [Verification]: Console logger verifying 0.0% CPU when idle and instant wake on focus│
├────────────────────────────────────────────────────────────────────────────────────────┤
│ Milestone 4: Storage & History Engine (ADCE.Storage)                                   │
│ • Implement In-Memory Live Cache and embedded SQLite WAL repository.                   │
│ • [Verification]: Performance test verifying < 1.0 ms cache hits and < 5 ms DB reads. │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ Milestone 5: Model Context Protocol Server (ADCE.Mcp)                                  │
│ • Implement MCP JSON-RPC 2.0 server over Stdio and SSE.                                │
│ • [Verification]: Test with MCP Inspector / Claude Desktop configuration.              │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ Milestone 6: System Tray Background Daemon (ADCE.Daemon)                               │
│ • Assemble all libraries into a silent Windows system tray application.                │
│ • [Verification]: End-to-end background execution with system tray controls.           │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Verification Strategy per Milestone

To ensure absolute stability, every milestone must pass three verification criteria:
1. **Compilation & Safety:** `dotnet build` succeeds with 0 warnings/errors, and `python scripts/check_repo_safety.py` passes with zero path/secret leaks.
2. **Empirical Performance SLA:** Every extraction must execute within its allocated budget (`< 0.5 ms` Win32 shallow, `< 15 ms` multi-zone UIA).
3. **Memory & CPU Isolation:** No lingering COM proxies (`AutomationElementMode.None`) and `0.0%` sustained idle CPU.
