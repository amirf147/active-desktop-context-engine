// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Linq;

namespace ADCE.Core.Models;

/// <summary>
/// Represents contextual state extracted from an IDE or code editor (e.g. VS Code, Antigravity, Visual Studio).
/// Implements deep value-based equality across tab and breadcrumb collections.
/// </summary>
public sealed record IdeContext : IEquatable<IdeContext>
{
    /// <summary>Workspace-relative or absolute file path currently being edited.</summary>
    public string? ActiveFilePath { get; init; }

    /// <summary>Name of the active sidebar view (e.g. "Explorer (Ctrl+Shift+E)", "Source Control").</summary>
    public string? ActiveSidebarView { get; init; }

    /// <summary>List of open editor tabs in the active editor group.</summary>
    public IReadOnlyList<TabItemInfo> OpenEditorTabs { get; init; } = [];

    /// <summary>Active document name or short edit buffer identifier.</summary>
    public string? EditBuffer { get; init; }

    /// <summary>Active Git branch name extracted from status bar or SCM view.</summary>
    public string? GitBranch { get; init; }

    /// <summary>Hierarchical path components from the Monaco breadcrumbs bar.</summary>
    public IReadOnlyList<string> Breadcrumbs { get; init; } = [];

    public bool Equals(IdeContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ActiveFilePath == other.ActiveFilePath &&
               ActiveSidebarView == other.ActiveSidebarView &&
               EditBuffer == other.EditBuffer &&
               GitBranch == other.GitBranch &&
               OpenEditorTabs.SequenceEqual(other.OpenEditorTabs) &&
               Breadcrumbs.SequenceEqual(other.Breadcrumbs);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ActiveFilePath);
        hash.Add(ActiveSidebarView);
        hash.Add(EditBuffer);
        hash.Add(GitBranch);
        foreach (var tab in OpenEditorTabs) hash.Add(tab);
        foreach (var b in Breadcrumbs) hash.Add(b);
        return hash.ToHashCode();
    }
}
