// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

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
/// High-speed scoped multi-zone extractor for Monaco/Electron IDEs (VS Code, Antigravity, Cursor).
/// Uses single-roundtrip FlaUI CacheRequest with zero DOM crawling.
/// </summary>
public static class MonacoIdeExtractor
{
    public static IdeContext Extract(AutomationElement windowElement, UIA3Automation automation)
    {
        var cf = automation.ConditionFactory;

        // 1. Extract Open Editor Tabs via Scoped CacheRequest (bounded, zero full-tree DOM crawl)
        var tabsBuilder = ImmutableArray.CreateBuilder<TabItemInfo>();
        var tabContainer = windowElement.FindFirstDescendant(cf.ByClassName("tabs-container")) ??
                           windowElement.FindFirstDescendant(cf.ByControlType(ControlType.Tab));

        if (tabContainer != null)
        {
            var cacheRequest = new CacheRequest();
            cacheRequest.AutomationElementMode = AutomationElementMode.None;
            cacheRequest.TreeScope = TreeScope.Children;
            cacheRequest.Properties.Add(automation.PropertyLibrary.Element.Name);
            cacheRequest.Properties.Add(automation.PropertyLibrary.Element.ClassName);
            cacheRequest.Patterns.Add(automation.PatternLibrary.SelectionItemPattern);

            using (cacheRequest.Activate())
            {
                var tabElements = tabContainer.FindAllChildren(cf.ByControlType(ControlType.TabItem));
                if (tabElements.Length == 0)
                {
                    tabElements = tabContainer.FindAllChildren();
                }
                int index = 1;
                foreach (var tab in tabElements)
                {
                    string name = tab.Properties.Name.ValueOrDefault ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string cls = tab.Properties.ClassName.ValueOrDefault ?? string.Empty;
                    bool isSelected = false;
                    try
                    {
                        var pattern = tab.Patterns.SelectionItem.PatternOrDefault;
                        isSelected = pattern?.IsSelected.ValueOrDefault ?? false;
                    }
                    catch
                    {
                        isSelected = cls.Contains("active", StringComparison.OrdinalIgnoreCase) ||
                                     cls.Contains("selected", StringComparison.OrdinalIgnoreCase);
                    }

                    if (!isSelected && (cls.Contains("active", StringComparison.OrdinalIgnoreCase) ||
                                        cls.Contains("selected", StringComparison.OrdinalIgnoreCase)))
                    {
                        isSelected = true;
                    }

                    bool isDirty = name.StartsWith("● ") || name.EndsWith(", preview", StringComparison.OrdinalIgnoreCase);
                    bool isPinned = cls.Contains("pinned", StringComparison.OrdinalIgnoreCase);

                    string cleanTitle = name.StartsWith("● ") ? name[2..] : name;
                    if (cleanTitle.EndsWith(", preview", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanTitle = cleanTitle[..^9];
                    }

                    tabsBuilder.Add(new TabItemInfo
                    {
                        Index = index++,
                        Title = cleanTitle,
                        IsActive = isSelected,
                        IsPinned = isPinned,
                        IsDirty = isDirty
                    });
                }
            }
        }

        // 2. Extract Monaco Breadcrumbs Path
        var breadcrumbsBuilder = ImmutableArray.CreateBuilder<string>();
        var breadcrumbsList = windowElement.FindFirstDescendant(cf.ByClassName("monaco-breadcrumbs"));
        if (breadcrumbsList != null)
        {
            var cacheRequest = new CacheRequest();
            cacheRequest.AutomationElementMode = AutomationElementMode.None;
            cacheRequest.TreeScope = TreeScope.Children;
            cacheRequest.Properties.Add(automation.PropertyLibrary.Element.Name);

            using (cacheRequest.Activate())
            {
                var items = breadcrumbsList.FindAllChildren(cf.ByControlType(ControlType.ListItem));
                foreach (var item in items)
                {
                    string part = item.Properties.Name.ValueOrDefault ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        breadcrumbsBuilder.Add(part);
                    }
                }
            }
        }

        // 3. Active Sidebar View
        string? activeSidebar = null;
        var activityBar = windowElement.FindFirstDescendant(cf.ByClassName("actions-container"));
        if (activityBar != null)
        {
            var activeViewItem = activityBar.FindAllChildren()
                .FirstOrDefault(item => (item.Properties.ClassName.ValueOrDefault ?? string.Empty)
                    .Contains("checked", StringComparison.OrdinalIgnoreCase));

            activeSidebar = activeViewItem?.Properties.Name.ValueOrDefault;
        }

        // 4. Active File Path resolution from breadcrumbs or active tab
        string? activeFilePath = null;
        if (breadcrumbsBuilder.Count > 0)
        {
            activeFilePath = string.Join('/', breadcrumbsBuilder);
        }
        else
        {
            var activeTab = tabsBuilder.FirstOrDefault(t => t.IsActive);
            activeFilePath = activeTab?.Title;
        }

        // 5. Workspace Root resolution from window title (e.g. "repo-name - Antigravity IDE" -> "repo-name")
        string? workspaceRoot = null;
        string windowTitle = windowElement.Properties.Name.ValueOrDefault ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            int dashIdx = windowTitle.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dashIdx > 0)
            {
                string candidate = windowTitle[..dashIdx].Trim();
                int secondDash = candidate.LastIndexOf(" - ", StringComparison.Ordinal);
                workspaceRoot = secondDash > 0 ? candidate[(secondDash + 3)..].Trim() : candidate;
            }
        }

        // 6. Diff Editor detection
        bool isDiffEditor = tabsBuilder.Any(t => t.IsActive && (t.Title.Contains("(Working Tree)", StringComparison.OrdinalIgnoreCase) ||
                                                                t.Title.Contains("(Index)", StringComparison.OrdinalIgnoreCase) ||
                                                                t.Title.Contains("↔", StringComparison.OrdinalIgnoreCase)));

        return new IdeContext
        {
            WorkspaceRoot = workspaceRoot,
            ActiveFilePath = activeFilePath,
            ActiveSidebarView = activeSidebar,
            IsDiffEditor = isDiffEditor,
            EditBuffer = activeFilePath,
            Breadcrumbs = breadcrumbsBuilder.ToImmutable(),
            OpenEditorTabs = tabsBuilder.ToImmutable()
        };
    }
}
