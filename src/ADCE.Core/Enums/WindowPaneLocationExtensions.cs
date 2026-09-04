// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;

namespace ADCE.Core.Enums;

/// <summary>
/// Provides extension methods for serializing and parsing WindowPaneLocation.
/// </summary>
public static class WindowPaneLocationExtensions
{
    /// <summary>
    /// Converts a WindowPaneLocation to its canonical snake_case string representation.
    /// </summary>
    public static string ToSnakeCase(this WindowPaneLocation pane)
    {
        return pane switch
        {
            WindowPaneLocation.ActivityBar => "activity_bar",
            WindowPaneLocation.PrimarySidebar => "primary_sidebar",
            WindowPaneLocation.MainContent => "main_content",
            WindowPaneLocation.AuxiliarySidebar => "auxiliary_sidebar",
            WindowPaneLocation.BottomPanel => "bottom_panel",
            WindowPaneLocation.TopBar => "top_bar",
            WindowPaneLocation.StatusBar => "status_bar",
            WindowPaneLocation.OverlayModal => "overlay_modal",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Attempts to parse a string (canonical, snake_case, or common aliases) into a WindowPaneLocation.
    /// </summary>
    public static bool TryParseCanonical(string? input, out WindowPaneLocation pane)
    {
        pane = WindowPaneLocation.Unknown;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string normalized = input.Trim().Replace("_", "").Replace("-", "").ToLowerInvariant();

        switch (normalized)
        {
            case "activitybar":
            case "activity":
            case "actbar":
                pane = WindowPaneLocation.ActivityBar;
                return true;
            case "primarysidebar":
            case "sidebar":
            case "leftsidebar":
            case "leftdrawer":
                pane = WindowPaneLocation.PrimarySidebar;
                return true;
            case "maincontent":
            case "editor":
            case "editorgroup":
            case "content":
            case "viewport":
                pane = WindowPaneLocation.MainContent;
                return true;
            case "auxiliarysidebar":
            case "auxiliarybar":
            case "rightsidebar":
            case "assistant":
            case "chatpanel":
            case "secondarysidebar":
                pane = WindowPaneLocation.AuxiliarySidebar;
                return true;
            case "bottompanel":
            case "panel":
            case "terminalpanel":
            case "bottomdrawer":
                pane = WindowPaneLocation.BottomPanel;
                return true;
            case "topbar":
            case "titlebar":
            case "menubar":
            case "navbartop":
                pane = WindowPaneLocation.TopBar;
                return true;
            case "statusbar":
            case "status":
                pane = WindowPaneLocation.StatusBar;
                return true;
            case "overlaymodal":
            case "modal":
            case "overlay":
            case "dialog":
            case "palette":
                pane = WindowPaneLocation.OverlayModal;
                return true;
            case "unknown":
                pane = WindowPaneLocation.Unknown;
                return true;
            default:
                return Enum.TryParse(input, true, out pane);
        }
    }
}
