// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Serialization;
using ADCE.Mcp.Protocol;
using ADCE.Mcp.Transports;

namespace ADCE.Mcp.Server;

/// <summary>
/// Core Model Context Protocol (MCP) JSON-RPC 2.0 server.
/// Coordinates transport messaging, protocol lifecycle negotiation, and tool/resource execution.
/// </summary>
public sealed class McpServer
{
    private readonly IMcpTransport _transport;
    private readonly IMcpHandler _handler;
    private readonly ServerInfo _serverInfo;
    private bool _isInitialized;

    /// <summary>
    /// Gets whether the server has received and processed an initialize request.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServer"/> class.
    /// </summary>
    /// <param name="transport">Transport implementation (Stdio, SSE, or InMemory).</param>
    /// <param name="handler">Tool and resource provider.</param>
    /// <param name="serverInfo">Optional server metadata.</param>
    public McpServer(IMcpTransport transport, IMcpHandler handler, ServerInfo? serverInfo = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _serverInfo = serverInfo ?? new ServerInfo("ADCE.Mcp", "1.0.0");
    }

    /// <summary>
    /// Runs the message processing loop until the transport stream reaches EOF or cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var message in _transport.ReadIncomingMessagesAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(message)) continue;

            // Process message without blocking incoming reader loop
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"[ADCE.Mcp.Server] Unhandled message error: {ex}").ConfigureAwait(false);
                }
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Directly processes a raw JSON message string and sends the response over the transport.
    /// </summary>
    public async Task ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        JsonRpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<JsonRpcRequest>(message, AdceJsonSerializerOptions.Default);
        }
        catch (JsonException ex)
        {
            var errorResponse = JsonRpcResponse.ErrorResponse(null, JsonRpcErrorCode.ParseError, $"Invalid JSON: {ex.Message}");
            await SendResponseAsync(errorResponse, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Method))
        {
            var errorResponse = JsonRpcResponse.ErrorResponse(null, JsonRpcErrorCode.InvalidRequest, "Invalid JSON-RPC 2.0 request: 'method' is required.");
            await SendResponseAsync(errorResponse, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Handle Notifications (no response emitted per JSON-RPC 2.0 specification)
        if (request.IsNotification)
        {
            HandleNotification(request);
            return;
        }

        // Handle Standard Request (with ID)
        var response = await HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (response != null)
        {
            await SendResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private void HandleNotification(JsonRpcRequest notification)
    {
        switch (notification.Method)
        {
            case "notifications/initialized":
            case "initialized":
                _isInitialized = true;
                break;
            case "notifications/cancelled":
                // Client cancelled operation
                break;
        }
    }

    private async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            switch (request.Method)
            {
                case "initialize":
                    return HandleInitialize(request);

                case "ping":
                    return JsonRpcResponse.Success(request.Id, new { });

                case "tools/list":
                    return JsonRpcResponse.Success(request.Id, new ListToolsResult(_handler.GetTools()));

                case "tools/call":
                    return await HandleCallToolAsync(request, cancellationToken).ConfigureAwait(false);

                case "resources/list":
                    return JsonRpcResponse.Success(request.Id, new ListResourcesResult(_handler.GetResources()));

                case "resources/read":
                    return await HandleReadResourceAsync(request, cancellationToken).ConfigureAwait(false);

                case "prompts/list":
                    return JsonRpcResponse.Success(request.Id, new { prompts = Array.Empty<object>() });

                case "logging/setLevel":
                    return JsonRpcResponse.Success(request.Id, new { });

                default:
                    return JsonRpcResponse.ErrorResponse(
                        request.Id,
                        JsonRpcErrorCode.MethodNotFound,
                        $"Method '{request.Method}' not found.");
            }
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.ErrorResponse(
                request.Id,
                JsonRpcErrorCode.InternalError,
                $"Internal error handling method '{request.Method}': {ex.Message}");
        }
    }

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        _isInitialized = true;

        var negotiatedVersion = McpProtocolVersion.Latest;
        if (request.Params.HasValue && request.Params.Value.ValueKind == JsonValueKind.Object)
        {
            if (request.Params.Value.TryGetProperty("protocolVersion", out var pvElem) && pvElem.ValueKind == JsonValueKind.String)
            {
                var clientVersion = pvElem.GetString();
                if (!string.IsNullOrWhiteSpace(clientVersion))
                {
                    negotiatedVersion = clientVersion;
                }
            }
        }

        var result = new InitializeResult(
            ProtocolVersion: negotiatedVersion,
            Capabilities: new ServerCapabilities(
                Tools: new ToolsCapability(ListChanged: false),
                Resources: new ResourcesCapability(Subscribe: false, ListChanged: false),
                Prompts: new PromptsCapability(ListChanged: false),
                Logging: new LoggingCapability()),
            ServerInfo: _serverInfo,
            Instructions: "Active Desktop Context Engine (ADCE) MCP Server providing live desktop window, workspace, and focus telemetry.");

        return JsonRpcResponse.Success(request.Id, result);
    }

    private async Task<JsonRpcResponse> HandleCallToolAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (!request.Params.HasValue || request.Params.Value.ValueKind != JsonValueKind.Object)
        {
            return JsonRpcResponse.ErrorResponse(
                request.Id,
                JsonRpcErrorCode.InvalidParams,
                "Missing 'params' object in tools/call request.");
        }

        var p = request.Params.Value;
        if (!p.TryGetProperty("name", out var nameElem) || nameElem.ValueKind != JsonValueKind.String)
        {
            return JsonRpcResponse.ErrorResponse(
                request.Id,
                JsonRpcErrorCode.InvalidParams,
                "Missing required 'name' parameter in tools/call request.");
        }

        var toolName = nameElem.GetString() ?? string.Empty;
        JsonElement? arguments = null;
        if (p.TryGetProperty("arguments", out var argsElem) && argsElem.ValueKind == JsonValueKind.Object)
        {
            arguments = argsElem;
        }

        var toolResult = await _handler.CallToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);
        return JsonRpcResponse.Success(request.Id, toolResult);
    }

    private async Task<JsonRpcResponse> HandleReadResourceAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (!request.Params.HasValue || request.Params.Value.ValueKind != JsonValueKind.Object)
        {
            return JsonRpcResponse.ErrorResponse(
                request.Id,
                JsonRpcErrorCode.InvalidParams,
                "Missing 'params' object in resources/read request.");
        }

        var p = request.Params.Value;
        if (!p.TryGetProperty("uri", out var uriElem) || uriElem.ValueKind != JsonValueKind.String)
        {
            return JsonRpcResponse.ErrorResponse(
                request.Id,
                JsonRpcErrorCode.InvalidParams,
                "Missing required 'uri' parameter in resources/read request.");
        }

        var uri = uriElem.GetString() ?? string.Empty;

        try
        {
            var resourceResult = await _handler.ReadResourceAsync(uri, cancellationToken).ConfigureAwait(false);
            return JsonRpcResponse.Success(request.Id, resourceResult);
        }
        catch (KeyNotFoundException ex)
        {
            return JsonRpcResponse.ErrorResponse(
                request.Id,
                JsonRpcErrorCode.ResourceNotFound,
                ex.Message);
        }
    }

    private async Task SendResponseAsync(JsonRpcResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, AdceJsonSerializerOptions.Default);
        await _transport.SendMessageAsync(json, cancellationToken).ConfigureAwait(false);
    }
}
