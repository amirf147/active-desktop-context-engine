// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

public class McpToolExecutionTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDesktopStateStore _store;
    private readonly DesktopContextMcpHandler _handler;

    public McpToolExecutionTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"adce_mcp_tool_test_{Guid.NewGuid():N}.db");
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

    private DesktopContextSnapshot CreateSampleSnapshot(string processName = "Antigravity.exe", string title = "active-desktop-context-engine - Antigravity IDE")
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
                Hwnd = 0x1234,
                Title = title,
                ProcessName = processName,
                Pid = 12345,
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080),
                IsMinimized = false,
                IsMaximized = true
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "Chat Input",
                BoundingBox = new BoundingRectangle(100, 100, 400, 200),
                AutomationId = "chat-input",
                ClassName = "monaco-editor",
                SemanticZone = DesktopSemanticZone.ChatPrompt
            },
            IdeContext = new IdeContext
            {
                ActiveFilePath = "docs/CONTEXT.md",
                ActiveSidebarView = "Explorer",
                OpenEditorTabs = [new TabItemInfo { Index = 0, Title = "CONTEXT.md", IsActive = true }],
                EditBuffer = "CONTEXT.md"
            },
            ExtractionDurationMs = 1.25
        };
    }

    [Fact]
    public async Task ToolsList_ReturnsAllRegisteredTools()
    {
        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("""{"jsonrpc": "2.0", "id": 1, "method": "tools/list"}""");

        var responseJson = await transport.ReadServerResponseAsync();
        transport.CompleteClientInput();
        await runTask;

        var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("id").GetInt32());
        var tools = root.GetProperty("result").GetProperty("tools");
        Assert.True(tools.GetArrayLength() >= 2);

        var toolNames = new List<string>();
        foreach (var t in tools.EnumerateArray())
        {
            toolNames.Add(t.GetProperty("name").GetString()!);
        }

        Assert.Contains("get_desktop_context", toolNames);
        Assert.Contains("search_desktop_history", toolNames);
    }

    [Fact]
    public async Task CallTool_GetDesktopContext_ConformsToMcpContentSchema()
    {
        var snapshot = CreateSampleSnapshot();
        _store.UpdateCurrentSnapshot(snapshot);

        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 10,
            "method": "tools/call",
            "params": {
                "name": "get_desktop_context",
                "arguments": {}
            }
        }
        """);

        var responseJson = await transport.ReadServerResponseAsync();
        transport.CompleteClientInput();
        await runTask;

        var doc = JsonDocument.Parse(responseJson);
        var result = doc.RootElement.GetProperty("result");

        // Verification of MCP Spec Trap #1: tool calls MUST use singular property name 'content'
        Assert.True(result.TryGetProperty("content", out var contentElem), "tools/call result must have 'content' property");
        Assert.False(result.TryGetProperty("contents", out _), "tools/call result must NOT have plural 'contents' property");
        Assert.Equal(JsonValueKind.Array, contentElem.ValueKind);
        Assert.True(contentElem.GetArrayLength() > 0);

        var firstContent = contentElem[0];
        Assert.Equal("text", firstContent.GetProperty("type").GetString());
        var textContent = firstContent.GetProperty("text").GetString();
        Assert.NotNull(textContent);
        Assert.Contains("Antigravity.exe", textContent);
        Assert.Contains("active-desktop-context-engine", textContent);
    }

    [Fact]
    public async Task CallTool_GetDesktopContext_WithProcessFilter_FiltersCorrectly()
    {
        var snapshot = CreateSampleSnapshot("waterfox.exe", "Mozilla Waterfox");
        _store.UpdateCurrentSnapshot(snapshot);

        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());

        // 1. Matching filter
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "tools/call",
            "params": {
                "name": "get_desktop_context",
                "arguments": { "process_filter": "waterfox" }
            }
        }
        """);

        var response1 = await transport.ReadServerResponseAsync();
        Assert.Contains("waterfox.exe", response1);

        // 2. Mismatching filter
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/call",
            "params": {
                "name": "get_desktop_context",
                "arguments": { "process_filter": "code" }
            }
        }
        """);

        var response2 = await transport.ReadServerResponseAsync();
        Assert.Contains("filter_mismatch", response2);

        transport.CompleteClientInput();
        await runTask;
    }

    [Fact]
    public async Task CallTool_SearchDesktopHistory_ReturnsMatchingRecords()
    {
        var snapshot1 = CreateSampleSnapshot(title: "Working on MCP Spec in Antigravity");
        var snapshot2 = CreateSampleSnapshot(title: "Browsing C# 14 Documentation in Browser");

        _store.UpdateCurrentSnapshot(snapshot1);
        _store.UpdateCurrentSnapshot(snapshot2);

        // Wait for background SQLite channel persistence
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000)
        {
            int count = 0;
            await foreach (var _ in _store.GetHistoryAsync(DateTimeOffset.UtcNow.AddMinutes(-5), 10))
            {
                count++;
            }
            if (count >= 2) break;
            await Task.Delay(10);
        }

        var transport = new InMemoryMcpTransport();
        var server = new McpServer(transport, _handler);

        var runTask = Task.Run(() => server.RunAsync());
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 42,
            "method": "tools/call",
            "params": {
                "name": "search_desktop_history",
                "arguments": { "query": "MCP Spec", "limit": 10 }
            }
        }
        """);

        var responseJson = await transport.ReadServerResponseAsync();
        transport.CompleteClientInput();
        await runTask;

        var doc = JsonDocument.Parse(responseJson);
        var result = doc.RootElement.GetProperty("result");
        var content = result.GetProperty("content")[0].GetProperty("text").GetString();

        Assert.NotNull(content);
        Assert.Contains("MCP Spec", content);

        var resultsArray = JsonSerializer.Deserialize<DesktopContextSnapshot[]>(content, AdceJsonSerializerOptions.Default);
        Assert.NotNull(resultsArray);
        Assert.True(resultsArray.Length >= 1);
    }
}
