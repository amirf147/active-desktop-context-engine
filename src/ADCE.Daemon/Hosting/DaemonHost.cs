// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Daemon.Configuration;
using ADCE.Extraction.Engine;
using ADCE.Extraction.Events;
using ADCE.Mcp.Protocol;
using ADCE.Mcp.Server;
using ADCE.Mcp.Transports;
using ADCE.Storage.Database;
using ADCE.Storage.Options;

namespace ADCE.Daemon.Hosting;

/// <summary>
/// Root host coordinator managing the lifecycle of the WinEvent hook provider, debounced pipeline,
/// UIA extraction engine, SQLite state store, and MCP JSON-RPC servers.
/// </summary>
public sealed class DaemonHost : IAsyncDisposable, IDisposable
{
    private readonly DaemonOptions _options;
    private readonly IDesktopStateStore _store;
    private readonly IExtractionEngine _extractor;
    private readonly IEventHookProvider _hookProvider;
    private readonly DebouncedDesktopEventPipeline _pipeline;
    private readonly IMcpHandler _mcpHandler;

    private readonly HttpSseMcpTransport? _sseTransport;
    private readonly McpServer? _sseServer;
    private readonly StdioMcpTransport? _stdioTransport;
    private readonly McpServer? _stdioServer;

    private readonly CancellationTokenSource _cts = new();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    private Task? _snapshotConsumerTask;
    private Task? _sseServerTask;
    private Task? _stdioServerTask;

    private volatile bool _isPaused;
    private DaemonState _state = DaemonState.Starting;
    private long _totalSnapshotsExtracted;
    private string? _lastError;
    private bool _disposed;

    /// <summary>
    /// Event fired whenever a new debounced desktop snapshot is ingested and committed.
    /// </summary>
    public event Action<DesktopContextSnapshot>? SnapshotChanged;

    /// <summary>
    /// Event fired when the host state changes.
    /// </summary>
    public event Action<DaemonState>? StateChanged;

    /// <summary>
    /// Gets the operational configuration of this host.
    /// </summary>
    public DaemonOptions Options => _options;

    /// <summary>
    /// Gets whether event extraction is currently paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Gets the underlying state store.
    /// </summary>
    public IDesktopStateStore StateStore => _store;

    /// <summary>
    /// Initializes a new instance of <see cref="DaemonHost"/> with the provided options or default settings.
    /// </summary>
    public DaemonHost(
        DaemonOptions? options = null,
        IDesktopStateStore? store = null,
        IExtractionEngine? extractor = null,
        IEventHookProvider? hookProvider = null)
    {
        _options = options ?? new DaemonOptions();

        var dbPath = _options.ResolveEffectiveDatabasePath();
        _store = store ?? new SqliteDesktopStateStore(new StorageOptions
        {
            DatabasePath = dbPath
        });

        _extractor = extractor ?? new UiaExtractionEngine();
        _hookProvider = hookProvider ?? new WinEventHookProvider();

        _pipeline = new DebouncedDesktopEventPipeline(
            _hookProvider.EventReader,
            _extractor,
            TimeSpan.FromMilliseconds(_options.DebounceMs),
            TimeSpan.FromMilliseconds(_options.MaxBurstMs)
        );

        _mcpHandler = new DesktopContextMcpHandler(_store);

        if (_options.EnableSse)
        {
            _sseTransport = new HttpSseMcpTransport(_options.Port);
            _sseServer = new McpServer(_sseTransport, _mcpHandler, new ServerInfo("ADCE.Daemon.SSE", "1.0.0"));
        }

        if (_options.IsStdio)
        {
            _stdioTransport = new StdioMcpTransport();
            _stdioServer = new McpServer(_stdioTransport, _mcpHandler, new ServerInfo("ADCE.Daemon.Stdio", "1.0.0"));
        }
    }

    /// <summary>
    /// Starts all subsystems: storage initialization, initial snapshot capture, hook provider,
    /// debounced event pipeline, snapshot consumer, and MCP servers.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            SetState(DaemonState.Starting);

            // 1. Initialize SQLite Database & L1 Cache if supported
            if (_store is SqliteDesktopStateStore sqliteStore)
            {
                await sqliteStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }

            // 2. Capture Initial Foreground Snapshot
            try
            {
                var initialSnapshot = await _extractor.ExtractForegroundSnapshotAsync(cancellationToken).ConfigureAwait(false);
                if (initialSnapshot != null)
                {
                    _store.UpdateCurrentSnapshot(initialSnapshot);
                    Interlocked.Increment(ref _totalSnapshotsExtracted);
                    SnapshotChanged?.Invoke(initialSnapshot);
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Initial snapshot extraction warning: {ex.Message}";
            }

            // 3. Start Hook Provider & Debounced Pipeline
            _hookProvider.Start();
            _pipeline.Start();

            // 4. Start Snapshot Consumer Task
            _snapshotConsumerTask = Task.Run(() => ConsumeSnapshotsAsync(_cts.Token), _cts.Token);

            // 5. Start MCP Servers
            if (_sseTransport != null && _sseServer != null)
            {
                _sseTransport.Start();
                _sseServerTask = Task.Run(() => _sseServer.RunAsync(_cts.Token), _cts.Token);
            }

            if (_stdioTransport != null && _stdioServer != null)
            {
                _stdioServerTask = Task.Run(() => _stdioServer.RunAsync(_cts.Token), _cts.Token);
            }

            SetState(DaemonState.Running);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            SetState(DaemonState.Faulted);
            throw;
        }
    }

    /// <summary>
    /// Pauses UIA extraction of incoming desktop events while maintaining the message loop.
    /// </summary>
    public void Pause()
    {
        if (_state == DaemonState.Running)
        {
            _isPaused = true;
            SetState(DaemonState.Paused);
        }
    }

    /// <summary>
    /// Resumes UIA extraction and immediately captures the current foreground window.
    /// </summary>
    public void Resume()
    {
        if (_state == DaemonState.Paused)
        {
            _isPaused = false;
            SetState(DaemonState.Running);

            // Trigger immediate foreground refresh
            _ = Task.Run(async () =>
            {
                try
                {
                    var snapshot = await _extractor.ExtractForegroundSnapshotAsync(_cts.Token).ConfigureAwait(false);
                    if (snapshot != null)
                    {
                        _store.UpdateCurrentSnapshot(snapshot);
                        Interlocked.Increment(ref _totalSnapshotsExtracted);
                        SnapshotChanged?.Invoke(snapshot);
                    }
                }
                catch { }
            });
        }
    }

    /// <summary>
    /// Obtains an immutable telemetry snapshot of the live daemon health and metrics.
    /// </summary>
    public DaemonStatus GetStatus()
    {
        var currentSnapshot = _store.GetCurrentSnapshot();
        var now = DateTimeOffset.UtcNow;

        return new DaemonStatus
        {
            State = _state,
            StartTime = _startTime,
            Uptime = now - _startTime,
            TotalEventsReceived = _pipeline.RawEventsReceived,
            TotalSnapshotsExtracted = Interlocked.Read(ref _totalSnapshotsExtracted),
            TotalMcpRequestsServed = 0,
            CurrentSnapshot = currentSnapshot,
            SsePort = _options.Port,
            IsSseActive = _sseTransport != null && _state == DaemonState.Running,
            IsStdioActive = _stdioTransport != null && _state == DaemonState.Running,
            DatabasePath = _options.ResolveEffectiveDatabasePath(),
            LastError = _lastError
        };
    }

    /// <summary>
    /// Gets the current active desktop snapshot directly from the L1 state cache.
    /// </summary>
    public DesktopContextSnapshot? GetCurrentSnapshot()
    {
        return _store.GetCurrentSnapshot();
    }

    /// <summary>
    /// Gracefully stops all workers, hook providers, pipelines, and transports.
    /// </summary>
    public async Task StopAsync()
    {
        if (_state == DaemonState.Stopped) return;

        SetState(DaemonState.Stopped);

        try
        {
            await _cts.CancelAsync().ConfigureAwait(false);

            _hookProvider.Dispose();
            await _pipeline.StopAsync().ConfigureAwait(false);

            if (_snapshotConsumerTask != null)
            {
                try { await _snapshotConsumerTask.ConfigureAwait(false); } catch { }
            }

            if (_sseTransport != null)
            {
                await _sseTransport.DisposeAsync().ConfigureAwait(false);
            }

            if (_sseServerTask != null)
            {
                try { await _sseServerTask.ConfigureAwait(false); } catch { }
            }

            if (_stdioTransport != null)
            {
                await _stdioTransport.DisposeAsync().ConfigureAwait(false);
            }

            if (_stdioServerTask != null)
            {
                try { await _stdioServerTask.ConfigureAwait(false); } catch { }
            }

            if (_store is IAsyncDisposable asyncStore)
            {
                await asyncStore.DisposeAsync().ConfigureAwait(false);
            }
            else if (_store is IDisposable dispStore)
            {
                dispStore.Dispose();
            }

            if (_extractor is IDisposable dispExtractor)
            {
                dispExtractor.Dispose();
            }
        }
        catch (Exception ex)
        {
            _lastError = $"Stop error: {ex.Message}";
        }
    }

    private async Task ConsumeSnapshotsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var snapshot in _pipeline.SnapshotReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_isPaused) continue;

                _store.UpdateCurrentSnapshot(snapshot);
                Interlocked.Increment(ref _totalSnapshotsExtracted);
                SnapshotChanged?.Invoke(snapshot);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _lastError = $"Snapshot consumer fault: {ex.Message}";
        }
    }

    private void SetState(DaemonState newState)
    {
        _state = newState;
        StateChanged?.Invoke(newState);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAsync().GetAwaiter().GetResult();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
