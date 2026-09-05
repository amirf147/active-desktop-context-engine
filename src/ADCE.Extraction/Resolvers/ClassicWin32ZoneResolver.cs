// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using ADCE.Core.Enums;
using ADCE.Extraction.Models;

namespace ADCE.Extraction.Resolvers;

/// <summary>
/// Dedicated semantic zone and layout pane resolver for classic Win32 applications
/// (Notepad, Command Prompt, System Dialogs).
/// </summary>
public sealed class ClassicWin32ZoneResolver : IArchetypeZoneResolver
{
    public static readonly ClassicWin32ZoneResolver Instance = new();

    public DesktopAppArchetype SupportedArchetype => DesktopAppArchetype.ClassicWin32;

    public bool TryResolve(
        FocusedControlDescriptor control,
        AncestorChain ancestors,
        out SemanticResolution resolution)
    {
        string cls = control.ClassName;
        string cType = control.ControlType;

        // 1. Native Modal Dialogs & Message Boxes
        if (cls.Equals("#32770", StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith("#32770", StringComparison.OrdinalIgnoreCase))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.SystemDialog,
                WindowPaneLocation.OverlayModal,
                "Dialog",
                null);
            return true;
        }

        // 2. Legacy Console Window
        if (cls.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.Terminal,
                WindowPaneLocation.MainContent,
                "Terminal",
                null);
            return true;
        }

        // 3. Classic Common Controls (SysListView32, SysTreeView32)
        if (cls.Contains("SysListView32", StringComparison.OrdinalIgnoreCase) ||
            cType.Equals("ListItem", StringComparison.OrdinalIgnoreCase))
        {
            resolution = new SemanticResolution(
                DesktopSemanticZone.ShellItemList,
                WindowPaneLocation.MainContent,
                "ShellItems",
                null);
            return true;
        }

        resolution = SemanticResolution.Unresolved;
        return false;
    }
}
