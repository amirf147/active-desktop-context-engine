// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Collections.Immutable;
using ADCE.Core.Enums;
using ADCE.Core.Models;

namespace ADCE.Extraction.Models;

/// <summary>
/// Represents an observed parent node in a control's accessibility hierarchy.
/// </summary>
public record AncestorNode(
    int Depth,
    int ControlTypeId,
    string ControlType,
    string Name,
    string AutomationId,
    string ClassName,
    int ProcessId,
    nint NativeWindowHandle
);

/// <summary>
/// Immutable representation of the harvested ancestor chain for a focused element.
/// </summary>
public record AncestorChain(
    ImmutableArray<string> ContainerPaths,
    ImmutableArray<string> ContainerClasses,
    ImmutableArray<AncestorNode> Nodes
)
{
    public static readonly AncestorChain Empty = new(
        ImmutableArray<string>.Empty,
        ImmutableArray<string>.Empty,
        ImmutableArray<AncestorNode>.Empty);

    public bool HasId(string id) =>
        ContainerPaths.Any(p => p.Contains(id, System.StringComparison.OrdinalIgnoreCase));

    public bool HasClass(string cls) =>
        ContainerClasses.Any(c => c.Contains(cls, System.StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Immutable descriptor of raw physical properties read from a focused element.
/// </summary>
public record FocusedControlDescriptor(
    string ControlType,
    string Name,
    string AutomationId,
    string ClassName,
    BoundingRectangle BoundingBox,
    bool IsOverlay
);

/// <summary>
/// The outcome of semantic classification on a control and its ancestor hierarchy.
/// </summary>
public record SemanticResolution(
    DesktopSemanticZone Zone,
    WindowPaneLocation Pane,
    string? ActiveView,
    string? SectionName
)
{
    public static readonly SemanticResolution Unresolved = new(
        DesktopSemanticZone.Unknown,
        WindowPaneLocation.Unknown,
        null,
        null);
}
