// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Linq;
using ADCE.Core.Enums;
using ADCE.Extraction.Models;

namespace ADCE.Extraction.Resolvers;

/// <summary>
/// Dedicated semantic zone and layout pane resolver for Mozilla Gecko applications
/// (Waterfox, Firefox, Thunderbird).
/// </summary>
public sealed class GeckoZoneResolver : IArchetypeZoneResolver
{
    public static readonly GeckoZoneResolver Instance = new();

    public DesktopAppArchetype SupportedArchetype => DesktopAppArchetype.Gecko;

    public bool TryResolve(
        FocusedControlDescriptor control,
        AncestorChain ancestors,
        out SemanticResolution resolution)
    {
        string cType = control.ControlType;
        string name = control.Name;
        string autoId = control.AutomationId;
        string cls = control.ClassName;
        var paths = ancestors.ContainerPaths;
        var classes = ancestors.ContainerClasses;

        // 1. Address Bar / URL Input Box
        if (autoId.Contains("urlbar-input", StringComparison.OrdinalIgnoreCase) ||
            autoId.Equals("urlbar", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Search with Google or enter address", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Address and search bar", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Equals("urlbar", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.AddressBar,
                WindowPaneLocation.TopBar,
                "NavigationBar",
                null);
            return true;
        }

        // 2. Navigation Action Buttons & Bookmarks Toolbar
        if (autoId.Contains("back-button", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("forward-button", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("reload-button", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Contains("nav-bar", StringComparison.OrdinalIgnoreCase) ||
                           p.Contains("navigator-toolbox", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.NavigationPanel,
                WindowPaneLocation.TopBar,
                "NavigationBar",
                null);
            return true;
        }

        if (autoId.Contains("PersonalToolbar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("PlacesToolbar", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Contains("PersonalToolbar", StringComparison.OrdinalIgnoreCase) ||
                           p.Contains("PlacesToolbar", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.NavigationPanel,
                WindowPaneLocation.TopBar,
                "BookmarksToolbar",
                null);
            return true;
        }

        // 3. Browser Tab Strip
        if (autoId.Contains("tabbrowser-tab", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("tabbrowser-tab", StringComparison.OrdinalIgnoreCase) ||
            cType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Contains("TabsToolbar", StringComparison.OrdinalIgnoreCase) ||
                           p.Contains("tabbrowser-tabs", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.TabBar,
                WindowPaneLocation.TopBar,
                "TabStrip",
                null);
            return true;
        }

        // 4. In-Browser Sidebar (e.g. Tree Style Tab, History, Bookmarks)
        if (autoId.Contains("sidebar-box", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("sidebar-header", StringComparison.OrdinalIgnoreCase) ||
            autoId.Equals("sidebar", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("sidebar-box", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Contains("sidebar-box", StringComparison.OrdinalIgnoreCase)))
        {
            // Vertical tabs inside browser sidebar must be TabBar, not SidebarExplorer
            if (cls.Contains("tab", StringComparison.OrdinalIgnoreCase) || autoId.Contains("tab", StringComparison.OrdinalIgnoreCase))
            {
                resolution = new SemanticResolution(
                    DesktopSemanticZone.TabBar,
                    WindowPaneLocation.PrimarySidebar,
                    "SidebarTabs",
                    null);
                return true;
            }

            // Web documents inside webextension sidebar
            if (cType.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
                cls.Contains("webextension-panel", StringComparison.OrdinalIgnoreCase))
            {
                resolution = new SemanticResolution(
                    DesktopSemanticZone.WebDocument,
                    WindowPaneLocation.PrimarySidebar,
                    "SidebarWebDocument",
                    null);
                return true;
            }

            resolution = new SemanticResolution(
                DesktopSemanticZone.SidebarExplorer,
                WindowPaneLocation.PrimarySidebar,
                "Sidebar",
                null);
            return true;
        }

        // 5. Rendered Web Document Viewport (including in-page DOM elements)
        if (cType.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase) ||
            classes.Any(c => c.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase)) ||
            paths.Any(p => p.Contains("tabbrowser-tabpanels", StringComparison.OrdinalIgnoreCase) ||
                           p.Contains("appcontent", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.WebDocument,
                WindowPaneLocation.MainContent,
                "WebDocument",
                null);
            return true;
        }

        resolution = SemanticResolution.Unresolved;
        return false;
    }
}
