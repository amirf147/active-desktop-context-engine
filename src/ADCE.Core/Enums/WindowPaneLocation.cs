// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

namespace ADCE.Core.Enums;

/// <summary>
/// Defines the primary physical window panes and structural layout regions of an application window.
/// </summary>
public enum WindowPaneLocation
{
    /// <summary>Unrecognized or unmapped pane location.</summary>
    Unknown = 0,

    /// <summary>Primary icon strip or navigation rail (Pane 0 / Activity Bar, W &lt;= 42px).</summary>
    ActivityBar = 1,

    /// <summary>Primary sidebar container hosting views such as Explorer, Source Control, and Extensions (Pane 1).</summary>
    PrimarySidebar = 2,

    /// <summary>Central document editor group, diff view, or primary document viewport (Pane 2).</summary>
    MainContent = 3,

    /// <summary>Secondary sidebar or auxiliary drawer hosting AI chat assistant or side-by-side documentation (Pane 3).</summary>
    AuxiliarySidebar = 4,

    /// <summary>Bottom drawer hosting integrated terminals, output streams, problems, and consoles (Pane 4).</summary>
    BottomPanel = 5,

    /// <summary>Top window title bar, menu bar, or global search input.</summary>
    TopBar = 6,

    /// <summary>Bottom status strip indicating branch, encoding, and background statuses.</summary>
    StatusBar = 7,

    /// <summary>Floating modal overlay, quick open switcher, command palette, or dialog.</summary>
    OverlayModal = 8
}
