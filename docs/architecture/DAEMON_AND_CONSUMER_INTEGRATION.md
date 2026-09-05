<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# ADCE Daemon Host & Consumer Integration Specification

> **Document Status:** Active / Normative Daemon & MCP Reference
> **Epistemic Authority:** Tier 1 (Normative Production Specification)
> **Implementation Target:** `src/ADCE.Daemon/`, `src/ADCE.Mcp/` (.NET 10 / C# 14 / WinForms STA / JSON-RPC)
> **Test Baseline:** 50/50 Passing Unit Tests (28 Daemon + 22 MCP)

---

## 1. System Topology

`ADCE.Daemon` hosts the WinEvent pump and extraction engine as a Windows background system tray application. It exposes context to AI agents and voice interfaces via `ADCE.Mcp`.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        ADCE RUNTIME TOPOLOGY                           │
├────────────────────────────────────────────────────────────────────────┤
│ Windows OS Event Loop                                                  │
│   │ SetWinEventHook (EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_FOCUS)      │
│   ▼                                                                    │
│ [ ADCE.Daemon (STA Thread) ]                                           │
│   ├── TrayIconFactory: System tray icon & context menu                 │
│   ├── FloatingHudForm: Optional non-activating transparent HUD overlay │
│   ├── SingleInstanceMutex: Enforces single running process instance    │
│   └── DaemonHost: Orchestrates pipeline, cache, and MCP listener       │
│         │                                                              │
│         ▼                                                              │
│ [ ADCE.Mcp (JSON-RPC 2.0 Engine) ]                                     │
│   ├── Stdio Transport: For CLI AI agent processes                      │
│   └── HTTP / SSE Transport: http://localhost:8424                      │
│         ├── GET  /sse      (Continuous stream of DesktopContextSnapshot)│
│         └── POST /messages (JSON-RPC 2.0 tool execution)               │
│                                                                        │
│ Live Consumers:                                                        │
│   ├── Caster Voice Engine: Dynamic terminal and editor voice grammars  │
│   └── AI Coding Assistants: Claude Desktop, Antigravity IDE, Cline     │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Windows Daemon Host Invariants

### 2.1 STA WinEvent Pump
* `SetWinEventHook` requires a running Win32 message pump on a Single-Threaded Apartment (STA) thread.
* `ADCE.Daemon` runs a standard WinForms `ApplicationContext` message loop, guaranteeing responsive event delivery without hooking overhead in target processes.

### 2.2 Single-Instance Mutex
* A named system mutex (`Local\ADCE.Daemon.SingleInstance`) prevents duplicate daemon processes from running concurrently and competing for WinEvent hooks or database locks.
* Attempting to launch a second instance terminates immediately with exit code 0.

### 2.3 Non-Activating HUD Overlay
* `FloatingHudForm` displays real-time telemetry (active process, window title, semantic zone, pane location).
* The form uses `WS_EX_NOACTIVATE` (`0x08000000`) and `WS_EX_TOPMOST` window styles, ensuring that the HUD never steals focus or triggers focus-switch WinEvents.

---

## 3. Model Context Protocol (MCP) Endpoints

`ADCE.Mcp` implements standard JSON-RPC 2.0 endpoints for live tool calls:

| Tool Endpoint | Parameters | Returns | Description |
| :--- | :--- | :--- | :--- |
| `get_current_snapshot` | None | `DesktopContextSnapshot` | Fetches the latest point-in-time desktop state from L1 cache (< 1 µs). |
| `get_active_control_hierarchy` | `includeAncestors: bool` | `ControlHierarchyResult` | Returns focused control details and parent breadcrumb chain. |
| `tag_active_control` | `zone: string, ruleName: string` | `TagResult` | Adds or updates a dynamic rule in `%LOCALAPPDATA%\ADCE\semantic_rules.json`. |
| `get_recent_snapshots` | `limit: int, processFilter: string?` | `List<DesktopContextSnapshot>` | Queries historical snapshots from SQLite WAL time-series storage. |

---

## 4. Consumer Integration: Caster Dynamic Voice Grammars

The primary real-world consumer of ADCE is dynamic voice control via Caster.

### 4.1 The Integration Problem
Traditional voice coding grammars must manually track active applications using coarse Win32 window titles or process names. When focus shifts into an integrated terminal inside VS Code, voice engines remain stuck in code editing mode.

### 4.2 Dynamic Grammar Activation
By querying `http://localhost:8424/messages` or subscribing to `/sse`:
1. When `Focus.SemanticZone == DesktopSemanticZone.Terminal`, Caster activates shell grammars (e.g. `git status`, `cargo run`, `dotnet test`).
2. When `Focus.SemanticZone == DesktopSemanticZone.EditorBuffer`, Caster switches to language-specific navigation and syntax grammars.
3. When `Focus.SemanticZone == DesktopSemanticZone.GitCommitBox`, Caster switches to conventional commit voice templates.
