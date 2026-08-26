// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Storage.Cache;
using Xunit;

namespace ADCE.Storage.Tests;

public class InMemoryDesktopStateCacheTests
{
    [Fact]
    public void GetCurrentSnapshot_InitialState_ReturnsNull()
    {
        var cache = new InMemoryDesktopStateCache();
        Assert.Null(cache.GetCurrentSnapshot());
    }

    [Fact]
    public void UpdateCurrentSnapshot_SetsAtomicPointer_RetrievableInstantly()
    {
        var cache = new InMemoryDesktopStateCache();
        var snapshot = CreateTestSnapshot("Window 1");

        cache.UpdateCurrentSnapshot(snapshot);
        var retrieved = cache.GetCurrentSnapshot();

        Assert.NotNull(retrieved);
        Assert.Same(snapshot, retrieved);
        Assert.Equal("Window 1", retrieved.Window.Title);
    }

    [Fact]
    public void UpdateCurrentSnapshot_OverwritesExistingPointer_Atomically()
    {
        var cache = new InMemoryDesktopStateCache();
        var snapshot1 = CreateTestSnapshot("Window 1");
        var snapshot2 = CreateTestSnapshot("Window 2");

        cache.UpdateCurrentSnapshot(snapshot1);
        Assert.Same(snapshot1, cache.GetCurrentSnapshot());

        cache.UpdateCurrentSnapshot(snapshot2);
        Assert.Same(snapshot2, cache.GetCurrentSnapshot());
    }

    [Fact]
    public void Clear_ResetsPointerToNull()
    {
        var cache = new InMemoryDesktopStateCache();
        cache.UpdateCurrentSnapshot(CreateTestSnapshot("Window 1"));
        Assert.NotNull(cache.GetCurrentSnapshot());

        cache.Clear();
        Assert.Null(cache.GetCurrentSnapshot());
    }

    [Fact]
    public async Task ConcurrentAccess_RemainsConsistentWithoutCorruption()
    {
        var cache = new InMemoryDesktopStateCache();
        const int writers = 4;
        const int iterations = 10_000;

        var tasks = new Task[writers];
        for (int w = 0; w < writers; w++)
        {
            int writerId = w;
            tasks[w] = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    var s = CreateTestSnapshot($"Writer {writerId} - Iteration {i}");
                    cache.UpdateCurrentSnapshot(s);
                    var read = cache.GetCurrentSnapshot();
                    Assert.NotNull(read);
                }
            });
        }

        await Task.WhenAll(tasks);
        Assert.NotNull(cache.GetCurrentSnapshot());
    }

    private static DesktopContextSnapshot CreateTestSnapshot(string title)
    {
        return new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Default",
                MonitorIndex = 0,
                MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x1000,
                Title = title,
                ProcessName = "test.exe",
                Pid = 1234,
                ClassName = "TestClass",
                Archetype = DesktopAppArchetype.ClassicWin32,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080),
                IsMinimized = false,
                IsMaximized = false
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "Input",
                AutomationId = "input_1",
                ClassName = "Edit",
                BoundingBox = BoundingRectangle.Empty,
                SemanticZone = DesktopSemanticZone.EditorCodeBuffer,
                ValueSnippet = null
            },
            ExtractionDurationMs = 0.5
        };
    }
}
