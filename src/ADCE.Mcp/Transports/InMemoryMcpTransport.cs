// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ADCE.Mcp.Transports;

/// <summary>
/// In-memory channel-based transport for unit testing and deterministic simulation.
/// </summary>
public sealed class InMemoryMcpTransport : IMcpTransport
{
    private readonly Channel<string> _incomingChannel;
    private readonly Channel<string> _outgoingChannel;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryMcpTransport"/> class.
    /// </summary>
    public InMemoryMcpTransport()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _incomingChannel = Channel.CreateUnbounded<string>(options);
        _outgoingChannel = Channel.CreateUnbounded<string>(options);
    }

    /// <summary>
    /// Simulates client pushing a message into the server's incoming queue.
    /// </summary>
    public async ValueTask PushClientMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _incomingChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the next response emitted by the server to the client.
    /// </summary>
    public async ValueTask<string> ReadServerResponseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return await _outgoingChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the incoming client stream (simulating EOF).
    /// </summary>
    public void CompleteClientInput()
    {
        _incomingChannel.Writer.TryComplete();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ReadIncomingMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _incomingChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_incomingChannel.Reader.TryRead(out var message))
            {
                yield return message;
            }
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _outgoingChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_isDisposed) return ValueTask.CompletedTask;
        _isDisposed = true;
        _incomingChannel.Writer.TryComplete();
        _outgoingChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
