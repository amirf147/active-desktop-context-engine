// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Collections.Immutable;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using ADCE.Extraction.Models;
using ADCE.Extraction.Resolvers;
using Xunit;

namespace ADCE.Extraction.Tests;

public class WindowsTerminalClassificationTests
{
    [Theory]
    [InlineData("TabItem", "PowerShell", "", "ListViewItem", DesktopSemanticZone.TabBar, WindowPaneLocation.TopBar, "TabStrip")]
    [InlineData("SplitButton", "New Tab", "NewTabButton", "Microsoft.UI.Xaml.Controls.SplitButton", DesktopSemanticZone.TabBar, WindowPaneLocation.TopBar, "TabStrip")]
    [InlineData("Button", "Add Tab", "AddButton", "Button", DesktopSemanticZone.TabBar, WindowPaneLocation.TopBar, "TabStrip")]
    [InlineData("Text", "PowerShell", "", "TermControl", DesktopSemanticZone.Terminal, WindowPaneLocation.MainContent, "Terminal")]
    [InlineData("Custom", "Terminal Viewport", "terminal-pane", "TermControl", DesktopSemanticZone.Terminal, WindowPaneLocation.MainContent, "Terminal")]
    [InlineData("Edit", "Type a command name...", "CommandPaletteControl", "PaletteControl", DesktopSemanticZone.CommandPalette, WindowPaneLocation.OverlayModal, "CommandPalette")]
    [InlineData("Custom", "Settings", "SettingsView", "SettingsControl", DesktopSemanticZone.NavigationPanel, WindowPaneLocation.MainContent, "Settings")]
    public void WindowsTerminal_Resolver_ResolvesExpectedZones(
        string cType, string name, string autoId, string className,
        DesktopSemanticZone expectedZone, WindowPaneLocation expectedPane, string expectedView)
    {
        var descriptor = new FocusedControlDescriptor(cType, name, autoId, className, BoundingRectangle.Empty, false);
        bool resolved = WinUi3XamlZoneResolver.Instance.TryResolve(descriptor, AncestorChain.Empty, out var res);

        Assert.True(resolved);
        Assert.Equal(expectedZone, res.Zone);
        Assert.Equal(expectedPane, res.Pane);
        Assert.Equal(expectedView, res.ActiveView);
    }

    [Fact]
    public void WindowsTerminal_InnerElementWithTabViewAncestor_ResolvesToTabBar()
    {
        var descriptor = new FocusedControlDescriptor("Button", "Close Tab", "CloseButton", "Button", BoundingRectangle.Empty, false);
        var ancestors = new AncestorChain(
            ImmutableArray.Create("TabView", "TabListView"),
            ImmutableArray.Create("ListView", "TabView"),
            ImmutableArray<AncestorNode>.Empty);

        bool resolved = WinUi3XamlZoneResolver.Instance.TryResolve(descriptor, ancestors, out var res);

        Assert.True(resolved);
        Assert.Equal(DesktopSemanticZone.TabBar, res.Zone);
        Assert.Equal(WindowPaneLocation.TopBar, res.Pane);
        Assert.Equal("TabStrip", res.ActiveView);
    }

    [Fact]
    public void WindowsTerminal_InnerElementWithCommandPaletteAncestor_ResolvesToCommandPalette()
    {
        var descriptor = new FocusedControlDescriptor("ListItem", "New Tab", "", "ListViewItem", BoundingRectangle.Empty, false);
        var ancestors = new AncestorChain(
            ImmutableArray.Create("CommandPaletteControl", "FilteredCommandList"),
            ImmutableArray.Create("CommandPalette", "ListView"),
            ImmutableArray<AncestorNode>.Empty);

        bool resolved = WinUi3XamlZoneResolver.Instance.TryResolve(descriptor, ancestors, out var res);

        Assert.True(resolved);
        Assert.Equal(DesktopSemanticZone.CommandPalette, res.Zone);
        Assert.Equal(WindowPaneLocation.OverlayModal, res.Pane);
        Assert.Equal("CommandPalette", res.ActiveView);
    }

    [Fact]
    public void WindowsTerminal_InnerElementWithSettingsAncestor_ResolvesToSettings()
    {
        var descriptor = new FocusedControlDescriptor("Edit", "Starting directory", "StartingDirectoryTextBox", "TextBox", BoundingRectangle.Empty, false);
        var ancestors = new AncestorChain(
            ImmutableArray.Create("SettingsView", "GeneralSettingsPage"),
            ImmutableArray.Create("SettingsControl", "Page"),
            ImmutableArray<AncestorNode>.Empty);

        bool resolved = WinUi3XamlZoneResolver.Instance.TryResolve(descriptor, ancestors, out var res);

        Assert.True(resolved);
        Assert.Equal(DesktopSemanticZone.NavigationPanel, res.Zone);
        Assert.Equal(WindowPaneLocation.MainContent, res.Pane);
        Assert.Equal("Settings", res.ActiveView);
    }
}
