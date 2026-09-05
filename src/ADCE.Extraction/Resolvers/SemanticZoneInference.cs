// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using ADCE.Core.Enums;

namespace ADCE.Extraction.Resolvers;

/// <summary>
/// Static mapping tables inferring standard window pane locations, active views,
/// and section names from resolved semantic zones.
/// </summary>
public static class SemanticZoneInference
{
    public static WindowPaneLocation InferPaneFromZone(DesktopSemanticZone zone)
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

    public static string? InferViewFromZone(DesktopSemanticZone zone)
    {
        return zone switch
        {
            DesktopSemanticZone.GitCommitBox => "SourceControl",
            DesktopSemanticZone.Timeline => "Explorer",
            DesktopSemanticZone.Outline => "Explorer",
            DesktopSemanticZone.SidebarExplorer => "Explorer",
            DesktopSemanticZone.ChatPrompt or DesktopSemanticZone.ChatConversation => "Chat",
            DesktopSemanticZone.EditorBuffer => "Editor",
            DesktopSemanticZone.Terminal => "Terminal",
            DesktopSemanticZone.ActivityBar => "ActivityBar",
            DesktopSemanticZone.AddressBar => "NavigationBar",
            DesktopSemanticZone.TabBar => "TabStrip",
            DesktopSemanticZone.WebDocument => "WebDocument",
            DesktopSemanticZone.StatusBar => "StatusBar",
            DesktopSemanticZone.QuickOpen or DesktopSemanticZone.CommandPalette => "QuickOpen",
            _ => null
        };
    }

    public static string? InferSectionFromZone(DesktopSemanticZone zone)
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
