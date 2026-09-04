// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;

namespace ADCE.Core.Enums;

/// <summary>
/// Provides extension methods for projecting granular semantic zones into macro typing anchors
/// and parsing canonical zone strings.
/// </summary>
public static class DesktopSemanticZoneExtensions
{
    /// <summary>
    /// Projects a fine-grained semantic zone into a high-level macro typing anchor.
    /// </summary>
    /// <param name="zone">The fine-grained semantic zone.</param>
    /// <returns>The corresponding macro typing zone.</returns>
    public static DesktopSemanticZone ToMacroZone(this DesktopSemanticZone zone)
    {
        return zone switch
        {
            DesktopSemanticZone.EditorBuffer => DesktopSemanticZone.EditorBuffer,
            DesktopSemanticZone.GitCommitBox => DesktopSemanticZone.EditorBuffer,
            DesktopSemanticZone.Terminal => DesktopSemanticZone.Terminal,
            DesktopSemanticZone.ChatPrompt => DesktopSemanticZone.ChatPrompt,
            DesktopSemanticZone.ChatConversation => DesktopSemanticZone.ChatPrompt,
            DesktopSemanticZone.WebDocument => DesktopSemanticZone.WebDocument,
            DesktopSemanticZone.SidebarExplorer => DesktopSemanticZone.NavigationPanel,
            DesktopSemanticZone.Timeline => DesktopSemanticZone.NavigationPanel,
            DesktopSemanticZone.Outline => DesktopSemanticZone.NavigationPanel,
            DesktopSemanticZone.ActivityBar => DesktopSemanticZone.NavigationPanel,
            DesktopSemanticZone.ShellItemList => DesktopSemanticZone.NavigationPanel,
            DesktopSemanticZone.TabBar => DesktopSemanticZone.NavigationPanel,
            DesktopSemanticZone.StatusBar => DesktopSemanticZone.NavigationPanel,
            DesktopSemanticZone.NavigationPanel => DesktopSemanticZone.NavigationPanel,
            DesktopSemanticZone.AddressBar => DesktopSemanticZone.QuickOpen,
            DesktopSemanticZone.CommandPalette => DesktopSemanticZone.QuickOpen,
            DesktopSemanticZone.QuickOpen => DesktopSemanticZone.QuickOpen,
            DesktopSemanticZone.SystemDialog => DesktopSemanticZone.SystemDialog,
            _ => DesktopSemanticZone.Unknown
        };
    }

    /// <summary>
    /// Attempts to parse a canonical or alias string into a DesktopSemanticZone.
    /// </summary>
    public static bool TryParseCanonical(string? input, out DesktopSemanticZone zone)
    {
        zone = DesktopSemanticZone.Unknown;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string normalized = input.Trim().Replace("_", "").Replace("-", "").ToLowerInvariant();

        switch (normalized)
        {
            case "editorbuffer":
            case "editor":
            case "codebuffer":
            case "editorcodebuffer":
                zone = DesktopSemanticZone.EditorBuffer;
                return true;
            case "terminal":
            case "integratedterminal":
            case "console":
                zone = DesktopSemanticZone.Terminal;
                return true;
            case "gitcommitbox":
            case "gitcommit":
            case "commitbox":
            case "scm":
                zone = DesktopSemanticZone.GitCommitBox;
                return true;
            case "sidebarexplorer":
            case "explorer":
            case "filetree":
            case "workspacetree":
                zone = DesktopSemanticZone.SidebarExplorer;
                return true;
            case "timeline":
            case "gitline":
            case "history":
                zone = DesktopSemanticZone.Timeline;
                return true;
            case "outline":
            case "symboltree":
            case "symbols":
                zone = DesktopSemanticZone.Outline;
                return true;
            case "activitybar":
            case "actionbar":
            case "viewswitcher":
                zone = DesktopSemanticZone.ActivityBar;
                return true;
            case "chatprompt":
            case "chat":
            case "assistant":
            case "chatinput":
                zone = DesktopSemanticZone.ChatPrompt;
                return true;
            case "chatconversation":
            case "chathistory":
            case "conversation":
                zone = DesktopSemanticZone.ChatConversation;
                return true;
            case "webdocument":
            case "document":
            case "page":
                zone = DesktopSemanticZone.WebDocument;
                return true;
            case "addressbar":
            case "urlbar":
            case "locationbar":
                zone = DesktopSemanticZone.AddressBar;
                return true;
            case "shellitemlist":
            case "itemsview":
            case "filelist":
                zone = DesktopSemanticZone.ShellItemList;
                return true;
            case "tabbar":
            case "tabs":
            case "tabstrip":
                zone = DesktopSemanticZone.TabBar;
                return true;
            case "statusbar":
            case "status":
                zone = DesktopSemanticZone.StatusBar;
                return true;
            case "commandpalette":
            case "palette":
                zone = DesktopSemanticZone.CommandPalette;
                return true;
            case "quickopen":
            case "quickinput":
                zone = DesktopSemanticZone.QuickOpen;
                return true;
            case "systemdialog":
            case "dialog":
            case "modal":
                zone = DesktopSemanticZone.SystemDialog;
                return true;
            case "navigationpanel":
            case "navpanel":
                zone = DesktopSemanticZone.NavigationPanel;
                return true;
            default:
                return Enum.TryParse(input, true, out zone);
        }
    }

    /// <summary>
    /// Infers the default window pane location for this semantic zone.
    /// </summary>
    public static WindowPaneLocation ToDefaultPaneLocation(this DesktopSemanticZone zone)
    {
        return zone switch
        {
            DesktopSemanticZone.GitCommitBox => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.SidebarExplorer => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.Timeline => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.Outline => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.ShellItemList => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.NavigationPanel => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.EditorBuffer => WindowPaneLocation.MainContent,
            DesktopSemanticZone.WebDocument => WindowPaneLocation.MainContent,
            DesktopSemanticZone.ChatPrompt => WindowPaneLocation.AuxiliarySidebar,
            DesktopSemanticZone.ChatConversation => WindowPaneLocation.AuxiliarySidebar,
            DesktopSemanticZone.Terminal => WindowPaneLocation.BottomPanel,
            DesktopSemanticZone.ActivityBar => WindowPaneLocation.ActivityBar,
            DesktopSemanticZone.AddressBar => WindowPaneLocation.TopBar,
            DesktopSemanticZone.TabBar => WindowPaneLocation.TopBar,
            DesktopSemanticZone.StatusBar => WindowPaneLocation.StatusBar,
            DesktopSemanticZone.QuickOpen or DesktopSemanticZone.CommandPalette or DesktopSemanticZone.SystemDialog => WindowPaneLocation.OverlayModal,
            _ => WindowPaneLocation.Unknown
        };
    }

    /// <summary>
    /// Infers the default view name for this semantic zone.
    /// </summary>
    public static string? ToDefaultView(this DesktopSemanticZone zone)
    {
        return zone switch
        {
            DesktopSemanticZone.GitCommitBox => "SourceControl",
            DesktopSemanticZone.Timeline => "Explorer",
            DesktopSemanticZone.Outline => "Explorer",
            DesktopSemanticZone.SidebarExplorer => "Explorer",
            DesktopSemanticZone.ChatPrompt => "Chat",
            DesktopSemanticZone.ChatConversation => "Chat",
            DesktopSemanticZone.EditorBuffer => "Editor",
            DesktopSemanticZone.Terminal => "Terminal",
            DesktopSemanticZone.ActivityBar => "ActivityBar",
            DesktopSemanticZone.StatusBar => "StatusBar",
            DesktopSemanticZone.QuickOpen or DesktopSemanticZone.CommandPalette => "QuickOpen",
            _ => null
        };
    }

    /// <summary>
    /// Infers the default section name for this semantic zone.
    /// </summary>
    public static string? ToDefaultSection(this DesktopSemanticZone zone)
    {
        return zone switch
        {
            DesktopSemanticZone.GitCommitBox => "CommitBox",
            DesktopSemanticZone.Timeline => "Timeline",
            DesktopSemanticZone.Outline => "Outline",
            DesktopSemanticZone.ChatPrompt => "ChatPrompt",
            DesktopSemanticZone.ChatConversation => "Conversation",
            _ => null
        };
    }
}
