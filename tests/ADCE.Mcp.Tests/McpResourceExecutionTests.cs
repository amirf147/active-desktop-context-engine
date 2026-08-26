// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Mcp.Protocol;
using ADCE.Mcp.Server;
using ADCE.Mcp.Transports;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Xunit;

namespace ADCE.Mcp.Tests;

public class McpResourceExecutionTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDesktopStateStore _store;
    private readonly DesktopContextMcpHandler _handler;

    public McpResourceExecutionTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"adce_mcp_res_test_{Guid.NewGuid():N}.db");
        var options = new StorageOptions
        {
            DatabasePath = _testDbPath,
            RetentionWindow = TimeSpan.FromHours(1)
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

    private DesktopContextSnapshot CreateSampleSnapshot()
    {
        return new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Primary",
                MonitorIndex = 0,
                MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x5678,
                Title = "Resource Test Window",
                ProcessName = "test.exe",
                Pid = 9999,
                ClassName = "TestClass",
                Archetype = DesktopAppArchetype.ClassicWin32,
                Bounds = new BoundingRectangle(0, 0, 800, 600),
                IsMinimized = false,
                IsMaximized = false
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "Test Element",
                BoundingBox = new BoundingRectangle(0, 0, 100, 30),
                AutomationId = "test-id",
                ClassName = "Edit",
                SemanticZone = DesktopSemanticZone.Unknown
            },
            ExtractionDurationMs = 0.88
        };
    }

    [Fact]
    public async Task ResourcesList_ReturnsRegisteredResources()
    {
        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("""{"jsonrpc": "2.0", "id": 1, "method": "resources/list"}""");

        var responseJson = await transport.ReadServerResponseAsync();
        transport.CompleteClientInput();
        await runTask;

        var doc = JsonDocument.Parse(responseJson);
        var resources = doc.RootElement.GetProperty("result").GetProperty("resources");
        Assert.True(resources.GetArrayLength() >= 2);

        var uris = new List<string>();
        foreach (var r in resources.EnumerateArray())
        {
            uris.Add(r.GetProperty("uri").GetString()!);
        }

        Assert.Contains("desktop://current", uris);
        Assert.Contains("desktop://history", uris);
    }

    [Fact]
    public async Task ReadResource_DesktopCurrent_ConformsToMcpContentsSchema()
    {
        var snapshot = CreateSampleSnapshot();
        _store.UpdateCurrentSnapshot(snapshot);

        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 20,
            "method": "resources/read",
            "params": {
                "uri": "desktop://current"
            }
        }
        """);

        var responseJson = await transport.ReadServerResponseAsync();
        transport.CompleteClientInput();
        await runTask;

        var doc = JsonDocument.Parse(responseJson);
        var result = doc.RootElement.GetProperty("result");

        // Verification of MCP Spec Trap #1: resource reads MUST use plural property name 'contents'
        Assert.True(result.TryGetProperty("contents", out var contentsElem), "resources/read result must have 'contents' property");
        Assert.False(result.TryGetProperty("content", out _), "resources/read result must NOT have singular 'content' property");
        Assert.Equal(JsonValueKind.Array, contentsElem.ValueKind);
        Assert.True(contentsElem.GetArrayLength() > 0);

        var firstContent = contentsElem[0];
        Assert.Equal("desktop://current", firstContent.GetProperty("uri").GetString());
        Assert.Equal("application/json", firstContent.GetProperty("mimeType").GetString());

        var textContent = firstContent.GetProperty("text").GetString();
        Assert.NotNull(textContent);
        Assert.Contains("Resource Test Window", textContent);
    }

    [Fact]
    public async Task ReadResource_DesktopHistory_ReturnsSnapshotList()
    {
        var snapshot = CreateSampleSnapshot();
        _store.UpdateCurrentSnapshot(snapshot);

        // Wait for background SQLite channel persistence
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000)
        {
            int count = 0;
            await foreach (var _ in _store.GetHistoryAsync(DateTimeOffset.UtcNow.AddMinutes(-5), 10))
            {
                count++;
            }
            if (count >= 1) break;
            await Task.Delay(10);
        }

        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 21,
            "method": "resources/read",
            "params": {
                "uri": "desktop://history?minutes=30&limit=10"
            }
        }
        """);

        var responseJson = await transport.ReadServerResponseAsync();
        transport.CompleteClientInput();
        await runTask;

        var doc = JsonDocument.Parse(responseJson);
        var result = doc.RootElement.GetProperty("result");
        var contents = result.GetProperty("contents");
        Assert.True(contents.GetArrayLength() > 0);

        var jsonText = contents[0].GetProperty("text").GetString();
        Assert.NotNull(jsonText);
        Assert.Contains("Resource Test Window", jsonText);
    }

    [Fact]
    public async Task ReadResource_UnknownUri_ReturnsResourceNotFoundError()
    {
        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 999,
            "method": "resources/read",
            "params": {
                "uri": "desktop://invalid_resource_path"
            }
        }
        """);

        var responseJson = await transport.ReadServerResponseAsync();
        transport.CompleteClientInput();
        await runTask;

        var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        Assert.Equal(999, root.GetProperty("id").GetInt32());
        Assert.True(root.TryGetProperty("error", out var errorElem));
        Assert.Equal(JsonRpcErrorCode.ResourceNotFound, errorElem.GetProperty("code").GetInt32());
    }
}
