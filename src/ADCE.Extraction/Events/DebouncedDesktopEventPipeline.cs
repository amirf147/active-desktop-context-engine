// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ADCE.Core.Events;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;

namespace ADCE.Extraction.Events;

/// <summary>
/// High-throughput, zero-allocation event debouncing pipeline.
/// Consumes raw unmanaged DesktopEventToken structs, coalesces high-frequency bursts via trailing-edge timer,
/// guards against in-flight race conditions with monotonic epoch supersession, and dispatches UIA extraction on MTA workers.
/// </summary>
public sealed class DebouncedDesktopEventPipeline : IDisposable
{
    private readonly ChannelReader<DesktopEventToken> _inputReader;
    private readonly IExtractionEngine _extractor;
    private readonly Channel<DesktopContextSnapshot> _outputChannel;
    private readonly TimeSpan _debounceWindow;
    private readonly TimeSpan _maxDelayWindow;
    private readonly TimeProvider _timeProvider;

    private readonly CancellationTokenSource _cts = new();
    private Task? _processingTask;
    private long _currentEpoch;
    private int _isRunning;
    private bool _disposed;
    private DesktopContextSnapshot? _lastCommittedSnapshot;

    // Metrics & Telemetry
    private long _rawEventsReceived;
    private long _debouncedExtractionsTriggered;
    private long _extractionsCommitted;
    private long _supersededExtractionsDropped;
    private long _noiseEventsDropped;
    private long _duplicateSnapshotsSuppressed;

    public DebouncedDesktopEventPipeline(
        ChannelReader<DesktopEventToken> inputReader,
        IExtractionEngine extractor,
        TimeSpan? debounceWindow = null,
        TimeSpan? maxDelayWindow = null,
        int outputChannelCapacity = 32,
        TimeProvider? timeProvider = null)
    {
        _inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _debounceWindow = debounceWindow ?? TimeSpan.FromMilliseconds(50);
        _maxDelayWindow = maxDelayWindow ?? TimeSpan.FromMilliseconds(250);
        _timeProvider = timeProvider ?? TimeProvider.System;

        _outputChannel = Channel.CreateBounded<DesktopContextSnapshot>(new BoundedChannelOptions(outputChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = false
        });
    }

    public ChannelReader<DesktopContextSnapshot> SnapshotReader => _outputChannel.Reader;

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    public long RawEventsReceived => Interlocked.Read(ref _rawEventsReceived);
    public long DebouncedExtractionsTriggered => Interlocked.Read(ref _debouncedExtractionsTriggered);
    public long ExtractionsCommitted => Interlocked.Read(ref _extractionsCommitted);
    public long SupersededExtractionsDropped => Interlocked.Read(ref _supersededExtractionsDropped);
    public long NoiseEventsDropped => Interlocked.Read(ref _noiseEventsDropped);
    public long DuplicateSnapshotsSuppressed => Interlocked.Read(ref _duplicateSnapshotsSuppressed);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return; // Already running
        }

        _processingTask = Task.Run(() => ProcessingLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (Interlocked.CompareExchange(ref _isRunning, 0, 1) != 1)
        {
            return; // Already stopped
        }

        await _cts.CancelAsync();

        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException) { }
            _processingTask = null;
        }

        _outputChannel.Writer.TryComplete();
    }

    private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
    {
        long burstStartTimestamp = 0;
        DesktopEventToken latestToken = DesktopEventToken.Empty;

        try
        {
            while (await _inputReader.WaitToReadAsync(cancellationToken))
            {
                // 1. Drain all currently available tokens immediately without waiting
                while (_inputReader.TryRead(out var token))
                {
                    Interlocked.Increment(ref _rawEventsReceived);
                    if (token.IsValid)
                    {
                        latestToken = token;
                        long now = _timeProvider.GetTimestamp();
                        if (burstStartTimestamp == 0)
                        {
                            burstStartTimestamp = now;
                        }
                    }
                }

                if (!latestToken.IsValid)
                {
                    continue;
                }

                // 2. WP 3.4: Check if burst max delay clamp is exceeded (e.g. continuous typing storm >= 250ms)
                if (burstStartTimestamp != 0 && _timeProvider.GetElapsedTime(burstStartTimestamp) >= _maxDelayWindow)
                {
                    Interlocked.Increment(ref _debouncedExtractionsTriggered);
                    _ = DispatchExtractionAsync(latestToken, cancellationToken);
                    burstStartTimestamp = 0;
                    latestToken = DesktopEventToken.Empty;
                    continue;
                }

                // 3. Trailing-edge debounce delay: absorb subsequent event bursts (typing, cursor jitter)
                if (_debounceWindow > TimeSpan.Zero)
                {
                    await Task.Delay(_debounceWindow, _timeProvider, cancellationToken);

                    // Drain any additional tokens that arrived during the trailing-edge debounce window
                    while (_inputReader.TryRead(out var additionalToken))
                    {
                        Interlocked.Increment(ref _rawEventsReceived);
                        if (additionalToken.IsValid)
                        {
                            latestToken = additionalToken;
                            long now = _timeProvider.GetTimestamp();
                            if (burstStartTimestamp == 0)
                            {
                                burstStartTimestamp = now;
                            }
                        }
                    }
                }

                if (latestToken.IsValid)
                {
                    // 4. Dispatch extraction with monotonic epoch supersession guard
                    Interlocked.Increment(ref _debouncedExtractionsTriggered);
                    _ = DispatchExtractionAsync(latestToken, cancellationToken);
                    burstStartTimestamp = 0;
                    latestToken = DesktopEventToken.Empty;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful pipeline shutdown
        }
    }

    private async Task DispatchExtractionAsync(DesktopEventToken token, CancellationToken cancellationToken)
    {
        // Trap 4: Monotonic epoch supersession
        // Each new extraction gets a strictly increasing epoch ID
        long epoch = Interlocked.Increment(ref _currentEpoch);

        try
        {
            var snapshot = await _extractor.ExtractSnapshotAsync(token.Hwnd, cancellationToken);

            // Filter OS kernel subsystem arbitration and destroyed transient windows
            if (snapshot.Window.Hwnd == nint.Zero ||
                snapshot.Window.Title.Equals("Invalid Window Handle", StringComparison.OrdinalIgnoreCase) ||
                snapshot.Window.ProcessName.Equals("csrss", StringComparison.OrdinalIgnoreCase) ||
                snapshot.Window.ProcessName.Equals("dwm", StringComparison.OrdinalIgnoreCase) ||
                snapshot.Window.ClassName.Equals("OLEChannelWnd", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _noiseEventsDropped);
                return;
            }

            // Commit to output channel ONLY if no newer event arrived while UIA was extracting
            if (Interlocked.Read(ref _currentEpoch) == epoch)
            {
                // Value-Equality Deduplication: Drop redundant twin-wavelet events (e.g. Foreground + Focus pairs)
                if (_lastCommittedSnapshot != null && _lastCommittedSnapshot.HasSameSemanticState(snapshot))
                {
                    Interlocked.Increment(ref _duplicateSnapshotsSuppressed);
                    return;
                }

                _lastCommittedSnapshot = snapshot;
                _outputChannel.Writer.TryWrite(snapshot);
                Interlocked.Increment(ref _extractionsCommitted);
            }
            else
            {
                Interlocked.Increment(ref _supersededExtractionsDropped);
            }
        }
        catch (Exception)
        {
            // Resilient degradation: individual snapshot extraction failure does not crash the pipeline
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _outputChannel.Writer.TryComplete();
            _cts.Dispose();
            _disposed = true;
        }
    }
}
