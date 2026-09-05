<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **ADCE Focus & Zone Detection Explained**

---

# ADCE Focus & Semantic Zone Detection: Plain-English Guide

> **Document Status:** Active Educational Guide
> **Epistemic Authority:** Tier 4 (Pedagogical Overview — Subordinate to Tier 1 Code & Tier 2 Specs)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).
> **Purpose:** A visual, step-by-step explanation of how Windows focus works, what was happening in the baseline, and how ADCE detects whether you are in a Code Editor, Terminal, Search Box, or Web Document.

---

## 1. What You Saw in the Baseline (The "Text Area" Mystery)

Look at the 12 windows captured in your baseline `--grab all` test:
* Window 1 (Antigravity): `Focus Target : [Unknown] 'Text Area' (Document)`
* Window 2 (Waterfox): `Focus Target : [Unknown] 'Text Area' (Document)`
* Window 3 (File Explorer): `Focus Target : [Unknown] 'Text Area' (Document)`
* Window 4 (Caster IDE): `Focus Target : [Unknown] 'Text Area' (Document)`
* ... all 12 windows had the exact same `'Text Area'` target with `[Unknown]` zone!

### Why Did This Happen?
1. **Windows UIA has a Single Global Focus Pointer:** Windows remembers the last UI control that had keyboard input across the entire computer.
2. **Generic Leaf Controls:** When you click inside modern apps (like VS Code, Antigravity, or Chrome), the raw control at your mouse cursor is just a blank canvas called `'Text Area'` or `'Document'`. The app does not label the tiny leaf control with its real identity.
3. **The Baseline Had No Ancestor Climbing:** The baseline code looked only at the raw leaf control. Because the leaf control said `'Text Area'`, the baseline stamped `[Unknown] 'Text Area'` onto every window on your desktop.

---

## 2. How ADCE Solves This (The 3 Building Blocks)

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                          HOW ADCE IDENTIFIES YOUR EXACT ZONE                           │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│   Your Mouse / Keyboard Focus                                                          │
│              │                                                                         │
│              ▼                                                                         │
│   [Raw Leaf Control: 'Text Area' (Edit / Document)] ──> Default: [Unknown]             │
│              │                                                                         │
│              ▼ (Climb 1-2 Parent Steps in UIA Tree)                                    │
│   ┌────────────────────────────────────────────────────────┐                           │
│   │ Is Parent class "monaco-editor" or "editor-container"? │ ──> [EditorCodeBuffer]    │
│   │ Is Parent class "terminal-wrapper" or "xterm"?         │ ──> [IntegratedTerminal]  │
│   │ Is Parent ID "workbench.view.explorer" (IDE)?          │ ──> [SidebarExplorer]     │
│   │ Is Parent in Browser Sidebar (Tree Style Tab)?         │ ──> [TabBar]              │
│   │ Is Parent ID "urlbar-input" or "address-bar"?          │ ──> [AddressBar]          │
│   │ Is Control Type "Document" in a Web Browser?           │ ──> [DocumentContent]     │
│   └────────────────────────────────────────────────────────┘                           │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### The Three Protections:
1. **Parent-Chain Climbing & Archetype Scoping (WP 2.4 / 2.5):** When you click inside Monaco or Terminal, ADCE climbs 1–2 steps up to the container to detect whether you are in code, terminal, or sidebar, while scoping IDE explorers vs browser vertical tab sidebars (like Tree Style Tab).
2. **Process Boundary Guard:** ADCE verifies that the focused element actually belongs to the active window's Process ID. This stops Waterfox's text box from "bleeding" onto PowerShell or Explorer.
3. **Root Window Normalization:** When you click on sub-panels in Antigravity (chat, history, file list), ADCE automatically resolves the top-level window handle so child clicks are never discarded as noise.

---

## 3. How Live Events vs. Instant Snapshots Work

| Tool Mode | What It Does | Best Use Case |
| :--- | :--- | :--- |
| **`--grab <AppName>`** | Takes an instant, full-detail picture of an application (tabs, active file, breadcrumbs, semantic zone, and MCP JSON). | When you want to inspect what ADCE sees for a specific app without worrying about timing. |
| **`--grab-delay 3`** | Counts down 3 seconds, then grabs whatever window you clicked on. | When you want to click inside an app and see its full snapshot. |
| **`--events -d 20`** | Runs a live event recorder for 20 seconds, logging every window focus switch and debounce event as you use your PC. | When you want to verify that background switching and debounce noise reduction are working smoothly. |

---

## 4. Intra-App Clicks vs. Application Switching (The Physics of Electron)

Why does clicking between applications (e.g. Waterfox to Antigravity) trigger immediately, while clicking between sub-panels inside Antigravity (Chat -> Source Control -> Terminal) feels different?

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                      TWO DIFFERENT OS EVENT MECHANISMS AT PLAY                         │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. Switching Applications (Windows OS Kernel Level):                                   │
│    [Waterfox] ────────── Click ──────────> [Antigravity IDE]                           │
│    -> Windows DWM immediately fires EVENT_SYSTEM_FOREGROUND (0x0003).                   │
│    -> 100% reliable, zero delay, triggers instant window snapshot capture.            │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 2. Intra-App Panel Clicks (Internal Chromium DOM / AXTree Level):                      │
│    [Chat Input] ────────── Click ──────────> [Git Commit Box]                          │
│    -> Both controls live inside the SAME single Win32 window (Chrome_WidgetWin_1).     │
│    -> Electron does NOT fire a foreground event; it fires EVENT_OBJECT_FOCUS (0x8005). │
│    -> Chromium accessibility thread (AXTree) marshals the click asynchronously.       │
│    -> If clicked within 50ms, the ADCE debouncer coalesces them into 1 clean event.   │
│    -> If both controls share the same semantic zone, deduplication drops the twin.    │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Are We Hardcoding Rules? (Dynamic Discovery Architecture)

A natural question arises: *Are we hardcoding string names like 'Message input' or 'monaco-editor'? Is that fragile?*

ADCE solves this through a **3-Tier Dynamic Discovery Model**:

### Tier 1: Universal UIA Control Types (Zero Hardcoding)
* Any control of type `TabItem` or `Tab` $\rightarrow$ dynamically resolved to **`[TabBar]`**.
* Any control of type `Document` in a browser $\rightarrow$ dynamically resolved to **`[DocumentContent]`**.
* Any control of type `ListItem` in Explorer $\rightarrow$ dynamically resolved to **`[ShellItemList]`**.
* These rules do not rely on app names; they work universally across all Windows software.

### Tier 2: Universal Framework Archetypes (Dynamic Archetype Mapping)
Rather than writing rules for 1,000 individual programs, ADCE groups software into **5 Universal Archetypes**:
1. `ChromiumElectron` (VS Code, Antigravity, Cursor, Slack, Discord)
2. `Gecko` (Waterfox, Firefox, Zen, Floorp)
3. `WinUI3Xaml` (Windows 11 File Explorer, Windows Terminal, Settings)
4. `ClassicWin32` (Notepad, PowerShell console, legacy tools)
5. `WpfModern` (Visual Studio, Blend, Enterprise tools)

Because Antigravity, VS Code, and Cursor all share the **Monaco / xterm container architecture**, 1 archetype pattern covers hundreds of developer tools.

### Tier 3: Extensible Semantic Fallback & Future AI Self-Labeling
* When an unmapped control is encountered, ADCE captures its **Bounding Box, Control Type, Role, and Value Snippet** and tags it `[Unknown]`.
* In future milestones (Milestones 5–6 / MCP integration), local AI models or heuristic layout classifiers can dynamically inspect the surrounding context and **self-label** unrecognized zones in real-time without needing manual rules.

---

## 6. Summary & Lessons Learned

1. **Window-Level Switching is Instant:** Windows OS kernel guarantees foreground switches.
2. **Sub-Panel Switching is Debounced:** Intra-app clicks inside Electron are coalesced to prevent high-frequency UI jitter.
3. **The Engine is Dynamic:** Universal Control Types + Framework Archetypes handle apps without rigid per-app hardcoding.
4. **All Empirical Tests Verified:** Waterfox (`[DocumentContent]`), Monaco Editor (`[EditorCodeBuffer]`), Integrated Terminal (`[IntegratedTerminal]`), Chat Input (`[ChatAssistant]`), and PowerShell are all cleanly isolated with 0 cross-process bleed.
