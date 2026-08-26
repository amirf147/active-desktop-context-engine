// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

namespace ADCE.Core.Enums;

/// <summary>
/// Categorizes desktop applications into universal architectural UI archetypes.
/// </summary>
public enum DesktopAppArchetype
{
    /// <summary>Unclassified or generic application.</summary>
    Unknown = 0,

    /// <summary>Chromium or Electron based application (e.g. VS Code, Antigravity, Slack, Chrome).</summary>
    ChromiumElectron = 1,

    /// <summary>Gecko based application (e.g. Waterfox, Firefox, Thunderbird).</summary>
    Gecko = 2,

    /// <summary>Modern Windows XAML / WinUI 3 application (e.g. Windows 11 File Explorer, Windows Terminal).</summary>
    WinUI3Xaml = 3,

    /// <summary>Classic Win32 or Common Controls application (e.g. Notepad, 7-Zip, standard dialogs).</summary>
    ClassicWin32 = 4,

    /// <summary>Custom non-native canvas / toolkit application (e.g. JetBrains Swing, Qt, Flutter, WPF).</summary>
    CanvasToolkit = 5
}
