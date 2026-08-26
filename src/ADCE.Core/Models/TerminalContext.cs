// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;

namespace ADCE.Core.Models;

/// <summary>
/// Represents contextual state extracted from Windows Terminal or console windows.
/// Implements deep value-based equality across tab collections with zero heap allocations.
/// </summary>
public sealed record TerminalContext : IEquatable<TerminalContext>
{
    /// <summary>Active terminal tab title or shell name (e.g. "pwsh", "cmd").</summary>
    public string? ShellTitle { get; init; }

    /// <summary>Snippet of recent terminal output / active command line if accessible.</summary>
    public string? ActiveBuffer { get; init; }

    /// <summary>Open tabs in Windows Terminal.</summary>
    public ImmutableArray<TabItemInfo> Tabs { get; init; } = ImmutableArray<TabItemInfo>.Empty;

    public bool Equals(TerminalContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (ShellTitle != other.ShellTitle || ActiveBuffer != other.ActiveBuffer)
            return false;

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
        hash.Add(ShellTitle);
        hash.Add(ActiveBuffer);

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
