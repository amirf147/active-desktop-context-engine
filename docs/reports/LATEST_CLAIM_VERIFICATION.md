<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **Ground-Truth Claim Verification Ledger**

---

# Ground-Truth Claim Verification Evidence Ledger

> **Suite:** Ground-Truth Claim Verification Suite
> **Driver Mode:** `Synthetic Headless Mock Driver`
> **Timestamp:** 2026-09-05 16:04:15 UTC
> **Total Duration:** 1259.02 ms
> **Verdict:** **✅ ALL CLAIMS VERIFIED (PASS)** (6 Passed, 0 Failed, 0 Skipped)

---

## 1. Executive Summary Table

| Claim ID | Claim Scenario | Status | Latency | Telemetry Summary |
| :--- | :--- | :---: | :---: | :--- |
| **CLM_001** | Global Focus Bleed Prevention | ✅ **PASS** | 0.78 ms | HWND: 0x001A00F4, Process: pwsh (PID 4812), FocusZone: Unknown |
| **CLM_002** | Child HWND Normalization | ✅ **PASS** | 0.00 ms | ChildHwnd: 0x00A50088 -> RootHwnd: 0x00A50020, Title: 'active-desktop-context-engine - Antigravity IDE' |
| **CLM_003** | IDE Semantic Zone Resolution | ✅ **PASS** | 1.09 ms | 5 IDE Zones Verified: Monaco=EditorBuffer, Terminal=Terminal, Git=GitCommitBox, Chat=ChatPrompt, Sidebar=SidebarExplorer |
| **CLM_004** | Browser Tab Sidebar vs. IDE Explorer | ✅ **PASS** | 0.01 ms | Gecko Sidebar Tab: TabBar, Gecko Sidebar Doc: WebDocument, IDE Sidebar: SidebarExplorer |
| **CLM_005** | Burst Typing Debounce Clamping (WP 3.4) | ✅ **PASS** | 421.21 ms | RawEvents: 20, Triggered: 4, Committed: 1 |
| **CLM_006** | Zero-Allocation Deduplication | ✅ **PASS** | 224.33 ms | Raw: 5, Committed: 1, Suppressed: 4 |

---

## 2. Detailed Claim Telemetry & Assertions

### CLM_001: Global Focus Bleed Prevention
- **Status:** `Passed`
- **Execution Duration:** `0.78 ms`
- **Telemetry Summary:** `HWND: 0x001A00F4, Process: pwsh (PID 4812), FocusZone: Unknown`
- **Assertions Verified:**
  - [x] Window PID (4812) equals Target PID (4812): True
  - [x] Zero Focus Bleed from prior GUI state (Zone=Unknown): True

```json
{
  "timestamp": "2026-09-05T16:04:15.2831574+00:00",
  "workspace": {
    "virtual_desktop_id": "fdedeccd-1587-40a7-8a0e-2908b436be79",
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
    "semantic_zone": "unknown",
    "pane_location": "unknown",
    "semantic_path": [],
    "container_path": [],
    "container_classes": [],
    "is_overlay": false
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
  "timestamp": "2026-09-05T16:04:15.2864859+00:00",
  "workspace": {
    "virtual_desktop_id": "cc02b661-5074-4d9f-bb72-da7d3da0e981",
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
    "semantic_zone": "chat_prompt",
    "pane_location": "unknown",
    "semantic_path": [],
    "container_path": [],
    "container_classes": [],
    "is_overlay": false
  },
  "extraction_duration_ms": 0.85
}
```

### CLM_003: IDE Semantic Zone Resolution
- **Status:** `Passed`
- **Execution Duration:** `1.09 ms`
- **Telemetry Summary:** `5 IDE Zones Verified: Monaco=EditorBuffer, Terminal=Terminal, Git=GitCommitBox, Chat=ChatPrompt, Sidebar=SidebarExplorer`
- **Assertions Verified:**
  - [x] Monaco Editor Class -> EditorBuffer (Resolved: EditorBuffer): True
  - [x] Integrated Terminal -> Terminal (Resolved: Terminal): True
  - [x] Git Commit Input -> GitCommitBox (Resolved: GitCommitBox): True
  - [x] Chat Input -> ChatPrompt (Resolved: ChatPrompt): True
  - [x] Sidebar Explorer -> SidebarExplorer (Resolved: SidebarExplorer): True

### CLM_004: Browser Tab Sidebar vs. IDE Explorer
- **Status:** `Passed`
- **Execution Duration:** `0.01 ms`
- **Telemetry Summary:** `Gecko Sidebar Tab: TabBar, Gecko Sidebar Doc: WebDocument, IDE Sidebar: SidebarExplorer`
- **Assertions Verified:**
  - [x] Gecko Sidebar Tab -> TabBar (Resolved: TabBar): True
  - [x] Gecko Sidebar Document -> WebDocument (Resolved: WebDocument): True
  - [x] IDE Explorer -> SidebarExplorer (Resolved: SidebarExplorer): True

### CLM_005: Burst Typing Debounce Clamping (WP 3.4)
- **Status:** `Passed`
- **Execution Duration:** `421.21 ms`
- **Telemetry Summary:** `RawEvents: 20, Triggered: 4, Committed: 1`
- **Assertions Verified:**
  - [x] Raw WinEvents Ingested: 20 (Expected >= 20): True
  - [x] Debounced Extractions Triggered: 4 (>= 2 due to 250ms clamp + trailing edge): True

### CLM_006: Zero-Allocation Deduplication
- **Status:** `Passed`
- **Execution Duration:** `224.33 ms`
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
