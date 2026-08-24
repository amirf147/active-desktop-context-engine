// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ADCE.Core.Serialization;

/// <summary>
/// Provides pre-configured, high-performance JsonSerializerOptions aligned with the ADCE MCP schema.
/// </summary>
public static class AdceJsonSerializerOptions
{
    private static readonly JsonSerializerOptions s_defaultOptions = CreateDefaultOptions();

    /// <summary>
    /// Gets the shared singleton JsonSerializerOptions instance conforming to ADCE MCP schema.
    /// </summary>
    public static JsonSerializerOptions Default => s_defaultOptions;

    /// <summary>
    /// Creates a new instance of JsonSerializerOptions configured for ADCE context serialization.
    /// </summary>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        options.Converters.Add(new HwndJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        return options;
    }
}
