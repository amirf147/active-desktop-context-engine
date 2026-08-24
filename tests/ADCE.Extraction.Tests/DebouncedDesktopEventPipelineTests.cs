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
