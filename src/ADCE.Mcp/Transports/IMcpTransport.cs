// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ADCE.Mcp.Transports;

/// <summary>
/// Defines the transport abstraction for bi-directional MCP protocol communication.
/// </summary>
public interface IMcpTransport : IAsyncDisposable
{
    /// <summary>
    /// Reads incoming raw JSON-RPC messages from the transport stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream of raw message strings.</returns>
    IAsyncEnumerable<string> ReadIncomingMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a raw JSON-RPC message string over the transport.
    /// </summary>
    /// <param name="message">JSON message to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
}
