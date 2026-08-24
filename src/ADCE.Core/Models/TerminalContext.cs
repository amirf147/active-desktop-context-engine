// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Linq;

namespace ADCE.Core.Models;

/// <summary>
/// Represents contextual state extracted from Windows Terminal or console windows.
/// Implements deep value-based equality across tab collections.
/// </summary>
public sealed record TerminalContext : IEquatable<TerminalContext>
{
    /// <summary>Active terminal tab title or shell name (e.g. "pwsh", "cmd").</summary>
    public string? ShellTitle { get; init; }

    /// <summary>Snippet of recent terminal output / active command line if accessible.</summary>
    public string? ActiveBuffer { get; init; }

    /// <summary>Open tabs in Windows Terminal.</summary>
    public IReadOnlyList<TabItemInfo> Tabs { get; init; } = [];

    public bool Equals(TerminalContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ShellTitle == other.ShellTitle &&
               ActiveBuffer == other.ActiveBuffer &&
               Tabs.SequenceEqual(other.Tabs);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ShellTitle);
        hash.Add(ActiveBuffer);
        foreach (var t in Tabs) hash.Add(t);
        return hash.ToHashCode();
    }
}
