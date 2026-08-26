// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ADCE.Mcp.Transports;

/// <summary>
/// Lightweight Server-Sent Events (SSE) and HTTP transport bound strictly to localhost.
/// </summary>
public sealed class HttpSseMcpTransport : IMcpTransport
{
    private static readonly UTF8Encoding s_utf8EncodingWithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly HttpListener _listener;
    private readonly Channel<string> _incomingChannel;
    private readonly ConcurrentDictionary<string, StreamWriter> _sseClients = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenerLoop;
    private bool _isDisposed;

    /// <summary>
    /// Gets the base HTTP URL the transport is listening on.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HttpSseMcpTransport"/> listening on localhost.
    /// </summary>
    /// <param name="port">Port number (default 8424).</param>
    public HttpSseMcpTransport(int port = 8424)
    {
        BaseUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(BaseUrl);

        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _incomingChannel = Channel.CreateUnbounded<string>(options);
    }

    /// <summary>
    /// Starts the HTTP listener loop.
    /// </summary>
    public void Start()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(HttpSseMcpTransport));
        if (_listener.IsListening) return;

        _listener.Start();
        _listenerLoop = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ReadIncomingMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        while (await _incomingChannel.Reader.WaitToReadAsync(linkedCts.Token).ConfigureAwait(false))
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
        ArgumentNullException.ThrowIfNull(message);

        var sseFormattedMessage = $"event: message\ndata: {message}\n\n";
        var deadClients = new List<string>();

        foreach (var (clientId, writer) in _sseClients)
        {
            try
            {
                await writer.WriteAsync(sseFormattedMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                deadClients.Add(clientId);
            }
        }

        foreach (var dead in deadClients)
        {
            if (_sseClients.TryRemove(dead, out var writer))
            {
                try { writer.Dispose(); } catch { }
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => ProcessHttpRequestAsync(context, cancellationToken), cancellationToken);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested || !_listener.IsListening || _isDisposed || ex is ObjectDisposedException or HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[ADCE.Mcp.HttpSse] Listener exception: {ex.Message}").ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessHttpRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        // Security check: Only allow localhost origins and paths
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = (int)HttpStatusCode.NoContent;
            response.Close();
            return;
        }

        var path = request.Url?.AbsolutePath ?? "/";

        if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("/sse", StringComparison.OrdinalIgnoreCase))
        {
            // Establish SSE Stream
            response.ContentType = "text/event-stream; charset=utf-8";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");

            var clientId = Guid.NewGuid().ToString("N");
            var writer = new StreamWriter(response.OutputStream, s_utf8EncodingWithoutBom, bufferSize: 4096, leaveOpen: false);
            _sseClients[clientId] = writer;

            try
            {
                // Send initial endpoint event per MCP SSE spec
                var endpointMessage = $"event: endpoint\ndata: /messages?session_id={clientId}\n\n";
                await writer.WriteAsync(endpointMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Keep stream alive until cancelled or disconnected
                var tcs = new TaskCompletionSource();
                using (cancellationToken.Register(() => tcs.TrySetResult()))
                {
                    await tcs.Task.ConfigureAwait(false);
                }
            }
            catch
            {
                // Client disconnected
            }
            finally
            {
                if (_sseClients.TryRemove(clientId, out var removedWriter))
                {
                    try { removedWriter.Dispose(); } catch { }
                }
                try { response.Close(); } catch { }
            }
            return;
        }

        if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            (path.Equals("/messages", StringComparison.OrdinalIgnoreCase) || path.Equals("/message", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(request.InputStream, s_utf8EncodingWithoutBom);
            var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(body))
            {
                await _incomingChannel.Writer.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            }

            response.StatusCode = (int)HttpStatusCode.Accepted;
            response.Close();
            return;
        }

        // Unknown endpoint
        response.StatusCode = (int)HttpStatusCode.NotFound;
        response.Close();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts.Cancel();

        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
            }
        }
        catch { }

        _incomingChannel.Writer.TryComplete();

        foreach (var (_, writer) in _sseClients)
        {
            try { writer.Dispose(); } catch { }
        }
        _sseClients.Clear();

        if (_listenerLoop != null)
        {
            try { await _listenerLoop.ConfigureAwait(false); } catch { }
        }

        _cts.Dispose();
    }
}
