// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using ADCE.Core.Enums;

namespace ADCE.Core.Models;

/// <summary>
/// Represents the focused UI control or active keyboard input target.
/// </summary>
public sealed record FocusedControlInfo
{
    /// <summary>UI Automation ControlType name (e.g. "Edit", "Button", "ListItem").</summary>
    public required string ControlType { get; init; }

    /// <summary>Accessibility name of the focused element.</summary>
    public required string ElementName { get; init; }

    /// <summary>Screen bounding coordinates of the focused element.</summary>
    public required BoundingRectangle BoundingBox { get; init; }

    /// <summary>UI Automation AutomationId property if assigned.</summary>
    public string AutomationId { get; init; } = string.Empty;

    /// <summary>Win32 / UI Automation class name of the control.</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Identified semantic zone of the focused control.</summary>
    public DesktopSemanticZone SemanticZone { get; init; } = DesktopSemanticZone.Unknown;

    /// <summary>Optional text or value snippet extracted from ValuePattern / TextPattern.</summary>
    public string? ValueSnippet { get; init; }
}
