// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using ADCE.Core.Enums;
using ADCE.Extraction.Engine;
using Xunit;

namespace ADCE.Extraction.Tests;

public class IdePaneClassificationTests
{
    [Theory]
    [InlineData("Button", "Explorer", "workbench.view.explorer", "activitybar", DesktopSemanticZone.ActivityBar, WindowPaneLocation.ActivityBar, "ActivityBar")]
    [InlineData("Edit", "Message (Ctrl+Enter to commit)", "scm.input", "native-edit-context", DesktopSemanticZone.GitCommitBox, WindowPaneLocation.PrimarySidebar, "SourceControl")]
    [InlineData("TreeItem", "Timeline", "timeline.tree", "monaco-list-row", DesktopSemanticZone.Timeline, WindowPaneLocation.PrimarySidebar, "Explorer")]
    [InlineData("Button", "Timeline Section", "pane.header", "pane-header", DesktopSemanticZone.Timeline, WindowPaneLocation.PrimarySidebar, "Explorer")]
    [InlineData("TreeItem", "Outline", "outline.tree", "monaco-list-row", DesktopSemanticZone.Outline, WindowPaneLocation.PrimarySidebar, "Explorer")]
    [InlineData("Button", "Outline Section", "pane.header", "pane-header", DesktopSemanticZone.Outline, WindowPaneLocation.PrimarySidebar, "Explorer")]
    [InlineData("Edit", "Message input", "antigravity.agentSidePanelInputBox", "chat-input", DesktopSemanticZone.ChatPrompt, WindowPaneLocation.AuxiliarySidebar, "Chat")]
    [InlineData("Group", "Agent Conversation", "conversation", "conversation-stream", DesktopSemanticZone.ChatConversation, WindowPaneLocation.AuxiliarySidebar, "Chat")]
    [InlineData("Edit", "Program.cs", "editor-instance", "native-edit-context", DesktopSemanticZone.EditorBuffer, WindowPaneLocation.MainContent, "Editor")]
    [InlineData("Document", "Terminal 1", "terminal-instance", "terminal xterm", DesktopSemanticZone.Terminal, WindowPaneLocation.BottomPanel, "Terminal")]
    [InlineData("StatusBar", "Git Branch", "workbench.parts.statusbar", "status-bar", DesktopSemanticZone.StatusBar, WindowPaneLocation.StatusBar, "StatusBar")]
    public void Classification_ResolvesExpectedZoneAndPane(
        string cType, string name, string autoId, string className,
        DesktopSemanticZone expectedZone, WindowPaneLocation expectedPane, string expectedView)
    {
        var zone = UiaExtractionEngine.ResolveSemanticZone(cType, name, autoId, className, DesktopAppArchetype.ChromiumElectron);
        Assert.Equal(expectedZone, zone);

        var pane = UiaExtractionEngine.InferPaneFromZone(zone);
        Assert.Equal(expectedPane, pane);

        var view = UiaExtractionEngine.InferViewFromZone(zone);
        Assert.Equal(expectedView, view);
    }
}
