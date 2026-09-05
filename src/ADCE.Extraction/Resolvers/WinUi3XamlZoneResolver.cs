// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Linq;
using ADCE.Core.Enums;
using ADCE.Extraction.Models;

namespace ADCE.Extraction.Resolvers;

/// <summary>
/// Dedicated semantic zone and layout pane resolver for modern Windows WinUI 3,
/// XAML Islands, and Cascadia applications (Windows Terminal, Windows 11 Explorer).
/// </summary>
public sealed class WinUi3XamlZoneResolver : IArchetypeZoneResolver
{
    public static readonly WinUi3XamlZoneResolver Instance = new();

    public DesktopAppArchetype SupportedArchetype => DesktopAppArchetype.WinUI3Xaml;

    public bool TryResolve(
        FocusedControlDescriptor control,
        AncestorChain ancestors,
        out SemanticResolution resolution)
    {
        string cType = control.ControlType;
        string name = control.Name;
        string autoId = control.AutomationId;
        string cls = control.ClassName;
        var classes = ancestors.ContainerClasses;

        // 1. Command Palette / Overlay Flyout (highest precedence to capture modal controls)
        if (control.IsOverlay ||
            autoId.Contains("command-palette", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("CommandPalette", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("PaletteControl", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("CommandPalette", StringComparison.OrdinalIgnoreCase) ||
            ancestors.HasId("CommandPalette") ||
            ancestors.HasClass("PaletteControl") ||
            ancestors.HasClass("CommandPalette"))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.CommandPalette,
                WindowPaneLocation.OverlayModal,
                "CommandPalette",
                null);
            return true;
        }

        // 2. Tab Items / TabView / TabStrip (including New Tab button)
        if (cType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) ||
            cType.Equals("Tab", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("TabViewItem", StringComparison.OrdinalIgnoreCase) ||
            autoId.Equals("NewTabButton", StringComparison.OrdinalIgnoreCase) ||
            autoId.Equals("AddButton", StringComparison.OrdinalIgnoreCase) ||
            ancestors.HasId("TabView") ||
            ancestors.HasId("TabListView"))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.TabBar,
                WindowPaneLocation.TopBar,
                "TabStrip",
                null);
            return true;
        }

        // 3. Settings View / Configuration Panel
        if (cls.Contains("SettingsControl", StringComparison.OrdinalIgnoreCase) ||
            ancestors.HasClass("SettingsControl") ||
            ancestors.HasClass("SettingsPage") ||
            ancestors.HasId("Settings"))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.NavigationPanel,
                WindowPaneLocation.MainContent,
                "Settings",
                null);
            return true;
        }

        // 4. Terminal Console Buffer Viewport
        if (cls.Equals("TermControl", StringComparison.OrdinalIgnoreCase) ||
            classes.Any(c => c.Equals("TermControl", StringComparison.OrdinalIgnoreCase)) ||
            autoId.Contains("terminal", StringComparison.OrdinalIgnoreCase))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.Terminal,
                WindowPaneLocation.MainContent,
                "Terminal",
                null);
            return true;
        }

        // 5. Windows Explorer Shell Items View
        if (cType.Equals("ListItem", StringComparison.OrdinalIgnoreCase) ||
            cType.Equals("List", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("ItemsView", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("UIItemsView", StringComparison.OrdinalIgnoreCase))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.ShellItemList,
                WindowPaneLocation.MainContent,
                "ShellItems",
                null);
            return true;
        }

        // 6. Windows Explorer Tree Navigation
        if (cType.Equals("TreeItem", StringComparison.OrdinalIgnoreCase) ||
            cType.Equals("Tree", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("NamespaceTreeControl", StringComparison.OrdinalIgnoreCase))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.SidebarExplorer,
                WindowPaneLocation.PrimarySidebar,
                "Explorer",
                null);
            return true;
        }

        resolution = SemanticResolution.Unresolved;
        return false;
    }
}
