// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Storage.Cache;
using ADCE.Storage.Options;
using Microsoft.Data.Sqlite;

namespace ADCE.Storage.Database;

/// <summary>
/// Production-grade time-series desktop state repository and in-memory live cache.
/// Implements IDesktopStateStore with SQLite WAL persistence, single-writer background queue,
/// sub-millisecond atomic L1 cache reads, and bounded periodic maintenance.
/// </summary>
public sealed class SqliteDesktopStateStore : IDesktopStateStore, IAsyncDisposable, IDisposable
{
    private readonly StorageOptions _options;
    private readonly InMemoryDesktopStateCache _cache = new();
    private readonly Channel<DesktopContextSnapshot> _writeChannel;
    private readonly string _connectionString;

    private SqliteConnection? _writerConnection;
    private Task? _backgroundWriterTask;
    private readonly CancellationTokenSource _cts = new();

    private long _totalSnapshotsIngested;
    private long _totalSnapshotsCommitted;
    private long _commitsSinceLastMaintenance;
    private long _lastMaintenanceTimestamp;
    private bool _disposed;

    public SqliteDesktopStateStore(StorageOptions? options = null)
    {
        _options = options ?? new StorageOptions();

        if (_options.DatabasePath.Equals(":memory:", StringComparison.OrdinalIgnoreCase) ||
            _options.DatabasePath.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            _connectionString = "Data Source=adce_inmemory;Mode=Memory;Cache=Shared";
        }
        else
        {
            string? dir = Path.GetDirectoryName(_options.DatabasePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _options.DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                DefaultTimeout = 2
            }.ToString();
        }

        _writeChannel = Channel.CreateBounded<DesktopContextSnapshot>(new BoundedChannelOptions(_options.WriteQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });

        _lastMaintenanceTimestamp = Stopwatch.GetTimestamp();
    }

    public long TotalSnapshotsIngested => Interlocked.Read(ref _totalSnapshotsIngested);
    public long TotalSnapshotsCommitted => Interlocked.Read(ref _totalSnapshotsCommitted);

    /// <summary>
    /// Initializes database connection, runs table and index migrations, and starts the background writer task.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _writerConnection = new SqliteConnection(_connectionString);
        await _writerConnection.OpenAsync(cancellationToken);

        await InitializePragmasAndSchemaAsync(_writerConnection, cancellationToken);

        _backgroundWriterTask = Task.Run(() => BackgroundWriterLoopAsync(_cts.Token), CancellationToken.None);
    }

    /// <summary>
    /// Synchronous initialization helper for environments where async constructor initialization is impractical.
    /// </summary>
    public void Initialize()
    {
        InitializeAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public DesktopContextSnapshot? GetCurrentSnapshot()
    {
        return _cache.GetCurrentSnapshot();
    }

    /// <inheritdoc />
    public void UpdateCurrentSnapshot(DesktopContextSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Interlocked.Increment(ref _totalSnapshotsIngested);

        // 1. Update L1 In-Memory atomic cache (< 0.001 ms)
        _cache.UpdateCurrentSnapshot(snapshot);

        // 2. Enqueue for asynchronous SQLite WAL persistence
        _writeChannel.Writer.TryWrite(snapshot);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DesktopContextSnapshot> GetHistoryAsync(
        DateTimeOffset since,
        int limit = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long sinceUnixMs = since.ToUnixTimeMilliseconds();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT snapshot_json FROM desktop_snapshots
            WHERE timestamp_unix_ms >= @sinceUnixMs
            ORDER BY timestamp_unix_ms DESC, id DESC
            LIMIT @limit;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@sinceUnixMs", sinceUnixMs);
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string json = reader.GetString(0);
            var snapshot = JsonSerializer.Deserialize<DesktopContextSnapshot>(json, AdceJsonSerializerOptions.Default);
            if (snapshot != null)
            {
                yield return snapshot;
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DesktopContextSnapshot> SearchHistoryAsync(
        string query,
        int limit = 50,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            yield break;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT snapshot_json FROM desktop_snapshots
            WHERE window_title LIKE @q
               OR process_name LIKE @q
               OR active_file_or_tab LIKE @q
               OR focus_element_name LIKE @q
            ORDER BY timestamp_unix_ms DESC, id DESC
            LIMIT @limit;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@q", $"%{query}%");
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string json = reader.GetString(0);
            var snapshot = JsonSerializer.Deserialize<DesktopContextSnapshot>(json, AdceJsonSerializerOptions.Default);
            if (snapshot != null)
            {
                yield return snapshot;
            }
        }
    }

    private async Task BackgroundWriterLoopAsync(CancellationToken cancellationToken)
    {
        if (_writerConnection == null) return;

        const string insertSql = """
            INSERT INTO desktop_snapshots (
                timestamp_utc,
                timestamp_unix_ms,
                hwnd,
                window_title,
                process_name,
                class_name,
                archetype,
                focus_control_type,
                focus_element_name,
                focus_semantic_zone,
                pane_location,
                active_view,
                section_name,
                semantic_path,
                active_file_or_tab,
                container_path,
                container_classes,
                snapshot_json
            ) VALUES (
                @timestamp_utc,
                @timestamp_unix_ms,
                @hwnd,
                @window_title,
                @process_name,
                @class_name,
                @archetype,
                @focus_control_type,
                @focus_element_name,
                @focus_semantic_zone,
                @pane_location,
                @active_view,
                @section_name,
                @semantic_path,
                @active_file_or_tab,
                @container_path,
                @container_classes,
                @snapshot_json
            );
            """;

        try
        {
            while (await _writeChannel.Reader.WaitToReadAsync(CancellationToken.None))
            {
                while (_writeChannel.Reader.TryRead(out var snapshot))
                {
                    try
                    {
                        await using var command = new SqliteCommand(insertSql, _writerConnection);
                        BindInsertParameters(command, snapshot);
                        await command.ExecuteNonQueryAsync(CancellationToken.None);

                        Interlocked.Increment(ref _totalSnapshotsCommitted);
                        _commitsSinceLastMaintenance++;

                        // Check maintenance cadence
                        if (_commitsSinceLastMaintenance >= _options.MaintenanceCommitCadence ||
                            Stopwatch.GetElapsedTime(_lastMaintenanceTimestamp) >= _options.MaintenanceInterval)
                        {
                            await RunMaintenancePassAsync(_writerConnection);
                            _commitsSinceLastMaintenance = 0;
                            _lastMaintenanceTimestamp = Stopwatch.GetTimestamp();
                        }
                    }
                    catch (Exception)
                    {
                        // Resilient degradation: write error does not terminate background persistence loop
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private static void BindInsertParameters(SqliteCommand command, DesktopContextSnapshot snapshot)
    {
        string activeFileOrTab = ExtractActiveFileOrTab(snapshot);
        string json = JsonSerializer.Serialize(snapshot, AdceJsonSerializerOptions.Default);

        string containerPath = snapshot.Focus.ContainerPath.IsDefault || snapshot.Focus.ContainerPath.IsEmpty
            ? string.Empty
            : string.Join('/', snapshot.Focus.ContainerPath);

        string containerClasses = snapshot.Focus.ContainerClasses.IsDefault || snapshot.Focus.ContainerClasses.IsEmpty
            ? string.Empty
            : string.Join('/', snapshot.Focus.ContainerClasses);

        string semanticPath = snapshot.Focus.SemanticPath.IsDefault || snapshot.Focus.SemanticPath.IsEmpty
            ? string.Empty
            : string.Join('/', snapshot.Focus.SemanticPath);

        command.Parameters.AddWithValue("@timestamp_utc", snapshot.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("@timestamp_unix_ms", snapshot.Timestamp.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@hwnd", snapshot.Window.Hwnd.ToInt64());
        command.Parameters.AddWithValue("@window_title", snapshot.Window.Title);
        command.Parameters.AddWithValue("@process_name", snapshot.Window.ProcessName);
        command.Parameters.AddWithValue("@class_name", snapshot.Window.ClassName);
        command.Parameters.AddWithValue("@archetype", (int)snapshot.Window.Archetype);
        command.Parameters.AddWithValue("@focus_control_type", snapshot.Focus.ControlType);
        command.Parameters.AddWithValue("@focus_element_name", snapshot.Focus.ElementName);
        command.Parameters.AddWithValue("@focus_semantic_zone", (int)snapshot.Focus.SemanticZone);
        command.Parameters.AddWithValue("@pane_location", snapshot.Focus.PaneLocation.ToSnakeCase());
        command.Parameters.AddWithValue("@active_view", snapshot.Focus.ActiveView ?? string.Empty);
        command.Parameters.AddWithValue("@section_name", snapshot.Focus.SectionName ?? string.Empty);
        command.Parameters.AddWithValue("@semantic_path", semanticPath);
        command.Parameters.AddWithValue("@active_file_or_tab", activeFileOrTab);
        command.Parameters.AddWithValue("@container_path", containerPath);
        command.Parameters.AddWithValue("@container_classes", containerClasses);
        command.Parameters.AddWithValue("@snapshot_json", json);
    }

    private static string ExtractActiveFileOrTab(DesktopContextSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.IdeContext?.ActiveFilePath))
            return snapshot.IdeContext.ActiveFilePath;

        if (!string.IsNullOrWhiteSpace(snapshot.BrowserContext?.ActiveTab))
            return snapshot.BrowserContext.ActiveTab;

        if (!string.IsNullOrWhiteSpace(snapshot.ExplorerContext?.CurrentPath))
            return snapshot.ExplorerContext.CurrentPath;

        if (!string.IsNullOrWhiteSpace(snapshot.TerminalContext?.ShellTitle))
            return snapshot.TerminalContext.ShellTitle;

        return snapshot.Focus.ElementName;
    }

    private static async Task InitializePragmasAndSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string pragmas = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;
            PRAGMA busy_timeout = 2000;
            """;

        await using (var pragmaCmd = new SqliteCommand(pragmas, connection))
        {
            await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS desktop_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp_utc TEXT NOT NULL,
                timestamp_unix_ms INTEGER NOT NULL,
                hwnd INTEGER NOT NULL,
                window_title TEXT NOT NULL,
                process_name TEXT NOT NULL,
                class_name TEXT NOT NULL,
                archetype INTEGER NOT NULL,
                focus_control_type TEXT,
                focus_element_name TEXT,
                focus_semantic_zone INTEGER NOT NULL,
                pane_location TEXT DEFAULT 'unknown',
                active_view TEXT DEFAULT '',
                section_name TEXT DEFAULT '',
                semantic_path TEXT DEFAULT '',
                active_file_or_tab TEXT,
                container_path TEXT DEFAULT '',
                container_classes TEXT DEFAULT '',
                snapshot_json TEXT NOT NULL
            );
            """;

        await using (var schemaCmd = new SqliteCommand(createTableSql, connection))
        {
            await schemaCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Run migrations on existing databases if columns are missing BEFORE index creation
        try
        {
            await using var migCmd = new SqliteCommand("ALTER TABLE desktop_snapshots ADD COLUMN container_path TEXT DEFAULT '';", connection);
            await migCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var migCmd2 = new SqliteCommand("ALTER TABLE desktop_snapshots ADD COLUMN container_classes TEXT DEFAULT '';", connection);
            await migCmd2.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var migCmd3 = new SqliteCommand("ALTER TABLE desktop_snapshots ADD COLUMN pane_location TEXT DEFAULT 'unknown';", connection);
            await migCmd3.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var migCmd4 = new SqliteCommand("ALTER TABLE desktop_snapshots ADD COLUMN active_view TEXT DEFAULT '';", connection);
            await migCmd4.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var migCmd5 = new SqliteCommand("ALTER TABLE desktop_snapshots ADD COLUMN section_name TEXT DEFAULT '';", connection);
            await migCmd5.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var migCmd6 = new SqliteCommand("ALTER TABLE desktop_snapshots ADD COLUMN semantic_path TEXT DEFAULT '';", connection);
            await migCmd6.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        const string createIndexesSql = """
            CREATE INDEX IF NOT EXISTS idx_snapshots_time_desc ON desktop_snapshots(timestamp_unix_ms DESC, id DESC);
            CREATE INDEX IF NOT EXISTS idx_snapshots_process ON desktop_snapshots(process_name, timestamp_unix_ms DESC);
            CREATE INDEX IF NOT EXISTS idx_snapshots_file_tab ON desktop_snapshots(active_file_or_tab);
            CREATE INDEX IF NOT EXISTS idx_snapshots_container ON desktop_snapshots(process_name, container_path, timestamp_unix_ms DESC);
            """;

        await using (var indexCmd = new SqliteCommand(createIndexesSql, connection))
        {
            await indexCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task RunMaintenancePassAsync(SqliteConnection connection)
    {
        try
        {
            long cutoffUnixMs = DateTimeOffset.UtcNow.Subtract(_options.RetentionWindow).ToUnixTimeMilliseconds();

            const string pruneSql = """
                DELETE FROM desktop_snapshots WHERE timestamp_unix_ms < @cutoff;
                """;

            await using (var pruneCmd = new SqliteCommand(pruneSql, connection))
            {
                pruneCmd.Parameters.AddWithValue("@cutoff", cutoffUnixMs);
                await pruneCmd.ExecuteNonQueryAsync(CancellationToken.None);
            }

            // Enforce max retention count clamp
            if (_options.MaxRetentionCount > 0)
            {
                const string countPruneSql = """
                    DELETE FROM desktop_snapshots WHERE id IN (
                        SELECT id FROM desktop_snapshots ORDER BY timestamp_unix_ms DESC, id DESC LIMIT -1 OFFSET @maxCount
                    );
                    """;

                await using var countCmd = new SqliteCommand(countPruneSql, connection);
                countCmd.Parameters.AddWithValue("@maxCount", _options.MaxRetentionCount);
                await countCmd.ExecuteNonQueryAsync(CancellationToken.None);
            }

            // Passive checkpoint to keep WAL file bounded without blocking readers
            await using (var checkpointCmd = new SqliteCommand("PRAGMA wal_checkpoint(PASSIVE);", connection))
            {
                await checkpointCmd.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
        catch { }
    }

    /// <summary>
    /// Flushes all pending write queue items to disk, awaits background completion, and closes the database connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _writeChannel.Writer.Complete();

        if (_backgroundWriterTask != null)
        {
            try
            {
                await _backgroundWriterTask;
            }
            catch (OperationCanceledException) { }
            _backgroundWriterTask = null;
        }

        if (_writerConnection != null)
        {
            await _writerConnection.CloseAsync();
            await _writerConnection.DisposeAsync();
            _writerConnection = null;
        }

        await _cts.CancelAsync();
        _cts.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
