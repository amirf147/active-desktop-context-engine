// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

namespace ADCE.Core.Enums;

/// <summary>
/// Provides extension methods for projecting granular semantic zones into macro typing anchors.
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
            DesktopSemanticZone.WebDocument => DesktopSemanticZone.WebDocument,
            DesktopSemanticZone.SidebarExplorer => DesktopSemanticZone.NavigationPanel,
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
}
