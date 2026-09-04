// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Events;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Daemon.Configuration;
using ADCE.Daemon.Hosting;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Xunit;

namespace ADCE.Daemon.Tests;

public sealed class DaemonHostLifecycleTests
{
    private sealed class MockExtractionEngine : IExtractionEngine
    {
        public DesktopContextSnapshot NextSnapshot { get; set; } = CreateSampleSnapshot("Default");
        public int ExtractionCount { get; private set; }

        public ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default)
        {
            ExtractionCount++;
            return ValueTask.FromResult(NextSnapshot);
        }

        public ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default)
        {
            ExtractionCount++;
            return ValueTask.FromResult(NextSnapshot);
        }
    }

    private sealed class MockHookProvider : IEventHookProvider
    {
        private readonly Channel<DesktopEventToken> _channel = Channel.CreateUnbounded<DesktopEventToken>();
        public ChannelReader<DesktopEventToken> EventReader => _channel.Reader;
        public ChannelWriter<DesktopEventToken> EventWriter => _channel.Writer;

        public bool IsRunning { get; private set; }

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() => Stop();
    }

    private static DesktopContextSnapshot CreateSampleSnapshot(string title = "Sample App")
    {
        return new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 1,
                VirtualDesktopName = "Desktop 1"
            },
            Window = new WindowEnvelope
            {
                Hwnd = 12345,
                Pid = 999,
                ProcessName = "sample",
                Title = title,
                ClassName = "SampleClass",
                Archetype = DesktopAppArchetype.ClassicWin32,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                AutomationId = "editor",
                ElementName = "Code Area",
                ControlType = "Edit",
                BoundingBox = new BoundingRectangle(100, 100, 800, 600),
                SemanticZone = DesktopSemanticZone.EditorBuffer
            }
        };
    }

    [Fact]
    public async Task DaemonHost_Start_Pause_Resume_Stop_TransitionsStateAccurately()
    {
        var options = new DaemonOptions
        {
            IsHeadless = true,
            EnableSse = false,
            DatabasePath = ":memory:",
            DebounceMs = 10,
            MaxBurstMs = 50
        };

        var store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = ":memory:" });
        var extractor = new MockExtractionEngine { NextSnapshot = CreateSampleSnapshot("Test Editor") };
        var hookProvider = new MockHookProvider();

        var host = new DaemonHost(options, store, extractor, hookProvider);

        bool snapshotChangedFired = false;
        host.SnapshotChanged += snap => snapshotChangedFired = true;

        // 1. Start Host
        await host.StartAsync();
        Assert.True(hookProvider.IsRunning);
        Assert.False(host.IsPaused);
        Assert.True(snapshotChangedFired);

        var status = host.GetStatus();
        Assert.Equal(DaemonState.Running, status.State);
        Assert.NotNull(status.CurrentSnapshot);
        Assert.Equal("Test Editor", status.CurrentSnapshot.Window.Title);

        // 2. Pause Host
        host.Pause();
        Assert.True(host.IsPaused);
        Assert.Equal(DaemonState.Paused, host.GetStatus().State);

        // 3. Resume Host
        host.Resume();
        Assert.False(host.IsPaused);
        Assert.Equal(DaemonState.Running, host.GetStatus().State);

        // 4. Stop Host
        await host.StopAsync();
        Assert.Equal(DaemonState.Stopped, host.GetStatus().State);
    }

    [Fact]
    public async Task DaemonHost_PipelineEvent_UpdatesStoreAndFiresEvent()
    {
        var options = new DaemonOptions
        {
            IsHeadless = true,
            EnableSse = false,
            DatabasePath = ":memory:",
            DebounceMs = 10,
            MaxBurstMs = 50
        };

        var store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = ":memory:" });
        var extractor = new MockExtractionEngine { NextSnapshot = CreateSampleSnapshot("Window 1") };
        var hookProvider = new MockHookProvider();

        var host = new DaemonHost(options, store, extractor, hookProvider);

        await host.StartAsync();

        var tcs = new TaskCompletionSource<DesktopContextSnapshot>();
        host.SnapshotChanged += snap =>
        {
            if (snap.Window.Title == "Window 2")
            {
                tcs.TrySetResult(snap);
            }
        };

        extractor.NextSnapshot = CreateSampleSnapshot("Window 2");

        // Push event through mock hook
        hookProvider.EventWriter.TryWrite(new DesktopEventToken(0x0003, 12345, 100));

        // Await snapshot update deterministically
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Same(tcs.Task, completed);

        var current = host.GetCurrentSnapshot();
        Assert.NotNull(current);
        Assert.Equal("Window 2", current.Window.Title);

        await host.StopAsync();
    }
}
