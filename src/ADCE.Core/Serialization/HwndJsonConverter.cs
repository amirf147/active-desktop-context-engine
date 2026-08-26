// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ADCE.Core.Serialization;

/// <summary>
/// Serializes native window handles (nint / IntPtr / HWND) to and from hex strings (e.g. "0x00DB083E").
/// Uses unsigned casting to prevent negative sign artifacts and OverflowExceptions when high-order bits are set.
/// </summary>
public sealed class HwndJsonConverter : JsonConverter<nint>
{
    public override nint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string for HWND token, but got {reader.TokenType}.");
        }

        string? raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        ReadOnlySpan<char> span = raw.AsSpan().Trim();
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            span = span[2..];
        }

        if (ulong.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong unsignedValue))
        {
            return (nint)unsignedValue;
        }

        throw new JsonException($"Invalid HWND hex string format: '{raw}'.");
    }

    public override void Write(Utf8JsonWriter writer, nint value, JsonSerializerOptions options)
    {
        // Format as unsigned hex with at least 8 digits and standard 0x prefix
        writer.WriteStringValue($"0x{(nuint)value:X8}");
    }
}
