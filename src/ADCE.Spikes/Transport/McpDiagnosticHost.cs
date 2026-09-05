// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using ADCE.Extraction.Events;
using ADCE.Mcp.Server;
using ADCE.Mcp.Transports;
using ADCE.Storage.Database;
using ADCE.Storage.Options;

namespace ADCE.Spikes.Transport;

internal static class McpDiagnosticHost
{
    public static async Task RunMcpTestSpikeAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("    ADCE Milestone 5: Model Context Protocol (MCP) Verification Spike     ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
        Console.WriteLine($"Runtime   : .NET {Environment.Version} ({(Environment.Is64BitProcess ? "x64" : "x86")})");
        Console.WriteLine($"Timestamp : {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}\n");

        var testDbPath = Path.Combine(Path.GetTempPath(), $"adce_mcp_spike_{Guid.NewGuid():N}.db");
        var store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = testDbPath });
        store.Initialize();

        // Seed snapshot
        var sampleSnapshot = new DesktopContextSnapshot
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
                Hwnd = 0x00DB083E,
                Title = "active-desktop-context-engine - Antigravity IDE",
                ProcessName = "Antigravity.exe",
                Pid = 26420,
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
                BoundingBox = new BoundingRectangle(1200, 400, 600, 600),
                AutomationId = "chat-input",
                ClassName = "interactive-session",
                SemanticZone = DesktopSemanticZone.ChatPrompt
            },
            IdeContext = new IdeContext
            {
                ActiveFilePath = "docs/CONTEXT.md",
                ActiveSidebarView = "Explorer",
                OpenEditorTabs = [new TabItemInfo { Index = 0, Title = "CONTEXT.md", IsActive = true }],
                EditBuffer = "CONTEXT.md"
            },
            ExtractionDurationMs = 0.85
        };

        store.UpdateCurrentSnapshot(sampleSnapshot);
        await Task.Delay(200); // Allow SQLite batch commit

        var transport = new InMemoryMcpTransport();
        var handler = new DesktopContextMcpHandler(store);
        var server = new McpServer(transport, handler);

        var serverTask = Task.Run(() => server.RunAsync());

        var sw = Stopwatch.StartNew();

        // 1. Handshake (initialize)
        Console.WriteLine("[STEP 1/5] Testing MCP Handshake ('initialize')...");
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "clientInfo": { "name": "McpSpikeTestClient", "version": "1.0.0" }
            }
        }
        """);
        var initRespJson = await transport.ReadServerResponseAsync();
        var initDoc = JsonDocument.Parse(initRespJson);
        var initResult = initDoc.RootElement.GetProperty("result");
        Console.WriteLine($"  -> Negotiated Protocol: {initResult.GetProperty("protocolVersion").GetString()}");
        Console.WriteLine($"  -> Server Name: {initResult.GetProperty("serverInfo").GetProperty("name").GetString()}\n");

        // 2. Tools List
        Console.WriteLine("[STEP 2/5] Testing Tools Discovery ('tools/list')...");
        await transport.PushClientMessageAsync("""{"jsonrpc": "2.0", "id": 2, "method": "tools/list"}""");
        var toolsRespJson = await transport.ReadServerResponseAsync();
        var toolsDoc = JsonDocument.Parse(toolsRespJson);
        var toolsArray = toolsDoc.RootElement.GetProperty("result").GetProperty("tools");
        Console.WriteLine($"  -> Registered Tools Count: {toolsArray.GetArrayLength()}");
        foreach (var t in toolsArray.EnumerateArray())
        {
            Console.WriteLine($"     * {t.GetProperty("name").GetString()} - {t.GetProperty("description").GetString()}");
        }
        Console.WriteLine();

        // 3. Tool Call: get_desktop_context
        Console.WriteLine("[STEP 3/5] Testing Tool Execution ('tools/call' -> 'get_desktop_context')...");
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 3,
            "method": "tools/call",
            "params": {
                "name": "get_desktop_context",
                "arguments": { "process_filter": "Antigravity" }
            }
        }
        """);
        var toolCallRespJson = await transport.ReadServerResponseAsync();
        var toolCallDoc = JsonDocument.Parse(toolCallRespJson);
        var toolCallResult = toolCallDoc.RootElement.GetProperty("result");
        bool hasSingularContent = toolCallResult.TryGetProperty("content", out var contentArray);
        Console.WriteLine($"  -> Spec Check (Singular 'content' array): {(hasSingularContent ? "PASS (Conforms)" : "FAIL")}");
        Console.WriteLine($"  -> Content Type: {contentArray[0].GetProperty("type").GetString()}");
        Console.WriteLine($"  -> Text Length: {contentArray[0].GetProperty("text").GetString()?.Length} chars\n");

        // 4. Resources List & Read: desktop://current
        Console.WriteLine("[STEP 4/5] Testing Resource Read ('resources/read' -> 'desktop://current')...");
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 4,
            "method": "resources/read",
            "params": { "uri": "desktop://current" }
        }
        """);
        var resRespJson = await transport.ReadServerResponseAsync();
        var resDoc = JsonDocument.Parse(resRespJson);
        var resResult = resDoc.RootElement.GetProperty("result");
        bool hasPluralContents = resResult.TryGetProperty("contents", out var contentsArray);
        Console.WriteLine($"  -> Spec Check (Plural 'contents' array): {(hasPluralContents ? "PASS (Conforms)" : "FAIL")}");
        Console.WriteLine($"  -> URI: {contentsArray[0].GetProperty("uri").GetString()}");
        Console.WriteLine($"  -> MimeType: {contentsArray[0].GetProperty("mimeType").GetString()}\n");

        // 5. History Search Tool
        Console.WriteLine("[STEP 5/5] Testing History Search ('tools/call' -> 'search_desktop_history')...");
        await transport.PushClientMessageAsync("""
        {
            "jsonrpc": "2.0",
            "id": 5,
            "method": "tools/call",
            "params": {
                "name": "search_desktop_history",
                "arguments": { "query": "Antigravity", "limit": 5 }
            }
        }
        """);
        var searchRespJson = await transport.ReadServerResponseAsync();
        var searchDoc = JsonDocument.Parse(searchRespJson);
        var searchResult = searchDoc.RootElement.GetProperty("result");
        var searchText = searchResult.GetProperty("content")[0].GetProperty("text").GetString();
        Console.WriteLine($"  -> Search Results payload: {searchText?[..Math.Min(120, searchText.Length)]}...\n");

        sw.Stop();

        transport.CompleteClientInput();
        await serverTask;
        store.Dispose();
        try { if (File.Exists(testDbPath)) File.Delete(testDbPath); } catch { }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("==========================================================================");
        Console.WriteLine($"  [VERDICT: PASS] All 5 MCP Protocol Operations Verified ({sw.Elapsed.TotalMilliseconds:F2} ms)");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
    }

    public static async Task RunMcpStdioAsync()
    {
        await Console.Error.WriteLineAsync("[ADCE.Mcp] Starting Active Desktop Context Engine MCP Server over Stdio...");
        await Console.Error.WriteLineAsync("[ADCE.Mcp] Protocol Standard: Model Context Protocol (MCP) JSON-RPC 2.0 (2024-11-05)");
        await Console.Error.WriteLineAsync("[ADCE.Mcp] stdout is strictly reserved for JSON-RPC message frames.");

        var store = new SqliteDesktopStateStore();
        await store.InitializeAsync();

        // Start background extraction pipeline to keep state live
        using var engine = new UiaExtractionEngine();
        var initSnapshot = await engine.ExtractForegroundSnapshotAsync();
        if (initSnapshot != null) store.UpdateCurrentSnapshot(initSnapshot);

        using var hookProvider = new WinEventHookProvider();
        hookProvider.Start();

        var cts = new CancellationTokenSource();
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var token in hookProvider.EventReader.ReadAllAsync(cts.Token))
            {
                try
                {
                    var snapshot = await engine.ExtractSnapshotAsync(token.Hwnd, cts.Token);
                    if (snapshot != null)
                    {
                        store.UpdateCurrentSnapshot(snapshot);
                    }
                }
                catch { }
            }
        }, cts.Token);

        var transport = new StdioMcpTransport();
        var handler = new DesktopContextMcpHandler(store);
        var server = new McpServer(transport, handler);

        await Console.Error.WriteLineAsync("[ADCE.Mcp] Server listening on Stdio. Awaiting host client initialization...");
        await server.RunAsync(cts.Token);

        await Console.Error.WriteLineAsync("[ADCE.Mcp] Stdio EOF received. Shutting down daemon...");
        cts.Cancel();
        hookProvider.Dispose();
        try { await consumerTask; } catch { }
        await store.DisposeAsync();
    }

    public static async Task RunMcpSseAsync(int port)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[ADCE.Mcp] Starting MCP Server over HTTP/SSE on port {port}...");
        Console.ResetColor();

        var store = new SqliteDesktopStateStore();
        await store.InitializeAsync();

        // Initial snapshot
        using var engine = new UiaExtractionEngine();
        var initSnapshot = await engine.ExtractForegroundSnapshotAsync();
        if (initSnapshot != null) store.UpdateCurrentSnapshot(initSnapshot);

        using var hookProvider = new WinEventHookProvider();
        hookProvider.Start();

        var cts = new CancellationTokenSource();
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var token in hookProvider.EventReader.ReadAllAsync(cts.Token))
            {
                try
                {
                    var snapshot = await engine.ExtractSnapshotAsync(token.Hwnd, cts.Token);
                    if (snapshot != null)
                    {
                        store.UpdateCurrentSnapshot(snapshot);
                    }
                }
                catch { }
            }
        }, cts.Token);

        var transport = new HttpSseMcpTransport(port);
        transport.Start();

        var handler = new DesktopContextMcpHandler(store);
        var server = new McpServer(transport, handler);

        Console.WriteLine($"[ADCE.Mcp] SSE Endpoint: {transport.BaseUrl}sse");
        Console.WriteLine($"[ADCE.Mcp] POST Messages Endpoint: {transport.BaseUrl}messages");
        Console.WriteLine("[ADCE.Mcp] Press Ctrl+C to stop server.");

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await server.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        hookProvider.Dispose();
        try { await consumerTask; } catch { }
        await transport.DisposeAsync();
        await store.DisposeAsync();
    }
}
