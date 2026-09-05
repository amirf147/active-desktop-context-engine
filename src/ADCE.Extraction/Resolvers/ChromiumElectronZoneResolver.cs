// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Linq;
using ADCE.Core.Enums;
using ADCE.Extraction.Models;

namespace ADCE.Extraction.Resolvers;

/// <summary>
/// Dedicated semantic zone and layout pane resolver for Chromium/Electron IDE applications
/// (VS Code, Antigravity IDE, Cursor, Windsurf, Slack).
/// </summary>
public sealed class ChromiumElectronZoneResolver : IArchetypeZoneResolver
{
    public static readonly ChromiumElectronZoneResolver Instance = new();

    public DesktopAppArchetype SupportedArchetype => DesktopAppArchetype.ChromiumElectron;

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

        // 1. Ephemeral top-bar menu items and dropdown overlays (Action-Invoker Guard)
        if (cType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) ||
            cType.Equals("Menu", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("monaco-menu", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("context-view", StringComparison.OrdinalIgnoreCase) ||
            classes.Any(c => c.Contains("monaco-menu", StringComparison.OrdinalIgnoreCase) || c.Contains("context-view", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.NavigationPanel,
                WindowPaneLocation.TopBar,
                "MenuBar",
                "Menu");
            return true;
        }

        // 2. Centered Modal Overlays (Command Palette / Quick Open)
        if (control.IsOverlay ||
            autoId.Contains("quickInput", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("quick-input", StringComparison.OrdinalIgnoreCase) ||
            classes.Any(c => c.Contains("quick-input", StringComparison.OrdinalIgnoreCase)))
        {
            var zone = autoId.Contains("command-palette", StringComparison.OrdinalIgnoreCase)
                ? DesktopSemanticZone.CommandPalette
                : DesktopSemanticZone.QuickOpen;

            resolution = new SemanticResolution(zone, WindowPaneLocation.OverlayModal, "QuickOpen", null);
            return true;
        }

        // 3. AI Agent Auxiliary Drawer (Chat Prompt & Conversation Stream)
        if (autoId.Contains("antigravity.agentSidePanelInputBox", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Message input", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("chat-input", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("interactive-session", StringComparison.OrdinalIgnoreCase))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.ChatPrompt,
                WindowPaneLocation.AuxiliarySidebar,
                "Chat",
                "ChatPrompt");
            return true;
        }

        if (autoId.Equals("conversation", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Agent Conversation", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Toggle Agent", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("codicon-layout-sidebar-right", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("antigravity-agent-side-panel", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Contains("workbench.parts.auxiliarybar", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.ChatConversation,
                WindowPaneLocation.AuxiliarySidebar,
                "Chat",
                "Conversation");
            return true;
        }

        // 4. Source Control / Git Commit Input Box
        if (name.Contains("Message (Ctrl+Enter to commit", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("scm.input", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("git-commit", StringComparison.OrdinalIgnoreCase) ||
            classes.Any(c => c.Contains("scm-editor-container", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.GitCommitBox,
                WindowPaneLocation.PrimarySidebar,
                "SourceControl",
                "CommitBox");
            return true;
        }

        // 5. Activity Bar Launcher Rail
        if (autoId.Contains("workbench.parts.activitybar", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("activitybar", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("codicon-explorer-view-icon", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Explorer (Ctrl+Shift+E)", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Contains("workbench.parts.activitybar", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.ActivityBar,
                WindowPaneLocation.ActivityBar,
                "ActivityBar",
                null);
            return true;
        }

        // 6. Primary Sidebar Sections & Tree Explorer
        if (cls.Contains("pane-header", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Timeline Section", StringComparison.OrdinalIgnoreCase) ||
            (cType.Equals("TreeItem", StringComparison.OrdinalIgnoreCase) && name.Equals("Timeline", StringComparison.OrdinalIgnoreCase)) ||
            autoId.Contains("timeline", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Timeline", StringComparison.OrdinalIgnoreCase) || autoId.Contains("timeline", StringComparison.OrdinalIgnoreCase))
            {
                resolution = new SemanticResolution(
                    DesktopSemanticZone.Timeline,
                    WindowPaneLocation.PrimarySidebar,
                    "Explorer",
                    "Timeline");
                return true;
            }
        }

        if (name.Contains("Outline Section", StringComparison.OrdinalIgnoreCase) ||
            (cType.Equals("TreeItem", StringComparison.OrdinalIgnoreCase) && name.Equals("Outline", StringComparison.OrdinalIgnoreCase)) ||
            autoId.Contains("outline", StringComparison.OrdinalIgnoreCase))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.Outline,
                WindowPaneLocation.PrimarySidebar,
                "Explorer",
                "Outline");
            return true;
        }

        if (cType.Equals("TreeItem", StringComparison.OrdinalIgnoreCase) ||
            cType.Equals("Tree", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("workbench.view.explorer", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Contains("workbench.parts.sidebar", StringComparison.OrdinalIgnoreCase)) ||
            classes.Any(c => c.Contains("part sidebar", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.SidebarExplorer,
                WindowPaneLocation.PrimarySidebar,
                "Explorer",
                null);
            return true;
        }

        // 7. Editor Area (Breadcrumbs, Tab Strip, Monaco Code Buffer)
        if (cls.Contains("monaco-breadcrumbs", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("breadcrumbs", StringComparison.OrdinalIgnoreCase) ||
            classes.Any(c => c.Contains("monaco-breadcrumbs", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.NavigationPanel,
                WindowPaneLocation.MainContent,
                "Editor",
                "Breadcrumbs");
            return true;
        }

        if (cType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) ||
            cType.Equals("Tab", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("tabs-container", StringComparison.OrdinalIgnoreCase) ||
            classes.Any(c => c.Contains("tabs-container", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.TabBar,
                WindowPaneLocation.MainContent,
                "Editor",
                null);
            return true;
        }

        if (cls.Contains("monaco-editor", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("native-edit-context", StringComparison.OrdinalIgnoreCase) ||
            classes.Any(c => c.Contains("monaco-editor", StringComparison.OrdinalIgnoreCase) ||
                             c.Contains("native-edit-context", StringComparison.OrdinalIgnoreCase)) ||
            paths.Any(p => p.Contains("workbench.parts.editor", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.EditorBuffer,
                WindowPaneLocation.MainContent,
                "Editor",
                null);
            return true;
        }

        // 8. Bottom Panel / Integrated Terminal
        if (cls.Contains("single-terminal-tab", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("xterm", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
            (cType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) && name.Equals("Terminal", StringComparison.OrdinalIgnoreCase)) ||
            classes.Any(c => c.Contains("xterm", StringComparison.OrdinalIgnoreCase) || c.Contains("terminal", StringComparison.OrdinalIgnoreCase)) ||
            paths.Any(p => p.Contains("workbench.parts.panel", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.Terminal,
                WindowPaneLocation.BottomPanel,
                "Terminal",
                null);
            return true;
        }

        // 9. Status Bar Footer
        if (autoId.Contains("workbench.parts.statusbar", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("status-bar", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("statusbar", StringComparison.OrdinalIgnoreCase) ||
            paths.Any(p => p.Contains("workbench.parts.statusbar", StringComparison.OrdinalIgnoreCase)))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.StatusBar,
                WindowPaneLocation.StatusBar,
                "StatusBar",
                null);
            return true;
        }

        resolution = SemanticResolution.Unresolved;
        return false;
    }
}
