// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;
using ADCE.Core.Enums;

namespace ADCE.Core.Models;

/// <summary>
/// Represents the focused UI control or active keyboard input target with explicit structural hierarchy.
/// Implements zero-allocation value equality for high-throughput event debouncing.
/// </summary>
public sealed record FocusedControlInfo : IEquatable<FocusedControlInfo>
{
    /// <summary>UI Automation ControlType name (e.g. "Edit", "Button", "ListItem", "Document").</summary>
    public required string ControlType { get; init; }

    /// <summary>Accessibility name of the focused element.</summary>
    public required string ElementName { get; init; }

    /// <summary>Screen bounding coordinates of the focused element.</summary>
    public required BoundingRectangle BoundingBox { get; init; }

    /// <summary>UI Automation AutomationId property if assigned.</summary>
    public string AutomationId { get; init; } = string.Empty;

    /// <summary>Win32 / UI Automation class name of the control.</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Resolved fine-grained semantic typing anchor.</summary>
    public DesktopSemanticZone SemanticZone { get; init; } = DesktopSemanticZone.Unknown;

    /// <summary>Resolved macro window pane hosting this control.</summary>
    public WindowPaneLocation PaneLocation { get; init; } = WindowPaneLocation.Unknown;

    /// <summary>Active view or tool container hosting this control (e.g. "Explorer", "SourceControl", "Chat").</summary>
    public string? ActiveView { get; init; }

    /// <summary>Inner section or accordion container within the active view (e.g. "active-desktop-context-engine", "Timeline", "Outline", "CommitBox").</summary>
    public string? SectionName { get; init; }

    /// <summary>Hierarchical semantic path from pane down to section (e.g. ["PrimarySidebar", "Explorer", "Timeline"]).</summary>
    public ImmutableArray<string> SemanticPath { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Normalized ancestor automation IDs from immediate container to macro parent.</summary>
    public ImmutableArray<string> ContainerPath { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Normalized ancestor class names when automation IDs are absent.</summary>
    public ImmutableArray<string> ContainerClasses { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Indicates if the focused control is within a modal popup or overlay (e.g. Command Palette).</summary>
    public bool IsOverlay { get; init; }

    /// <summary>Optional sanitized text or value snippet extracted from ValuePattern / TextPattern.</summary>
    public string? ValueSnippet { get; init; }

    public bool Equals(FocusedControlInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (ControlType != other.ControlType ||
            ElementName != other.ElementName ||
            AutomationId != other.AutomationId ||
            ClassName != other.ClassName ||
            SemanticZone != other.SemanticZone ||
            PaneLocation != other.PaneLocation ||
            ActiveView != other.ActiveView ||
            SectionName != other.SectionName ||
            IsOverlay != other.IsOverlay ||
            ValueSnippet != other.ValueSnippet ||
            BoundingBox != other.BoundingBox)
        {
            return false;
        }

        return FastArrayEquals(ContainerPath, other.ContainerPath) &&
               FastArrayEquals(ContainerClasses, other.ContainerClasses) &&
               FastArrayEquals(SemanticPath, other.SemanticPath);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ControlType);
        hash.Add(ElementName);
        hash.Add(AutomationId);
        hash.Add(ClassName);
        hash.Add((int)SemanticZone);
        hash.Add((int)PaneLocation);
        hash.Add(ActiveView);
        hash.Add(SectionName);
        hash.Add(IsOverlay);
        hash.Add(ValueSnippet);
        hash.Add(BoundingBox);

        if (!SemanticPath.IsDefault)
        {
            for (int i = 0; i < SemanticPath.Length; i++)
                hash.Add(SemanticPath[i]);
        }

        if (!ContainerPath.IsDefault)
        {
            for (int i = 0; i < ContainerPath.Length; i++)
                hash.Add(ContainerPath[i]);
        }

        if (!ContainerClasses.IsDefault)
        {
            for (int i = 0; i < ContainerClasses.Length; i++)
                hash.Add(ContainerClasses[i]);
        }

        return hash.ToHashCode();
    }

    private static bool FastArrayEquals(ImmutableArray<string> a, ImmutableArray<string> b)
    {
        if (a.IsDefault && b.IsDefault) return true;
        if (a.IsDefault || b.IsDefault) return false;
        if (a.Length != b.Length) return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                return false;
        }
        return true;
    }
}
