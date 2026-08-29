// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;

namespace ADCE.Core.Models;

/// <summary>
/// Represents contextual state extracted from an IDE or code editor (e.g. VS Code, Antigravity, Visual Studio).
/// Implements deep value-based equality across tab and breadcrumb collections with zero heap allocations.
/// </summary>
public sealed record IdeContext : IEquatable<IdeContext>
{
    /// <summary>Resolved root directory of the active workspace or repository.</summary>
    public string? WorkspaceRoot { get; init; }

    /// <summary>Workspace-relative or absolute file path currently being edited.</summary>
    public string? ActiveFilePath { get; init; }

    /// <summary>Name or automation ID of the active sidebar view (e.g. "workbench.view.scm", "Explorer").</summary>
    public string? ActiveSidebarView { get; init; }

    /// <summary>True if the active editor is a Git diff or side-by-side comparison.</summary>
    public bool IsDiffEditor { get; init; }

    /// <summary>List of open editor tabs in the active editor group.</summary>
    public ImmutableArray<TabItemInfo> OpenEditorTabs { get; init; } = ImmutableArray<TabItemInfo>.Empty;

    /// <summary>Active document name or short edit buffer identifier.</summary>
    public string? EditBuffer { get; init; }

    /// <summary>Active Git branch name extracted from status bar or SCM view.</summary>
    public string? GitBranch { get; init; }

    /// <summary>Hierarchical path components from the Monaco breadcrumbs bar.</summary>
    public ImmutableArray<string> Breadcrumbs { get; init; } = ImmutableArray<string>.Empty;

    public bool Equals(IdeContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (WorkspaceRoot != other.WorkspaceRoot ||
            ActiveFilePath != other.ActiveFilePath ||
            ActiveSidebarView != other.ActiveSidebarView ||
            IsDiffEditor != other.IsDiffEditor ||
            EditBuffer != other.EditBuffer ||
            GitBranch != other.GitBranch)
        {
            return false;
        }

        var thisTabs = OpenEditorTabs.IsDefault ? ImmutableArray<TabItemInfo>.Empty : OpenEditorTabs;
        var otherTabs = other.OpenEditorTabs.IsDefault ? ImmutableArray<TabItemInfo>.Empty : other.OpenEditorTabs;
        if (thisTabs.Length != otherTabs.Length) return false;
        for (int i = 0; i < thisTabs.Length; i++)
        {
            if (thisTabs[i] != otherTabs[i]) return false;
        }

        var thisBreadcrumbs = Breadcrumbs.IsDefault ? ImmutableArray<string>.Empty : Breadcrumbs;
        var otherBreadcrumbs = other.Breadcrumbs.IsDefault ? ImmutableArray<string>.Empty : other.Breadcrumbs;
        if (thisBreadcrumbs.Length != otherBreadcrumbs.Length) return false;
        for (int i = 0; i < thisBreadcrumbs.Length; i++)
        {
            if (thisBreadcrumbs[i] != otherBreadcrumbs[i]) return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(WorkspaceRoot);
        hash.Add(ActiveFilePath);
        hash.Add(ActiveSidebarView);
        hash.Add(IsDiffEditor);
        hash.Add(EditBuffer);
        hash.Add(GitBranch);

        if (!OpenEditorTabs.IsDefault)
        {
            for (int i = 0; i < OpenEditorTabs.Length; i++)
            {
                hash.Add(OpenEditorTabs[i]);
            }
        }

        if (!Breadcrumbs.IsDefault)
        {
            for (int i = 0; i < Breadcrumbs.Length; i++)
            {
                hash.Add(Breadcrumbs[i]);
            }
        }

        return hash.ToHashCode();
    }
}
