// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;

namespace ADCE.Core.Models;

/// <summary>
/// Represents contextual state extracted from Windows File Explorer.
/// Implements deep value-based equality across path, selection, and tab collections with zero heap allocations.
/// </summary>
public sealed record ExplorerContext : IEquatable<ExplorerContext>
{
    /// <summary>Current directory path displayed in the address bar.</summary>
    public string? CurrentPath { get; init; }

    /// <summary>Breadcrumb components leading to the current directory.</summary>
    public ImmutableArray<string> Breadcrumbs { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Names of files or folders currently selected in the Items View.</summary>
    public ImmutableArray<string> SelectedItems { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Open tabs in Windows 11 File Explorer TabView.</summary>
    public ImmutableArray<TabItemInfo> Tabs { get; init; } = ImmutableArray<TabItemInfo>.Empty;

    public bool Equals(ExplorerContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (CurrentPath != other.CurrentPath) return false;

        var thisBreadcrumbs = Breadcrumbs.IsDefault ? ImmutableArray<string>.Empty : Breadcrumbs;
        var otherBreadcrumbs = other.Breadcrumbs.IsDefault ? ImmutableArray<string>.Empty : other.Breadcrumbs;
        if (thisBreadcrumbs.Length != otherBreadcrumbs.Length) return false;
        for (int i = 0; i < thisBreadcrumbs.Length; i++)
        {
            if (thisBreadcrumbs[i] != otherBreadcrumbs[i]) return false;
        }

        var thisSelected = SelectedItems.IsDefault ? ImmutableArray<string>.Empty : SelectedItems;
        var otherSelected = other.SelectedItems.IsDefault ? ImmutableArray<string>.Empty : other.SelectedItems;
        if (thisSelected.Length != otherSelected.Length) return false;
        for (int i = 0; i < thisSelected.Length; i++)
        {
            if (thisSelected[i] != otherSelected[i]) return false;
        }

        var thisTabs = Tabs.IsDefault ? ImmutableArray<TabItemInfo>.Empty : Tabs;
        var otherTabs = other.Tabs.IsDefault ? ImmutableArray<TabItemInfo>.Empty : other.Tabs;
        if (thisTabs.Length != otherTabs.Length) return false;
        for (int i = 0; i < thisTabs.Length; i++)
        {
            if (thisTabs[i] != otherTabs[i]) return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CurrentPath);

        if (!Breadcrumbs.IsDefault)
        {
            for (int i = 0; i < Breadcrumbs.Length; i++)
            {
                hash.Add(Breadcrumbs[i]);
            }
        }

        if (!SelectedItems.IsDefault)
        {
            for (int i = 0; i < SelectedItems.Length; i++)
            {
                hash.Add(SelectedItems[i]);
            }
        }

        if (!Tabs.IsDefault)
        {
            for (int i = 0; i < Tabs.Length; i++)
            {
                hash.Add(Tabs[i]);
            }
        }

        return hash.ToHashCode();
    }
}
