// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Extraction.Engine;
using Xunit;

namespace ADCE.Extraction.Tests;

public class UiaExtractionEngineTests
{
    [Fact]
    public async Task ExtractSnapshotAsync_HandlesInvalidHwndGracefully()
    {
        using var engine = new UiaExtractionEngine();
        var snapshot = await engine.ExtractSnapshotAsync(nint.Zero);

        Assert.NotNull(snapshot);
        Assert.Equal((nint)0, snapshot.Window.Hwnd);
        Assert.Equal(DesktopAppArchetype.Unknown, snapshot.Window.Archetype);
        Assert.Null(snapshot.IdeContext);
        Assert.Null(snapshot.BrowserContext);
        Assert.Null(snapshot.ExplorerContext);
        Assert.Null(snapshot.TerminalContext);
    }

    [Fact]
    public async Task ExtractForegroundSnapshotAsync_ReturnsValidSnapshotInstance()
    {
        using var engine = new UiaExtractionEngine();
        var snapshot = await engine.ExtractForegroundSnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Window);
        Assert.NotNull(snapshot.Workspace);
        Assert.NotNull(snapshot.Focus);
        Assert.True(snapshot.ExtractionDurationMs >= 0.0);
    }

    [Theory]
    [InlineData("Edit", "Message (Ctrl+Enter to commit)", "scm.input", "monaco-editor", DesktopAppArchetype.ChromiumElectron, false, DesktopSemanticZone.GitCommitBox)]
    [InlineData("Document", "Terminal 1", "workbench.action.terminal.focus", "xterm", DesktopAppArchetype.ChromiumElectron, false, DesktopSemanticZone.Terminal)]
    [InlineData("Edit", "Type a message", "chat-input", "interactive-session", DesktopAppArchetype.ChromiumElectron, false, DesktopSemanticZone.ChatPrompt)]
    [InlineData("Edit", "Search box", "quickInput", "quick-input-widget", DesktopAppArchetype.ChromiumElectron, true, DesktopSemanticZone.QuickOpen)]
    [InlineData("TreeItem", "Source Control", "workbench.view.scm", "monaco-list", DesktopAppArchetype.ChromiumElectron, false, DesktopSemanticZone.SidebarExplorer)]
    [InlineData("Document", "Mozilla Firefox", "urlbar-input", "MozillaWindowClass", DesktopAppArchetype.Gecko, false, DesktopSemanticZone.AddressBar)]
    public void ResolveSemanticZone_MapsToExpectedMacroAnchors(
        string cType, string name, string autoId, string className, DesktopAppArchetype archetype, bool isOverlay, DesktopSemanticZone expected)
    {
        var zone = UiaExtractionEngine.ResolveSemanticZone(cType, name, autoId, className, archetype, isOverlay);
        Assert.Equal(expected, zone);
    }

    [Fact]
    public void ExtractionEngine_CanToggleSemanticZones()
    {
        using var engine = new UiaExtractionEngine();
        Assert.True(engine.EnableSemanticZones);

        engine.EnableSemanticZones = false;
        Assert.False(engine.EnableSemanticZones);
    }
}
