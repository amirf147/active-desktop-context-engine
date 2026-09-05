<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# ADCE Core Domain Model Specification

> **Document Status:** Active / Normative Core Architecture Reference
> **Epistemic Authority:** Tier 1 (Normative Production Contract)
> **Implementation Target:** `src/ADCE.Core/` (.NET 10 / C# 14)
> **Test Baseline:** 98/98 Passing Unit Tests in `tests/ADCE.Core.Tests/`

---

## 1. Primary Invariants & Responsibilities

`ADCE.Core` defines the domain types and immutable contracts for the Active Desktop Context Engine. It has zero external dependencies outside the .NET 10 Base Class Library and Windows SDK targeting.

The core responsibilities are:
1. Provide immutable snapshot models (`DesktopContextSnapshot`, `FocusedControlInfo`, `WindowEnvelope`) representing point-in-time desktop state.
2. Define canonical enumerations for semantic classification (`DesktopSemanticZone`, `DesktopAppArchetype`, `WindowPaneLocation`).
3. Guarantee deterministic JSON-RPC 2.0 serialization for MCP clients and storage engines.
4. Provide structured domain event records for pipeline dispatch.

---

## 2. Desktop Context Hierarchy

Desktop context is structured in a three-tier hierarchy: Window Envelope, Pane Location, and Semantic Focus.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   DesktopContextSnapshot (Root Record)                 │
├────────────────────────────────────────────────────────────────────────┤
│ WindowEnvelope Window:                                                 │
│   - Hwnd (nint): Top-level window handle normalized from child HWND    │
│   - ProcessId (int): Process ID owning the top-level window            │
│   - ProcessName (string): Process image name (e.g. "antigravity.exe")   │
│   - Title (string): Window title text                                  │
│   - BoundingBox (Rect): Top-level screen coordinates                   │
│   - Archetype (DesktopAppArchetype): App technology classification     │
├────────────────────────────────────────────────────────────────────────┤
│ FocusedControlInfo Focus:                                              │
│   - ControlType (string): UIA control type name                        │
│   - ElementName (string): Accessible element name                      │
│   - AutomationId (string): Developer-assigned automation ID            │
│   - ClassName (string): Window or control class name                   │
│   - SemanticZone (DesktopSemanticZone): Resolved semantic typing anchor│
│   - PaneLocation (WindowPaneLocation): Spatial position in window      │
│   - HierarchyPath (IReadOnlyList<string>): Breadcrumb ancestor chain   │
│   - BoundingBox (Rect): Element screen bounds                          │
│   - IsKeyboardFocusable (bool): Focus acceptance flag                  │
├────────────────────────────────────────────────────────────────────────┤
│ ExtractedMetadata Metadata:                                            │
│   - FilePath (string?): Active file path extracted from title/buffer   │
│   - WorkspaceRoot (string?): Root workspace folder                     │
│   - GitBranch (string?): Active Git branch                             │
│   - ExtractionDurationMs (double): Pipeline execution latency          │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Canonical Enumerations

### 3.1 DesktopSemanticZone (19 Values)

`DesktopSemanticZone` categorizes the operational role of the focused control.

| Value | Name | Description |
| :--- | :--- | :--- |
| `0` | `Unknown` | Unrecognized or unmapped semantic zone. |
| `1` | `EditorBuffer` | Text editor buffer (Monaco, Notepad, source code editor). |
| `2` | `Terminal` | Command shell or console buffer (pwsh, cmd, Cascadia xterm). |
| `3` | `GitCommitBox` | Source control commit message input. |
| `4` | `SidebarExplorer` | Navigation file tree or project directory explorer. |
| `5` | `AddressBar` | Browser URL address bar or navigation input. |
| `6` | `WebDocument` | Rendered web page viewport or HTML document body. |
| `7` | `ShellItemList` | Windows Explorer folder item list view. |
| `8` | `TabBar` | Container hosting editor or browser document tabs. |
| `9` | `StatusBar` | Application footer displaying line, encoding, or branch info. |
| `10` | `CommandPalette` | Quick command input overlay (Ctrl+Shift+P). |
| `11` | `ChatPrompt` | AI chat assistant input prompt box. |
| `12` | `QuickOpen` | File switcher or modal jump overlay (Ctrl+P). |
| `13` | `SystemDialog` | Modal message box, alert, or file picker. |
| `14` | `NavigationPanel` | High-level tool rail or browser navigation cluster. |
| `15` | `ActivityBar` | Primary icon dock strip in Electron IDEs. |
| `16` | `Timeline` | File history or version timeline tree. |
| `17` | `Outline` | Document symbol tree or code structure outline. |
| `18` | `ChatConversation` | Rendered conversational stream or response message history. |

### 3.2 DesktopAppArchetype (6 Values)

`DesktopAppArchetype` governs extraction strategy and selector precedence.

| Value | Name | Heuristic Identification |
| :--- | :--- | :--- |
| `0` | `Unknown` | Unclassified application. |
| `1` | `NativeWin32` | Standard Win32 controls, Win32 console, or legacy forms. |
| `2` | `ChromiumElectron` | Chrome, Edge, VS Code, Antigravity IDE, Slack. |
| `3` | `Gecko` | Firefox, Waterfox, Thunderbird. |
| `4` | `WindowsTerminal` | Cascadia terminal host (`WindowsTerminal.exe`). |
| `5` | `ModernWinUI` | XAML islands, Windows 11 Settings, Calculator. |

### 3.3 WindowPaneLocation (9 Values)

`WindowPaneLocation` resolves coarse spatial quadrant bounding when fine-grained automation properties are missing.

| Value | Name | Quadrant / Position |
| :--- | :--- | :--- |
| `0` | `Unknown` | Spatial bounds unmapped. |
| `1` | `LeftPane` | Normalized X < 0.33 of window width. |
| `2` | `CenterPane` | Normalized X between 0.33 and 0.66 of window width. |
| `3` | `RightPane` | Normalized X > 0.66 of window width. |
| `4` | `TopPane` | Normalized Y < 0.15 of window height. |
| `5` | `BottomPane` | Normalized Y > 0.70 of window height. |
| `6` | `Sidebar` | Collapsible lateral tool container. |
| `7` | `FloatingOverlay` | Popover or non-tiled viewport child. |
| `8` | `DocumentBody` | Main workspace or editor canvas. |

---

## 4. Serialization Contracts

All domain models serialize via `System.Text.Json` using strict camelCase formatting:
* Native pointers (`nint` HWND) serialize to hex strings (e.g. `"0x00010204"`) via `HwndJsonConverter`.
* Enumerations serialize as string identifiers (e.g. `"EditorBuffer"`, `"ChromiumElectron"`) for JSON-RPC interoperability.
* Coordinate rectangles serialize as `{ "x": double, "y": double, "width": double, "height": double }`.
