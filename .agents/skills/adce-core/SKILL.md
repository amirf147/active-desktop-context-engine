---
name: adce-core
description: Core architectural reference, FlaUI 5 UIA3 caching patterns, archetype extractors, and MCP contracts for the Active Desktop Context Engine (ADCE) in .NET 10.
---
<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# Active Desktop Context Engine (ADCE) — Core Architectural Skill

Use this skill when developing, refactoring, extending, or testing the Active Desktop Context Engine (ADCE) daemon, UIA extractors, storage subsystem, or Model Context Protocol (MCP) server.

---

## 1. Technical Stack & Environment
* **Framework:** `.NET 10 (LTS)` (`<TargetFramework>net10.0-windows</TargetFramework>`)
* **Language Version:** C# 14 / preview (`<LangVersion>preview</LangVersion>`)
* **UIA Stack:** `FlaUI.UIA3` (v5.0.0+) over native `UIAutomationCore.dll`
* **Storage Engine:** L1 Atomic In-Memory Snapshot + L2 Channel-Decoupled SQLite WAL (`desktop_snapshots`)
* **Protocol Distribution:** Model Context Protocol (MCP) JSON-RPC 2.0 + HTTP Server-Sent Events (SSE on `:8424`)

---

## 2. Architectural Layering & Perception Purity
ADCE maintains a strict separation of concerns between raw perception and semantic consumption:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              ADCE ARCHITECTURAL LAYERS                                 │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [ LAYER 0: CORE CONTEXT & TOPOLOGY PROVIDER ]                                          │
│ • Deterministic physical truth: HWND, PID, ProcessName, WindowTitle, Bounds            │
│ • Virtual Desktop GUID, Name, and Display Monitor coordinates                          │
│ • Application Envelopes: Browser tabs/URLs, IDE files/tabs, Terminal buffers           │
│ • Focused control leaf UIA properties, ancestor hierarchy chain, bounding box          │
│ • Sub-microsecond RAM atomic cache + background SQLite WAL time-series store           │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [ LAYER 1: RUNTIME CLASSIFIERS & ALIASING ]                                            │
│ • Dynamic JSON rules (%LOCALAPPDATA%\ADCE\semantic_rules.json)                         │
│ • Maps container paths to high-level semantic zones without polluting Layer 0          │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [ LAYER 2: DOWNSTREAM CONSUMERS & ACTORS ]                                             │
│ • Caster Dynamic Voice Grammars (Python Dragonfly rules switching on HWND/context)     │
│ • AI Coding Assistants & Agents via MCP Tools & Resources                              │
│ • Automation Drivers & Window Switchers targeting Win32 HWND directly                  │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. High-Performance FlaUI 5 Caching Patterns

### 3.1 Strict Invariant: Zero Unbounded DOM Crawling
Never perform unpruned recursive tree walks on web browsers (`MozillaWindowClass`, `Chrome_WidgetWin_1`) or Monaco IDE trees. Unbounded walks cause UI thread freezes (> 2,000 ms) and severe COM memory leaks.

### 3.2 Canonical FlaUI 5 CacheRequest Pattern
Always scope caching to specific containers and set `AutomationElementMode = AutomationElementMode.None` to avoid RCW overhead:

```csharp
var cacheRequest = new CacheRequest
{
    AutomationElementMode = AutomationElementMode.None,
    TreeScope = TreeScope.Children
};

// Register properties using PropertyLibrary
cacheRequest.Properties.Add(automation.PropertyLibrary.Element.Name);
cacheRequest.Properties.Add(automation.PropertyLibrary.Element.ClassName);

// Register patterns using PatternLibrary
cacheRequest.Patterns.Add(automation.PatternLibrary.SelectionItemPattern);

using (cacheRequest.Activate())
{
    // Queries inside using-block automatically populate the cache in a single COM roundtrip
    var tabElements = tabContainer.FindAllChildren(cf.ByControlType(ControlType.TabItem));
    foreach (var tab in tabElements)
    {
        // Always use ValueOrDefault to prevent COM exceptions on stale elements
        string title = tab.Properties.Name.ValueOrDefault ?? string.Empty;
        bool isSelected = tab.Patterns.SelectionItem.PatternOrDefault?.IsSelected.ValueOrDefault ?? false;

        tabsBuilder.Add(new TabItemInfo
        {
            Index = index++,
            Title = title,
            IsActive = isSelected,
            IsPinned = false
        });
    }
}
```

---

## 4. Specialized Archetype Extractors (`ADCE.Extraction`)

ADCE utilizes dedicated, single-roundtrip extractors located in `src/ADCE.Extraction/Extractors/`:

| Extractor | Target Applications | Key Extracted Fields | Performance |
| :--- | :--- | :--- | :--- |
| `GeckoBrowserExtractor` | Waterfox, Firefox | Tab titles, active tab, container type (`TreeStyleTab` vs `NativeTabstrip`), sanitized address bar URL (`ContextPrivacySanitizer.SanitizeUrl`) | < 5 ms |
| `MonacoIdeExtractor` | VS Code, Antigravity IDE, Cursor | Open editor tabs, Monaco breadcrumbs path, active sidebar view, active file path, workspace root folder | < 10 ms |
| `WinUIExplorerExtractor` | Windows 11 File Explorer | TabView tabs, breadcrumb bar path, selected items list from Items View | < 5 ms |
| `TerminalExtractor` | Windows Terminal (Cascadia), conhost | Terminal tab titles, shell process title, command buffer snippets | < 3 ms |

---

## 5. Model Context Protocol (MCP) Server (`ADCE.Mcp`)

`DesktopContextMcpHandler` exposes the following tools and resources over JSON-RPC 2.0:

### MCP Tools
* `get_desktop_context` (alias `get_active_context`):
  * `process_filter` (string, optional): Filter by process name (e.g. `'code'`, `'waterfox'`).
  * `projection` (string, optional): `'full'` (default), `'compact'` (omits bounding boxes), `'ide'` (IDE file/tabs only), `'terminal'` (shell/terminal only).
* `search_desktop_history`:
  * `query` (string, required): Keyword search across window titles, tab titles, and file paths.
  * `limit` (int, default 20, max 100): Maximum chronological results.
* `tag_active_control`:
  * `target_zone` (string, required): Semantic zone name (e.g. `GitCommitBox`, `EditorBuffer`, `Terminal`).
  * `target_pane`, `target_view`, `target_section`, `scope`, `comment`.

### MCP Resources
* `desktop://current`: Live JSON snapshot of the foreground desktop state.
* `desktop://history?minutes=15&limit=50`: Time-series focus transitions and window shifts.

---

## 6. Concurrency & Storage Invariants

1. **Decoupled Event Ingestion:**
   * WinEvent hooks (`EVENT_SYSTEM_FOREGROUND`, `EVENT_OBJECT_FOCUS`) execute inside an STA message loop.
   * Callbacks allocate a lightweight `DesktopEventToken` and push it to `Channel<DesktopEventToken>` immediately (< 0.2 ms), never invoking UIA directly.
2. **Debounce Gate:**
   * The pipeline debounces rapid events by 50–75 ms on the trailing edge to let the target window settle before UIA inspection begins.
3. **Dual-Tier Persistence:**
   * **Tier 1:** Atomic memory cache (`_currentSnapshot`) delivers sub-microsecond snapshot reads to callers.
   * **Tier 2:** Asynchronous SQLite WAL worker writes snapshots to `desktop_snapshots` with automatic retention pruning.
4. **Privacy Firewall:**
   * Password controls (`IsPassword == true`) and sensitive input fields are automatically replaced with `[REDACTED_PASSWORD]`.
   * URLs are sanitized to strip query parameters, session tokens, and credentials.

---

## 7. Change-Aware Verification Commands

Always run the intelligent test runner before finalizing changes:
```pwsh
# Runs change-aware Merkle-cached verification across 7 domains
python scripts/test_runner.py

# Verify path hygiene and secret scanning
python scripts/check_repo_safety.py

# Verify all markdown links resolve
python scripts/verify_markdown_links.py
```
