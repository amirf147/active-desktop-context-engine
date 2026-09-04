// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Linq;
using ADCE.Core.Enums;
using Xunit;

namespace ADCE.Core.Tests;

public class WindowPaneLocationTests
{
    [Theory]
    [InlineData(WindowPaneLocation.ActivityBar, "activity_bar")]
    [InlineData(WindowPaneLocation.PrimarySidebar, "primary_sidebar")]
    [InlineData(WindowPaneLocation.MainContent, "main_content")]
    [InlineData(WindowPaneLocation.AuxiliarySidebar, "auxiliary_sidebar")]
    [InlineData(WindowPaneLocation.BottomPanel, "bottom_panel")]
    [InlineData(WindowPaneLocation.TopBar, "top_bar")]
    [InlineData(WindowPaneLocation.StatusBar, "status_bar")]
    [InlineData(WindowPaneLocation.OverlayModal, "overlay_modal")]
    [InlineData(WindowPaneLocation.Unknown, "unknown")]
    public void ToSnakeCase_ReturnsExpectedFormat(WindowPaneLocation pane, string expected)
    {
        Assert.Equal(expected, pane.ToSnakeCase());
    }

    [Theory]
    [InlineData("ActivityBar", WindowPaneLocation.ActivityBar)]
    [InlineData("activity_bar", WindowPaneLocation.ActivityBar)]
    [InlineData("activity", WindowPaneLocation.ActivityBar)]
    [InlineData("PrimarySidebar", WindowPaneLocation.PrimarySidebar)]
    [InlineData("primary_sidebar", WindowPaneLocation.PrimarySidebar)]
    [InlineData("sidebar", WindowPaneLocation.PrimarySidebar)]
    [InlineData("leftsidebar", WindowPaneLocation.PrimarySidebar)]
    [InlineData("MainContent", WindowPaneLocation.MainContent)]
    [InlineData("main_content", WindowPaneLocation.MainContent)]
    [InlineData("editor", WindowPaneLocation.MainContent)]
    [InlineData("viewport", WindowPaneLocation.MainContent)]
    [InlineData("AuxiliarySidebar", WindowPaneLocation.AuxiliarySidebar)]
    [InlineData("auxiliary_sidebar", WindowPaneLocation.AuxiliarySidebar)]
    [InlineData("auxiliarybar", WindowPaneLocation.AuxiliarySidebar)]
    [InlineData("rightsidebar", WindowPaneLocation.AuxiliarySidebar)]
    [InlineData("assistant", WindowPaneLocation.AuxiliarySidebar)]
    [InlineData("chatpanel", WindowPaneLocation.AuxiliarySidebar)]
    [InlineData("BottomPanel", WindowPaneLocation.BottomPanel)]
    [InlineData("bottom_panel", WindowPaneLocation.BottomPanel)]
    [InlineData("terminalpanel", WindowPaneLocation.BottomPanel)]
    [InlineData("TopBar", WindowPaneLocation.TopBar)]
    [InlineData("top_bar", WindowPaneLocation.TopBar)]
    [InlineData("titlebar", WindowPaneLocation.TopBar)]
    [InlineData("StatusBar", WindowPaneLocation.StatusBar)]
    [InlineData("status_bar", WindowPaneLocation.StatusBar)]
    [InlineData("OverlayModal", WindowPaneLocation.OverlayModal)]
    [InlineData("overlay_modal", WindowPaneLocation.OverlayModal)]
    [InlineData("modal", WindowPaneLocation.OverlayModal)]
    [InlineData("palette", WindowPaneLocation.OverlayModal)]
    public void TryParseCanonical_ParsesKnownFormatsAndAliases(string input, WindowPaneLocation expected)
    {
        bool success = WindowPaneLocationExtensions.TryParseCanonical(input, out var parsed);
        Assert.True(success);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void CanonicalValues_AreDistinct()
    {
        var values = (WindowPaneLocation[])Enum.GetValues(typeof(WindowPaneLocation));
        var intValues = values.Select(v => (int)v).ToList();
        var distinctIntValues = intValues.Distinct().ToList();

        Assert.Equal(intValues.Count, distinctIntValues.Count);
    }

    [Theory]
    [InlineData(DesktopSemanticZone.GitCommitBox, WindowPaneLocation.PrimarySidebar, "SourceControl", "CommitBox")]
    [InlineData(DesktopSemanticZone.Timeline, WindowPaneLocation.PrimarySidebar, "Explorer", "Timeline")]
    [InlineData(DesktopSemanticZone.Outline, WindowPaneLocation.PrimarySidebar, "Explorer", "Outline")]
    [InlineData(DesktopSemanticZone.SidebarExplorer, WindowPaneLocation.PrimarySidebar, "Explorer", null)]
    [InlineData(DesktopSemanticZone.ChatPrompt, WindowPaneLocation.AuxiliarySidebar, "Chat", "ChatPrompt")]
    [InlineData(DesktopSemanticZone.ChatConversation, WindowPaneLocation.AuxiliarySidebar, "Chat", "Conversation")]
    [InlineData(DesktopSemanticZone.EditorBuffer, WindowPaneLocation.MainContent, "Editor", null)]
    [InlineData(DesktopSemanticZone.Terminal, WindowPaneLocation.BottomPanel, "Terminal", null)]
    [InlineData(DesktopSemanticZone.ActivityBar, WindowPaneLocation.ActivityBar, "ActivityBar", null)]
    [InlineData(DesktopSemanticZone.StatusBar, WindowPaneLocation.StatusBar, "StatusBar", null)]
    [InlineData(DesktopSemanticZone.QuickOpen, WindowPaneLocation.OverlayModal, "QuickOpen", null)]
    public void DesktopSemanticZone_InfersExpectedDefaults(
        DesktopSemanticZone zone, WindowPaneLocation expectedPane, string? expectedView, string? expectedSection)
    {
        Assert.Equal(expectedPane, zone.ToDefaultPaneLocation());
        Assert.Equal(expectedView, zone.ToDefaultView());
        Assert.Equal(expectedSection, zone.ToDefaultSection());
    }
}
