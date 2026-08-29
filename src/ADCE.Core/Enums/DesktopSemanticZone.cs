// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

namespace ADCE.Core.Enums;

/// <summary>
/// Defines high-level macro typing anchors and interaction domains within desktop applications.
/// </summary>
public enum DesktopSemanticZone
{
    /// <summary>Unrecognized or unmapped semantic zone.</summary>
    Unknown = 0,
    None = 0,

    /// <summary>Active code/text editor buffer (e.g. Monaco editor, Notepad, text inputs).</summary>
    EditorBuffer = 1,

    /// <summary>Command shell or terminal window (integrated or standalone).</summary>
    Terminal = 2,

    /// <summary>AI chat assistant prompt or interactive conversational input.</summary>
    ChatPrompt = 3,

    /// <summary>Main document or rendered web page viewport.</summary>
    WebDocument = 4,

    /// <summary>Navigation tree, sidebar explorer, or project file list.</summary>
    NavigationPanel = 5,

    /// <summary>Command palette, quick switcher, or modal search overlay.</summary>
    QuickOpen = 6,

    /// <summary>Modal system dialog, message box, or file picker.</summary>
    SystemDialog = 7,

    #region Backward Compatibility Aliases
    /// <summary>Legacy alias for EditorBuffer.</summary>
    EditorCodeBuffer = 1,

    /// <summary>Legacy alias for Terminal.</summary>
    IntegratedTerminal = 2,

    /// <summary>Legacy alias for NavigationPanel.</summary>
    GitCommitBox = 8,

    /// <summary>Legacy alias for NavigationPanel.</summary>
    SidebarExplorer = 5,

    /// <summary>Legacy alias for WebDocument / QuickOpen.</summary>
    AddressBar = 9,

    /// <summary>Legacy alias for WebDocument.</summary>
    DocumentContent = 4,

    /// <summary>Legacy alias for NavigationPanel.</summary>
    ShellItemList = 10,

    /// <summary>Legacy alias for NavigationPanel.</summary>
    TabBar = 11,

    /// <summary>Legacy alias for NavigationPanel.</summary>
    StatusBar = 12,

    /// <summary>Legacy alias for QuickOpen.</summary>
    CommandPalette = 6,

    /// <summary>Legacy alias for ChatPrompt.</summary>
    ChatAssistant = 3
    #endregion
}
