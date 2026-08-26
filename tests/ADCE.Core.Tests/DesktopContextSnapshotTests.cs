// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using Xunit;

namespace ADCE.Core.Tests;

public class DesktopContextSnapshotTests
{
    [Fact]
    public void Snapshot_RecordEquality_WorksByValue()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var guid = Guid.NewGuid();

        var snapshotA = new DesktopContextSnapshot
        {
            Timestamp = timestamp,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = guid,
                DesktopIndex = 1,
                VirtualDesktopName = "Development"
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x00DB083E,
                Title = "Antigravity IDE",
                ProcessName = "Antigravity.exe",
                Pid = 26420,
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "CONTEXT.md",
                AutomationId = "editor",
                BoundingBox = new BoundingRectangle(400, 120, 1200, 800),
                SemanticZone = DesktopSemanticZone.EditorCodeBuffer
            },
            IdeContext = new IdeContext
            {
                ActiveFilePath = "docs/CONTEXT.md",
                ActiveSidebarView = "Explorer",
                OpenEditorTabs =
                [
                    new() { Index = 1, Title = "CONTEXT.md", IsActive = true }
                ]
            }
        };

        var snapshotB = new DesktopContextSnapshot
        {
            Timestamp = timestamp,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = guid,
                DesktopIndex = 1,
                VirtualDesktopName = "Development"
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x00DB083E,
                Title = "Antigravity IDE",
                ProcessName = "Antigravity.exe",
                Pid = 26420,
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "CONTEXT.md",
                AutomationId = "editor",
                BoundingBox = new BoundingRectangle(400, 120, 1200, 800),
                SemanticZone = DesktopSemanticZone.EditorCodeBuffer
            },
            IdeContext = new IdeContext
            {
                ActiveFilePath = "docs/CONTEXT.md",
                ActiveSidebarView = "Explorer",
                OpenEditorTabs =
                [
                    new() { Index = 1, Title = "CONTEXT.md", IsActive = true }
                ]
            }
        };

        // Value-based equality (records compare by value, not reference)
        Assert.Equal(snapshotA, snapshotB);
        Assert.True(snapshotA == snapshotB);
    }

    [Fact]
    public void Snapshot_NonDestructiveMutation_CreatesNewInstance()
    {
        var original = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Main"
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x00123456,
                Title = "Old Title",
                ProcessName = "app.exe",
                Pid = 1000,
                ClassName = "WindowClass"
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Window",
                ElementName = "Main",
                BoundingBox = new BoundingRectangle(0, 0, 800, 600)
            }
        };

        var updated = original with
        {
            Window = original.Window with { Title = "New Title" }
        };

        Assert.NotSame(original, updated);
        Assert.Equal("Old Title", original.Window.Title);
        Assert.Equal("New Title", updated.Window.Title);
        Assert.NotEqual(original, updated);
    }

    [Fact]
    public void BoundingRectangle_Calculations_AreAccurate()
    {
        var rect = new BoundingRectangle(100, 200, 800, 600);

        Assert.Equal(100, rect.Left);
        Assert.Equal(200, rect.Top);
        Assert.Equal(800, rect.Width);
        Assert.Equal(600, rect.Height);
        Assert.Equal(900, rect.Right);
        Assert.Equal(800, rect.Bottom);
        Assert.False(rect.IsEmpty);

        var empty = BoundingRectangle.Empty;
        Assert.True(empty.IsEmpty);
    }

    [Fact]
    public void DefaultImmutableArray_Equality_DoesNotThrow()
    {
        // Verified Gate 2 fix: uninitialized default ImmutableArray must not throw NullReferenceException in Equals()
        var contextDefaultA = new IdeContext();
        var contextDefaultB = new IdeContext();
        var contextEmpty = new IdeContext
        {
            OpenEditorTabs = [],
            Breadcrumbs = []
        };

        Assert.Equal(contextDefaultA, contextDefaultB);
        Assert.Equal(contextDefaultA, contextEmpty);

        var browserDefaultA = new BrowserContext();
        var browserEmpty = new BrowserContext { Tabs = [] };
        Assert.Equal(browserDefaultA, browserEmpty);
    }
}
