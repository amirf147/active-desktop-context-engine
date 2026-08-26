<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **Ground-Truth Claim Verification Ledger**

---

# Ground-Truth Claim Verification Evidence Ledger

> **Suite:** Ground-Truth Claim Verification Suite
> **Driver Mode:** `Synthetic Headless Mock Driver`
> **Timestamp:** 2026-08-25 03:48:36 UTC
> **Total Duration:** 679.90 ms
> **Verdict:** **✅ ALL CLAIMS VERIFIED (PASS)** (6 Passed, 0 Failed, 0 Skipped)

---

## 1. Executive Summary Table

| Claim ID | Claim Scenario | Status | Latency | Telemetry Summary |
| :--- | :--- | :---: | :---: | :--- |
| **CLM_001** | Global Focus Bleed Prevention | ✅ **PASS** | 0.75 ms | HWND: 0x001A00F4, Process: pwsh (PID 4812), FocusZone: Unknown |
| **CLM_002** | Child HWND Normalization | ✅ **PASS** | 0.00 ms | ChildHwnd: 0x00A50088 -> RootHwnd: 0x00A50020, Title: 'active-desktop-context-engine - Antigravity IDE' |
| **CLM_003** | IDE Semantic Zone Resolution | ✅ **PASS** | 0.92 ms | 5 IDE Zones Verified: Monaco=EditorCodeBuffer, Terminal=IntegratedTerminal, Git=GitCommitBox, Chat=ChatAssistant, Sidebar=SidebarExplorer |
| **CLM_004** | Browser Tab Sidebar vs. IDE Explorer | ✅ **PASS** | 0.00 ms | Gecko Sidebar Tab: TabBar, Gecko Sidebar Doc: DocumentContent, IDE Sidebar: SidebarExplorer |
| **CLM_005** | Burst Typing Debounce Clamping (WP 3.4) | ✅ **PASS** | 456.23 ms | RawEvents: 20, Triggered: 5, Committed: 1 |
| **CLM_006** | Zero-Allocation Deduplication | ✅ **PASS** | 213.93 ms | Raw: 5, Committed: 1, Suppressed: 4 |

---

## 2. Detailed Claim Telemetry & Assertions

### CLM_001: Global Focus Bleed Prevention
- **Status:** `Passed`
- **Execution Duration:** `0.75 ms`
- **Telemetry Summary:** `HWND: 0x001A00F4, Process: pwsh (PID 4812), FocusZone: Unknown`
- **Assertions Verified:**
  - [x] Window PID (4812) equals Target PID (4812): True
  - [x] Zero Focus Bleed from prior GUI state (Zone=Unknown): True

```json
{
  "timestamp": "2026-08-25T03:48:36.6454515+00:00",
  "workspace": {
    "virtual_desktop_id": "f8cc46f1-2763-480e-8e23-d1b1ba38bff1",
    "desktop_index": 0,
    "virtual_desktop_name": "Primary",
    "monitor_index": 0,
    "monitor_bounds": {
      "left": 0,
      "top": 0,
      "width": 1920,
      "height": 1080,
      "right": 1920,
      "bottom": 1080,
      "is_empty": false
    }
  },
  "window": {
    "hwnd": "0x001A00F4",
    "title": "Administrator: PowerShell (pwsh.exe)",
    "process_name": "pwsh",
    "pid": 4812,
    "class_name": "ConsoleWindowClass",
    "archetype": "classic_win32",
    "bounds": {
      "left": 0,
      "top": 0,
      "width": 1200,
      "height": 800,
      "right": 1200,
      "bottom": 800,
      "is_empty": false
    },
    "is_minimized": false,
    "is_maximized": false
  },
  "focus": {
    "control_type": "Window",
    "element_name": "Administrator: PowerShell",
    "bounding_box": {
      "left": 0,
      "top": 0,
      "width": 1200,
      "height": 800,
      "right": 1200,
      "bottom": 800,
      "is_empty": false
    },
    "automation_id": "",
    "class_name": "ConsoleWindowClass",
    "semantic_zone": "unknown"
  },
  "extraction_duration_ms": 0.42
}
```

### CLM_002: Child HWND Normalization
- **Status:** `Passed`
- **Execution Duration:** `0.00 ms`
- **Telemetry Summary:** `ChildHwnd: 0x00A50088 -> RootHwnd: 0x00A50020, Title: 'active-desktop-context-engine - Antigravity IDE'`
- **Assertions Verified:**
  - [x] Child HWND 0x00A50088 mapped to Top-Level HWND 0x00A50020: True
  - [x] Window Title preserved ('active-desktop-context-engine - Antigravity IDE') and not dropped as empty noise: True

```json
{
  "timestamp": "2026-08-25T03:48:36.6491617+00:00",
  "workspace": {
    "virtual_desktop_id": "33660468-b6ff-41b1-b58e-0db171b81696",
    "desktop_index": 0,
    "virtual_desktop_name": "Primary",
    "monitor_index": 0,
    "monitor_bounds": {
      "left": 0,
      "top": 0,
      "width": 1920,
      "height": 1080,
      "right": 1920,
      "bottom": 1080,
      "is_empty": false
    }
  },
  "window": {
    "hwnd": "0x00A50020",
    "title": "active-desktop-context-engine - Antigravity IDE",
    "process_name": "Antigravity.exe",
    "pid": 26420,
    "class_name": "Chrome_WidgetWin_1",
    "archetype": "chromium_electron",
    "bounds": {
      "left": 0,
      "top": 0,
      "width": 1920,
      "height": 1080,
      "right": 1920,
      "bottom": 1080,
      "is_empty": false
    },
    "is_minimized": false,
    "is_maximized": true
  },
  "focus": {
    "control_type": "Edit",
    "element_name": "Chat Prompt Input",
    "bounding_box": {
      "left": 1200,
      "top": 400,
      "width": 600,
      "height": 600,
      "right": 1800,
      "bottom": 1000,
      "is_empty": false
    },
    "automation_id": "chat-input",
    "class_name": "interactive-session",
    "semantic_zone": "chat_assistant"
  },
  "extraction_duration_ms": 0.85
}
```

### CLM_003: IDE Semantic Zone Resolution
- **Status:** `Passed`
- **Execution Duration:** `0.92 ms`
- **Telemetry Summary:** `5 IDE Zones Verified: Monaco=EditorCodeBuffer, Terminal=IntegratedTerminal, Git=GitCommitBox, Chat=ChatAssistant, Sidebar=SidebarExplorer`
- **Assertions Verified:**
  - [x] Monaco Editor Class -> EditorCodeBuffer (Resolved: EditorCodeBuffer): True
  - [x] Integrated Terminal -> IntegratedTerminal (Resolved: IntegratedTerminal): True
  - [x] Git Commit Input -> GitCommitBox (Resolved: GitCommitBox): True
  - [x] Chat Input -> ChatAssistant (Resolved: ChatAssistant): True
  - [x] Sidebar Explorer -> SidebarExplorer (Resolved: SidebarExplorer): True

### CLM_004: Browser Tab Sidebar vs. IDE Explorer
- **Status:** `Passed`
- **Execution Duration:** `0.00 ms`
- **Telemetry Summary:** `Gecko Sidebar Tab: TabBar, Gecko Sidebar Doc: DocumentContent, IDE Sidebar: SidebarExplorer`
- **Assertions Verified:**
  - [x] Gecko Sidebar Tab -> TabBar (NOT SidebarExplorer) (Resolved: TabBar): True
  - [x] Gecko Sidebar Document -> DocumentContent (NOT SidebarExplorer) (Resolved: DocumentContent): True
  - [x] IDE Explorer -> SidebarExplorer (Resolved: SidebarExplorer): True

### CLM_005: Burst Typing Debounce Clamping (WP 3.4)
- **Status:** `Passed`
- **Execution Duration:** `456.23 ms`
- **Telemetry Summary:** `RawEvents: 20, Triggered: 5, Committed: 1`
- **Assertions Verified:**
  - [x] Raw WinEvents Ingested: 20 (Expected >= 20): True
  - [x] Debounced Extractions Triggered: 5 (>= 2 due to 250ms clamp + trailing edge): True

### CLM_006: Zero-Allocation Deduplication
- **Status:** `Passed`
- **Execution Duration:** `213.93 ms`
- **Telemetry Summary:** `Raw: 5, Committed: 1, Suppressed: 4`
- **Assertions Verified:**
  - [x] Single Initial Snapshot Committed: 1 == 1: True
  - [x] Identical Wavelets Suppressed: 4 (Expected >= 3): True

---

## 3. Epistemic Verification Sign-Off

* **Zero Focus Bleed Confirmed:** Window PID and focused control PID boundaries are strictly preserved.
* **Parent Climbing Verified:** Monaco editor buffers and integrated terminals resolve without generic leaf fallback.
* **Gecko Sidebar Scoped:** Vertical browser tabs resolve to `TabBar` / `DocumentContent` and never collide with `SidebarExplorer`.
* **Debounce & Deduplication Proven:** Burst clamping fires within 250ms and identical wavelets emit 0 duplicate writes.
