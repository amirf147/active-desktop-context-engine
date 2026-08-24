// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

namespace ADCE.Core.Models;

/// <summary>
/// Represents an open tab item in an IDE, web browser, file explorer, or terminal tabstrip.
/// </summary>
public sealed record TabItemInfo
{
    /// <summary>1-based or 0-based visual index of the tab in its container.</summary>
    public int Index { get; init; }

    /// <summary>Tab label or document title.</summary>
    public required string Title { get; init; }

    /// <summary>Indicates whether this tab is currently selected/active in the container.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Indicates whether the tab is pinned (e.g. pinned browser tabs).</summary>
    public bool IsPinned { get; init; }

    /// <summary>Indicates whether the tab contains unsaved/dirty modifications (e.g. Monaco "●").</summary>
    public bool IsDirty { get; init; }

    /// <summary>Accessibility tooltip or full path if available.</summary>
    public string? Tooltip { get; init; }

    /// <summary>UI Automation AutomationId of the tab element.</summary>
    public string? AutomationId { get; init; }
}
