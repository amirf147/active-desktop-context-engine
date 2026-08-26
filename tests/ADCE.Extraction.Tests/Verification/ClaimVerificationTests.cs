// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Extraction.Engine;
using ADCE.Spikes.Verification.Drivers;
using ADCE.Spikes.Verification.Models;
using Xunit;

namespace ADCE.Extraction.Tests.Verification;

/// <summary>
/// Automated CI unit test suite verifying the Claim Verification Matrix (CLM-001 through CLM-006).
/// </summary>
public class ClaimVerificationTests
{
    private readonly MockStimulusDriver _driver = new();

    [Fact]
    public async Task CLM_001_GlobalFocusBleedPrevention_FocusedPidMustMatchWindowPid()
    {
        var result = await _driver.VerifyClm001GlobalFocusBleedAsync();
        Assert.Equal(ClaimStatus.Passed, result.Status);
        Assert.NotNull(result.CapturedSnapshot);
        Assert.Equal(4812, result.CapturedSnapshot.Window.Pid);
        Assert.Equal(DesktopSemanticZone.Unknown, result.CapturedSnapshot.Focus.SemanticZone);
    }

    [Fact]
    public async Task CLM_002_ChildHwndNormalization_MapsToTopLevelWindow()
    {
        var result = await _driver.VerifyClm002ChildHwndNormalizationAsync();
        Assert.Equal(ClaimStatus.Passed, result.Status);
        Assert.NotNull(result.CapturedSnapshot);
        Assert.Equal((nint)0x00A50020, result.CapturedSnapshot.Window.Hwnd);
        Assert.False(string.IsNullOrWhiteSpace(result.CapturedSnapshot.Window.Title));
    }

    [Theory]
    [InlineData("Edit", "CONTEXT.md", "native-edit-context", "monaco-editor", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.EditorCodeBuffer)]
    [InlineData("Document", "Terminal 1", "terminal.integrated", "xterm", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.IntegratedTerminal)]
    [InlineData("Edit", "Message (Ctrl+Enter to commit", "scm.input", "monaco-editor", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.GitCommitBox)]
    [InlineData("Edit", "Message input", "chat-input", "interactive-session", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.ChatAssistant)]
    [InlineData("Tree", "Explorer", "workbench.view.explorer", "view-pane", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.SidebarExplorer)]
    public void CLM_003_IdeSemanticZoneResolution_ResolvesExpectedZones(
        string controlType, string name, string autoId, string className, DesktopAppArchetype archetype, DesktopSemanticZone expectedZone)
    {
        var resolvedZone = UiaExtractionEngine.ResolveSemanticZone(controlType, name, autoId, className, archetype);
        Assert.Equal(expectedZone, resolvedZone);
    }

    [Fact]
    public async Task CLM_003_IdeSemanticZoneResolution_MockDriverPasses()
    {
        var result = await _driver.VerifyClm003IdeSemanticZoneResolutionAsync();
        Assert.Equal(ClaimStatus.Passed, result.Status);
    }

    [Theory]
    [InlineData("ListItem", "Tab Title", "sidebar-box", "tab", DesktopAppArchetype.Gecko, DesktopSemanticZone.TabBar)]
    [InlineData("Document", "Tree Style Tab", "sidebar-box", "webextension-panel", DesktopAppArchetype.Gecko, DesktopSemanticZone.DocumentContent)]
    [InlineData("Tree", "File Explorer", "sidebar-box", "view-pane", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.SidebarExplorer)]
    public void CLM_004_BrowserSidebarVsIdeExplorer_GeckoNeverResolvesToSidebarExplorer(
        string controlType, string name, string autoId, string className, DesktopAppArchetype archetype, DesktopSemanticZone expectedZone)
    {
        var resolvedZone = UiaExtractionEngine.ResolveSemanticZone(controlType, name, autoId, className, archetype);
        Assert.Equal(expectedZone, resolvedZone);
        if (archetype == DesktopAppArchetype.Gecko)
        {
            Assert.NotEqual(DesktopSemanticZone.SidebarExplorer, resolvedZone);
        }
    }

    [Fact]
    public async Task CLM_004_BrowserSidebarVsIdeExplorer_MockDriverPasses()
    {
        var result = await _driver.VerifyClm004BrowserSidebarVsIdeExplorerAsync();
        Assert.Equal(ClaimStatus.Passed, result.Status);
    }
}
