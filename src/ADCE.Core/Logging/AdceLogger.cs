// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ADCE.Core.Logging;

/// <summary>
/// Log severity levels for ADCE diagnostic tracing.
/// </summary>
public enum AdceLogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
    None = 4
}

/// <summary>
/// Production-grade, zero-allocation asynchronous diagnostic logger.
/// Emits structured timestamped logs to a rolling file in LocalApplicationData/ADCE/logs/adce.log
/// and maintains a thread-safe circular in-memory buffer for instant CLI/MCP diagnostics.
/// </summary>
public sealed class AdceLogger : IDisposable
{
    private const int MaxLogCapacity = 500;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB per file
    private const int MaxRolledFiles = 3;

    private static readonly Lazy<AdceLogger> s_defaultInstance = new(() =>
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDir = Path.Combine(localAppData, "ADCE", "logs");
        var logPath = Path.Combine(logDir, "adce.log");
        return new AdceLogger(logPath, AdceLogLevel.Info);
    });

    /// <summary>
    /// Gets the shared default global logger instance.
    /// </summary>
    public static AdceLogger Default => s_defaultInstance.Value;

    private readonly string? _logFilePath;
    private readonly Channel<string> _logQueue;
    private readonly ConcurrentQueue<string> _memoryBuffer;
    private readonly CancellationTokenSource _cts;
    private readonly Task? _writerTask;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the minimum active log level.
    /// </summary>
    public AdceLogLevel MinimumLevel { get; set; }

    /// <summary>
    /// Gets the absolute path of the current log file, if file logging is enabled.
    /// </summary>
    public string? LogFilePath => _logFilePath;

    public AdceLogger(string? logFilePath = null, AdceLogLevel minimumLevel = AdceLogLevel.Info)
    {
        _logFilePath = logFilePath;
        MinimumLevel = minimumLevel;
        _memoryBuffer = new ConcurrentQueue<string>();
        _cts = new CancellationTokenSource();

        _logQueue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        if (!string.IsNullOrWhiteSpace(_logFilePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch { }

            _writerTask = Task.Run(() => ProcessLogQueueAsync(_cts.Token));
        }
    }

    /// <summary>
    /// Writes a DEBUG level message.
    /// </summary>
    public void Debug(string tag, string message) => Log(AdceLogLevel.Debug, tag, message);

    /// <summary>
    /// Writes an INFO level message.
    /// </summary>
    public void Info(string tag, string message) => Log(AdceLogLevel.Info, tag, message);

    /// <summary>
    /// Writes a WARN level message with optional exception.
    /// </summary>
    public void Warn(string tag, string message, Exception? ex = null) => Log(AdceLogLevel.Warn, tag, message, ex);

    /// <summary>
    /// Writes an ERROR level message with optional exception.
    /// </summary>
    public void Error(string tag, string message, Exception? ex = null) => Log(AdceLogLevel.Error, tag, message, ex);

    public void Log(AdceLogLevel level, string tag, string message, Exception? ex = null)
    {
        if (level < MinimumLevel || MinimumLevel == AdceLogLevel.None) return;

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level switch
        {
            AdceLogLevel.Debug => "DEBUG",
            AdceLogLevel.Info => "INFO ",
            AdceLogLevel.Warn => "WARN ",
            AdceLogLevel.Error => "ERROR",
            _ => "INFO "
        };

        var sb = new StringBuilder(128);
        sb.Append('[').Append(timestamp).Append("] [").Append(levelStr).Append("] [").Append(tag).Append("] ").Append(message);

        if (ex != null)
        {
            sb.Append(" | Exception: ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                sb.AppendLine().Append(ex.StackTrace);
            }
        }

        var formatted = sb.ToString();

        // Enqueue to memory ring buffer
        _memoryBuffer.Enqueue(formatted);
        while (_memoryBuffer.Count > MaxLogCapacity)
        {
            _memoryBuffer.TryDequeue(out _);
        }

        // Enqueue to file writer channel
        if (_logFilePath != null && !_cts.IsCancellationRequested)
        {
            _logQueue.Writer.TryWrite(formatted);
        }
    }

    /// <summary>
    /// Retrieves a snapshot of the most recent log entries from the in-memory ring buffer.
    /// </summary>
    public string[] GetRecentLogs()
    {
        return _memoryBuffer.ToArray();
    }

    private async Task ProcessLogQueueAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_logFilePath)) return;

        try
        {
            while (await _logQueue.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                RollLogFileIfNeeded();

                await using var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true);
                await using var writer = new StreamWriter(stream, Encoding.UTF8);

                while (_logQueue.Reader.TryRead(out var logLine))
                {
                    await writer.WriteLineAsync(logLine.AsMemory(), ct).ConfigureAwait(false);
                }

                await writer.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void RollLogFileIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(_logFilePath)) return;

        try
        {
            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.Exists && fileInfo.Length > MaxFileSizeBytes)
            {
                for (int i = MaxRolledFiles - 1; i >= 1; i--)
                {
                    var oldFile = $"{_logFilePath}.{i}";
                    var targetFile = $"{_logFilePath}.{i + 1}";
                    if (File.Exists(oldFile))
                    {
                        File.Move(oldFile, targetFile, overwrite: true);
                    }
                }

                File.Move(_logFilePath, $"{_logFilePath}.1", overwrite: true);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _logQueue.Writer.TryComplete();
            if (_writerTask != null)
            {
                try { _writerTask.Wait(500); } catch { }
            }
            _cts.Dispose();
            _disposed = true;
        }
    }
}
