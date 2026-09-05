<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# Application UI Automation Layout Hierarchies & Semantic Profiles

This directory contains modular, per-application empirical research profiles documenting the exact physical UI Automation (UIA) structures, container ancestries, layout bounds, and semantic zone mappings across major desktop applications.

---

## Purpose & Architectural Mandate

In desktop perception systems, mapping an active focus element to an interaction domain cannot rely on flat heuristics or naive spatial assumptions (e.g., assuming anything on the left side of the window is a sidebar, or anything at the bottom is a terminal panel).

Instead, every application architecture partitions its interface into two fundamentally distinct tiers:

1. **Application Window Chrome (The Host Shell):**
   - Structural containers, title bars, tab strips, navigation toolbars, sidebars, activity launchers, docked tool panels, and modal dialogs.
   - Elements here reflect window-level operations (opening tabs, switching views, adjusting settings, navigating URLs).
2. **Client Document Viewports (Rendered Workspaces):**
   - The primary work surface hosting application-specific content (code buffers, rendered HTML/DOM viewports, shell consoles, image canvases).
   - Once focus enters a client document viewport, **application window chrome rules (e.g. `PrimarySidebar`, `BottomPanel`) cease to apply**. Web page elements (like a left-hand navigation list or bottom comment form on a website) must not be miscategorized as desktop window chrome.

---

## Application Profile Index

| # | Application | Engine / Archetype | Window Class | Document Link | Status |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **01** | **Waterfox / Firefox** | Gecko | `MozillaWindowClass` | [`01_waterfox.md`](./01_waterfox.md) | ✅ Verified Ground Truth |
| **02** | **Antigravity IDE / VS Code** | Chromium / Electron | `Chrome_WidgetWin_1` | [`02_antigravity_ide.md`](./02_antigravity_ide.md) | ✅ Verified Ground Truth |
| **03** | **Windows Terminal** | WinUI 3 / XAML | `CASCADIA_HOSTING_WINDOW_CLASS` | [`03_windows_terminal.md`](./03_windows_terminal.md) | ⚡ Resting-State Baseline (3/7 Surfaces Verified; Modals Deferred) |
| **04** | **Windows Settings** | WinUI 3 / CoreWindow | `ApplicationFrameWindow` | `04_windows_settings.md` | 📋 Scheduled (VM Harness) |
| **05** | **File Explorer** | WinUI 3 Shell | `CabinetWClass` | `05_file_explorer.md` | 📋 Scheduled (VM Harness) |
| **06** | **Notepad** | Modern WinUI / Win32 | `Notepad` | `06_notepad.md` | 📋 Scheduled (VM Harness) |
| **07** | **Antigravity Standalone** | Chromium / Electron | `Chrome_WidgetWin_1` | `07_antigravity_standalone.md` | 📋 Scheduled (VM Harness) |

---

## Research Methodology & Isolated VM Profiling Strategy

### The Limits of Host-Level Synthetic Drivers
During initial research spikes, automated stimulus drivers attempted to inject synthetic keystrokes and mouse clicks directly on the developer's live workstation to trigger flyouts, command palettes, and configuration workspaces. This approach encountered key operating system barriers:
- **Session Security & Privilege Barriers:** Background developer processes cannot reliably inject hardware events into interactive desktop applications across Windows session boundaries and integrity levels (UIPI).
- **Focus Contention:** Automated drivers actively hijack the developer's foreground focus, mouse cursor, and active window state.

### Next Steps: Shifting Deep Modal Exploration to an Isolated VM
To maintain strict epistemic integrity without fighting the host operating system:
1. **Host Workstation:** Restricted to passive resting-state observation and non-intrusive UIA tree sampling.
2. **Dedicated Profiling VM / Windows Sandbox:** Deep interactive exploration (triggering complex flyouts, modal overlays, elevated settings workspaces, and multi-window state transitions) will be conducted in an isolated virtual machine or sandbox environment.
3. **Current State:** Live host-level driver spikes are paused while the VM-based automated profiling harness and existing tooling options are evaluated.

---

## Standard Profile Specification Structure

Every profile document in this folder follows an identical 6-part epistemic verification structure:

1. **Physical Window & Process Specification:** PID, HWND, Win32 window classes, multi-process topology, DPI/bounds.
2. **Structural Container Anatomy:** Complete dissection of major window containers (`#navigator-toolbox`, `#sidebar-box`, `#devtools-toolbox`, etc.).
3. **Viewport Boundary & In-Content Semantics:** Strict boundary conditions isolating application chrome from inner canvas/document elements.
4. **Live Empirical Telemetry & Ancestor Chains:** Verifiable tables recording `ControlType`, `AutomationId`, `ClassName`, `Name`, `Bounds`, and full ancestor parent hierarchies captured from live running processes.
5. **Taxonomy & Semantic Path Mappings:** Formal projection from raw UIA properties to `(WindowPaneLocation, ActiveView, SectionName, DesktopSemanticZone)`.
6. **Visual Evidence Catalog:** High-resolution screenshots with highlighted bounding boxes confirming the physical location of each control stop.
