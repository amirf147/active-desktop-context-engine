// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Mcp.Protocol;

namespace ADCE.Mcp.Server;

/// <summary>
/// Defines the contract for an MCP tool and resource provider.
/// </summary>
public interface IMcpHandler
{
    /// <summary>
    /// Gets the collection of tools provided by this handler.
    /// </summary>
    IReadOnlyList<McpTool> GetTools();

    /// <summary>
    /// Gets the collection of resources provided by this handler.
    /// </summary>
    IReadOnlyList<McpResource> GetResources();

    /// <summary>
    /// Executes a tool call by name.
    /// </summary>
    /// <param name="toolName">Name of the tool.</param>
    /// <param name="arguments">Tool arguments as JSON element.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result.</returns>
    Task<CallToolResult> CallToolAsync(string toolName, JsonElement? arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a resource by URI.
    /// </summary>
    /// <param name="uri">Resource URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resource read result.</returns>
    Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default);
}
