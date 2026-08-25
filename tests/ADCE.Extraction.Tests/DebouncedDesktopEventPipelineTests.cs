// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Events;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Extraction.Events;
using Xunit;

namespace ADCE.Extraction.Tests;

public class DebouncedDesktopEventPipelineTests
{
    private sealed class MockExtractionEngine : IExtractionEngine
    {
        private readonly Func<nint, Task>? _onExtract;

        public MockExtractionEngine(Func<nint, Task>? onExtract = null)
        {
            _onExtract = onExtract;
        }

        public int CallCount;

        public async ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref CallCount);

            if (_onExtract != null)
            {
                await _onExtract(hwnd);
            }

            return new DesktopContextSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Workspace = new WorkspaceEnvelope
                {
                    VirtualDesktopId = Guid.Empty,
                    DesktopIndex = 0,
                    VirtualDesktopName = "Test Workspace",
                    MonitorIndex = 0,
                    MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
                },
                Window = new WindowEnvelope
                {
                    Hwnd = hwnd,
                    Title = $"Mock Window 0x{hwnd:X8}",
                    ProcessName = "TestApp.exe",
                    Pid = 1234,
                    ClassName = "TestClass",
                    Archetype = DesktopAppArchetype.ClassicWin32,
                    Bounds = new BoundingRectangle(0, 0, 800, 600),
                    IsMinimized = false,
                    IsMaximized = false
                },
                Focus = new FocusedControlInfo
                {
                    ControlType = "Edit",
                    ElementName = "Input",
                    AutomationId = "txtInput",
                    ClassName = "Edit",
                    BoundingBox = new BoundingRectangle(10, 10, 100, 20),
                    SemanticZone = DesktopSemanticZone.EditorCodeBuffer
                },
                ExtractionDurationMs = 1.5
            };
        }

        public ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return ExtractSnapshotAsync(new nint(0x100), cancellationToken);
        }
    }

    [Fact]
    public async Task BurstEvents_AreCoalescedIntoSingleExtraction()
    {
        var inputChannel = Channel.CreateUnbounded<DesktopEventToken>();
        var mockEngine = new MockExtractionEngine();

        using var pipeline = new DebouncedDesktopEventPipeline(
            inputChannel.Reader,
            mockEngine,
            debounceWindow: TimeSpan.FromMilliseconds(40));

        pipeline.Start();

        // Send a burst of 10 events within 5 ms
        for (int i = 1; i <= 10; i++)
        {
            inputChannel.Writer.TryWrite(new DesktopEventToken(
                (ushort)DesktopEventType.FocusChanged,
                new nint(0x1000 + i),
                (uint)(i * 10)));
        }

        // Wait for debounce window (40ms) + buffer
        await Task.Delay(120);

        await pipeline.StopAsync();

        // 10 events should have coalesced into 1 extraction
        Assert.Equal(10, pipeline.RawEventsReceived);
        Assert.Equal(1, mockEngine.CallCount);
        Assert.Equal(1, pipeline.ExtractionsCommitted);

        // Read the single committed snapshot from output channel
        Assert.True(pipeline.SnapshotReader.TryRead(out var snapshot));
        Assert.NotNull(snapshot);
        // The snapshot should correspond to the latest token in the burst (0x1000 + 10 = 0x100A)
        Assert.Equal(new nint(0x100A), snapshot.Window.Hwnd);
    }

    [Fact]
    public async Task MonotonicEpochSupersession_DropsStaleExtractions()
    {
        var inputChannel = Channel.CreateUnbounded<DesktopEventToken>();
        var extractBarrier = new SemaphoreSlim(0, 1);

        // Mock engine that artificially delays extraction until signaled
        var mockEngine = new MockExtractionEngine(async (hwnd) =>
        {
            if (hwnd == new nint(0xAAA))
            {
                // Slow first extraction
                await extractBarrier.WaitAsync(TimeSpan.FromSeconds(2));
            }
        });

        using var pipeline = new DebouncedDesktopEventPipeline(
            inputChannel.Reader,
            mockEngine,
            debounceWindow: TimeSpan.FromMilliseconds(20));

        pipeline.Start();

        // 1. Send first event
        inputChannel.Writer.TryWrite(new DesktopEventToken((ushort)DesktopEventType.ForegroundChanged, new nint(0xAAA), 100));

        // Wait for debounce window so extraction begins
        await Task.Delay(40);

        // 2. Send second event while first is in-flight
        inputChannel.Writer.TryWrite(new DesktopEventToken((ushort)DesktopEventType.ForegroundChanged, new nint(0xBBB), 200));

        // Wait for second debounce window to settle and second extraction to complete
        await Task.Delay(60);

        // 3. Unblock first (stale) extraction
        extractBarrier.Release();
        await Task.Delay(40);

        await pipeline.StopAsync();

        // First extraction was superseded and dropped; second extraction was committed
        Assert.True(pipeline.SupersededExtractionsDropped >= 1 || pipeline.ExtractionsCommitted == 1);
        Assert.True(pipeline.SnapshotReader.TryRead(out var snapshot));
        Assert.NotNull(snapshot);
        Assert.Equal(new nint(0xBBB), snapshot.Window.Hwnd);
    }

    [Fact]
    public async Task DuplicateSnapshots_AreSuppressedByValueEquality()
    {
        var inputChannel = Channel.CreateUnbounded<DesktopEventToken>();
        var mockEngine = new MockExtractionEngine();

        using var pipeline = new DebouncedDesktopEventPipeline(
            inputChannel.Reader,
            mockEngine,
            debounceWindow: TimeSpan.FromMilliseconds(10));

        pipeline.Start();

        // 1. Send first event
        inputChannel.Writer.TryWrite(new DesktopEventToken((ushort)DesktopEventType.ForegroundChanged, new nint(0x111), 10));
        await Task.Delay(30);

        // 2. Send second event for same HWND (mock engine returns identical snapshot)
        inputChannel.Writer.TryWrite(new DesktopEventToken((ushort)DesktopEventType.FocusChanged, new nint(0x111), 20));
        await Task.Delay(30);

        await pipeline.StopAsync();

        // Second event produced identical snapshot -> duplicate suppressed!
        Assert.Equal(1, pipeline.ExtractionsCommitted);
        Assert.Equal(1, pipeline.DuplicateSnapshotsSuppressed);
    }

    [Fact]
    public async Task NoiseSnapshots_AreDroppedWithoutEmitting()
    {
        var inputChannel = Channel.CreateUnbounded<DesktopEventToken>();
        // Mock engine that returns csrss or Invalid Window Handle
        var mockEngine = new MockExtractionEngine();

        using var pipeline = new DebouncedDesktopEventPipeline(
            inputChannel.Reader,
            mockEngine,
            debounceWindow: TimeSpan.FromMilliseconds(10));

        pipeline.Start();

        // Send event with HWND 0 (empty)
        inputChannel.Writer.TryWrite(new DesktopEventToken((ushort)DesktopEventType.ForegroundChanged, nint.Zero, 10));
        await Task.Delay(30);

        await pipeline.StopAsync();

        Assert.Equal(0, pipeline.ExtractionsCommitted);
    }

    [Fact]
    public async Task ContinuousTypingFlood_TriggersMaxDelayClamp_WithoutStarving()
    {
        var inputChannel = Channel.CreateUnbounded<DesktopEventToken>();
        var mockEngine = new MockExtractionEngine();

        // 50ms debounce window, 150ms max delay clamp
        using var pipeline = new DebouncedDesktopEventPipeline(
            inputChannel.Reader,
            mockEngine,
            debounceWindow: TimeSpan.FromMilliseconds(50),
            maxDelayWindow: TimeSpan.FromMilliseconds(150));

        pipeline.Start();

        // Simulate continuous typing: send an event every 20ms for 400ms (20 events)
        for (int i = 1; i <= 20; i++)
        {
            inputChannel.Writer.TryWrite(new DesktopEventToken(
                (ushort)DesktopEventType.FocusChanged,
                new nint(0x2000 + i), // Different HWND each time to avoid value deduplication
                (uint)(i * 10)));

            await Task.Delay(20);
        }

        await Task.Delay(100);
        await pipeline.StopAsync();

        // Under 400ms of continuous events with 150ms max clamp, at least 2 clamped/settled extractions must have triggered
        Assert.True(pipeline.DebouncedExtractionsTriggered >= 2,
            $"Expected at least 2 extractions during 400ms burst, but got {pipeline.DebouncedExtractionsTriggered}");
        Assert.True(pipeline.ExtractionsCommitted >= 2,
            $"Expected at least 2 committed extractions, but got {pipeline.ExtractionsCommitted}");
    }

    [Fact]
    public async Task TransientShellWindows_AreFilteredAsNoise()
    {
        var inputChannel = Channel.CreateUnbounded<DesktopEventToken>();

        // Engine that returns Shell_TrayWnd
        var shellEngine = new MockExtractionEngineCustom(hwnd => new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.Empty,
                DesktopIndex = 0,
                VirtualDesktopName = "Test",
                MonitorIndex = 0,
                MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Window = new WindowEnvelope
            {
                Hwnd = hwnd,
                Title = "",
                ProcessName = "explorer",
                Pid = 999,
                ClassName = "Shell_TrayWnd",
                Archetype = DesktopAppArchetype.Unknown,
                Bounds = new BoundingRectangle(0, 1040, 1920, 40),
                IsMinimized = false,
                IsMaximized = false
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Pane",
                ElementName = "",
                AutomationId = "",
                ClassName = "Shell_TrayWnd",
                BoundingBox = new BoundingRectangle(0, 1040, 1920, 40),
                SemanticZone = DesktopSemanticZone.Unknown
            },
            ExtractionDurationMs = 1.0
        });

        using var pipeline = new DebouncedDesktopEventPipeline(
            inputChannel.Reader,
            shellEngine,
            debounceWindow: TimeSpan.FromMilliseconds(10));

        pipeline.Start();

        inputChannel.Writer.TryWrite(new DesktopEventToken((ushort)DesktopEventType.FocusChanged, new nint(0x777), 10));
        await Task.Delay(30);

        await pipeline.StopAsync();

        Assert.Equal(0, pipeline.ExtractionsCommitted);
        Assert.Equal(1, pipeline.NoiseEventsDropped);
    }

    private sealed class MockExtractionEngineCustom : IExtractionEngine
    {
        private readonly Func<nint, DesktopContextSnapshot> _factory;

        public MockExtractionEngineCustom(Func<nint, DesktopContextSnapshot> factory)
        {
            _factory = factory;
        }

        public ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_factory(hwnd));
        }

        public ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return ExtractSnapshotAsync(new nint(0x100), cancellationToken);
        }
    }

    [Fact]
    public async Task IntraAppSelectionChanged_TriggersExtractionAndUpdatesSnapshot()
    {
        var inputChannel = Channel.CreateUnbounded<DesktopEventToken>();
        int extractCount = 0;

        var tabEngine = new MockExtractionEngineCustom(hwnd =>
        {
            int current = Interlocked.Increment(ref extractCount);
            return new DesktopContextSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Workspace = new WorkspaceEnvelope
                {
                    VirtualDesktopId = Guid.Empty,
                    DesktopIndex = 0,
                    VirtualDesktopName = "Workspace 1",
                    MonitorIndex = 0,
                    MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
                },
                Window = new WindowEnvelope
                {
                    Hwnd = hwnd,
                    Title = $"Browser - Tab {current}",
                    ProcessName = "waterfox",
                    Pid = 4321,
                    ClassName = "MozillaWindowClass",
                    Archetype = DesktopAppArchetype.Gecko,
                    Bounds = new BoundingRectangle(0, 0, 1280, 800),
                    IsMinimized = false,
                    IsMaximized = false
                },
                Focus = new FocusedControlInfo
                {
                    ControlType = "TabItem",
                    ElementName = $"Tab {current}",
                    AutomationId = $"tab-{current}",
                    ClassName = "tabbrowser-tab",
                    BoundingBox = new BoundingRectangle(10, 10, 100, 30),
                    SemanticZone = DesktopSemanticZone.DocumentContent
                },
                BrowserContext = new BrowserContext
                {
                    ActiveTab = $"Tab {current}",
                    TotalCount = 1,
                    Tabs = System.Collections.Immutable.ImmutableArray<TabItemInfo>.Empty,
                    ContainerType = "NativeTabstrip"
                },
                ExtractionDurationMs = 1.2
            };
        });

        using var pipeline = new DebouncedDesktopEventPipeline(
            inputChannel.Reader,
            tabEngine,
            debounceWindow: TimeSpan.FromMilliseconds(10));

        pipeline.Start();

        // Simulate Tab 1 selection
        inputChannel.Writer.TryWrite(new DesktopEventToken((ushort)DesktopEventType.SelectionChanged, new nint(0x888), 10));
        await Task.Delay(40);

        Assert.True(pipeline.SnapshotReader.TryRead(out var snap1));
        Assert.Equal("Browser - Tab 1", snap1?.Window.Title);

        // Simulate Tab 2 selection on same HWND (intra-app tab switch)
        inputChannel.Writer.TryWrite(new DesktopEventToken((ushort)DesktopEventType.SelectionChanged, new nint(0x888), 60));
        await Task.Delay(40);

        Assert.True(pipeline.SnapshotReader.TryRead(out var snap2));
        Assert.Equal("Browser - Tab 2", snap2?.Window.Title);

        await pipeline.StopAsync();
    }

    [Fact]
    public async Task Pipeline_StopAsync_CompletesCleanly()
    {
        var inputChannel = Channel.CreateUnbounded<DesktopEventToken>();
        var mockEngine = new MockExtractionEngine();

        var pipeline = new DebouncedDesktopEventPipeline(inputChannel.Reader, mockEngine);
        pipeline.Start();
        Assert.True(pipeline.IsRunning);

        await pipeline.StopAsync();
        Assert.False(pipeline.IsRunning);
        Assert.True(pipeline.SnapshotReader.Completion.IsCompleted);
    }
}
