// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Linq;

namespace ADCE.Core.Models;

/// <summary>
/// Represents contextual state extracted from a web browser (e.g. Waterfox, Chrome, Firefox, Edge).
/// Implements deep value-based equality across tab collections.
/// </summary>
public sealed record BrowserContext : IEquatable<BrowserContext>
{
    /// <summary>Type of tab container (e.g. "TreeStyleTab", "NativeTabstrip", "PinnedSidebar").</summary>
    public string? ContainerType { get; init; }

    /// <summary>Total count of open tabs in the active window container.</summary>
    public int TotalCount { get; init; }

    /// <summary>Title of the currently active/selected browser tab.</summary>
    public string? ActiveTab { get; init; }

    /// <summary>List of open tab items extracted from the browser tabstrip.</summary>
    public IReadOnlyList<TabItemInfo> Tabs { get; init; } = [];

    /// <summary>URL or search text extracted from the browser address bar (if captured).</summary>
    public string? UrlAddress { get; init; }

    public bool Equals(BrowserContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ContainerType == other.ContainerType &&
               TotalCount == other.TotalCount &&
               ActiveTab == other.ActiveTab &&
               UrlAddress == other.UrlAddress &&
               Tabs.SequenceEqual(other.Tabs);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ContainerType);
        hash.Add(TotalCount);
        hash.Add(ActiveTab);
        hash.Add(UrlAddress);
        foreach (var tab in Tabs) hash.Add(tab);
        return hash.ToHashCode();
    }
}
