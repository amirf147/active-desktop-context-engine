<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# ADCE Core Domain Model Specification

> **Document Status:** Active / Normative Core Architecture Reference
> **Epistemic Authority:** Tier 1 (Normative Production Contract)
> **Implementation Target:** `src/ADCE.Core/` (.NET 10 / C# 14)
> **Test Baseline:** 281 Passing Unit Tests (98 Core, 119 Extraction, 14 Storage, 22 Mcp, 28 Daemon)

---

## 1. Primary Invariants & Responsibilities

`ADCE.Core` defines the domain types and immutable contracts for the Active Desktop Context Engine. It has zero external dependencies outside the .NET 10 Base Class Library and Windows SDK targeting.

The core responsibilities are:
1. Provide immutable snapshot models (`DesktopContextSnapshot`, `FocusedControlInfo`, `WindowEnvelope`, `WorkspaceEnvelope`) representing point-in-time desktop state.
2. Define canonical enumerations for semantic classification (`DesktopSemanticZone`, `DesktopAppArchetype`, `WindowPaneLocation`).
3. Guarantee deterministic JSON-RPC 2.0 serialization for MCP clients, SQLite time-series storage, and SSE streaming consumers.
4. Provide structured domain event records (`DesktopEvent`, `DesktopEventToken`) for pipeline dispatch.

---

## 2. Desktop Context Hierarchy

Desktop context is structured in a four-tier aggregate hierarchy: Workspace Envelope, Window Envelope, Macro Pane Location, and Semantic Focus.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                          DesktopContextSnapshot (Root Aggregate)                       │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ DateTimeOffset Timestamp: ISO-8601 UTC capture timestamp                               │
│ WorkspaceEnvelope Workspace:                                                           │
│   - VirtualDesktopId (Guid): Default Guid.Empty (Virtual Desktop GUID extraction scheduled for Phase 8 / Slion-VirtualDesktop integration due to Windows 11 vtable & window pinning constraints) │
│   - DesktopIndex (int): 0-based virtual desktop workspace index                        │
│   - VirtualDesktopName (string): User-assigned or system desktop name                  │
│   - MonitorIndex (int): Display monitor index hosting window center                    │
│   - MonitorBounds (BoundingRectangle): Screen bounds for the target monitor            │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ WindowEnvelope Window:                                                                 │
│   - Hwnd (nint): Top-level window handle normalized from child HWND via Win32          │
│   - Title (string): Window title text                                                  │
│   - ProcessName (string): Executable process name (e.g. "Antigravity.exe")             │
│   - Pid (int): Operating system Process ID (PID)                                       │
│   - ClassName (string): Win32 window class (e.g. "Chrome_WidgetWin_1")                 │
│   - Archetype (DesktopAppArchetype): Classified architectural UI framework             │
│   - Bounds (BoundingRectangle): Top-level window coordinates (Left, Top, Width, Height)│
│   - IsMinimized (bool): True if window is minimized (iconic)                           │
│   - IsMaximized (bool): True if window is maximized (zoomed)                           │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ FocusedControlInfo Focus:                                                              │
│   - ControlType (string): UIA control type name (e.g. "Edit", "Button", "Document")    │
│   - ElementName (string): Accessible element name                                      │
│   - BoundingBox (BoundingRectangle): Screen bounding coordinates                       │
│   - AutomationId (string): UIA AutomationId if assigned                                │
│   - ClassName (string): Win32 or UIA class name of the control                         │
│   - SemanticZone (DesktopSemanticZone): Resolved fine-grained typing anchor (1 of 19)  │
│   - PaneLocation (WindowPaneLocation): Resolved macro application pane (1 of 9)        │
│   - ActiveView (string?): Active view container (e.g. "Explorer", "SourceControl")     │
│   - SectionName (string?): Inner accordion section (e.g. "Timeline", "Outline")        │
│   - SemanticPath (ImmutableArray<string>): Hierarchical path [Pane, View, Section]     │
│   - ContainerPath (ImmutableArray<string>): Ancestor automation IDs                    │
│   - ContainerClasses (ImmutableArray<string>): Ancestor class names                    │
│   - IsOverlay (bool): True if element is inside a modal dialog or floating popup       │
│   - ValueSnippet (string?): Sanitized text snippet from ValuePattern / TextPattern     │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ Specialized Application Contexts (Optional Nullable Records):                          │
│   - IdeContext? IdeContext: ActiveFilePath, ActiveSidebarView, OpenEditorTabs, GitBranch (optional / reserved for status bar integration) │
│   - BrowserContext? BrowserContext: ContainerType, TotalTabCount, ActiveTab, OpenTabs  │
│   - ExplorerContext? ExplorerContext: CurrentPath, SelectedItems, OpenFolderTabs       │
│   - TerminalContext? TerminalContext: ShellTitle, BufferSnippet, ActiveTab             │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ double ExtractionDurationMs: Pipeline extraction latency in milliseconds               │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Canonical Enumerations

### 3.1 DesktopSemanticZone (19 Values)

`DesktopSemanticZone` categorizes the fine-grained operational role of the focused control.

| Value | Name | Description | Projected Macro Anchor (`ToMacroZone()`) |
| :--- | :--- | :--- | :--- |
| `0` | `Unknown` | Unrecognized or unmapped semantic zone. | `Unknown` |
| `1` | `EditorBuffer` | Text editor buffer (Monaco, Notepad, source code buffer). | `EditorBuffer` |
| `2` | `Terminal` | Command shell or console buffer (pwsh, cmd, Cascadia xterm). | `Terminal` |
| `3` | `GitCommitBox` | Source control commit message input. | `EditorBuffer` |
| `4` | `SidebarExplorer` | Navigation file tree or project directory explorer. | `NavigationPanel` |
| `5` | `AddressBar` | Browser URL address bar or navigation input. | `QuickOpen` |
| `6` | `WebDocument` | Rendered web page viewport or HTML document body. | `WebDocument` |
| `7` | `ShellItemList` | Windows Explorer folder item list view. | `NavigationPanel` |
| `8` | `TabBar` | Container hosting editor or browser document tabs. | `NavigationPanel` |
| `9` | `StatusBar` | Application footer displaying line, encoding, or branch info. | `NavigationPanel` |
| `10` | `CommandPalette` | Quick command input overlay (Ctrl+Shift+P). | `QuickOpen` |
| `11` | `ChatPrompt` | AI chat assistant input prompt box. | `ChatPrompt` |
| `12` | `QuickOpen` | File switcher or modal jump overlay (Ctrl+P). | `QuickOpen` |
| `13` | `SystemDialog` | Modal message box, alert, or file picker. | `SystemDialog` |
| `14` | `NavigationPanel` | High-level tool rail or browser navigation cluster. | `NavigationPanel` |
| `15` | `ActivityBar` | Primary icon dock strip in Electron IDEs. | `NavigationPanel` |
| `16` | `Timeline` | File history or version timeline tree. | `NavigationPanel` |
| `17` | `Outline` | Document symbol tree or code structure outline. | `NavigationPanel` |
| `18` | `ChatConversation` | Rendered conversational stream or response message history. | `ChatPrompt` |

### 3.2 DesktopAppArchetype (6 Values)

`DesktopAppArchetype` governs extraction strategy, UIA caching behavior, and selector precedence.

| Value | Name | Technical Definition & Examples |
| :--- | :--- | :--- |
| `0` | `Unknown` | Unclassified application window. |
| `1` | `ChromiumElectron` | Chromium and Electron applications (VS Code, Antigravity IDE, Slack, Discord, Chrome, Edge). |
| `2` | `Gecko` | Mozilla Gecko applications (Waterfox, Firefox, Thunderbird). |
| `3` | `WinUI3Xaml` | Modern Windows XAML and WinUI 3 applications (Windows 11 File Explorer, Windows Terminal). |
| `4` | `ClassicWin32` | Standard Win32 Common Controls (Notepad, 7-Zip, standard dialogs, `ConsoleWindowClass`). |
| `5` | `CanvasToolkit` | Non-native rendered GUI toolkits (JetBrains Swing `SunAwt`, Qt, Flutter, WPF `HwndWrapper`). |

### 3.3 WindowPaneLocation (9 Values)

`WindowPaneLocation` resolves macro application layout regions, anchoring controls to structural window containers.

| Value | Name | Structural Region & Layout Role |
| :--- | :--- | :--- |
| `0` | `Unknown` | Unrecognized or unmapped pane location. |
| `1` | `ActivityBar` | Primary icon strip or navigation rail (Width <= 42px). |
| `2` | `PrimarySidebar` | Primary sidebar container hosting views (Explorer, Source Control, Extensions). |
| `3` | `MainContent` | Central document editor group, diff view, or primary document viewport. |
| `4` | `AuxiliarySidebar` | Secondary sidebar hosting AI chat assistant or side-by-side documentation. |
| `5` | `BottomPanel` | Bottom drawer hosting integrated terminals, output streams, problems, and consoles. |
| `6` | `TopBar` | Top window title bar, menu bar, or global search input. |
| `7` | `StatusBar` | Bottom status strip indicating branch, encoding, and background statuses. |
| `8` | `OverlayModal` | Floating modal overlay, quick open switcher, command palette, or dialog. |

---

## 4. Serialization Contracts

All domain models serialize via `System.Text.Json` using shared singleton options defined in `AdceJsonSerializerOptions.Default`:

* **Naming Policy:** `JsonNamingPolicy.SnakeCaseLower` applies to all properties and dictionary keys (e.g. `process_name`, `bounding_box`, `semantic_zone`, `extraction_duration_ms`).
* **Enumeration Formatting:** Enumerations serialize as lowercase snake_case strings via `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)` (e.g. `"editor_buffer"`, `"chromium_electron"`, `"primary_sidebar"`).
* **Native Handles:** Win32 `nint` window handles serialize to formatted hexadecimal strings (e.g. `"0x00DB083E"`) via `HwndJsonConverter`.
* **Coordinate Rectangles:** `BoundingRectangle` serializes as `{ "left": int, "top": int, "width": int, "height": int }`.
* **Null Handling:** Null optional contexts (`ide_context`, `browser_context`, `explorer_context`, `terminal_context`) are omitted from output via `JsonIgnoreCondition.WhenWritingNull`.
* **Escaping:** Relaxed JSON escaping (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`) preserves readable characters in file paths and window titles without HTML entity encoding.
