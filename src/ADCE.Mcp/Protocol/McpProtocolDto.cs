// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ADCE.Mcp.Protocol;

/// <summary>
/// MCP Protocol Versions supported by ADCE.
/// </summary>
public static class McpProtocolVersion
{
    /// <summary>MCP specification version 2024-11-05.</summary>
    public const string V2024_11_05 = "2024-11-05";

    /// <summary>Legacy version identifier.</summary>
    public const string V1_0_0 = "1.0.0";

    /// <summary>Default protocol version offered by ADCE.</summary>
    public const string Latest = V2024_11_05;
}

#region Initialization DTOs

/// <summary>
/// Parameters for the MCP initialize request.
/// </summary>
public sealed record InitializeRequestParams(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] ClientCapabilities? Capabilities = null,
    [property: JsonPropertyName("clientInfo")] ClientInfo? ClientInfo = null);

/// <summary>
/// Client identity information.
/// </summary>
public sealed record ClientInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string? Version = null);

/// <summary>
/// Client capabilities advertised during initialization.
/// </summary>
public sealed record ClientCapabilities(
    [property: JsonPropertyName("experimental")] Dictionary<string, object>? Experimental = null,
    [property: JsonPropertyName("roots")] Dictionary<string, object>? Roots = null,
    [property: JsonPropertyName("sampling")] Dictionary<string, object>? Sampling = null);

/// <summary>
/// Result returned from the MCP initialize request.
/// </summary>
public sealed record InitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] ServerCapabilities Capabilities,
    [property: JsonPropertyName("serverInfo")] ServerInfo ServerInfo,
    [property: JsonPropertyName("instructions")] string? Instructions = null);

/// <summary>
/// Server identity information.
/// </summary>
public sealed record ServerInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);

/// <summary>
/// Server capabilities advertised by ADCE.
/// </summary>
public sealed record ServerCapabilities(
    [property: JsonPropertyName("tools")] ToolsCapability? Tools = null,
    [property: JsonPropertyName("resources")] ResourcesCapability? Resources = null,
    [property: JsonPropertyName("prompts")] PromptsCapability? Prompts = null,
    [property: JsonPropertyName("logging")] LoggingCapability? Logging = null);

/// <summary>Tools capability declaration.</summary>
public sealed record ToolsCapability(
    [property: JsonPropertyName("listChanged")] bool? ListChanged = false);

/// <summary>Resources capability declaration.</summary>
public sealed record ResourcesCapability(
    [property: JsonPropertyName("subscribe")] bool? Subscribe = false,
    [property: JsonPropertyName("listChanged")] bool? ListChanged = false);

/// <summary>Prompts capability declaration.</summary>
public sealed record PromptsCapability(
    [property: JsonPropertyName("listChanged")] bool? ListChanged = false);

/// <summary>Logging capability declaration.</summary>
public sealed record LoggingCapability();

#endregion

#region Tools DTOs

/// <summary>
/// Definition of an MCP Tool.
/// </summary>
public sealed record McpTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] object InputSchema);

/// <summary>
/// Result of tools/list request.
/// </summary>
public sealed record ListToolsResult(
    [property: JsonPropertyName("tools")] IReadOnlyList<McpTool> Tools);

/// <summary>
/// Parameters for tools/call request.
/// </summary>
public sealed record CallToolRequestParams(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] JsonElement? Arguments = null);

/// <summary>
/// Content block returned inside a tool execution result.
/// </summary>
public sealed record McpContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("data")] string? Data = null,
    [property: JsonPropertyName("mimeType")] string? MimeType = null)
{
    /// <summary>Creates a text content item.</summary>
    public static McpContent TextContent(string text) => new("text", Text: text);
}

/// <summary>
/// Result of tools/call request.
/// Conforms to MCP spec: returns 'content' array.
/// </summary>
public sealed record CallToolResult(
    [property: JsonPropertyName("content")] IReadOnlyList<McpContent> Content,
    [property: JsonPropertyName("isError")] bool IsError = false)
{
    /// <summary>Creates a successful text-based tool result.</summary>
    public static CallToolResult SuccessText(string text) =>
        new([McpContent.TextContent(text)], IsError: false);

    /// <summary>Creates an error tool result with a message.</summary>
    public static CallToolResult ErrorText(string message) =>
        new([McpContent.TextContent(message)], IsError: true);
}

#endregion

#region Resources DTOs

/// <summary>
/// Definition of an MCP Resource.
/// </summary>
public sealed record McpResource(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("mimeType")] string? MimeType = "application/json");

/// <summary>
/// Result of resources/list request.
/// </summary>
public sealed record ListResourcesResult(
    [property: JsonPropertyName("resources")] IReadOnlyList<McpResource> Resources);

/// <summary>
/// Parameters for resources/read request.
/// </summary>
public sealed record ReadResourceRequestParams(
    [property: JsonPropertyName("uri")] string Uri);

/// <summary>
/// Resource content block returned inside a resource read result.
/// </summary>
public sealed record McpResourceContent(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("mimeType")] string? MimeType = "application/json",
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("blob")] string? Blob = null)
{
    /// <summary>Creates a JSON text resource content item.</summary>
    public static McpResourceContent Json(string uri, string text) =>
        new(uri, MimeType: "application/json", Text: text);
}

/// <summary>
/// Result of resources/read request.
/// Conforms to MCP spec: returns 'contents' array.
/// </summary>
public sealed record ReadResourceResult(
    [property: JsonPropertyName("contents")] IReadOnlyList<McpResourceContent> Contents)
{
    /// <summary>Creates a single-content resource result.</summary>
    public static ReadResourceResult SingleJson(string uri, string text) =>
        new([McpResourceContent.Json(uri, text)]);
}

#endregion
