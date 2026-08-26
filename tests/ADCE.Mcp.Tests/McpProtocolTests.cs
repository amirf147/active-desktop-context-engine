// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ADCE.Core.Serialization;
using ADCE.Mcp.Protocol;
using ADCE.Mcp.Server;
using ADCE.Mcp.Transports;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Xunit;

namespace ADCE.Mcp.Tests;

public class McpProtocolTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDesktopStateStore _store;
    private readonly DesktopContextMcpHandler _handler;

    public McpProtocolTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"adce_proto_test_{Guid.NewGuid():N}.db");
        var options = new StorageOptions
        {
            DatabasePath = _testDbPath
        };
        _store = new SqliteDesktopStateStore(options);
        _store.Initialize();
        _handler = new DesktopContextMcpHandler(_store);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
    }

    [Fact]
    public async Task Initialize_ReturnsCapabilitiesAndServerInfo()
    {
        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var requestJson = """
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "clientInfo": {
                    "name": "AntigravityTestClient",
                    "version": "2.0.0"
                }
            }
        }
        """;

        await transport.PushClientMessageAsync(requestJson);
        var runTask = Task.Run(() => server.RunAsync());

        var responseJson = await transport.ReadServerResponseAsync();
        transport.CompleteClientInput();
        await runTask;

        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson, AdceJsonSerializerOptions.Default);
        Assert.NotNull(response);
        Assert.Equal(1, response.Id?.GetInt32());
        Assert.Null(response.Error);
        Assert.NotNull(response.Result);

        var resultJson = JsonSerializer.Serialize(response.Result, AdceJsonSerializerOptions.Default);
        var initResult = JsonSerializer.Deserialize<InitializeResult>(resultJson, AdceJsonSerializerOptions.Default);
        Assert.NotNull(initResult);
        Assert.Equal("2024-11-05", initResult.ProtocolVersion);
        Assert.Equal("ADCE.Mcp", initResult.ServerInfo.Name);
        Assert.NotNull(initResult.Capabilities.Tools);
        Assert.NotNull(initResult.Capabilities.Resources);
    }

    [Fact]
    public async Task PolymorphicId_SupportsBothIntegerAndStringIds()
    {
        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        // 1. Integer ID
        await transport.PushClientMessageAsync("""{"jsonrpc": "2.0", "id": 42, "method": "ping"}""");
        var runTask = Task.Run(() => server.RunAsync());
        var response1 = await transport.ReadServerResponseAsync();

        var res1 = JsonSerializer.Deserialize<JsonRpcResponse>(response1, AdceJsonSerializerOptions.Default);
        Assert.NotNull(res1);
        Assert.Equal(42, res1.Id?.GetInt32());

        // 2. String ID
        await transport.PushClientMessageAsync("""{"jsonrpc": "2.0", "id": "req-xyz-99", "method": "ping"}""");
        var response2 = await transport.ReadServerResponseAsync();

        var res2 = JsonSerializer.Deserialize<JsonRpcResponse>(response2, AdceJsonSerializerOptions.Default);
        Assert.NotNull(res2);
        Assert.Equal("req-xyz-99", res2.Id?.GetString());

        transport.CompleteClientInput();
        await runTask;
    }

    [Fact]
    public async Task Notification_DoesNotEmitResponse()
    {
        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());

        // Push notification without ID
        await transport.PushClientMessageAsync("""{"jsonrpc": "2.0", "method": "notifications/initialized"}""");

        // Then push a request with ID to verify server processed the notification and continues
        await transport.PushClientMessageAsync("""{"jsonrpc": "2.0", "id": 1, "method": "ping"}""");

        var response = await transport.ReadServerResponseAsync();
        var res = JsonSerializer.Deserialize<JsonRpcResponse>(response, AdceJsonSerializerOptions.Default);
        Assert.NotNull(res);
        Assert.Equal(1, res.Id?.GetInt32());
        Assert.True(server.IsInitialized);

        transport.CompleteClientInput();
        await runTask;
    }

    [Fact]
    public async Task MethodNotFound_ReturnsCorrectJsonRpcErrorCode()
    {
        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("""{"jsonrpc": "2.0", "id": 99, "method": "non_existent_method"}""");

        var response = await transport.ReadServerResponseAsync();
        var res = JsonSerializer.Deserialize<JsonRpcResponse>(response, AdceJsonSerializerOptions.Default);

        Assert.NotNull(res);
        Assert.Equal(99, res.Id?.GetInt32());
        Assert.NotNull(res.Error);
        Assert.Equal(JsonRpcErrorCode.MethodNotFound, res.Error.Code);

        transport.CompleteClientInput();
        await runTask;
    }

    [Fact]
    public async Task ParseError_ReturnsParseErrorCodeOnInvalidJson()
    {
        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("INVALID_NOT_JSON {{{");

        var response = await transport.ReadServerResponseAsync();
        var res = JsonSerializer.Deserialize<JsonRpcResponse>(response, AdceJsonSerializerOptions.Default);

        Assert.NotNull(res);
        Assert.Null(res.Id);
        Assert.NotNull(res.Error);
        Assert.Equal(JsonRpcErrorCode.ParseError, res.Error.Code);

        transport.CompleteClientInput();
        await runTask;
    }
}
