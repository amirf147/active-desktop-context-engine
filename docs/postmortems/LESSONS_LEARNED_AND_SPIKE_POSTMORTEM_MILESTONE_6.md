<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

# Milestone 6 Engineering Postmortem & Systems Verification Ledger

> **Milestone:** Milestone 6, 6.1 & 6.2: System Tray Background Daemon (`ADCE.Daemon`), Non-Activating HUD & Event Pipeline Hardening
> **Runtime:** .NET 10 (`net10.0-windows`) / C# 14 / `FlaUI.UIA3 5.0.0`
> **Date:** August 2026
> **Verification Status:** `[PASSED]` (136/136 Unit Tests Passing + Gate 3 Spikes 1–6 Verified)

---

## 1. Executive Summary

Milestone 6 represents the culmination of the Active Desktop Context Engine (ADCE) architecture, assembling all decoupled libraries (`ADCE.Core`, `ADCE.Extraction`, `ADCE.Storage`, `ADCE.Mcp`) into a unified Windows desktop background daemon: `ADCE.Daemon`.

Through live physical testing and empirical telemetry across real-world applications (**Antigravity IDE**, **Waterfox**, and **Windows 11 File Explorer**), seven subtle Windows OS interaction traps were identified, engineered against, and verified:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        ADCE MILESTONE 6 SYSTEMS HARDENING MATRIX                       │
├────┬─────────────────────────────┬────────────────────────────────────────────────────┤
│ ID │ Phenomenon / Trap           │ Engineered Architectural Solution                  │
├────┼─────────────────────────────┼────────────────────────────────────────────────────┤
│ 01 │ GDI Icon Handle Leaks       │ Native user32!DestroyIcon in TrayIconFactory       │
│ 02 │ Daemon Mutex Collisions     │ Global single-instance named Mutex                 │
│ 03 │ WinExe Silent Console Trap  │ Dynamic kernel32!AttachConsole parent attach       │
│ 04 │ OLE STA Threading Trap      │ StaClipboardHelper with 3-iteration backoff loop   │
│ 05 │ Taskbar Hover Overwrite     │ Win32Gating.IsTransientShellWindow class filter    │
│ 06 │ Observer Focus Stealing     │ FloatingHudForm with WS_EX_NOACTIVATE | TOPMOST    │
│ 07 │ Intra-App Focus Starvation  │ 4-hook mask + foreground NAMECHANGE storm guard    │
└────┴─────────────────────────────┴────────────────────────────────────────────────────┘
```

---

## 2. Empirical Verification Evidence Ledger

### 2.1 Full Solution Test Suite Summary
| Test Project | Passed | Failed | Skipped | Total | Duration |
| :--- | :---: | :---: | :---: | :---: | :---: |
| `ADCE.Core.Tests` | 20 | 0 | 0 | 20 | 95 ms |
| `ADCE.Storage.Tests` | 11 | 0 | 0 | 11 | 347 ms |
| `ADCE.Extraction.Tests` | 67 | 0 | 0 | 67 | 1.0 s |
| `ADCE.Mcp.Tests` | 16 | 0 | 0 | 16 | 483 ms |
| `ADCE.Daemon.Tests` | 22 | 0 | 0 | 22 | 171 ms |
| **Total** | **136** | **0** | **0** | **136** | **~2.1 s** |

---

## 3. The 7 Systems Traps & Lessons Learned

### Trap 1: Dynamic GDI Icon Handle Leaks in Windows System Tray
* **The Trap:** Calling `Bitmap.GetHicon()` allocates an unmanaged Windows GDI handle (`HICON`). In Windows, GDI handles are process-limited (default: 10,000 handles). Continuously creating status icons on every context change exhausts the OS GDI table, eventually causing desktop rendering crashes.
* **The Fix:** [`TrayIconFactory.cs`](../../src/ADCE.Daemon/UI/TrayIconFactory.cs) explicitly calls `NativeMethods.DestroyIcon(hIcon)` before managed icon wrappers are released, and old icons are cleanly disposed when state changes.

### Trap 2: Single-Instance Named Mutex Protection
* **The Trap:** Multiple background daemons writing concurrently to the same SQLite database cause WAL write locks (`SQLITE_BUSY`), while duplicate MCP servers fail on HTTP port 8424 (`HttpListenerException: Access is denied / Address in use`).
* **The Fix:** Guarded `Program.cs` with `new Mutex(true, "Local\\ADCE_Daemon_SingleInstance_Mutex", out bool createdNew)`. If another instance is running, the secondary process gracefully passes command-line arguments to the active daemon or exits.

### Trap 3: `WinExe` Console Attachment & Interactive Output
* **The Trap:** Windows binaries compiled as `<OutputType>WinExe</OutputType>` do not attach to stdout by default, causing CLI commands like `ADCE.Daemon.exe --status` or `--help` to output nothing in PowerShell.
* **The Fix:** [`NativeMethods.AttachConsole(ATTACH_PARENT_PROCESS)`](../../src/ADCE.Daemon/Program.cs) dynamically binds stdout and stderr to the caller's console when CLI switches are provided.

### Trap 4: OLE Clipboard STA ThreadException & Lock Contention
* **The Trap:** Resuming after `await host.StartAsync()` continues on a .NET ThreadPool (MTA) thread. Calling `Clipboard.SetDataObject()` from an MTA thread throws `System.Threading.ThreadStateException`. Furthermore, tools like Windows Clipboard History (`Win+V`) or Ditto can hold a lock on the clipboard (`CLIPBRD_E_CANT_OPEN` / `0x800401D0`).
* **The Fix:** [`StaClipboardHelper.cs`](../../src/ADCE.Daemon/UI/StaClipboardHelper.cs) guarantees execution on a dedicated STA apartment thread, wrapped in a 3-iteration backoff retry loop catching `ExternalException`.

### Trap 5: Taskbar Hover Overwrite (`Shell_TrayWnd`)
* **The Trap:** When moving the mouse over the Windows taskbar or tray icon, Windows fires `EVENT_OBJECT_FOCUS` on `Shell_TrayWnd` (`explorer.exe`), overwriting the active application context with `ADCE: [Unknown]`.
* **The Fix:** [`Win32Gating.IsTransientShellWindow`](../../src/ADCE.Extraction/Win32/Win32Gating.cs) filters `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`, `TopLevelWindowForOverflowXamlIsland`, and `tooltips_class32` as noise, preserving the active user application state.

### Trap 6: Observer Focus Stealing in DevTools Overlays
* **The Trap:** Standard GUI forms steal OS keyboard and window focus when clicked or activated, displacing the very application under inspection.
* **The Fix:** [`FloatingHudForm.cs`](../../src/ADCE.Daemon/UI/FloatingHudForm.cs) overrides `CreateParams` with `WS_EX_NOACTIVATE (0x08000000) | WS_EX_TOPMOST (0x00000008) | WS_EX_TOOLWINDOW (0x00000080)` and `ShowWithoutActivation => true`, allowing the HUD to be clicked and dragged without stealing focus.

### Trap 7: Intra-App Event Starvation in Multi-Process Browsers & Electron
* **The Trap:** Clicking tabs or URL bars within Waterfox (Gecko) or Antigravity (Electron) emits `EVENT_OBJECT_SELECTION` (`0x8006`), `EVENT_OBJECT_NAMECHANGE` (`0x800C`), or non-zero `idChild` tokens. Narrowing the hook strictly to `0x0003` and `0x8005` dropped valid intra-app transitions.
* **The Fix:**
  1. Installed 4 targeted hooks: `0x0003` (Foreground), `0x8005` (Focus), `0x8006` (Selection), and `0x800C` (NameChange).
  2. Gated `EVENT_OBJECT_NAMECHANGE` against `GetForegroundWindow()` to prevent background clock/downloader storms.
  3. Normalized child rendering HWNDs via `GetAncestor(hwnd, GA_ROOTOWNER)` before channel ingestion.

---

## 4. SQLite Time-Series Store & The `--timeline` Visualizer

ADCE includes an embedded SQLite time-series database running in WAL mode (`adce_history.db` stored in the user's LocalAppData `ADCE` directory).

### Schema & Indexing
```sql
CREATE TABLE desktop_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT NOT NULL,
    timestamp_unix_ms INTEGER NOT NULL,
    hwnd INTEGER NOT NULL,
    window_title TEXT NOT NULL,
    process_name TEXT NOT NULL,
    class_name TEXT NOT NULL,
    archetype INTEGER NOT NULL,
    focus_control_type TEXT,
    focus_element_name TEXT,
    focus_semantic_zone INTEGER NOT NULL,
    active_file_or_tab TEXT,
    snapshot_json TEXT NOT NULL
);

CREATE INDEX idx_snapshots_time_desc ON desktop_snapshots(timestamp_unix_ms DESC, id DESC);
CREATE INDEX idx_snapshots_process ON desktop_snapshots(process_name, timestamp_unix_ms DESC);
```

### Visualizing Context Transitions with `--timeline`
You can inspect and visualize recorded context transitions at any time using `ADCE.Spikes`:
```powershell
dotnet run --project src/ADCE.Spikes -- --timeline 15
```

Sample visual output:
```text
==============================================================================================================
#     | TIME (UTC)   | PROCESS        | SEMANTIC ZONE        | ACTIVE CONTEXT / TAB / FILE
--------------------------------------------------------------------------------------------------------------
322   | 13:33:33.907 | Antigravity ID | [Unknown]            | Stop recording
323   | 13:34:19.604 | Antigravity ID | [ChatAssistant]      | Message input
324   | 13:36:26.553 | Antigravity ID | [EditorCodeBuffer]   | use Alt+F1 to open the accessibility help.
325   | 13:36:27.388 | Antigravity ID | [ChatAssistant]      | Message input
326   | 13:36:27.790 | Antigravity ID | [DocumentContent]    | amirf147.github.io - Antigravity IDE
329   | 13:36:31.583 | waterfox       | [DocumentContent]    | Amir Farhadi - Portfolio Evidence
330   | 13:36:33.364 | waterfox       | [Unknown]            | Commit SHA 894ebf6 (opens in a new tab)
335   | 13:36:37.466 | Antigravity ID | [DocumentContent]    | active-desktop-context-engine - Antigravity IDE
336   | 13:36:38.140 | Antigravity ID | [GitCommitBox]       | Message (Ctrl+Enter to commit on "master")
==============================================================================================================

[1] Application Transition Distribution:
  Antigravity IDE  [████████████████         ]  66.7% (10 transitions)
  waterfox         [██████                   ]  26.7% (4 transitions)
  explorer         [█                        ]   6.7% (1 transitions)

[2] Semantic Zone Distribution:
  Unknown              [▓▓▓▓▓▓▓▓                 ]  33.3% (5 snapshots)
  ChatAssistant        [▓▓▓▓▓▓                   ]  26.7% (4 snapshots)
  DocumentContent      [▓▓▓▓▓                    ]  20.0% (3 snapshots)
  EditorCodeBuffer     [▓▓▓                      ]  13.3% (2 snapshots)
  GitCommitBox         [▓                        ]   6.7% (1 snapshots)
```

---

## 5. Demystifying `[Unknown]` Zones & Dynamic Heuristic Discovery

A key insight during live testing is that unfamiliar UI elements are classified as `[Unknown]`.

### Why Controls Start as `[Unknown]`
ADCE avoids hallucinating semantic labels. If an element does not match a known heuristic signature (such as Monaco editor pane, Terminal window, Git Commit Box, or Omnibox), ADCE assigns it `DesktopSemanticZone.Unknown`.

### How Unknowns are Logged & Refined
1. **Raw Attributes are Preserved:** In every `desktop_snapshots` row, the database records the exact raw unmanaged signature:
   * `process_name` (e.g. `Antigravity.exe`)
   * `class_name` (e.g. `Chrome_WidgetWin_1`)
   * `focus_control_type` (e.g. `Button`)
   * `focus_element_name` (e.g. `Stop recording`)
2. **Retrospective Discovery:** By running `--timeline`, the engine aggregates and displays unknown zone transitions:
   ```text
   [3] Discovery Telemetry: 5 Unknown Zone Transition(s) Detected
       • App: 'Antigravity IDE' | Class: 'Chrome_WidgetWin_1' | ControlType: 'Button' | Name: 'Stop recording'
       • App: 'waterfox' | Class: 'MozillaWindowClass' | ControlType: 'Hyperlink' | Name: 'Commit SHA 894ebf6'
   ```
3. **Adaptive Rule Expansion:** New semantic zone classifications (e.g. `AudioRecordingControls`, `WebHyperlinks`) can be mapped directly from observed telemetry without altering the core pipeline.
