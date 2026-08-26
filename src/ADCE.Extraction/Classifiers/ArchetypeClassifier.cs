// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using ADCE.Core.Enums;
using ADCE.Core.Interfaces;

namespace ADCE.Extraction.Classifiers;

/// <summary>
/// Default rule-based classifier categorizing Windows applications into 5 Universal UI Framework Archetypes.
/// </summary>
public sealed class ArchetypeClassifier : IArchetypeClassifier
{
    public static readonly ArchetypeClassifier Default = new();

    public DesktopAppArchetype Classify(string className, string processName, string title)
    {
        ReadOnlySpan<char> cls = className.AsSpan();
        ReadOnlySpan<char> proc = processName.AsSpan();

        // 1. Gecko / Mozilla Engine (Waterfox, Firefox, Thunderbird)
        if (cls.Equals("MozillaWindowClass", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("waterfox", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("firefox", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("thunderbird", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopAppArchetype.Gecko;
        }

        // 2. Chromium / Electron Engine (VS Code, Antigravity, Slack, Chrome, Edge)
        if (cls.Equals("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase) ||
            cls.Equals("Chrome_WidgetWin_0", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("code", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("antigravity", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("slack", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("discord", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopAppArchetype.ChromiumElectron;
        }

        // 3. WinUI 3 / XAML Islands / Modern Windows Shell (Explorer, Terminal, Settings)
        if (cls.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith("CASCADIA_HOSTING_WINDOW_CLASS", StringComparison.OrdinalIgnoreCase) ||
            cls.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase) ||
            cls.Equals("ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopAppArchetype.WinUI3Xaml;
        }

        // 4. Non-Native Canvas / Custom Toolkits (JetBrains/Swing, Qt, Flutter, WPF)
        if (cls.StartsWith("SunAwt", StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith("Qt5", StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith("Qt6", StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith("FLUTTER", StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith("HwndWrapper", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopAppArchetype.CanvasToolkit;
        }

        // 5. Classic Win32 / Common Controls (Notepad, 7-Zip, Command Prompt)
        if (cls.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase) ||
            cls.Equals("Notepad", StringComparison.OrdinalIgnoreCase) ||
            cls.Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith("#32770", StringComparison.OrdinalIgnoreCase)) // DialogBox
        {
            return DesktopAppArchetype.ClassicWin32;
        }

        return DesktopAppArchetype.Unknown;
    }
}
