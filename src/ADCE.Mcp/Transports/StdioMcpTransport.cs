// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ADCE.Mcp.Transports;

/// <summary>
/// High-performance standard I/O (stdio) transport for MCP communication.
/// Enforces UTF-8 encoding without Byte Order Marks (BOM) and handles graceful EOF teardown.
/// </summary>
public sealed class StdioMcpTransport : IMcpTransport
{
    private static readonly UTF8Encoding s_utf8EncodingWithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly bool _ownsStreams;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of <see cref="StdioMcpTransport"/> bound to standard console streams.
    /// </summary>
    public StdioMcpTransport()
    {
        // Enforce UTF-8 without BOM across Windows console runtime
        try
        {
            Console.OutputEncoding = s_utf8EncodingWithoutBom;
            Console.InputEncoding = s_utf8EncodingWithoutBom;
        }
        catch
        {
            // Ignore if running in non-interactive environment where console properties cannot be set
        }

        var inStream = Console.OpenStandardInput();
        var outStream = Console.OpenStandardOutput();

        _reader = new StreamReader(inStream, s_utf8EncodingWithoutBom, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        _writer = new StreamWriter(outStream, s_utf8EncodingWithoutBom, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };
        _ownsStreams = false;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="StdioMcpTransport"/> with custom input and output streams.
    /// Useful for testing stream behavior without console coupling.
    /// </summary>
    public StdioMcpTransport(Stream inputStream, Stream outputStream, bool ownsStreams = false)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(outputStream);

        _reader = new StreamReader(inputStream, s_utf8EncodingWithoutBom, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: !ownsStreams);
        _writer = new StreamWriter(outputStream, s_utf8EncodingWithoutBom, bufferSize: 4096, leaveOpen: !ownsStreams) { AutoFlush = true };
        _ownsStreams = ownsStreams;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ReadIncomingMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[ADCE.Mcp.Stdio] Read error: {ex.Message}").ConfigureAwait(false);
                break;
            }

            if (line == null)
            {
                // Clean EOF received from host process
                break;
            }

            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            yield return trimmed;
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(message);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(message.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _writeLock.Dispose();
        if (_ownsStreams)
        {
            _reader.Dispose();
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
    }
}
