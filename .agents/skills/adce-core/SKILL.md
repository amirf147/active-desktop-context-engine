---
name: adce-core
description: Core architectural reference, UIA 3 caching patterns, and MCP schemas for building the Active Desktop Context Engine (ADCE) in .NET 10 and FlaUI 5.
---
<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# Active Desktop Context Engine (ADCE) — Core Architectural Skill

Use this skill when developing, refactoring, or testing the C# desktop context engine and MCP daemon.

## 1. Technical Stack & Dependencies
* **Framework:** `.NET 10 (LTS)` (`<TargetFramework>net10.0-windows</TargetFramework>`)
* **UIA Library:** `FlaUI.UIA3` (v5.0.0+)
* **IPC/Server:** Model Context Protocol (MCP) C# SDK / ASP.NET Minimal API SSE

## 2. Core Operational Constraints
1. **Never Crawl Browser DOMs:** Never use recursive unpruned tree walks on `MozillaWindowClass` or `Chrome_WidgetWin_1`. Always use `CacheRequest` scoped to `TreeScope.Children` on the tabstrip container.
2. **Decouple Event Hooks from UIA:** All OS WinEvent callbacks must push tokens to a `Channel<T>` and exit immediately. UIA inspection runs on an MTA background worker.
3. **Trailing-Edge Debouncing:** Wait 50–75ms after the latest focus event before querying UIA to ensure the target window has settled.
4. **Self-Monitoring Filtering:** Ignore `GetConsoleWindow()`, own PID, and terminal processes (`WindowsTerminal.exe`, `conhost.exe`).

## 3. High-Performance FlaUI Tab Extraction Pattern
```csharp
public static List<TabItemModel> ExtractTabs(AutomationElement topWindow, string className)
{
    var tabs = new List<TabItemModel>();
    if (topWindow == null) return tabs;

    var cacheRequest = new CacheRequest { TreeScope = TreeScope.Children };
    cacheRequest.AddProperty(AutomationObjectIds.NameProperty);
    cacheRequest.AddPattern(AutomationObjectIds.SelectionItemPatternId);

    // Locate Tab Container directly
    var tabContainer = topWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
    if (tabContainer == null) return tabs;

    using (cacheRequest.Activate())
    {
        var tabElements = tabContainer.FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
        foreach (var tab in tabElements)
        {
            var isSelected = tab.Patterns.SelectionItem.PatternOrDefault?.IsSelected.Value ?? false;
            tabs.Add(new TabItemModel {
                Title = tab.Properties.Name.Value,
                IsSelected = isSelected
            });
        }
    }
    return tabs;
}
```

## 4. Documentation Strategy & Path Portability
* **Foundational Reference:** Historical research, telemetry benchmarks, and architectural investigations live in [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp).
* **Clean Boundaries:** This repo operates as a standalone C# system; historical documentation provides context on why certain UIA patterns and debounce parameters were chosen.
