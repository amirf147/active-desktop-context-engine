// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ADCE.Mcp.Protocol;

/// <summary>
/// Represents a JSON-RPC 2.0 Request message.
/// </summary>
public sealed record JsonRpcRequest(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonElement? Params = null,
    [property: JsonPropertyName("id")] JsonElement? Id = null,
    [property: JsonPropertyName("jsonrpc")] string JsonRpc = "2.0")
{
    /// <summary>
    /// Gets whether this request is a notification (no ID present).
    /// </summary>
    [JsonIgnore]
    public bool IsNotification => !Id.HasValue || Id.Value.ValueKind == JsonValueKind.Null || Id.Value.ValueKind == JsonValueKind.Undefined;
}

/// <summary>
/// Represents a JSON-RPC 2.0 Response message.
/// </summary>
public sealed record JsonRpcResponse(
    [property: JsonPropertyName("id")] JsonElement? Id,
    [property: JsonPropertyName("result")] object? Result = null,
    [property: JsonPropertyName("error")] JsonRpcError? Error = null,
    [property: JsonPropertyName("jsonrpc")] string JsonRpc = "2.0")
{
    /// <summary>
    /// Creates a successful JSON-RPC response.
    /// </summary>
    public static JsonRpcResponse Success(JsonElement? id, object result) =>
        new(id, Result: result);

    /// <summary>
    /// Creates a JSON-RPC error response.
    /// </summary>
    public static JsonRpcResponse ErrorResponse(JsonElement? id, int code, string message, object? data = null) =>
        new(id, Error: new JsonRpcError(code, message, data));
}

/// <summary>
/// Represents a JSON-RPC 2.0 Error object.
/// </summary>
public sealed record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] object? Data = null);

/// <summary>
/// Represents a JSON-RPC 2.0 Notification message.
/// </summary>
public sealed record JsonRpcNotification(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonElement? Params = null,
    [property: JsonPropertyName("jsonrpc")] string JsonRpc = "2.0");
