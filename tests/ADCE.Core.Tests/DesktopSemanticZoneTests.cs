// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using ADCE.Core.Enums;
using Xunit;

namespace ADCE.Core.Tests;

public class DesktopSemanticZoneTests
{
    [Theory]
    [InlineData(DesktopSemanticZone.EditorBuffer, DesktopSemanticZone.EditorBuffer)]
    [InlineData(DesktopSemanticZone.GitCommitBox, DesktopSemanticZone.EditorBuffer)]
    [InlineData(DesktopSemanticZone.Terminal, DesktopSemanticZone.Terminal)]
    [InlineData(DesktopSemanticZone.ChatPrompt, DesktopSemanticZone.ChatPrompt)]
    [InlineData(DesktopSemanticZone.WebDocument, DesktopSemanticZone.WebDocument)]
    [InlineData(DesktopSemanticZone.SidebarExplorer, DesktopSemanticZone.NavigationPanel)]
    [InlineData(DesktopSemanticZone.ShellItemList, DesktopSemanticZone.NavigationPanel)]
    [InlineData(DesktopSemanticZone.TabBar, DesktopSemanticZone.NavigationPanel)]
    [InlineData(DesktopSemanticZone.StatusBar, DesktopSemanticZone.NavigationPanel)]
    [InlineData(DesktopSemanticZone.NavigationPanel, DesktopSemanticZone.NavigationPanel)]
    [InlineData(DesktopSemanticZone.AddressBar, DesktopSemanticZone.QuickOpen)]
    [InlineData(DesktopSemanticZone.CommandPalette, DesktopSemanticZone.QuickOpen)]
    [InlineData(DesktopSemanticZone.QuickOpen, DesktopSemanticZone.QuickOpen)]
    [InlineData(DesktopSemanticZone.SystemDialog, DesktopSemanticZone.SystemDialog)]
    [InlineData(DesktopSemanticZone.Unknown, DesktopSemanticZone.Unknown)]
    public void ToMacroZone_ProjectsCorrectly(DesktopSemanticZone granular, DesktopSemanticZone expectedMacro)
    {
        var actual = granular.ToMacroZone();
        Assert.Equal(expectedMacro, actual);
    }

    [Fact]
    public void CanonicalValues_AreDistinct()
    {
        var values = (DesktopSemanticZone[])Enum.GetValues(typeof(DesktopSemanticZone));
        var intValues = values.Select(v => (int)v).ToList();
        var distinctIntValues = intValues.Distinct().ToList();

        Assert.Equal(intValues.Count, distinctIntValues.Count);
    }
}
