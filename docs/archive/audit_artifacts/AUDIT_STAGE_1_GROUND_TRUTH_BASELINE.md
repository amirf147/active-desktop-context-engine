<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 📋 Reports ](./LATEST_CLAIM_VERIFICATION.md) › **Stage 1 Ground-Truth Baseline Report**

---

# ADCE Stage 1 Audit: Ground-Truth Truth Extraction (Executable Contracts)

> **Document Status:** Active / Canonical Ground-Truth Baseline
> **Epistemic Authority:** Tier 1 (Derived strictly from compiled, passing C# 14 / .NET 10 code)
> **Audit Date:** September 2026
> **Verification Status:** 263/263 Unit Tests Passing Across Solution

---

## 1. Executive Summary & Epistemic Scope

This audit establishes the empirical ground-truth baseline for the Active Desktop Context Engine (ADCE). Under the **6-Tier Epistemic Ordering of Truth** defined in [`docs/CONTEXT.md`](../CONTEXT.md), **Tier 1 (Empirical Ground Truth & Executable Code)** is the supreme authority. Any narrative in specification documents, architectural blueprints, guides, or milestone postmortems that contradicts this baseline represents documentation drift.

Facts extracted in this document were retrieved directly from `src/ADCE.Core`, `src/ADCE.Extraction`, and `tests/ADCE.Extraction.Tests`.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                          TIER 1 EXECUTABLE BASELINE SUMMARY                            │
├──────────────────────┬──────────────────────────────────────────┬──────────────────────┤
│ Component Subsystem  │ Ground-Truth Artifacts                   │ Test Verification    │
├──────────────────────┼──────────────────────────────────────────┼──────────────────────┤
│ **Core Contracts**   │ 11 C# Record Models, 4 Canonical Enums   │ 98 Tests (Passed)    │
│ **Extraction**       │ 4 Dedicated Extractors, 2 Classifiers    │ 101 Tests (Passed)   │
│ **Storage**          │ L1 Memory Cache + SQLite WAL Store       │ 14 Tests (Passed)    │
│ **MCP Protocol**     │ JSON-RPC 2.0 (Stdio & SSE / HTTP)        │ 22 Tests (Passed)    │
│ **Daemon Host**      │ Win32 Hook Pump + Non-Activating HUD     │ 28 Tests (Passed)    │
├──────────────────────┴──────────────────────────────────────────┼──────────────────────┤
│ **TOTAL ACTIVE PASSING TESTS ACROSS SOLUTION**                  │ **263 Tests**        │
└─────────────────────────────────────────────────────────────────┴──────────────────────┘
```

---

## 2. Canonical Data Contracts (`ADCE.Core.Models`)

All ADCE data models are implemented as immutable C# records (`sealed record` or `readonly record struct`) featuring zero-allocation deep value equality (`IEquatable<T>`), sequence equality over `ImmutableArray<T>`, and custom JSON converters for native types (e.g. `nint` HWNDs).

### 2.1 The Master Snapshot Envelope (`DesktopContextSnapshot`)
The unified payload emitted across the Model Context Protocol (MCP) and persisted in time-series storage:

```csharp
public sealed record DesktopContextSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }
    public required WorkspaceEnvelope Workspace { get; init; }
    public required WindowEnvelope Window { get; init; }
    public required FocusedControlInfo Focus { get; init; }
    public IdeContext? IdeContext { get; init; }
    public BrowserContext? BrowserContext { get; init; }
    public ExplorerContext? ExplorerContext { get; init; }
    public TerminalContext? TerminalContext { get; init; }
    public double ExtractionDurationMs { get; init; }

    public bool HasSameSemanticState(DesktopContextSnapshot? other);
}
```

* **Deduplication:** `HasSameSemanticState()` performs deep semantic comparison across all envelopes while intentionally ignoring `Timestamp` and `ExtractionDurationMs`, enabling zero-allocation debounce suppression of twin wavelets.

### 2.2 Hierarchical Focus Model (`FocusedControlInfo`)
ADCE implements a **3-level hierarchical context model** rather than an antiquated flat zone assignment:

```csharp
public sealed record FocusedControlInfo : IEquatable<FocusedControlInfo>
{
    // Level 1: Win32 & UIA Primitive Attributes
    public required string ControlType { get; init; }
    public required string ElementName { get; init; }
    public required BoundingRectangle BoundingBox { get; init; }
    public string AutomationId { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;

    // Level 2: Macro Structural Panes & Containers
    public WindowPaneLocation PaneLocation { get; init; } = WindowPaneLocation.Unknown;
    public string? ActiveView { get; init; }
    public string? SectionName { get; init; }

    // Level 3: Fine-Grained Typing Anchor & Ancestry
    public DesktopSemanticZone SemanticZone { get; init; } = DesktopSemanticZone.Unknown;
    public ImmutableArray<string> SemanticPath { get; init; } = ImmutableArray<string>.Empty;
    public ImmutableArray<string> ContainerPath { get; init; } = ImmutableArray<string>.Empty;
    public ImmutableArray<string> ContainerClasses { get; init; } = ImmutableArray<string>.Empty;
    public bool IsOverlay { get; init; }
    public string? ValueSnippet { get; init; }
}
```

### 2.3 Window & Workspace Envelopes
* **`WindowEnvelope`:** Captures `Hwnd` (custom hex serializer), `Title`, `ProcessName`, `Pid`, `ClassName`, `Archetype` (`DesktopAppArchetype`), `Bounds` (`BoundingRectangle`), `IsMinimized`, and `IsMaximized`.
* **`WorkspaceEnvelope`:** Captures `VirtualDesktopId` (`Guid`), `DesktopIndex`, `VirtualDesktopName`, `MonitorIndex`, and `MonitorBounds`.

### 2.4 Domain-Specific Subsystem Envelopes
* **`IdeContext`:** `WorkspaceRoot`, `ActiveFilePath`, `ActiveSidebarView`, `IsDiffEditor`, `OpenEditorTabs` (`ImmutableArray<TabItemInfo>`), `EditBuffer`, `GitBranch`, `Breadcrumbs` (`ImmutableArray<string>`).
* **`BrowserContext`:** `ContainerType`, `TotalCount`, `ActiveTab`, `Tabs` (`ImmutableArray<TabItemInfo>`), `UrlAddress`.
* **`ExplorerContext`:** `CurrentPath`, `Breadcrumbs`, `SelectedItems`, `Tabs`.
* **`TerminalContext`:** `ShellTitle`, `ActiveBuffer`, `Tabs`.

---

## 3. Canonical Enumerations & Extensions (`ADCE.Core.Enums`)

### 3.1 `DesktopSemanticZone` (19 Active Values)
The fine-grained typing anchors recognized by the engine:

| Value | Name | Description | Projected Macro Zone (`ToMacroZone()`) |
| :--- | :--- | :--- | :--- |
| `0` | `Unknown` | Unrecognized or unmapped semantic zone | `Unknown` |
| `1` | `EditorBuffer` | Active code/text editor buffer (Monaco, Notepad) | `EditorBuffer` |
| `2` | `Terminal` | Command shell or terminal window | `Terminal` |
| `3` | `GitCommitBox` | Source control or Git commit message input box | `EditorBuffer` |
| `4` | `SidebarExplorer` | Navigation tree, sidebar explorer, file list | `NavigationPanel` |
| `5` | `AddressBar` | Web browser URL address bar or search input | `QuickOpen` |
| `6` | `WebDocument` | Main document or rendered web page viewport | `WebDocument` |
| `7` | `ShellItemList` | File/folder list view (Explorer Items View) | `NavigationPanel` |
| `8` | `TabBar` | Tabstrip container hosting open editor/browser tabs | `NavigationPanel` |
| `9` | `StatusBar` | Application status bar (branch, line info) | `NavigationPanel` |
| `10` | `CommandPalette` | Command palette / quick switcher (`Ctrl+Shift+P`) | `QuickOpen` |
| `11` | `ChatPrompt` | AI chat assistant prompt / input box | `ChatPrompt` |
| `12` | `QuickOpen` | Quick open file switcher or modal search overlay | `QuickOpen` |
| `13` | `SystemDialog` | Modal system dialog, message box, or file picker | `SystemDialog` |
| `14` | `NavigationPanel` | High-level navigation container or tool panel | `NavigationPanel` |
| `15` | `ActivityBar` | Primary activity bar strip or icon launcher buttons | `NavigationPanel` |
| `16` | `Timeline` | File or project version timeline history list | `NavigationPanel` |
| `17` | `Outline` | Document symbol or structure outline tree item | `NavigationPanel` |
| `18` | `ChatConversation`| Rendered chat history / conversation stream | `ChatPrompt` |

### 3.2 `DesktopAppArchetype` (6 Active Values)
Categorizes top-level windows into 5 universal structural archetypes plus unclassified:
1. `Unknown = 0`: Unclassified or generic window.
2. `ChromiumElectron = 1`: VS Code, Antigravity, Slack, Discord, Chrome, Edge.
3. `Gecko = 2`: Waterfox, Firefox, Thunderbird.
4. `WinUI3Xaml = 3`: Windows 11 Explorer (`CabinetWClass`), Windows Terminal (`CASCADIA_HOSTING_WINDOW_CLASS`), Modern Shell.
5. `ClassicWin32 = 4`: Notepad, 7-Zip, standard dialogs (`#32770`), `ConsoleWindowClass`.
6. `CanvasToolkit = 5`: JetBrains (Swing/`SunAwt`), Qt (`Qt5`/`Qt6`), Flutter (`FLUTTER`), WPF (`HwndWrapper`).

### 3.3 `WindowPaneLocation` (9 Active Values)
Defines the physical layout panes of modern applications:
* `Unknown = 0` ("unknown")
* `ActivityBar = 1` ("activity_bar", width <= 42px)
* `PrimarySidebar = 2` ("primary_sidebar", left pane)
* `MainContent = 3` ("main_content", central editor)
* `AuxiliarySidebar = 4` ("auxiliary_sidebar", right AI/chat panel)
* `BottomPanel = 5` ("bottom_panel", terminal/output console)
* `TopBar = 6` ("top_bar", title/menu bar)
* `StatusBar = 7` ("status_bar", bottom status strip)
* `OverlayModal = 8` ("overlay_modal", floating palettes)

---

## 4. Extraction Engine, Classifiers & Rules (`ADCE.Extraction`)

### 4.1 Win32 Fast Gating & UIPI Security (`Win32Gating.cs`)
1. **Shallow Win32 Gating (< 0.5 ms):**
   - Direct `user32` P/Invoke calls (`GetWindowTextW`, `GetClassNameW`, `GetWindowThreadProcessId`, `GetWindowRect`).
   - Bitmask filtering: filters out transient shell tooltips, taskbar previews, and hidden utility windows.
2. **UIPI Barrier Detection:**
   - Opens target process with `PROCESS_QUERY_LIMITED_INFORMATION`. If elevated and ADCE is non-elevated, gracefully emits a shallow Win32 snapshot without hanging in COM cross-process calls.
3. **Child HWND Root Normalization:**
   - Uses `NativeMethods.GetAncestor(hwnd, GA_ROOTOWNER)` to ensure child render surfaces (e.g. Electron sub-HWNDs) map to the true top-level desktop window.

### 4.2 Dynamic Self-Healing Rule Engine (`SemanticRuleEngine.cs`)
- Implements `ISemanticRuleEngine`.
- Rules are persisted to `%LOCALAPPDATA%\ADCE\semantic_rules.json`.
- Supports dynamic declarative overrides with prioritization, matching against `ProcessName`, `ControlType`, `ElementName`, `AutomationId`, `ClassName`, and ancestor `ContainerPath`.

### 4.3 Dedicated Single-Roundtrip Extractors
All extractors use `FlaUI.UIA3` with `AutomationElementMode.None` inside scoped `CacheRequest.Activate()` blocks:
- [`MonacoIdeExtractor.cs`](../../src/ADCE.Extraction/Extractors/MonacoIdeExtractor.cs): Electron/Monaco tabs (`tabs-container`), breadcrumbs (`monaco-breadcrumbs`), sidebar, editor instances.
- [`GeckoBrowserExtractor.cs`](../../src/ADCE.Extraction/Extractors/GeckoBrowserExtractor.cs): Tree Style Tab (`tabs normal`), pinned tabs, address bar (`urlbar-input`), with strict Document viewport isolation.
- [`WinUIExplorerExtractor.cs`](../../src/ADCE.Extraction/Extractors/WinUIExplorerExtractor.cs): Win11 tabs (`TabView`), breadcrumbs (`PART_BreadcrumbBar`), Items View.
- [`TerminalExtractor.cs`](../../src/ADCE.Extraction/Extractors/TerminalExtractor.cs): Cascadia tabs, console window text buffers.

---

## 5. Automated Verification & Invariants Baseline

### 5.1 Test Suite Inventory
All tests pass cleanly in .NET 10:
* **`ADCE.Core.Tests`:** 98 tests (model immutability, record equality, JSON serialization, enum extension roundtripping).
* **`ADCE.Extraction.Tests`:** 101 tests (archetype classification, rule matching, hierarchy extraction, event pipeline debouncing, privacy sanitization, Win32 gating).
* **`ADCE.Daemon.Tests`:** 28 tests (HUD positioning, TrayIcon factory, single-instance mutex, clipboard marshaling).
* **`ADCE.Mcp.Tests`:** 22 tests (JSON-RPC 2.0 schemas, Stdio transport, tool endpoints).
* **`ADCE.Storage.Tests`:** 14 tests (L1 atomic cache, SQLite WAL persistence, schema migrations).
* **Total:** **263 passing unit tests**.

### 5.2 Architectural Invariant Enforcement
[`ExtractorInvariantTests.cs`](../../tests/ADCE.Extraction.Tests/Architecture/ExtractorInvariantTests.cs) scans all C# source files in `src/ADCE.Extraction/Extractors` and fails the build if any extractor calls `windowElement.FindAllDescendants`. This enforces the zero unbounded DOM traversal invariant at compile/CI time.

### 5.3 Ground-Truth Claim Matrix
Verified deterministically via [`ClaimVerificationTests.cs`](../../tests/ADCE.Extraction.Tests/Verification/ClaimVerificationTests.cs):
* **CLM-001 (Global Focus Bleed Prevention):** Focused PID matches Window PID.
* **CLM-002 (Child HWND Normalization):** Sub-surfaces map to top-level window.
* **CLM-003 (IDE Semantic Zone Resolution):** Monaco editor, terminal, git commit, and chat prompt correctly resolve.
* **CLM-004 (Browser Sidebar vs. IDE Explorer):** Gecko sidebar is isolated and never falsely classified as IDE Explorer.
* **CLM-005 & CLM-006:** Verified via synthetic event pipeline tests.

---

## 6. Architectural Findings & Immediate Action Items

1. **Test-to-Spike Assembly Coupling:**
   `tests/ADCE.Extraction.Tests/ADCE.Extraction.Tests.csproj` references `src/ADCE.Spikes/ADCE.Spikes.csproj`, which references `src/ADCE.Daemon/ADCE.Daemon.csproj`. When `ADCE.Daemon` is running live as a background service, MSBuild fails with file lock errors on `ADCE.Core.dll` and `ADCE.Extraction.dll`.
   * **Action:** Decouple `MockStimulusDriver` and verification models directly into `ADCE.Extraction.Tests`, removing the `ADCE.Spikes` reference.

2. **Documentation Test Count Metric Drift:**
   `docs/CONTEXT.md` line 61 cites "136 unit tests". The true active test count is **263 passing unit tests**.
   * **Action:** Reconcile in Stage 2 and Stage 3.
