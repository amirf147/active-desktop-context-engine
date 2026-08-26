// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;

namespace ADCE.Core.Models;

/// <summary>
/// Represents the active virtual desktop workspace envelope and spatial metadata.
/// </summary>
public sealed record WorkspaceEnvelope
{
    /// <summary>Unique GUID identifier of the virtual desktop.</summary>
    public required Guid VirtualDesktopId { get; init; }

    /// <summary>Zero-indexed or 1-indexed order of the active desktop workspace.</summary>
    public required int DesktopIndex { get; init; }

    /// <summary>Human-readable name of the virtual desktop (e.g. "Desktop 1", "Development").</summary>
    public string VirtualDesktopName { get; init; } = string.Empty;

    /// <summary>Zero-indexed monitor display containing the active context.</summary>
    public int MonitorIndex { get; init; }

    /// <summary>Screen bounding rectangle of the target display monitor.</summary>
    public BoundingRectangle MonitorBounds { get; init; } = BoundingRectangle.Empty;
}
