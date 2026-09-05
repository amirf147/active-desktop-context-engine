// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using ADCE.Core.Enums;
using ADCE.Extraction.Engine;
using Xunit;

namespace ADCE.Extraction.Tests.Engine;

/// <summary>
/// Unit tests verifying semantic zone resolution across application archetypes and UI controls.
/// Tests production heuristic and dynamic rule evaluation in UiaExtractionEngine without synthetic mocks.
/// </summary>
public class SemanticZoneResolutionTests
{
    [Theory]
    [InlineData("Edit", "CONTEXT.md", "native-edit-context", "monaco-editor", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.EditorBuffer)]
    [InlineData("Document", "Terminal 1", "terminal.integrated", "xterm", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.Terminal)]
    [InlineData("Edit", "Message (Ctrl+Enter to commit", "scm.input", "monaco-editor", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.GitCommitBox)]
    [InlineData("Edit", "Message input", "chat-input", "interactive-session", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.ChatPrompt)]
    [InlineData("Tree", "Explorer", "workbench.view.explorer", "view-pane", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.SidebarExplorer)]
    public void ResolveSemanticZone_IdeControls_ResolvesExpectedZones(
        string controlType, string name, string autoId, string className, DesktopAppArchetype archetype, DesktopSemanticZone expectedZone)
    {
        var resolvedZone = UiaExtractionEngine.ResolveSemanticZone(controlType, name, autoId, className, archetype);
        Assert.Equal(expectedZone, resolvedZone);
    }

    [Theory]
    [InlineData("ListItem", "Tab Title", "sidebar-box", "tab", DesktopAppArchetype.Gecko, DesktopSemanticZone.TabBar)]
    [InlineData("Document", "Tree Style Tab", "sidebar-box", "webextension-panel", DesktopAppArchetype.Gecko, DesktopSemanticZone.WebDocument)]
    [InlineData("Tree", "File Explorer", "sidebar-box", "view-pane", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.SidebarExplorer)]
    public void ResolveSemanticZone_BrowserSidebarVsIdeExplorer_GeckoNeverResolvesToSidebarExplorer(
        string controlType, string name, string autoId, string className, DesktopAppArchetype archetype, DesktopSemanticZone expectedZone)
    {
        var resolvedZone = UiaExtractionEngine.ResolveSemanticZone(controlType, name, autoId, className, archetype);
        Assert.Equal(expectedZone, resolvedZone);
    }

    [Theory]
    [InlineData("Edit", "Address and search bar", "urlbar-input", "urlbar", DesktopAppArchetype.Gecko, DesktopSemanticZone.AddressBar)]
    [InlineData("Button", "Back", "back-button", "toolbarbutton", DesktopAppArchetype.Gecko, DesktopSemanticZone.NavigationPanel)]
    [InlineData("Custom", "Activity Bar", "workbench.parts.activitybar", "activitybar", DesktopAppArchetype.ChromiumElectron, DesktopSemanticZone.ActivityBar)]
    public void ResolveSemanticZone_BrowserAndIdeNavigation_ResolvesExpectedZones(
        string controlType, string name, string autoId, string className, DesktopAppArchetype archetype, DesktopSemanticZone expectedZone)
    {
        var resolvedZone = UiaExtractionEngine.ResolveSemanticZone(controlType, name, autoId, className, archetype);
        Assert.Equal(expectedZone, resolvedZone);
    }
}
