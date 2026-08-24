// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Text.Json.Serialization;

namespace ADCE.Core.Models;

/// <summary>
/// Immutable snapshot representing the unified desktop context state at a specific point in time.
/// Conforms to the ADCE Model Context Protocol (MCP) JSON-RPC 2.0 schema specification.
/// </summary>
public sealed record DesktopContextSnapshot
{
    /// <summary>ISO-8601 UTC timestamp when this context snapshot was captured.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Active virtual desktop workspace and monitor envelope.</summary>
    public required WorkspaceEnvelope Workspace { get; init; }

    /// <summary>Active foreground window metadata and process identity.</summary>
    public required WindowEnvelope Window { get; init; }

    /// <summary>Currently focused UI control or active keyboard input element.</summary>
    public required FocusedControlInfo Focus { get; init; }

    /// <summary>Semantic context if the foreground window is an IDE / code editor.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IdeContext? IdeContext { get; init; }

    /// <summary>Semantic context if the foreground window is a web browser.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BrowserContext? BrowserContext { get; init; }

    /// <summary>Semantic context if the foreground window is Windows File Explorer.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExplorerContext? ExplorerContext { get; init; }

    /// <summary>Semantic context if the foreground window is Windows Terminal or a console.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TerminalContext? TerminalContext { get; init; }

    /// <summary>Duration in milliseconds spent extracting this context snapshot.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double ExtractionDurationMs { get; init; }
}
