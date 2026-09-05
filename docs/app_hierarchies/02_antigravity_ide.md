<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 App Hierarchies ](./README.md) › **02. Antigravity IDE Profile**

---

# Antigravity IDE / VS Code (Monaco/Electron) UI Automation Hierarchy & Semantic Profile

> **Document Status:** Active / Verified Ground Truth Specification
> **Target Engine:** Chromium / Electron / Monaco (`Chrome_WidgetWin_1`)
> **Verification Date:** 2026-09-05 06:39:45 UTC
> **Target HWND:** `0x0001076E` | **PID:** `1180` | **Window Title:** `active-desktop-context-engine - Antigravity IDE`

---

## 1. Physical Window & Process Specification

| Property | Physical Telemetry Value | Architectural Significance |
| :--- | :--- | :--- |
| **Process Name** | `Antigravity IDE` | Multi-process Electron runtime architecture |
| **PID** | `1180` | Host main UI workbench window process |
| **Window HWND** | `0x0001076E` | Win32 top-level desktop window handle |
| **Window Class** | `Chrome_WidgetWin_1` | Standard Chromium / Electron top-level window class |
| **Window Title** | `active-desktop-context-engine - Antigravity IDE` | Workspace directory and active editor context |
| **Window Bounds** | `[X=715, Y=0, W=564, H=584]` | Full desktop window client envelope |

---

## 2. Structural Container Anatomy

The Antigravity IDE interface is organized as a multi-pane Monaco/Electron workbench partitioned into five primary layout zones:
1. **Activity Bar (`workbench.parts.activitybar`):** Activity switcher for Explorer, Search, Source Control, and Agent.
2. **Primary Sidebar (`workbench.parts.sidebar`):** Docked drawer hosting the repository file tree (`workbench.explorer.fileView`) and accordion headers.
3. **Editor Area (`workbench.parts.editor`):** Tab strip (`tabs-container`) and Monaco code editor buffer (`monaco-editor`).
4. **Auxiliary Bar (`workbench.parts.auxiliarybar`):** Secondary drawer hosting the Antigravity AI Agent chat panel and Artifact Viewer.
5. **Bottom Panel (`workbench.parts.panel`):** Integrated terminal (`xterm`), output channel, and debug console.
6. **Status Bar (`workbench.parts.statusbar`):** Git branch, language mode, and telemetry status.

> [!TIP]
> **Wide Diagram View:** For fluid zooming, panning, and fullscreen exploration, open the [Interactive HTML Diagram](../diagrams/antigravity_ide_hierarchy_diagram.html).

```mermaid
graph TD
    Root["Window: Chrome_WidgetWin_1"] --> Workbench["Container: workbench.main.container"]

    Workbench --> TitleBar["TitleBar: workbench.parts.titlebar"]
    Workbench --> ActivityBar["ActivityBar: workbench.parts.activitybar"]
    Workbench --> Sidebar["PrimarySidebar: workbench.parts.sidebar"]
    Workbench --> EditorArea["EditorArea: workbench.parts.editor"]
    Workbench --> AuxBar["AuxiliaryBar: workbench.parts.auxiliarybar"]
    Workbench --> Panel["Panel: workbench.parts.panel"]
    Workbench --> StatusBar["StatusBar: workbench.parts.statusbar"]

    ActivityBar --> ActButtons["Launcher: Explorer, SCM, Agent"]
    Sidebar --> FileTree["Tree: File Explorer (.agents, docs)"]

    EditorArea --> TabStrip["TabStrip: TabItem README.md"]
    EditorArea --> Breadcrumbs["Navigation: monaco-breadcrumbs"]
    EditorArea --> MonacoEditor["EditorBuffer: Monaco Code Editor"]

    AuxBar --> AgentChat["Chat: Conversation & Prompt Input"]
    AuxBar --> ArtifactView["ArtifactViewer: Header & Preview"]

    Panel --> Terminal["IntegratedTerminal: xterm / pwsh"]
    StatusBar --> StatusItems["Status: Remote, Git Branch, UTF-8"]
```

---

## 3. Empirical Telemetry Matrix

| Step | Zone Tag | Stimulus | Physical UIA Control | Bounds `[X, Y, W, H]` | ADCE Prediction | Correct? | Screenshot |
| :---: | :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| 1 | **ActivityBar** | `Focus Activity Bar Button` | [TabItem] (Explorer (Ctrl+Shift+E) - 1 unsaved file Explorer (Ctrl+Shift+E) - 1 unsaved file) | `[715, 35, 42, 42]` | Zone: `TabBar`<br>Pane: `ActivityBar`<br>Path: `[ActivityBar, ActivityBar]` | ✅ | [`step_01_activity_bar.png`](../media/antigravity_telemetry/step_01_activity_bar.png) |
| 2 | **SidebarExplorer** | `Focus File Explorer Item (Ctrl+Shift+E)` | [TreeItem] `list_id_3_0` (.agents) | `[757, 92, 169, 22]` | Zone: `SidebarExplorer`<br>Pane: `PrimarySidebar`<br>Path: `[PrimarySidebar, Explorer]` | ✅ | [`step_02_sidebar_explorer.png`](../media/antigravity_telemetry/step_02_sidebar_explorer.png) |
| 3 | **EditorTabStrip** | `Focus Active Editor TabItem` | [TabItem] (README.md) | `[926, 35, 233, 30]` | Zone: `TabBar`<br>Pane: `MainContent`<br>Path: `[MainContent, Editor]` | ✅ | [`step_03_editor_tabstrip.png`](../media/antigravity_telemetry/step_03_editor_tabstrip.png) |
| 4 | **EditorBuffer** | `Focus Monaco Editor Text Area` | [Edit] (README.md) | `[972, 87, 131, 19]` | Zone: `EditorBuffer`<br>Pane: `MainContent`<br>Path: `[MainContent, Editor]` | ✅ | [`step_04_editor_buffer.png`](../media/antigravity_telemetry/step_04_editor_buffer.png) |
| 5 | **Breadcrumbs** | `Focus Editor Breadcrumb Item` | [ListItem] (active-desktop-context-engine > README.md) | `[961, 65, 19, 22]` | Zone: `NavigationPanel`<br>Pane: `MainContent`<br>Path: `[MainContent, Editor]` | ✅ | [`step_05_breadcrumbs.png`](../media/antigravity_telemetry/step_05_breadcrumbs.png) |
| 6 | **AgentPanelToggle** | `Focus Toggle Agent Button (Ctrl+Alt+B)` | [CheckBox] (Toggle Agent (Ctrl+Alt+B)) | `[989, 6, 22, 22]` | Zone: `ChatConversation`<br>Pane: `AuxiliarySidebar`<br>Path: `[AuxiliarySidebar, Chat, Conversation]` | ✅ | [`step_06_agent_panel_toggle.png`](../media/antigravity_telemetry/step_06_agent_panel_toggle.png) |
| 7 | **ArtifactViewerHeader** | `Focus Artifact Viewer Panel Header` | [Group] (Artifact Viewer header) | `[-11351, 35, 154, 30]` | Zone: `TabBar`<br>Pane: `MainContent`<br>Path: `[MainContent, Editor]` | ✅ | [`step_07_artifact_header.png`](../media/antigravity_telemetry/step_07_artifact_header.png) |
| 8 | **IntegratedTerminal** | `Focus Terminal Viewport (Ctrl+J)` | [Group] () | `[715, 584, 564, 170]` | Zone: `Terminal`<br>Pane: `BottomPanel`<br>Path: `[BottomPanel, Terminal]` | ✅ | [`step_08_terminal_panel.png`](../media/antigravity_telemetry/step_08_terminal_panel.png) |
| 9 | **StatusBar** | `Focus Status Bar Indicator` | [Group] `status.host` (remote) | `[715, 562, 39, 22]` | Zone: `StatusBar`<br>Pane: `StatusBar`<br>Path: `[StatusBar, StatusBar]` | ✅ | [`step_09_statusbar.png`](../media/antigravity_telemetry/step_09_statusbar.png) |
| 10 | **TopBarMenuItem** | `Focus View Menu -> Command Palette...` | [MenuItem] "Command Palette... Ctrl+Shift+P" | `[0, 35, 260, 26]` | Zone: `CommandPalette`<br>Pane: `OverlayModal`<br>Path: `[OverlayModal, QuickOpen]` | ❌ | [`step_10_topbar_menuitem_misclassification.png`](../media/antigravity_telemetry/step_10_topbar_menuitem_misclassification.png) |

---

## 4. Ancestor Hierarchy Traces & Physical Dissection

### Step 1: ActivityBar — Activity Bar Action Launcher Button

- **Stimulus:** `Focus Activity Bar Button`
- **Physical Focus:** `[TabItem]` Name='`Explorer (Ctrl+Shift+E) - 1 unsaved file Explorer (Ctrl+Shift+E) - 1 unsaved file`' AutoId='``' Class='`action-item icon checked`'
- **Bounds:** `[X=715, Y=35, Width=42, Height=42]`
- **ADCE Output:** Zone: `TabBar`, Pane: `ActivityBar`, ActiveView: `ActivityBar`, Section: `null`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [TabItem] Name='Explorer (Ctrl+Shift+E) - 1 unsaved file Explorer (Ctrl+Shift+E) - 1 unsaved file' AutoId='' Cls='action-item icon checked'
[1] [Tab] Name='Active View Switcher' AutoId='' Cls='actions-container'
[2] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[3] [Group] Name='' AutoId='' Cls=''
[4] [Document] Name='active-desktop-context-engine - Antigravity IDE' AutoId='RootWebArea' Cls=''
[5] [Pane] Name='' AutoId='' Cls='View'
[6] [Pane] Name='' AutoId='' Cls='View'
[7] [Pane] Name='' AutoId='' Cls='View'
[8] [Pane] Name='' AutoId='' Cls='ClientView'
[9] [Pane] Name='' AutoId='' Cls='WinFrameView'
[10] [Pane] Name='' AutoId='' Cls='NonClientView'
[11] [Pane] Name='active-desktop-context-engine - Antigravity IDE' AutoId='' Cls='RootView'
[12] [Pane] Name='active-desktop-context-engine - Antigravity IDE' AutoId='' Cls='Chrome_WidgetWin_1'
```

![ActivityBar](../media/antigravity_telemetry/step_01_activity_bar.png)

### Step 2: SidebarExplorer — Primary Sidebar File Explorer Tree Item

- **Stimulus:** `Focus File Explorer Item (Ctrl+Shift+E)`
- **Physical Focus:** `[TreeItem]` Name='`.agents`' AutoId='`list_id_3_0`' Class='`monaco-list-row`'
- **Bounds:** `[X=757, Y=92, Width=169, Height=22]`
- **ADCE Output:** Zone: `SidebarExplorer`, Pane: `PrimarySidebar`, ActiveView: `Explorer`, Section: `null`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [TreeItem] Name='.agents' AutoId='list_id_3_0' Cls='monaco-list-row'
[1] [Tree] Name='Files Explorer' AutoId='' Cls='monaco-list list_id_3 mouse-support element-focused selection-single'
[2] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[3] [Group] Name='' AutoId='' Cls=''
[4] [Document] Name='active-desktop-context-engine - Antigravity IDE' AutoId='RootWebArea' Cls=''
[5] [Pane] Name='' AutoId='' Cls='View'
[6] [Pane] Name='' AutoId='' Cls='View'
[7] [Pane] Name='' AutoId='' Cls='View'
[8] [Pane] Name='' AutoId='' Cls='ClientView'
[9] [Pane] Name='' AutoId='' Cls='WinFrameView'
[10] [Pane] Name='' AutoId='' Cls='NonClientView'
[11] [Pane] Name='active-desktop-context-engine - Antigravity IDE' AutoId='' Cls='RootView'
[12] [Pane] Name='active-desktop-context-engine - Antigravity IDE' AutoId='' Cls='Chrome_WidgetWin_1'
```

![SidebarExplorer](../media/antigravity_telemetry/step_02_sidebar_explorer.png)

### Step 3: EditorTabStrip — Editor Area Active Document Tab

- **Stimulus:** `Focus Active Editor TabItem`
- **Physical Focus:** `[TabItem]` Name='`README.md`' AutoId='``' Class='`tab tab-actions-right sizing-fit has-icon tab-border-bottom active selected tab-border-top`'
- **Bounds:** `[X=926, Y=35, Width=233, Height=30]`
- **ADCE Output:** Zone: `TabBar`, Pane: `MainContent`, ActiveView: `Editor`, Section: `null`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [TabItem] Name='README.md' AutoId='' Cls='tab tab-actions-right sizing-fit has-icon tab-border-bottom active selected tab-border-top'
[1] [Tab] Name='' AutoId='' Cls='tabs-container'
[2] [Group] Name='' AutoId='' Cls='title tabs show-file-icons'
[3] [Group] Name='' AutoId='workbench.parts.editor' Cls='part editor'
[4] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[5] [Group] Name='' AutoId='' Cls=''
[6] [Document] Name='active-desktop-context-engine - Antigravity IDE' AutoId='RootWebArea' Cls=''
[7] [Pane] Name='' AutoId='' Cls='View'
[8] [Pane] Name='' AutoId='' Cls='View'
[9] [Pane] Name='' AutoId='' Cls='View'
[10] [Pane] Name='' AutoId='' Cls='ClientView'
[11] [Pane] Name='' AutoId='' Cls='WinFrameView'
[12] [Pane] Name='' AutoId='' Cls='NonClientView'
```

![EditorTabStrip](../media/antigravity_telemetry/step_03_editor_tabstrip.png)

### Step 4: EditorBuffer — Monaco Code Editor Document Buffer

- **Stimulus:** `Focus Monaco Editor Text Area`
- **Physical Focus:** `[Edit]` Name='`README.md`' AutoId='``' Class='`native-edit-context`'
- **Bounds:** `[X=972, Y=87, Width=131, Height=19]`
- **ADCE Output:** Zone: `EditorBuffer`, Pane: `MainContent`, ActiveView: `Editor`, Section: `null`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [Edit] Name='README.md' AutoId='' Cls='native-edit-context'
[1] [Text] Name='' AutoId='' Cls='monaco-editor no-user-select  showUnused showDeprecated vs-dark focused'
[2] [Group] Name='README.md' AutoId='' Cls='editor-instance'
[3] [Group] Name='' AutoId='workbench.parts.editor' Cls='part editor'
[4] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[5] [Group] Name='' AutoId='' Cls=''
[6] [Document] Name='active-desktop-context-engine - Antigravity IDE' AutoId='RootWebArea' Cls=''
[7] [Pane] Name='' AutoId='' Cls='View'
[8] [Pane] Name='' AutoId='' Cls='View'
[9] [Pane] Name='' AutoId='' Cls='View'
[10] [Pane] Name='' AutoId='' Cls='ClientView'
[11] [Pane] Name='' AutoId='' Cls='WinFrameView'
[12] [Pane] Name='' AutoId='' Cls='NonClientView'
```

![EditorBuffer](../media/antigravity_telemetry/step_04_editor_buffer.png)

### Step 5: Breadcrumbs — Editor Path Breadcrumb Navigation

- **Stimulus:** `Focus Editor Breadcrumb Item`
- **Physical Focus:** `[ListItem]` Name='`active-desktop-context-engine > README.md`' AutoId='``' Class='`monaco-breadcrumb-item`'
- **Bounds:** `[X=961, Y=65, Width=19, Height=22]`
- **ADCE Output:** Zone: `NavigationPanel`, Pane: `MainContent`, ActiveView: `Editor`, Section: `null`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [ListItem] Name='active-desktop-context-engine > README.md' AutoId='' Cls='monaco-breadcrumb-item'
[1] [List] Name='' AutoId='' Cls='monaco-breadcrumbs'
[2] [Group] Name='' AutoId='' Cls='title tabs show-file-icons'
[3] [Group] Name='' AutoId='workbench.parts.editor' Cls='part editor'
[4] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[5] [Group] Name='' AutoId='' Cls=''
[6] [Document] Name='active-desktop-context-engine - Antigravity IDE' AutoId='RootWebArea' Cls=''
[7] [Pane] Name='' AutoId='' Cls='View'
[8] [Pane] Name='' AutoId='' Cls='View'
[9] [Pane] Name='' AutoId='' Cls='View'
[10] [Pane] Name='' AutoId='' Cls='ClientView'
[11] [Pane] Name='' AutoId='' Cls='WinFrameView'
[12] [Pane] Name='' AutoId='' Cls='NonClientView'
```

![Breadcrumbs](../media/antigravity_telemetry/step_05_breadcrumbs.png)

### Step 6: AgentPanelToggle — Antigravity Agent Panel Action Toggle

- **Stimulus:** `Focus Toggle Agent Button (Ctrl+Alt+B)`
- **Physical Focus:** `[CheckBox]` Name='`Toggle Agent (Ctrl+Alt+B)`' AutoId='``' Class='`action-label checked codicon codicon-layout-sidebar-right`'
- **Bounds:** `[X=989, Y=6, Width=22, Height=22]`
- **ADCE Output:** Zone: `ChatConversation`, Pane: `AuxiliarySidebar`, ActiveView: `Chat`, Section: `Conversation`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [CheckBox] Name='Toggle Agent (Ctrl+Alt+B)' AutoId='' Cls='action-label checked codicon codicon-layout-sidebar-right'
[1] [ToolBar] Name='Toggle Agent (Ctrl+Alt+B)' AutoId='' Cls='actions-container'
[2] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[3] [Group] Name='' AutoId='' Cls=''
[4] [Document] Name='Toggle Agent (Ctrl+Alt+B)' AutoId='RootWebArea' Cls=''
[5] [Pane] Name='' AutoId='' Cls='View'
[6] [Pane] Name='' AutoId='' Cls='View'
[7] [Pane] Name='' AutoId='' Cls='View'
[8] [Pane] Name='' AutoId='' Cls='ClientView'
[9] [Pane] Name='' AutoId='' Cls='WinFrameView'
[10] [Pane] Name='' AutoId='' Cls='NonClientView'
[11] [Pane] Name='Toggle Agent (Ctrl+Alt+B)' AutoId='' Cls='RootView'
[12] [Pane] Name='Toggle Agent (Ctrl+Alt+B)' AutoId='' Cls='Chrome_WidgetWin_1'
```

![AgentPanelToggle](../media/antigravity_telemetry/step_06_agent_panel_toggle.png)

### Step 7: ArtifactViewerHeader — Antigravity Artifact Viewer Header

- **Stimulus:** `Focus Artifact Viewer Panel Header`
- **Physical Focus:** `[Group]` Name='`Artifact Viewer header`' AutoId='``' Class='`monaco-icon-label codicon-jetski-artifacts-implementation-plan-icon predefined-file-icon tab-label tab-label-has-badge`'
- **Bounds:** `[X=-11351, Y=35, Width=154, Height=30]`
- **ADCE Output:** Zone: `TabBar`, Pane: `MainContent`, ActiveView: `Editor`, Section: `null`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [Group] Name='Artifact Viewer header' AutoId='' Cls='monaco-icon-label codicon-jetski-artifacts-implementation-plan-icon predefined-file-icon tab-label tab-label-has-badge'
[1] [TabItem] Name='Artifact Viewer header' AutoId='' Cls='tab tab-actions-right sizing-fit has-icon'
[2] [Tab] Name='' AutoId='' Cls='tabs-container'
[3] [Group] Name='' AutoId='' Cls='title tabs show-file-icons'
[4] [Group] Name='' AutoId='workbench.parts.editor' Cls='part editor'
[5] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[6] [Group] Name='' AutoId='' Cls=''
[7] [Document] Name='Artifact Viewer header' AutoId='RootWebArea' Cls=''
[8] [Pane] Name='' AutoId='' Cls='View'
[9] [Pane] Name='' AutoId='' Cls='View'
[10] [Pane] Name='' AutoId='' Cls='View'
[11] [Pane] Name='' AutoId='' Cls='ClientView'
[12] [Pane] Name='' AutoId='' Cls='WinFrameView'
```

![ArtifactViewerHeader](../media/antigravity_telemetry/step_07_artifact_header.png)

### Step 8: IntegratedTerminal — Bottom Panel Integrated Terminal / Console

- **Stimulus:** `Focus Terminal Viewport (Ctrl+J)`
- **Physical Focus:** `[Group]` Name='``' AutoId='``' Class='`terminal xterm focus`'
- **Bounds:** `[X=715, Y=584, Width=564, Height=170]`
- **ADCE Output:** Zone: `Terminal`, Pane: `BottomPanel`, ActiveView: `Terminal`, Section: `null`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [Group] Name='' AutoId='' Cls='terminal xterm focus'
[1] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[2] [Group] Name='' AutoId='' Cls=''
[3] [Document] Name='active-desktop-context-engine - Antigravity IDE' AutoId='RootWebArea' Cls=''
[4] [Pane] Name='' AutoId='' Cls='View'
[5] [Pane] Name='' AutoId='' Cls='View'
[6] [Pane] Name='' AutoId='' Cls='View'
[7] [Pane] Name='' AutoId='' Cls='ClientView'
[8] [Pane] Name='' AutoId='' Cls='WinFrameView'
[9] [Pane] Name='' AutoId='' Cls='NonClientView'
[10] [Pane] Name='active-desktop-context-engine - Antigravity IDE' AutoId='' Cls='RootView'
[11] [Pane] Name='active-desktop-context-engine - Antigravity IDE' AutoId='' Cls='Chrome_WidgetWin_1'
[12] [Pane] Name='Job Search' AutoId='' Cls='#32769'
```

![IntegratedTerminal](../media/antigravity_telemetry/step_08_terminal_panel.png)

### Step 9: StatusBar — Workbench Status Bar & Telemetry Indicator

- **Stimulus:** `Focus Status Bar Indicator`
- **Physical Focus:** `[Group]` Name='`remote`' AutoId='`status.host`' Class='`statusbar-item left remote-kind has-background-color first-visible-item`'
- **Bounds:** `[X=715, Y=562, Width=39, Height=22]`
- **ADCE Output:** Zone: `StatusBar`, Pane: `StatusBar`, ActiveView: `StatusBar`, Section: `null`

#### Physical Ancestor Chain (Leaf -> Root)
```text
[0] [Group] Name='remote' AutoId='status.host' Cls='statusbar-item left remote-kind has-background-color first-visible-item'
[1] [StatusBar] Name='' AutoId='workbench.parts.statusbar' Cls='part statusbar status-border-top'
[2] [Pane] Name='' AutoId='' Cls='file-icons-enabled monaco-enable-motion monaco-workbench windows chromium vs-dark vscode-theme-defaults-themes-dark_modern-json nopanel'
[3] [Group] Name='' AutoId='' Cls=''
[4] [Document] Name='active-desktop-context-engine - Antigravity IDE' AutoId='RootWebArea' Cls=''
[5] [Pane] Name='' AutoId='' Cls='View'
[6] [Pane] Name='' AutoId='' Cls='View'
[7] [Pane] Name='' AutoId='' Cls='View'
[8] [Pane] Name='' AutoId='' Cls='ClientView'
[9] [Pane] Name='' AutoId='' Cls='WinFrameView'
[10] [Pane] Name='' AutoId='' Cls='NonClientView'
[11] [Pane] Name='active-desktop-context-engine - Antigravity IDE' AutoId='' Cls='RootView'
[12] [Pane] Name='active-desktop-context-engine - Antigravity IDE' AutoId='' Cls='Chrome_WidgetWin_1'
```

![StatusBar](../media/antigravity_telemetry/step_09_statusbar.png)

### Step 10: TopBarMenuItem (Misclassification Analysis & Action-Invoker Collision)

- **Stimulus:** `Focus View Menu -> Command Palette...`
- **Physical Focus:** `[MenuItem]` Name='`Command Palette...  Ctrl+Shift+P`' AutoId='' Class='`action-item`'
- **Observed HUD Output:**
  - `Focus: [MenuItem] "Command Palette... Ctrl+Shift+P"`
  - `Zone: [CommandPalette] | Pane: [OverlayModal]`
  - `Hierarchy: OverlayModal > QuickOpen`
- **Correct Target Semantics:**
  - `Zone: DesktopSemanticZone.NavigationPanel`
  - `Pane: WindowPaneLocation.TopBar`
  - `SemanticPath: [TopBar, MenuBar]`
- **Root Cause Dissection:**
  The engine evaluated `ResolveSemanticZone` in `UiaExtractionEngine.cs`:
  ```csharp
  if (autoId.Contains("command-palette", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Command Palette", StringComparison.OrdinalIgnoreCase))
  {
      return DesktopSemanticZone.CommandPalette;
  }
  ```
  The condition matched on `name.Contains("Command Palette")` without verifying `ControlType`.
  Because the returned zone was `DesktopSemanticZone.CommandPalette`, `InferPaneFromZone` mapped the pane to `WindowPaneLocation.OverlayModal` and `InferViewFromZone` mapped the view to `QuickOpen`.
  The menu item names the command to execute, but the active interaction surface is an ephemeral dropdown menu attached to the titlebar, not the command palette modal overlay.

![TopBarMenuItem](../media/antigravity_telemetry/step_10_topbar_menuitem_misclassification.png)

---

## 5. Viewport Boundary & In-Content Semantics

Just as with Gecko web documents, Chromium / Monaco applications require strict structural boundary isolation:

1. **Monaco Code Buffer Isolation:** When focus is inside `monaco-editor` or an `Edit`/`Document` control inside `workbench.parts.editor`, it is strictly `WindowPaneLocation.MainContent` and `DesktopSemanticZone.EditorBuffer`. Chrome navigation heuristics must not apply.
2. **Auxiliary Agent Chat Isolation:** The AI chat panel and Artifact Viewer inside `workbench.parts.auxiliarybar` contain arbitrary HTML elements (buttons, inputs, lists). They must map to `WindowPaneLocation.AuxiliarySidebar` and `DesktopSemanticZone.ChatConversation` or `DesktopSemanticZone.AuxiliaryToolPanel`, never leaking into `MainContent`.
3. **Integrated Terminal Isolation:** The terminal pane inside `workbench.parts.panel` must resolve to `WindowPaneLocation.BottomPanel` and `DesktopSemanticZone.Terminal`.
4. **Activity Bar & Primary Sidebar:** Launcher buttons in `workbench.parts.activitybar` map to `WindowPaneLocation.ActivityBar`, while the tree items in `workbench.parts.sidebar` map to `WindowPaneLocation.PrimarySidebar` and `DesktopSemanticZone.SidebarExplorer`.
5. **Top Bar Menu & Command Invoker Isolation:** Menu items (`ControlType.MenuItem`) and dropdown menus (`monaco-menu`, `context-view`) spawned from `workbench.parts.titlebar` must map to `WindowPaneLocation.TopBar` and `DesktopSemanticZone.NavigationPanel`. They must never be classified as the target zone or modal dialog they invoke.

---

## 6. Actionable Implementation Changes for `ADCE.Extraction`

The following structural rules in `UiaExtractionEngine.cs` ensure deterministic extraction for ChromiumElectron archetypes:

```csharp
// Chromium / Monaco / Antigravity IDE Boundary Isolation
if (archetype == DesktopAppArchetype.ChromiumElectron)
{
    // Ephemeral Top Bar Menu & Context Menu Guard
    if (controlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) ||
        controlType.Equals("Menu", StringComparison.OrdinalIgnoreCase) ||
        containerClasses.Any(c => c.Contains("monaco-menu") || c.Contains("context-view")))
    {
        pane = WindowPaneLocation.TopBar;
        zone = DesktopSemanticZone.NavigationPanel;
        activeView = "MenuBar";
    }
    else if (containerPath.Contains("workbench.parts.editor") || containerClasses.Any(c => c.Contains("monaco-editor")))
    {
        pane = WindowPaneLocation.MainContent;
        zone = DesktopSemanticZone.EditorBuffer;
        activeView = "Editor";
    }
    else if (containerPath.Contains("workbench.parts.auxiliarybar"))
    {
        pane = WindowPaneLocation.AuxiliarySidebar;
        zone = DesktopSemanticZone.ChatConversation;
        activeView = "Chat";
    }
}
```
