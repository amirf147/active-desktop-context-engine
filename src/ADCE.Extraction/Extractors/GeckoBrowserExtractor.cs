// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ADCE.Core.Models;
using ADCE.Extraction.Security;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace ADCE.Extraction.Extractors;

/// <summary>
/// High-speed scoped multi-zone extractor for Gecko browsers (Waterfox, Firefox).
/// Supports Tree Style Tab and native tabstrips with strict DOM viewport pruning.
/// </summary>
public static class GeckoBrowserExtractor
{
    public static BrowserContext Extract(AutomationElement windowElement, UIA3Automation automation)
    {
        var cf = automation.ConditionFactory;
        string containerType = "Unknown";
        var tabsBuilder = ImmutableArray.CreateBuilder<TabItemInfo>();
        string? activeTabTitle = null;

        // 1. Probe for Tree Style Tab Normal Tabs
        var tstContainer = windowElement.FindFirstDescendant(cf.ByClassName("tabs normal")) ??
                           windowElement.FindAllDescendants(cf.ByControlType(ControlType.List))
                                        .FirstOrDefault(l => (l.Properties.ClassName.ValueOrDefault ?? string.Empty).Contains("tabs", StringComparison.OrdinalIgnoreCase));
        if (tstContainer != null)
        {
            containerType = "TreeStyleTab";
            var cacheRequest = new CacheRequest();
            cacheRequest.AutomationElementMode = AutomationElementMode.None;
            cacheRequest.TreeScope = TreeScope.Children;
            cacheRequest.Properties.Add(automation.PropertyLibrary.Element.Name);
            cacheRequest.Patterns.Add(automation.PatternLibrary.SelectionItemPattern);

            using (cacheRequest.Activate())
            {
                var tabElements = tstContainer.FindAllChildren(cf.ByControlType(ControlType.ListItem));
                if (tabElements.Length == 0)
                {
                    tabElements = tstContainer.FindAllChildren(cf.ByControlType(ControlType.TabItem));
                }
                if (tabElements.Length == 0)
                {
                    tabElements = tstContainer.FindAllChildren();
                }

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
                        activeTabTitle = name;
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
        else
        {
            // 2. Probe for Native Firefox Tabstrip
            var nativeTabstrip = windowElement.FindFirstDescendant(cf.ByClassName("tabbrowser-tabs")) ??
                                 windowElement.FindFirstDescendant(cf.ByControlType(ControlType.Tab));
            if (nativeTabstrip != null)
            {
                containerType = "NativeTabstrip";
                var cacheRequest = new CacheRequest();
                cacheRequest.AutomationElementMode = AutomationElementMode.None;
                cacheRequest.TreeScope = TreeScope.Children;
                cacheRequest.Properties.Add(automation.PropertyLibrary.Element.Name);
                cacheRequest.Patterns.Add(automation.PatternLibrary.SelectionItemPattern);

                using (cacheRequest.Activate())
                {
                    var tabElements = nativeTabstrip.FindAllChildren(cf.ByControlType(ControlType.TabItem));
                    if (tabElements.Length == 0)
                    {
                        tabElements = nativeTabstrip.FindAllChildren();
                    }

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
                            activeTabTitle = name;
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
        }

        // 3. Extract and Sanitize Address Bar URL
        string? rawUrl = null;
        var urlEdit = windowElement.FindFirstDescendant(cf.ByAutomationId("urlbar-input"));
        if (urlEdit != null)
        {
            try
            {
                rawUrl = urlEdit.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
            }
            catch { }

            if (string.IsNullOrEmpty(rawUrl))
            {
                rawUrl = urlEdit.Properties.Name.ValueOrDefault;
            }
        }

        string sanitizedUrl = ContextPrivacySanitizer.SanitizeUrl(rawUrl);

        return new BrowserContext
        {
            ContainerType = containerType,
            TotalCount = tabsBuilder.Count,
            ActiveTab = activeTabTitle ?? tabsBuilder.FirstOrDefault(t => t.IsActive)?.Title,
            UrlAddress = sanitizedUrl,
            Tabs = tabsBuilder.ToImmutable()
        };
    }
}
