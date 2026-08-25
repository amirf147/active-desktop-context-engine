// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ADCE.Mcp.Transports;
using Xunit;

namespace ADCE.Mcp.Tests;

public class StdioTransportTests
{
    [Fact]
    public async Task StdioTransport_DoesNotEmitUtf8BomOnWrites()
    {
        using var outStream = new MemoryStream();
        using var inStream = new MemoryStream();

        var transport = new StdioMcpTransport(inStream, outStream);
        await transport.SendMessageAsync("""{"jsonrpc":"2.0","result":{"status":"ok"}}""");

        var bytes = outStream.ToArray();
        Assert.True(bytes.Length >= 3);

        // Verify first 3 bytes are NOT the UTF-8 BOM (0xEF, 0xBB, 0xBF)
        bool isBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        Assert.False(isBom, "Stdio stream output must NOT start with a UTF-8 Byte Order Mark (BOM).");

        // Verify valid UTF-8 string
        var text = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("{\"jsonrpc\":", text);
    }

    [Fact]
    public async Task StdioTransport_PreservesNonAsciiAndUnicodeCharacters()
    {
        var unicodePayload = "{\"title\":\"C# 14 & FlaUI — ä/ö/ü & 日本語 Context\"}";
        var inBytes = Encoding.UTF8.GetBytes(unicodePayload + "\n");
        using var inStream = new MemoryStream(inBytes);
        using var outStream = new MemoryStream();

        var transport = new StdioMcpTransport(inStream, outStream);

        var messages = new List<string>();
        await foreach (var msg in transport.ReadIncomingMessagesAsync())
        {
            messages.Add(msg);
        }

        Assert.Single(messages);
        Assert.Equal(unicodePayload, messages[0]);
    }

    [Fact]
    public async Task StdioTransport_TerminatesCleanlyOnEof()
    {
        // Empty stream simulates instant EOF
        using var inStream = new MemoryStream();
        using var outStream = new MemoryStream();

        var transport = new StdioMcpTransport(inStream, outStream);

        int count = 0;
        await foreach (var _ in transport.ReadIncomingMessagesAsync())
        {
            count++;
        }

        Assert.Equal(0, count);
    }
}
