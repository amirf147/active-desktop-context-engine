// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _requestIdToSessions = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<string?>? _initialPayloadProvider;
    private Task? _listenerLoop;
    private Task? _heartbeatTask;
    private bool _isDisposed;

    /// <summary>
    /// Gets the base HTTP URL the transport is listening on.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HttpSseMcpTransport"/> listening on localhost.
    /// </summary>
    /// <param name="port">Port number (default 8424).</param>
    public HttpSseMcpTransport(int port = 8424) : this(port, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="HttpSseMcpTransport"/> listening on localhost with an initial payload provider.
    /// </summary>
    /// <param name="port">Port number (default 8424).</param>
    /// <param name="initialPayloadProvider">Optional provider called on new SSE connection to supply current context snapshot.</param>
    public HttpSseMcpTransport(int port, Func<string?>? initialPayloadProvider)
    {
        BaseUrl = $"http://127.0.0.1:{port}/";
        _initialPayloadProvider = initialPayloadProvider;
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
        _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_cts.Token));
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

        // Try routing to specific client session by request ID
        string? targetSessionId = null;
        try
        {
            using var doc = JsonDocument.Parse(message);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
            {
                string idKey = idProp.GetRawText();
                if (_requestIdToSessions.TryGetValue(idKey, out var queue) && queue.TryDequeue(out var sid))
                {
                    targetSessionId = sid;
                    if (queue.IsEmpty)
                    {
                        _requestIdToSessions.TryRemove(idKey, out _);
                    }
                }
            }
        }
        catch
        {
            // Fall back to broadcast
        }

        if (targetSessionId != null)
        {
            if (_sseClients.TryGetValue(targetSessionId, out var writer))
            {
                try
                {
                    await writer.WriteAsync(sseFormattedMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    if (_sseClients.TryRemove(targetSessionId, out var deadWriter))
                    {
                        try { deadWriter.Dispose(); } catch { }
                    }
                }
            }
            return;
        }

        // Broadcast to all connected clients when no session target is identified
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

        // Security check: Only allow localhost origins and paths. Never wildcard '*' to prevent web browser tabs from bypassing SOP.
        var origin = request.Headers["Origin"];
        if (!string.IsNullOrEmpty(origin))
        {
            if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri) &&
                (originUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                 originUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
            {
                response.Headers.Add("Access-Control-Allow-Origin", origin);
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                response.Close();
                return;
            }
        }

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

                // Send immediate snapshot state if provider is configured
                var initialPayload = _initialPayloadProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(initialPayload))
                {
                    var initialEvent = $"event: message\ndata: {initialPayload}\n\n";
                    await writer.WriteAsync(initialEvent.AsMemory(), cancellationToken).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

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
            var sessionId = request.QueryString["session_id"] ?? request.QueryString["sessionId"];
            if (!string.IsNullOrWhiteSpace(sessionId) && !_sseClients.ContainsKey(sessionId))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
                return;
            }

            using var reader = new StreamReader(request.InputStream, s_utf8EncodingWithoutBom);
            var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(body))
            {
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("id", out var idProp))
                        {
                            string idKey = idProp.GetRawText();
                            _requestIdToSessions.GetOrAdd(idKey, _ => new ConcurrentQueue<string>()).Enqueue(sessionId);
                        }
                    }
                    catch
                    {
                        // Ignore parse errors here; McpServer will emit standard JSON-RPC parse error
                    }
                }

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
        _requestIdToSessions.Clear();

        if (_listenerLoop != null)
        {
            try { await _listenerLoop.ConfigureAwait(false); } catch { }
        }

        if (_heartbeatTask != null)
        {
            try { await _heartbeatTask.ConfigureAwait(false); } catch { }
        }

        _cts.Dispose();
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_sseClients.IsEmpty) continue;

            var heartbeat = ": keepalive\n\n";
            var deadClients = new List<string>();

            foreach (var (clientId, writer) in _sseClients)
            {
                try
                {
                    await writer.WriteAsync(heartbeat.AsMemory(), cancellationToken).ConfigureAwait(false);
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
    }
}
