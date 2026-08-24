// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using ADCE.Core.Serialization;
using Xunit;

namespace ADCE.Core.Tests;

public class HwndJsonConverterTests
{
    private record HwndWrapper(nint Hwnd);

    [Fact]
    public void Write_FormatsWithHexPrefixAndAtLeast8Digits()
    {
        var options = AdceJsonSerializerOptions.Default;

        var wrapper = new HwndWrapper(0x00DB083E);
        string json = JsonSerializer.Serialize(wrapper, options);

        Assert.Equal("{\"hwnd\":\"0x00DB083E\"}", json);
    }

    [Fact]
    public void Write_HighBitHandles_PreventsNegativeSignArtifacts()
    {
        var options = AdceJsonSerializerOptions.Default;

        // Simulate a handle with bit 63 or bit 31 set (-1)
        nint negativeHandle = (nint)(-1);
        var wrapper = new HwndWrapper(negativeHandle);
        string json = JsonSerializer.Serialize(wrapper, options);

        // Must not contain negative sign
        Assert.DoesNotContain("-", json);
        Assert.StartsWith("{\"hwnd\":\"0x", json);

        // Verify matches MCP schema regex: ^0x[0-9A-Fa-f]+$
        var match = Regex.Match(json, @"""hwnd"":\s*""(0x[0-9A-Fa-f]+)""");
        Assert.True(match.Success, $"JSON did not match MCP HWND schema regex: {json}");
    }

    [Fact]
    public void Read_ParsesHexWithOrWithoutPrefix()
    {
        var options = AdceJsonSerializerOptions.Default;

        string jsonWithPrefix = "{\"hwnd\":\"0x00DB083E\"}";
        var result1 = JsonSerializer.Deserialize<HwndWrapper>(jsonWithPrefix, options);
        Assert.NotNull(result1);
        Assert.Equal((nint)0x00DB083E, result1.Hwnd);

        string jsonWithoutPrefix = "{\"hwnd\":\"00DB083E\"}";
        var result2 = JsonSerializer.Deserialize<HwndWrapper>(jsonWithoutPrefix, options);
        Assert.NotNull(result2);
        Assert.Equal((nint)0x00DB083E, result2.Hwnd);
    }

    [Fact]
    public void Read_ParsesFull64BitMaxHexWithoutOverflow()
    {
        var options = AdceJsonSerializerOptions.Default;

        // Full 64-bit max hex: "0xFFFFFFFFFFFFFFFF" (would throw OverflowException if parsed with Convert.ToInt64)
        string json64BitMax = "{\"hwnd\":\"0xFFFFFFFFFFFFFFFF\"}";
        var result = JsonSerializer.Deserialize<HwndWrapper>(json64BitMax, options);

        Assert.NotNull(result);
        Assert.Equal((nint)(-1), result.Hwnd);
    }

    [Fact]
    public void Read_InvalidHex_ThrowsJsonException()
    {
        var options = AdceJsonSerializerOptions.Default;

        string invalidJson = "{\"hwnd\":\"0xNOT_A_HEX\"}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<HwndWrapper>(invalidJson, options));
    }
}
