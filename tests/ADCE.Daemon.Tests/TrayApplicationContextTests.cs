// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADCE.Core.Enums;
using ADCE.Core.Events;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Daemon.Configuration;
using ADCE.Daemon.Hosting;
using ADCE.Daemon.UI;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Xunit;

namespace ADCE.Daemon.Tests;

public sealed class TrayApplicationContextTests
{
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

    private sealed class DummyExtractor : IExtractionEngine
    {
        public ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateSampleSnapshot());

        public ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateSampleSnapshot());
    }

    private sealed class DummyHookProvider : IEventHookProvider
    {
        private readonly Channel<DesktopEventToken> _channel = Channel.CreateUnbounded<DesktopEventToken>();
        public ChannelReader<DesktopEventToken> EventReader => _channel.Reader;
        public bool IsRunning { get; private set; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() => Stop();
    }

    [Fact]
    public void TrayApplicationContext_CanInitializeAndDisposeCleanly()
    {
        var options = new DaemonOptions
        {
            IsHeadless = false,
            EnableSse = false,
            DatabasePath = ":memory:"
        };

        var store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = ":memory:" });
        var host = new DaemonHost(options, store, new DummyExtractor(), new DummyHookProvider());

        Exception? threadEx = null;

        var thread = new Thread(() =>
        {
            try
            {
                var context = new TrayApplicationContext(host, options);
                Assert.NotNull(context);
                context.Dispose();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool joined = thread.Join(5000);

        if (threadEx != null)
        {
            throw new InvalidOperationException($"STA thread failed: {threadEx.Message}", threadEx);
        }

        Assert.True(joined, "STA thread timed out joining");
        Assert.False(thread.IsAlive);
    }
}
