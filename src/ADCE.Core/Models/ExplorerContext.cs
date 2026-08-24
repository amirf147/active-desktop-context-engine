// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Linq;

namespace ADCE.Core.Models;

/// <summary>
/// Represents contextual state extracted from Windows File Explorer.
/// Implements deep value-based equality across path, selection, and tab collections.
/// </summary>
public sealed record ExplorerContext : IEquatable<ExplorerContext>
{
    /// <summary>Current directory path displayed in the address bar.</summary>
    public string? CurrentPath { get; init; }

    /// <summary>Breadcrumb components leading to the current directory.</summary>
    public IReadOnlyList<string> Breadcrumbs { get; init; } = [];

    /// <summary>Names of files or folders currently selected in the Items View.</summary>
    public IReadOnlyList<string> SelectedItems { get; init; } = [];

    /// <summary>Open tabs in Windows 11 File Explorer TabView.</summary>
    public IReadOnlyList<TabItemInfo> Tabs { get; init; } = [];

    public bool Equals(ExplorerContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CurrentPath == other.CurrentPath &&
               Breadcrumbs.SequenceEqual(other.Breadcrumbs) &&
               SelectedItems.SequenceEqual(other.SelectedItems) &&
               Tabs.SequenceEqual(other.Tabs);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CurrentPath);
        foreach (var b in Breadcrumbs) hash.Add(b);
        foreach (var s in SelectedItems) hash.Add(s);
        foreach (var t in Tabs) hash.Add(t);
        return hash.ToHashCode();
    }
}
