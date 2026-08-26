// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Mcp.Protocol;

namespace ADCE.Mcp.Server;

/// <summary>
/// Implements the default MCP tool and resource provider for ADCE desktop state and history.
/// </summary>
public sealed class DesktopContextMcpHandler : IMcpHandler
{
    private readonly IDesktopStateStore _stateStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopContextMcpHandler"/> class.
    /// </summary>
    /// <param name="stateStore">Underlying desktop state store.</param>
    public DesktopContextMcpHandler(IDesktopStateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    /// <inheritdoc />
    public IReadOnlyList<McpTool> GetTools()
    {
        return
        [
            new McpTool(
                Name: "get_desktop_context",
                Description: "Returns the current live active desktop context snapshot, optionally filtering by process name.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        process_filter = new
                        {
                            type = "string",
                            description = "Optional process name filter (e.g. 'code', 'waterfox', 'Antigravity')"
                        }
                    }
                }),
            new McpTool(
                Name: "search_desktop_history",
                Description: "Searches past desktop history for matching keywords in window titles, tabs, or file paths.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "Keyword search term (e.g. file name, window title, tab title)"
                        },
                        limit = new
                        {
                            type = "integer",
                            description = "Maximum results to return (default 20, max 100)"
                        }
                    },
                    required = new[] { "query" }
                })
        ];
    }

    /// <inheritdoc />
    public IReadOnlyList<McpResource> GetResources()
    {
        return
        [
            new McpResource(
                Uri: "desktop://current",
                Name: "Current Desktop Context",
                Description: "Live snapshot of the current foreground desktop state",
                MimeType: "application/json"),
            new McpResource(
                Uri: "desktop://history",
                Name: "Desktop State History",
                Description: "Time-series window transitions and focus history (supports ?minutes=15)",
                MimeType: "application/json")
        ];
    }

    /// <inheritdoc />
    public async Task<CallToolResult> CallToolAsync(string toolName, JsonElement? arguments, CancellationToken cancellationToken = default)
    {
        switch (toolName)
        {
            case "get_desktop_context":
                return ExecuteGetDesktopContext(arguments);

            case "search_desktop_history":
                return await ExecuteSearchDesktopHistoryAsync(arguments, cancellationToken).ConfigureAwait(false);

            default:
                return CallToolResult.ErrorText($"Unknown tool: '{toolName}'");
        }
    }

    /// <inheritdoc />
    public async Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var uriLower = uri.Trim().ToLowerInvariant();

        if (uriLower.StartsWith("desktop://current", StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = _stateStore.GetCurrentSnapshot();
            var json = snapshot != null
                ? JsonSerializer.Serialize(snapshot, AdceJsonSerializerOptions.Default)
                : "{}";

            return ReadResourceResult.SingleJson(uri, json);
        }

        if (uriLower.StartsWith("desktop://history", StringComparison.OrdinalIgnoreCase))
        {
            int minutes = 15;
            int limit = 50;

            var qIdx = uri.IndexOf('?');
            if (qIdx >= 0 && qIdx < uri.Length - 1)
            {
                var queryStr = uri[(qIdx + 1)..];
                var pairs = queryStr.Split('&', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length == 2)
                    {
                        var key = kv[0].Trim().ToLowerInvariant();
                        var val = kv[1].Trim();
                        if (key == "minutes" && int.TryParse(val, out var m))
                        {
                            minutes = Math.Clamp(m, 1, 1440);
                        }
                        else if (key == "limit" && int.TryParse(val, out var l))
                        {
                            limit = Math.Clamp(l, 1, 200);
                        }
                    }
                }
            }

            var since = DateTimeOffset.UtcNow.AddMinutes(-minutes);
            var results = new List<DesktopContextSnapshot>();
            await foreach (var item in _stateStore.GetHistoryAsync(since, limit, cancellationToken).ConfigureAwait(false))
            {
                results.Add(item);
            }

            var json = JsonSerializer.Serialize(results, AdceJsonSerializerOptions.Default);
            return ReadResourceResult.SingleJson(uri, json);
        }

        throw new KeyNotFoundException($"Resource URI '{uri}' not found.");
    }

    private CallToolResult ExecuteGetDesktopContext(JsonElement? arguments)
    {
        string? processFilter = null;
        if (arguments.HasValue && arguments.Value.ValueKind == JsonValueKind.Object)
        {
            if (arguments.Value.TryGetProperty("process_filter", out var filterElem) && filterElem.ValueKind == JsonValueKind.String)
            {
                processFilter = filterElem.GetString()?.Trim();
            }
        }

        var snapshot = _stateStore.GetCurrentSnapshot();
        if (snapshot == null)
        {
            return CallToolResult.SuccessText("{\"status\": \"no_active_context\"}");
        }

        if (!string.IsNullOrWhiteSpace(processFilter))
        {
            var procName = snapshot.Window.ProcessName;
            if (!procName.Contains(processFilter, StringComparison.OrdinalIgnoreCase))
            {
                return CallToolResult.SuccessText($"{{\"status\": \"filter_mismatch\", \"active_process\": \"{procName}\"}}");
            }
        }

        var json = JsonSerializer.Serialize(snapshot, AdceJsonSerializerOptions.Default);
        return CallToolResult.SuccessText(json);
    }

    private async Task<CallToolResult> ExecuteSearchDesktopHistoryAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        string query = string.Empty;
        int limit = 20;

        if (arguments.HasValue && arguments.Value.ValueKind == JsonValueKind.Object)
        {
            if (arguments.Value.TryGetProperty("query", out var qElem) && qElem.ValueKind == JsonValueKind.String)
            {
                query = qElem.GetString()?.Trim() ?? string.Empty;
            }

            if (arguments.Value.TryGetProperty("limit", out var lElem) && lElem.TryGetInt32(out var l))
            {
                limit = Math.Clamp(l, 1, 100);
            }
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return CallToolResult.ErrorText("Missing required parameter 'query'");
        }

        // Limit query length to 200 characters for safety
        if (query.Length > 200) query = query[..200];

        var results = new List<DesktopContextSnapshot>();
        await foreach (var item in _stateStore.SearchHistoryAsync(query, limit, cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }

        var json = JsonSerializer.Serialize(results, AdceJsonSerializerOptions.Default);
        return CallToolResult.SuccessText(json);
    }
}
