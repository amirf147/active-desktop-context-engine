<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 📚 Postmortems ](./README.md) › **Heuristic Name Matching & Menu Item Misclassification**

---

# Engineering Retrospective: Heuristic Name-Matching Collisions and Action-Invoker Misclassification

> **Document Status:** Active Engineering Retrospective / Heuristic Analysis
> **Epistemic Authority:** Tier 5 (Historical Empirical Retrospective — Non-Normative)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).
> **Target Systems:** `ADCE.Extraction`, `ADCE.Daemon`, `ADCE.Core` (.NET 10 / C# 14 / FlaUI 5)
> **Incident Verification Date:** 2026-09-05
> **Target Process:** Antigravity IDE / Electron (`PID 1180`)
> **Telemetry Artifact:** [`docs/media/antigravity_telemetry/step_10_topbar_menuitem_misclassification.png`](../media/antigravity_telemetry/step_10_topbar_menuitem_misclassification.png)

---

## 1. Problem Statement & Live Telemetry Incident

During live HUD verification on Antigravity IDE (VS Code / Electron runtime), expanding the top-level `View` menu and focusing the menu item `Command Palette...  Ctrl+Shift+P` produced the following HUD classification:

```text
Focus:     [MenuItem] "Command Palette... Ctrl+Shift+P"
Zone:      [CommandPalette]
Pane:      [OverlayModal]
Hierarchy: OverlayModal > QuickOpen
```

![TopBarMenuItem Telemetry Proof](../media/antigravity_telemetry/step_10_topbar_menuitem_misclassification.png)

### The Functional Failure

The active user interaction was with a top-bar dropdown menu (`monaco-menu` inside `context-view`), which belongs to `WindowPaneLocation.TopBar` and `DesktopSemanticZone.NavigationPanel`.
ADCE classified the element as `DesktopSemanticZone.CommandPalette` and assigned `WindowPaneLocation.OverlayModal`.
Downstream consumers, including the Caster voice integration bridge, received false notifications asserting that the user had entered the modal Command Palette text input overlay.

---

## 2. Root Cause Analysis: The Execution Path

The misclassification occurs deterministically through the following sequence in `src/ADCE.Extraction/Engine/UiaExtractionEngine.cs`:

1. **UIA Property Extraction:**
   The focused UIA node provides:
   - `cType`: `"MenuItem"`
   - `name`: `"Command Palette...  Ctrl+Shift+P"`
   - `autoId`: `""` (empty string)
   - `cls`: `"action-item"`

2. **Ancestor Hierarchy Traversal:**
   `ExtractAncestorHierarchy` traverses up the parent chain:
   `[MenuItem]` -> `[Menu]` -> `[Pane]` (`monaco-menu`) -> `[Pane]` (`context-view`).
   Because these ephemeral dropdown containers lack workbench layout IDs (`workbench.parts.sidebar`, `workbench.parts.editor`), `ancestorPane` remains `WindowPaneLocation.Unknown` and `ancestorZone` remains `DesktopSemanticZone.Unknown`.

3. **Fallback to Archetype Heuristics:**
   In line 478 of `UiaExtractionEngine.cs`:
   ```csharp
   if (zone == DesktopSemanticZone.Unknown)
   {
       zone = ResolveSemanticZone(cType, name, autoId, cls, archetype, isOverlay);
   }
   ```

4. **Global Substring Match on `name`:**
   In lines 1120-1124 of `ResolveSemanticZone`:
   ```csharp
   if (autoId.Contains("command-palette", StringComparison.OrdinalIgnoreCase) ||
       name.Contains("Command Palette", StringComparison.OrdinalIgnoreCase))
   {
       return DesktopSemanticZone.CommandPalette;
   }
   ```
   The rule evaluates `name.Contains("Command Palette")`.
   The string `"Command Palette...  Ctrl+Shift+P"` satisfies this condition.
   The function returns `DesktopSemanticZone.CommandPalette` without inspecting `cType`.

5. **Cascading Pane and View Inference:**
   - In line 491: `pane = InferPaneFromZone(zone);`
     `InferPaneFromZone` maps `CommandPalette` to `WindowPaneLocation.OverlayModal`.
   - In line 529: `activeView = InferViewFromZone(zone);`
     `InferViewFromZone` maps `CommandPalette` to `"QuickOpen"`.
   - In line 538: `semanticPath` is assembled as `["OverlayModal", "QuickOpen"]`.

---

## 3. Investigation: Why Was Name-Matching Implemented?

Evaluating why `name` matching was introduced requires analyzing the characteristics of Chromium and Electron accessibility trees.

### 3.1 Telemetry Gaps in Web-Rendered Desktop Frameworks

In Chromium-based desktop frameworks (VS Code, Antigravity IDE, Slack, Teams), the Win32 accessibility layer is synthesized from the DOM via `AXTree`.
Unlike native Win32 controls that expose distinct control IDs or WinUI controls with explicit `x:Name` mappings, web applications exhibit three structural omissions:

1. **Missing or Volatile `AutomationId` Properties:**
   Non-input controls such as Activity Bar tabs, breadcrumb links, and sidebar section headers frequently leave `AutomationId` empty (`""`) or assign transient numerical strings (such as `list_id_3_0`).
2. **Generic CSS Class Names:**
   Class names in the accessibility tree are derived from CSS classes (for example, `action-item`, `monaco-button`, `view-pane`). These classes identify visual styles, not functional responsibilities.
3. **Information Concentration in `Name`:**
   Chromium maps HTML `aria-label`, button text, and placeholder attributes directly to the UIA `Name` property. During initial inspection with tools like `Accessibility Insights` or FlaUI `UIAVerify`, the `Name` property was often the only field carrying human-readable semantic clues (`"Explorer (Ctrl+Shift+E)"`, `"Outline Section"`, `"Message input"`).

### 3.2 Pragmatic Rapid Prototyping During Early Spikes

During Milestones 2 through 4, matching substrings on `Name` provided immediate classification for key workspace areas without requiring multi-level ancestor traversals.
For example, distinguishing the Git commit box from a general Monaco editor was initially solved by checking `name.Contains("Message (Ctrl+Enter to commit")`.
This pattern was replicated across other functional areas, including `"Command Palette"`, `"Timeline"`, `"Outline"`, and `"Terminal"`.

---

## 4. The Architectural Anti-Pattern: Action-Invoker vs. Functional-Container Collision

The fundamental flaw of leaf-level name matching is the conflation of **Action Invokers** with **Functional Containers**.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          THE INVERSION FAILURE                              │
├──────────────────────────────────┬──────────────────────────────────────────┤
│ Action Invoker (Trigger Control) │ Functional Container (Target Workspace)  │
├──────────────────────────────────┼──────────────────────────────────────────┤
│ Top-bar menu item                │ Centered modal text box and result list  │
│ [MenuItem] "Command Palette..."  │ [Pane] class="quick-input-widget"        │
│ Location: WindowPaneLocation.TopBar Location: WindowPaneLocation.OverlayModal │
│ Zone: DesktopSemanticZone.NavigationPanel Zone: DesktopSemanticZone.CommandPalette │
└──────────────────────────────────┴──────────────────────────────────────────┘
```

An actionable control (such as a `MenuItem`, `Button`, or `Hyperlink`) specifies the destination command it will execute when clicked.
It does not mean the user is currently interacting inside that destination container.
When a heuristic checks `name.Contains("Target Name")` globally:
- Any menu item that exposes the command label triggers the destination zone rule.
- The trigger is classified as the target.
- The engine asserts the user has changed panes before the user has executed the action.

---

## 5. Codebase Audit: Vulnerable Name Heuristics in `UiaExtractionEngine.cs`

A review of `src/ADCE.Extraction/Engine/UiaExtractionEngine.cs` identified multiple locations where `name.Contains` or `name.Equals` is evaluated without control-type conditioning:

| Line Number | Heuristic Pattern | False-Positive Trigger Scenario |
| :--- | :--- | :--- |
| **Line 1121** | `name.Contains("Command Palette")` | Focusing `View -> Command Palette...` menu item resolves to `CommandPalette` and `OverlayModal`. |
| **Line 1084** | `name.Contains("Source Control")` | Focusing `View -> Open View... -> Source Control` resolves to `SidebarExplorer` and `PrimarySidebar`. |
| **Line 1041** | `name.Contains("Timeline Section")` | Focusing menu items or buttons with "Timeline" resolves to `Timeline`. |
| **Line 1048** | `name.Contains("Outline Section")` | Focusing menu items or buttons with "Outline" resolves to `Outline`. |
| **Line 1026** | `name.StartsWith("Terminal")` | Focusing `Terminal -> New Terminal` menu item resolves to `Terminal` and `BottomPanel`. |
| **Line 404** | `name.Contains("Focus Terminal")` | Focusing shortcut keys or tooltips resolves to `Terminal`. |
| **Line 375** | `name.Contains("Toggle Agent")` | Focusing a menu item to toggle the agent chat panel resolves to `ChatConversation`. |
| **Line 387** | `name.Contains("Explorer (Ctrl+Shift+E)")` | Focusing an activity bar menu shortcut resolves to `ActivityBar`. |
| **Line 994** | `name.Contains("Address and search bar")` | Focusing browser menu options referencing the address bar resolves to `AddressBar`. |

---

## 6. Architectural Remediation Blueprint

Remediation must not add further ad-hoc string checks. The architecture requires three structural constraints:

### Constraint 1: ControlType Disqualification for Action Invokers

Controls with `ControlType.MenuItem`, `ControlType.Menu`, or parent containers with class `monaco-menu` or `context-view` are ephemeral command surfaces.
They belong to `WindowPaneLocation.TopBar` (or a dedicated context menu pane) and `DesktopSemanticZone.NavigationPanel`.
They must be disqualified from matching functional editor, terminal, or modal zones regardless of their text content:

```csharp
if (controlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) ||
    controlType.Equals("Menu", StringComparison.OrdinalIgnoreCase) ||
    containerClasses.Any(c => c.Contains("monaco-menu") || c.Contains("context-view")))
{
    return DesktopSemanticZone.NavigationPanel;
}
```

### Constraint 2: Container-First Ancestry Verification

The true Command Palette in Monaco/Electron is characterized by its structural parent container:
- Container class: `quick-input-widget`
- Container automation ID: `quickInput`
- Child input class: `quick-input-box`

Zone classification for `DesktopSemanticZone.CommandPalette` or `QuickOpen` must require verification of this container in `containerClasses` or `containerPath`.
Leaf element text matching without container confirmation must be rejected.

### Constraint 3: ControlType Scoping on Residual Name Heuristics

When `Name` properties must be inspected due to missing `AutomationId` (such as Monaco breadcrumb elements or SCM commit inputs):
- The check must be explicitly bound to valid leaf control types (for example, `cType == "Edit"` or `cType == "TreeItem"`).
- Global unscoped fallback matching across all control types must be prohibited.

---

## 7. Operational Status

In accordance with user directives, no code modifications have been applied to `src/ADCE.Extraction/Engine/UiaExtractionEngine.cs` in this turn.
This retrospective serves as the formal design record and failure mode analysis for the upcoming extraction pipeline hardening task.
