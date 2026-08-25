// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

namespace ADCE.Core.Enums;

/// <summary>
/// Defines high-level semantic zones within desktop applications.
/// </summary>
public enum DesktopSemanticZone
{
    /// <summary>Unrecognized or unmapped semantic zone.</summary>
    Unknown = 0,

    /// <summary>Active code/text editor buffer (e.g. Monaco editor, Notepad body).</summary>
    EditorCodeBuffer = 1,

    /// <summary>Integrated terminal or shell window (e.g. VS Code terminal, Cascadia).</summary>
    IntegratedTerminal = 2,

    /// <summary>Source control / Git commit input box.</summary>
    GitCommitBox = 3,

    /// <summary>Sidebar explorer, tree view, or project navigator.</summary>
    SidebarExplorer = 4,

    /// <summary>Web browser URL address bar or search input.</summary>
    AddressBar = 5,

    /// <summary>Main document or web page content viewport.</summary>
    DocumentContent = 6,

    /// <summary>File or folder list view (e.g. Windows Explorer Items View).</summary>
    ShellItemList = 7,

    /// <summary>Tabstrip container hosting open editor or browser tabs.</summary>
    TabBar = 8,

    /// <summary>Application status bar (e.g. branch name, encoding, line info).</summary>
    StatusBar = 9,

    /// <summary>Command palette or quick open switcher (Ctrl+Shift+P / Ctrl+P).</summary>
    CommandPalette = 10,

    /// <summary>AI chat assistant panel or interactive prompt input.</summary>
    ChatAssistant = 11
}
