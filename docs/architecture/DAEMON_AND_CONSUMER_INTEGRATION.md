<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# ADCE Daemon Host & Consumer Integration Specification

> **Document Status:** Active / Normative Daemon & MCP Reference
> **Epistemic Authority:** Tier 1 (Normative Production Specification)
> **Implementation Target:** `src/ADCE.Daemon/`, `src/ADCE.Mcp/` (.NET 10 / C# 14 / WinForms STA / JSON-RPC)
> **Test Baseline:** 50/50 Passing Unit Tests (28 Daemon + 22 MCP)

---

## 1. System Topology

`ADCE.Daemon` hosts the WinEvent pump and extraction engine as a Windows background system tray application. It exposes desktop context to AI agents and voice interfaces via `ADCE.Mcp`.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        ADCE RUNTIME TOPOLOGY                           │
├────────────────────────────────────────────────────────────────────────┤
│ Windows OS Event Loop                                                  │
│   │ SetWinEventHook (EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_FOCUS)      │
│   ▼                                                                    │
│ [ ADCE.Daemon (STA Thread) ]                                           │
│   ├── TrayIconFactory: System tray icon and context menu               │
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

`ADCE.Mcp` implements standard JSON-RPC 2.0 endpoints for live tool calls and resource subscription:

### 3.1 Implemented Tools

| Tool Endpoint | Parameters | Returns | Description |
| :--- | :--- | :--- | :--- |
| `get_desktop_context` | `process_filter: string?`, `projection: string?` | `DesktopContextSnapshot` | Returns live desktop context with container hierarchy, optional process filtering, and projection modes (`full`, `compact`, `ide`, `terminal`). |
| `get_active_context` | `process_filter: string?`, `projection: string?` | `DesktopContextSnapshot` | Canonical alias for `get_desktop_context`. |
| `search_desktop_history` | `query: string` (required), `limit: int` (default 20, max 100) | `List<DesktopContextSnapshot>` | Searches past desktop history in SQLite WAL storage matching keywords in titles, tabs, or file paths. |
| `tag_active_control` | `target_zone: string` (required), `target_pane: string?`, `target_view: string?`, `target_section: string?`, `scope: string?`, `comment: string?` | `TagResult` | Creates a persistent semantic rule for the active control in `%LOCALAPPDATA%\ADCE\semantic_rules.json` and updates live context immediately. |

### 3.2 Implemented Resources

| Resource URI | MimeType | Description | SLA |
| :--- | :--- | :--- | :--- |
| `desktop://current` | `application/json` | Live point-in-time snapshot from L1 atomic cache. | `< 1 µs` |
| `desktop://history` | `application/json` | Time-series query of recent focus transitions (supports `?minutes=15` and `?limit=50`). | `< 5 ms` |

---

## 4. Consumer Integration: Caster Dynamic Voice Grammars

The primary real-world consumer of ADCE is dynamic voice control via Caster.

### 4.1 The Integration Problem
Traditional voice coding grammars must manually track active applications using coarse Win32 window titles or process names. When focus shifts into an integrated terminal inside VS Code, voice engines remain stuck in code editing mode.

### 4.2 Dynamic Grammar Activation
By querying `http://localhost:8424/messages` or subscribing to `/sse`:
1. When `Focus.SemanticZone == DesktopSemanticZone.Terminal`, Caster activates shell grammars (`git status`, `cargo run`, `dotnet test`).
2. When `Focus.SemanticZone == DesktopSemanticZone.EditorBuffer`, Caster switches to language-specific navigation and syntax grammars.
3. When `Focus.SemanticZone == DesktopSemanticZone.GitCommitBox`, Caster switches to conventional commit voice templates.
