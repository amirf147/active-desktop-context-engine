// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

namespace ADCE.Core.Enums;

/// <summary>
/// Defines fine-grained semantic typing anchors and interaction domains within desktop applications.
/// </summary>
public enum DesktopSemanticZone
{
    /// <summary>Unrecognized or unmapped semantic zone.</summary>
    Unknown = 0,

    /// <summary>Active code/text editor buffer (e.g. Monaco editor, Notepad, text inputs).</summary>
    EditorBuffer = 1,

    /// <summary>Command shell or terminal window (integrated or standalone).</summary>
    Terminal = 2,

    /// <summary>Source control or Git commit input box.</summary>
    GitCommitBox = 3,

    /// <summary>Navigation tree, sidebar explorer, or project file list.</summary>
    SidebarExplorer = 4,

    /// <summary>Web browser URL address bar or search input.</summary>
    AddressBar = 5,

    /// <summary>Main document or rendered web page viewport.</summary>
    WebDocument = 6,

    /// <summary>File or folder list view (e.g. Windows Explorer Items View).</summary>
    ShellItemList = 7,

    /// <summary>Tabstrip container hosting open editor or browser tabs.</summary>
    TabBar = 8,

    /// <summary>Application status bar (e.g. branch name, encoding, line info).</summary>
    StatusBar = 9,

    /// <summary>Command palette or quick switcher (Ctrl+Shift+P / Ctrl+P).</summary>
    CommandPalette = 10,

    /// <summary>AI chat assistant prompt or interactive conversational input.</summary>
    ChatPrompt = 11,

    /// <summary>Quick open switcher or modal search overlay.</summary>
    QuickOpen = 12,

    /// <summary>Modal system dialog, message box, or file picker.</summary>
    SystemDialog = 13,

    /// <summary>High-level navigation container or tool panel.</summary>
    NavigationPanel = 14,

    /// <summary>Primary activity bar strip or icon launcher buttons.</summary>
    ActivityBar = 15,

    /// <summary>File or project version timeline history list item.</summary>
    Timeline = 16,

    /// <summary>Document symbol or structure outline tree item.</summary>
    Outline = 17,

    /// <summary>Rendered chat history or conversational stream in an AI assistant panel.</summary>
    ChatConversation = 18
}
