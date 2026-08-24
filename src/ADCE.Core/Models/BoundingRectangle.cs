// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

namespace ADCE.Core.Models;

/// <summary>
/// Represents a screen coordinate bounding rectangle for a UI element or window.
/// </summary>
public readonly record struct BoundingRectangle(int Left, int Top, int Width, int Height)
{
    /// <summary>Gets the right screen coordinate (Left + Width).</summary>
    public int Right => Left + Width;

    /// <summary>Gets the bottom screen coordinate (Top + Height).</summary>
    public int Bottom => Top + Height;

    /// <summary>Gets a value indicating whether this bounding box has zero or negative area.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Represents an empty or uninitialized bounding rectangle.</summary>
    public static readonly BoundingRectangle Empty = new(0, 0, 0, 0);
}
