<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

# Milestone 6 Engineering Postmortem & Systems Verification Ledger

> **Milestone:** Milestone 6: System Tray Background Daemon (`ADCE.Daemon`)
> **Runtime:** .NET 10 (`net10.0-windows`) / C# 14 / `FlaUI.UIA3 5.0.0`
> **Date:** August 2026
> **Verification Status:** `[PASSED]` (130/130 Unit Tests Passing + Gate 3 Spike 6 Verified)

---

## 1. Executive Summary

Milestone 6 represents the culmination of the Active Desktop Context Engine (ADCE) architecture, assembling all decoupled libraries (`ADCE.Core`, `ADCE.Extraction`, `ADCE.Storage`, `ADCE.Mcp`) into a unified Windows desktop background daemon: `ADCE.Daemon`.

Through rigorous adherence to the **4-Gate Epistemic Protocol**, 4 subtle systems traps were identified, engineered against, and empirically verified before production release:
1. **Unmanaged GDI Handle Destruction:** Integrated `user32.dll!DestroyIcon` in `TrayIconFactory` to prevent GDI table leaks during dynamic icon state changes.
2. **Single-Instance Mutex Protection:** Guarded startup with `Local\ADCE_Daemon_SingleInstance_Mutex` to eliminate SQLite WAL write lock contention and HTTP port 8424 collision errors.
3. **`WinExe` Console Attachment:** Integrated `kernel32.dll!AttachConsole(ATTACH_PARENT_PROCESS)` for seamless interactive terminal output (`--help`, `--status`) while maintaining silent GUI execution.
4. **Canonical Port Alignment:** Standardized default HTTP/SSE port across all libraries and documentation to **8424**.

---

## 2. Empirical Verification Evidence Ledger

### Gate 3 Spike 6 Telemetry
```text
================================================================================
  ADCE GATE 3 MICRO-SPIKE 6: SYSTEM TRAY BACKGROUND DAEMON & E2E INTEGRATION
================================================================================

[1/6] Instantiating and initializing DaemonHost (Port: 8424, Storage: in-memory)...
      DaemonHost started in 372 ms. State: Running

[2/6] Verifying Live Status & Initial Snapshot Extraction...
      State: Running, Uptime: 80 ms
      Total Events Received: 0
      Total Snapshots Extracted: 1
      Active Window: [No Active Window] ()
      Focused Zone: [Unknown] 'No Active Window'

[3/6] Querying MCP Server over HTTP/SSE endpoint...
      MCP Initialize Status: Accepted (Response: ...)
      MCP Tool Call Latency: 1 ms (Status: Accepted)

[4/6] Testing Pause & Resume Lifecycle...
      Paused State: Paused, IsPaused: True
      Resumed State: Running, IsPaused: False

[5/6] Testing Dynamic TrayIconFactory (Trap 1: GDI Handle Leak Verification)...
      60 state icons dynamically created and destroyed with zero GDI leaks.

[6/6] Shutting Down DaemonHost gracefully...
      DaemonHost cleanly stopped in 16 ms. Final State: Stopped

================================================================================
  [PASSED] ALL MILESTONE 6 DAEMON SUBSYSTEM CHECKS VERIFIED SUCCESSFULLY
================================================================================
```

### Full Solution Test Suite Summary
| Test Project | Passed | Failed | Skipped | Total | Duration |
| :--- | :---: | :---: | :---: | :---: | :---: |
| `ADCE.Core.Tests` | 20 | 0 | 0 | 20 | 87 ms |
| `ADCE.Storage.Tests` | 11 | 0 | 0 | 11 | 348 ms |
| `ADCE.Extraction.Tests` | 65 | 0 | 0 | 65 | 1.0 s |
| `ADCE.Mcp.Tests` | 16 | 0 | 0 | 16 | 486 ms |
| `ADCE.Daemon.Tests` | 18 | 0 | 0 | 18 | 165 ms |
| **Total** | **130** | **0** | **0** | **130** | **~2.1 s** |

---

## 3. Key Lessons Learned

1. **`Path.GetFullPath(":memory:")` Trap:**
   * In .NET, passing `:memory:` (standard SQLite in-memory identifier) into `Path.GetFullPath()` treats `:memory:` as a relative Windows file path (`<current_dir>\:memory:`), throwing Win32 file access errors.
   * `DaemonOptions.ResolveEffectiveDatabasePath()` must explicitly check for `:memory:` before calling path resolution methods.

2. **Clean Stream Shutdown in `HttpListener`:**
   * Calling `HttpListener.Stop()` or `Dispose()` while `GetContextAsync()` is awaiting asynchronously throws `HttpListenerException` / `ObjectDisposedException`.
   * Explicitly filtering these known teardown exceptions in the reader loop guarantees silent and clean multi-threaded teardown.

3. **Per-Monitor V2 DPI Awareness in .NET 10 WinForms:**
   * While .NET 10 supports `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in the `.csproj`, embedding `<dpiAwareness>PerMonitorV2</dpiAwareness>` in `app.manifest` and invoking `SetProcessDpiAwarenessContext` in `Program.cs` ensures absolute coordinate accuracy for Win32 and UIA element rects across mixed DPI monitors.
