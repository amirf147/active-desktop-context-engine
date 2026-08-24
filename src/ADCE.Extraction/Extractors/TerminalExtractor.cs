// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ADCE.Core.Models;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace ADCE.Extraction.Extractors;

/// <summary>
/// Context extractor for Windows Terminal (Cascadia) and classic console windows.
/// </summary>
public static class TerminalExtractor
{
    public static TerminalContext Extract(AutomationElement windowElement, UIA3Automation automation)
    {
        var cf = automation.ConditionFactory;
        var tabsBuilder = ImmutableArray.CreateBuilder<TabItemInfo>();
        string? activeTitle = null;

        var tabContainer = windowElement.FindFirstDescendant(cf.ByControlType(ControlType.Tab));
        if (tabContainer != null)
        {
            var cacheRequest = new CacheRequest();
            cacheRequest.AutomationElementMode = AutomationElementMode.None;
            cacheRequest.TreeScope = TreeScope.Children;
            cacheRequest.Properties.Add(automation.PropertyLibrary.Element.Name);
            cacheRequest.Patterns.Add(automation.PatternLibrary.SelectionItemPattern);

            using (cacheRequest.Activate())
            {
                var tabElements = tabContainer.FindAllChildren(cf.ByControlType(ControlType.TabItem));
                int index = 1;
                foreach (var tab in tabElements)
                {
                    string name = tab.Properties.Name.ValueOrDefault ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    bool isSelected = false;
                    try
                    {
                        var pattern = tab.Patterns.SelectionItem.PatternOrDefault;
                        isSelected = pattern?.IsSelected.ValueOrDefault ?? false;
                    }
                    catch { }

                    if (isSelected)
                    {
                        activeTitle = name;
                    }

                    tabsBuilder.Add(new TabItemInfo
                    {
                        Index = index++,
                        Title = name,
                        IsActive = isSelected,
                        IsPinned = false
                    });
                }
            }
        }

        return new TerminalContext
        {
            ShellTitle = activeTitle ?? windowElement.Properties.Name.ValueOrDefault,
            ActiveBuffer = null, // Terminal buffer extraction deferred to focused element
            Tabs = tabsBuilder.ToImmutable()
        };
    }
}
