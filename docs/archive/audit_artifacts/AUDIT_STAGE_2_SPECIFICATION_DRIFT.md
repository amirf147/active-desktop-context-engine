<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 📋 Reports ](./LATEST_CLAIM_VERIFICATION.md) › **Stage 2 Specification Drift Audit**

---

# ADCE Stage 2 Audit: Tier 2 Specification Alignment & Drift Analysis

> **Document Status:** Active / Canonical Specification Drift Ledger
> **Epistemic Authority:** Tier 2 Audit (Auditing Blueprints against Tier 1 Ground-Truth Baseline)
> **Audit Date:** September 2026
> **Baseline Reference:** [`docs/reports/AUDIT_STAGE_1_GROUND_TRUTH_BASELINE.md`](./AUDIT_STAGE_1_GROUND_TRUTH_BASELINE.md)

---

## 1. Executive Summary

This audit compares the Tier 2 master architecture blueprints against the compiled Tier 1 ground-truth code baseline established in Stage 1. Specifically, it reviews:
1. [`docs/architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md`](../architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md) (Spec 018)
2. [`docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md`](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md) (Spec 017)
3. [`docs/architecture/MCP_SCHEMA_SPEC.md`](../architecture/MCP_SCHEMA_SPEC.md)
4. [`docs/CONTEXT.md`](../CONTEXT.md)

Across these specifications, five distinct architectural drifts were identified where production code evolved ahead of early research blueprints. This document catalogs each drift and details the required synchronization.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                         SPECIFICATION DRIFT AUDIT MATRIX                               │
├────┬────────────────────────────┬─────────────────────────────┬────────────────────────┤
│ ID │ Subsystem / Domain         │ Specification (Tier 2 Spec) │ Production Code (Tier 1)│
├────┼────────────────────────────┼─────────────────────────────┼────────────────────────┤
│ D1 │ Focus & Zone Modeling      │ Flat single `TargetZone`    │ 3-level hierarchy model│
│ D2 │ Semantic Zone Enumeration  │ 10–12 ad-hoc zone names     │ 19 canonical enum items│
│ D3 │ Dynamic App Discovery      │ Open gap / hypothetical     │ `SemanticRuleEngine`   │
│ D4 │ Time-Series Storage Schema │ 3-table normalized schema   │ Denormalized WAL table │
│ D5 │ Test Verification Metrics  │ 136 unit tests cited        │ 263 passed unit tests  │
└────┴────────────────────────────┴─────────────────────────────┴────────────────────────┘
```

---

## 2. Detailed Drift Breakdown & Analysis

### 2.1 Drift D1: 3-Level Hierarchy Model vs. Flat Zone Modeling
* **The Specification (`017`, `018`):** Early revisions treated focus context as a single flat string or enum property (`TargetZone: EditorBuffer` or `TargetZone: ActivityBar`).
* **The Ground Truth (`ADCE.Core`):** Production code implements a **3-level structural hierarchy** in [`FocusedControlInfo`](../../src/ADCE.Core/Models/FocusedControlInfo.cs):
  1. **Macro Layout Pane (`WindowPaneLocation`):** `ActivityBar`, `PrimarySidebar`, `MainContent`, `AuxiliarySidebar`, `BottomPanel`, `TopBar`, `StatusBar`, `OverlayModal`.
  2. **Logical Container & View (`ActiveView`, `SectionName`):** e.g. `ActiveView = "workbench.view.explorer"`, `SectionName = "Outline"`.
  3. **Leaf Semantic Typing Anchor (`DesktopSemanticZone`):** e.g. `EditorBuffer`, `ChatPrompt`, `Terminal`, supplemented by immutable ancestor arrays (`SemanticPath`, `ContainerPath`, `ContainerClasses`).
* **Resolution:** Reconcile `UI_AUTOMATION_STRUCTURES_REFERENCE.md` to explicitly promote the 3-level model as the primary structural schema and mark the flat target zone view as a deprecated legacy abstraction.

### 2.2 Drift D2: Canonical Semantic Zone Enumeration (19 Active Values)
* **The Specification (`017`):** Listed an ad-hoc set of target zones (Editor Tabs, Activity Bar, Breadcrumbs, Code Buffer, Git Commit Box, Status Bar, Tree Style Tabs, URL Bar, Explorer Tabs, Terminal Tabs).
* **The Ground Truth (`ADCE.Core.Enums.DesktopSemanticZone`):** The canonical enum defines exactly 19 values:
  `Unknown`, `EditorBuffer`, `Terminal`, `GitCommitBox`, `SidebarExplorer`, `AddressBar`, `WebDocument`, `ShellItemList`, `TabBar`, `StatusBar`, `CommandPalette`, `ChatPrompt`, `QuickOpen`, `SystemDialog`, `NavigationPanel`, `ActivityBar`, `Timeline`, `Outline`, `ChatConversation`.
  Furthermore, `DesktopSemanticZoneExtensions.cs` provides `ToMacroZone()` mapping each granular zone into 6 high-level macro anchors (`EditorBuffer`, `Terminal`, `ChatPrompt`, `WebDocument`, `NavigationPanel`, `QuickOpen`, `SystemDialog`).
* **Resolution:** Embed the full 19-item `DesktopSemanticZone` table and `ToMacroZone()` projection rules directly into `UI_AUTOMATION_STRUCTURES_REFERENCE.md`.

### 2.3 Drift D3: Dynamic App Discovery & Self-Healing Rule Engine
* **The Specification (`018`):** Formulated "Gap 1: Dynamic App Discovery" as a critical unresolved risk, warning against the "Hardcoded Selector Trap" and hypothesizing about an `app_definitions.json` schema.
* **The Ground Truth (`ADCE.Extraction`):** The gap is fully resolved in code:
  1. [`ArchetypeClassifier.cs`](../../src/ADCE.Extraction/Classifiers/ArchetypeClassifier.cs) categorizes active windows into 6 universal archetypes (`Unknown`, `ChromiumElectron`, `Gecko`, `WinUI3Xaml`, `ClassicWin32`, `CanvasToolkit`).
  2. [`SemanticRuleEngine.cs`](../../src/ADCE.Extraction/Rules/SemanticRuleEngine.cs) implements `ISemanticRuleEngine`, loading and persisting dynamic declarative rules to `%LOCALAPPDATA%\ADCE\semantic_rules.json` with support for prioritization, wildcard matches, and container path evaluation.
* **Resolution:** Update `REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md` Section 1 to mark Gap 1 as resolved by Milestones 4.5 and 5, documenting the active `ISemanticRuleEngine` contract.

### 2.4 Drift D4: Storage Architecture & SQLite Schema Blueprint
* **The Specification (`018` Section 3):** Outlined a proposed relational schema with 3 tables (`window_sessions`, `focus_transitions`, `tab_snapshots`) and suggested exploring DuckDB for columnar analytics.
* **The Ground Truth (`ADCE.Storage`):** Benchmarks and live stimulus testing revealed that splitting high-frequency desktop transitions across 3 relational tables incurred excessive write transaction overhead and lock contention during typing bursts. Production code in [`SqliteDesktopStateStore.cs`](../../src/ADCE.Storage/Database/SqliteDesktopStateStore.cs) implements:
  - An ultra-fast **L1 atomic memory cache** (`_cache.UpdateCurrentSnapshot()`) serving live MCP queries in `< 0.001 ms`.
  - A single denormalized **`desktop_snapshots`** table in **SQLite WAL mode** with indexed query columns (`timestamp_unix_ms`, `hwnd`, `process_name`, `focus_semantic_zone`, `pane_location`, `active_view`) and an immutable JSON payload (`snapshot_json`).
  - Decoupled asynchronous channel writing (`Channel<DesktopContextSnapshot>` with `DropOldest` bounded queue) and automatic periodic maintenance pruning.
* **Resolution:** Update `REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md` Section 3 to document the actual `desktop_snapshots` WAL architecture and record the empirical rationale for superseding the 3-table relational design.

### 2.5 Drift D5: Test Verification Metrics
* **The Specification (`docs/CONTEXT.md`):** Stated the automated test suite contained "136 unit tests".
* **The Ground Truth (`tests/`):** The active solution contains **263 passing unit tests**:
  - `ADCE.Core.Tests`: 98
  - `ADCE.Extraction.Tests`: 101
  - `ADCE.Daemon.Tests`: 28
  - `ADCE.Mcp.Tests`: 22
  - `ADCE.Storage.Tests`: 14
* **Resolution:** Reconcile `docs/CONTEXT.md` and related documentation headers to reflect the accurate 263-test baseline.

---

## 3. Implemented Synchronization Actions

1. **`REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md`:**
   - Standardized status header added: marked as Active Tier 2 Specification reconciled with Milestone 6.
   - Section 1 Knowledge Gaps updated with resolution status.
   - Section 2 updated with the 6 `DesktopAppArchetype` values and `ISemanticRuleEngine` contract.
   - Section 3 updated with the `desktop_snapshots` SQLite WAL schema.
2. **`UI_AUTOMATION_STRUCTURES_REFERENCE.md`:**
   - Standardized status header added: marked as Active Tier 2 Reference.
   - Master Target Directory augmented with the complete 19-entry `DesktopSemanticZone` matrix and `WindowPaneLocation` layout anchors.
3. **Test Decoupling Verified:**
   - `ADCE.Extraction.Tests` completely decoupled from `ADCE.Spikes` and `ADCE.Daemon`, allowing live background testing without assembly locking.
