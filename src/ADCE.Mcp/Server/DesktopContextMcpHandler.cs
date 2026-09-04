// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Mcp.Protocol;

namespace ADCE.Mcp.Server;

/// <summary>
/// Implements the default MCP tool and resource provider for ADCE desktop state and history.
/// </summary>
public sealed class DesktopContextMcpHandler : IMcpHandler
{
    private readonly IDesktopStateStore _stateStore;
    private readonly ISemanticRuleEngine? _ruleEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopContextMcpHandler"/> class.
    /// </summary>
    /// <param name="stateStore">Underlying desktop state store.</param>
    /// <param name="ruleEngine">Optional semantic rule engine for runtime tagging mutations.</param>
    public DesktopContextMcpHandler(IDesktopStateStore stateStore, ISemanticRuleEngine? ruleEngine = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _ruleEngine = ruleEngine;
    }

    /// <inheritdoc />
    public IReadOnlyList<McpTool> GetTools()
    {
        return
        [
            new McpTool(
                Name: "get_desktop_context",
                Description: "Returns the current live active desktop context snapshot with explicit container hierarchy, optionally filtering by process name or projecting specific context.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        process_filter = new
                        {
                            type = "string",
                            description = "Optional process name filter (e.g. 'code', 'waterfox', 'Antigravity')"
                        },
                        projection = new
                        {
                            type = "string",
                            description = "Optional projection mode: 'full' (default), 'compact' (omits bounding boxes), 'ide' (only IDE file/tabs), 'terminal' (only shell/terminal)"
                        }
                    }
                }),
            new McpTool(
                Name: "get_active_context",
                Description: "Alias for get_desktop_context.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        process_filter = new { type = "string" },
                        projection = new { type = "string" }
                    }
                }),
            new McpTool(
                Name: "search_desktop_history",
                Description: "Searches past desktop history for matching keywords in window titles, tabs, or file paths.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "Keyword search term (e.g. file name, window title, tab title)"
                        },
                        limit = new
                        {
                            type = "integer",
                            description = "Maximum results to return (default 20, max 100)"
                        }
                    },
                    required = new[] { "query" }
                }),
            new McpTool(
                Name: "tag_active_control",
                Description: "Creates a persistent semantic rule for the currently focused desktop control and updates live context immediately.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        target_zone = new
                        {
                            type = "string",
                            description = "The target DesktopSemanticZone name (e.g. 'GitCommitBox', 'EditorBuffer', 'Terminal', 'SidebarExplorer', 'ChatPrompt', 'Timeline', 'Outline', 'ActivityBar', 'TabBar', 'QuickOpen', 'AddressBar')"
                        },
                        target_pane = new
                        {
                            type = "string",
                            description = "Optional target WindowPaneLocation name (e.g. 'ActivityBar', 'PrimarySidebar', 'MainContent', 'AuxiliarySidebar', 'BottomPanel', 'TopBar', 'StatusBar', 'OverlayModal')"
                        },
                        target_view = new
                        {
                            type = "string",
                            description = "Optional target view name (e.g. 'Explorer', 'SourceControl', 'Chat', 'Terminal', 'Editor')"
                        },
                        target_section = new
                        {
                            type = "string",
                            description = "Optional target section name (e.g. 'Timeline', 'Outline', 'CommitBox', 'ChatPrompt')"
                        },
                        scope = new
                        {
                            type = "string",
                            description = "Matching scope: 'element' (default, matches AutomationId/ClassName/Name), 'container' (matches container path), or 'process' (matches control type within process)"
                        },
                        comment = new
                        {
                            type = "string",
                            description = "Optional comment or reason for this rule"
                        }
                    },
                    required = new[] { "target_zone" }
                })
        ];
    }

    /// <inheritdoc />
    public IReadOnlyList<McpResource> GetResources()
    {
        return
        [
            new McpResource(
                Uri: "desktop://current",
                Name: "Current Desktop Context",
                Description: "Live snapshot of the current foreground desktop state",
                MimeType: "application/json"),
            new McpResource(
                Uri: "desktop://history",
                Name: "Desktop State History",
                Description: "Time-series window transitions and focus history (supports ?minutes=15)",
                MimeType: "application/json")
        ];
    }

    /// <inheritdoc />
    public async Task<CallToolResult> CallToolAsync(string toolName, JsonElement? arguments, CancellationToken cancellationToken = default)
    {
        switch (toolName)
        {
            case "get_desktop_context":
            case "get_active_context":
                return ExecuteGetDesktopContext(arguments);

            case "search_desktop_history":
                return await ExecuteSearchDesktopHistoryAsync(arguments, cancellationToken).ConfigureAwait(false);

            case "tag_active_control":
                return ExecuteTagActiveControl(arguments);

            default:
                return CallToolResult.ErrorText($"Unknown tool: '{toolName}'");
        }
    }

    /// <inheritdoc />
    public async Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var uriLower = uri.Trim().ToLowerInvariant();

        if (uriLower.StartsWith("desktop://current", StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = _stateStore.GetCurrentSnapshot();
            var json = snapshot != null
                ? JsonSerializer.Serialize(snapshot, AdceJsonSerializerOptions.Default)
                : "{}";

            return ReadResourceResult.SingleJson(uri, json);
        }

        if (uriLower.StartsWith("desktop://history", StringComparison.OrdinalIgnoreCase))
        {
            int minutes = 15;
            int limit = 50;

            var qIdx = uri.IndexOf('?');
            if (qIdx >= 0 && qIdx < uri.Length - 1)
            {
                var queryStr = uri[(qIdx + 1)..];
                var pairs = queryStr.Split('&', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length == 2)
                    {
                        var key = kv[0].Trim().ToLowerInvariant();
                        var val = kv[1].Trim();
                        if (key == "minutes" && int.TryParse(val, out var m))
                        {
                            minutes = Math.Clamp(m, 1, 1440);
                        }
                        else if (key == "limit" && int.TryParse(val, out var l))
                        {
                            limit = Math.Clamp(l, 1, 200);
                        }
                    }
                }
            }

            var since = DateTimeOffset.UtcNow.AddMinutes(-minutes);
            var results = new List<DesktopContextSnapshot>();
            await foreach (var item in _stateStore.GetHistoryAsync(since, limit, cancellationToken).ConfigureAwait(false))
            {
                results.Add(item);
            }

            var json = JsonSerializer.Serialize(results, AdceJsonSerializerOptions.Default);
            return ReadResourceResult.SingleJson(uri, json);
        }

        throw new KeyNotFoundException($"Resource URI '{uri}' not found.");
    }

    private CallToolResult ExecuteGetDesktopContext(JsonElement? arguments)
    {
        string? processFilter = null;
        if (arguments.HasValue && arguments.Value.ValueKind == JsonValueKind.Object)
        {
            if (arguments.Value.TryGetProperty("process_filter", out var filterElem) && filterElem.ValueKind == JsonValueKind.String)
            {
                processFilter = filterElem.GetString()?.Trim();
            }
        }

        var snapshot = _stateStore.GetCurrentSnapshot();
        if (snapshot == null)
        {
            return CallToolResult.SuccessText("{\"status\": \"no_active_context\"}");
        }

        if (!string.IsNullOrWhiteSpace(processFilter))
        {
            var procName = snapshot.Window.ProcessName;
            if (!procName.Contains(processFilter, StringComparison.OrdinalIgnoreCase))
            {
                return CallToolResult.SuccessText($"{{\"status\": \"filter_mismatch\", \"active_process\": \"{procName}\"}}");
            }
        }

        var json = JsonSerializer.Serialize(snapshot, AdceJsonSerializerOptions.Default);
        return CallToolResult.SuccessText(json);
    }

    private async Task<CallToolResult> ExecuteSearchDesktopHistoryAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        string query = string.Empty;
        int limit = 20;

        if (arguments.HasValue && arguments.Value.ValueKind == JsonValueKind.Object)
        {
            if (arguments.Value.TryGetProperty("query", out var qElem) && qElem.ValueKind == JsonValueKind.String)
            {
                query = qElem.GetString()?.Trim() ?? string.Empty;
            }

            if (arguments.Value.TryGetProperty("limit", out var lElem) && lElem.TryGetInt32(out var l))
            {
                limit = Math.Clamp(l, 1, 100);
            }
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return CallToolResult.ErrorText("Missing required parameter 'query'");
        }

        // Limit query length to 200 characters for safety
        if (query.Length > 200) query = query[..200];

        var results = new List<DesktopContextSnapshot>();
        await foreach (var item in _stateStore.SearchHistoryAsync(query, limit, cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }

        var json = JsonSerializer.Serialize(results, AdceJsonSerializerOptions.Default);
        return CallToolResult.SuccessText(json);
    }

    private CallToolResult ExecuteTagActiveControl(JsonElement? arguments)
    {
        if (arguments == null || !arguments.Value.TryGetProperty("target_zone", out var targetZoneElem) || string.IsNullOrWhiteSpace(targetZoneElem.GetString()))
        {
            return CallToolResult.ErrorText("Missing required parameter 'target_zone'.");
        }

        string targetZoneStr = targetZoneElem.GetString()!.Trim();
        if (!DesktopSemanticZoneExtensions.TryParseCanonical(targetZoneStr, out var targetZone) || targetZone == DesktopSemanticZone.Unknown)
        {
            if (!Enum.TryParse<DesktopSemanticZone>(targetZoneStr, ignoreCase: true, out targetZone) || targetZone == DesktopSemanticZone.Unknown)
            {
                return CallToolResult.ErrorText($"Invalid target_zone: '{targetZoneStr}'. Valid options are: {string.Join(", ", Enum.GetNames<DesktopSemanticZone>())}");
            }
        }

        WindowPaneLocation? targetPane = null;
        if (arguments.Value.TryGetProperty("target_pane", out var targetPaneElem) && !string.IsNullOrWhiteSpace(targetPaneElem.GetString()))
        {
            string paneStr = targetPaneElem.GetString()!.Trim();
            if (!WindowPaneLocationExtensions.TryParseCanonical(paneStr, out var parsedPane) || parsedPane == WindowPaneLocation.Unknown)
            {
                if (!Enum.TryParse<WindowPaneLocation>(paneStr, ignoreCase: true, out parsedPane) || parsedPane == WindowPaneLocation.Unknown)
                {
                    return CallToolResult.ErrorText($"Invalid target_pane: '{paneStr}'. Valid options are: {string.Join(", ", Enum.GetNames<WindowPaneLocation>())}");
                }
            }
            targetPane = parsedPane;
        }

        string? targetView = null;
        if (arguments.Value.TryGetProperty("target_view", out var targetViewElem) && !string.IsNullOrWhiteSpace(targetViewElem.GetString()))
        {
            targetView = targetViewElem.GetString()!.Trim();
        }

        string? targetSection = null;
        if (arguments.Value.TryGetProperty("target_section", out var targetSectionElem) && !string.IsNullOrWhiteSpace(targetSectionElem.GetString()))
        {
            targetSection = targetSectionElem.GetString()!.Trim();
        }

        var snapshot = _stateStore.GetCurrentSnapshot();
        if (snapshot == null || snapshot.Focus == null || string.IsNullOrWhiteSpace(snapshot.Window.ProcessName))
        {
            return CallToolResult.ErrorText("No active desktop snapshot available to tag.");
        }

        string scope = "element";
        if (arguments.Value.TryGetProperty("scope", out var scopeElem) && !string.IsNullOrWhiteSpace(scopeElem.GetString()))
        {
            scope = scopeElem.GetString()!.Trim().ToLowerInvariant();
        }

        string? comment = null;
        if (arguments.Value.TryGetProperty("comment", out var commentElem))
        {
            comment = commentElem.GetString();
        }

        var focus = snapshot.Focus;
        var window = snapshot.Window;

        SemanticRule rule;
        if (scope == "container")
        {
            string containerTarget = focus.ContainerPath.Length > 0 ? focus.ContainerPath[0] : focus.AutomationId;
            rule = new SemanticRule
            {
                TargetZone = targetZone,
                TargetPane = targetPane,
                TargetView = targetView,
                TargetSection = targetSection,
                ProcessPattern = window.ProcessName,
                ContainerPattern = !string.IsNullOrWhiteSpace(containerTarget) ? containerTarget : null,
                Priority = 100,
                IsUserOverride = true,
                Comment = comment ?? $"Tagged container as {targetZone}"
            };
        }
        else if (scope == "process")
        {
            rule = new SemanticRule
            {
                TargetZone = targetZone,
                TargetPane = targetPane,
                TargetView = targetView,
                TargetSection = targetSection,
                ProcessPattern = window.ProcessName,
                ControlType = focus.ControlType,
                Priority = 90,
                IsUserOverride = true,
                Comment = comment ?? $"Tagged all {focus.ControlType} in {window.ProcessName} as {targetZone}"
            };
        }
        else
        {
            rule = new SemanticRule
            {
                TargetZone = targetZone,
                TargetPane = targetPane,
                TargetView = targetView,
                TargetSection = targetSection,
                ProcessPattern = window.ProcessName,
                ControlType = focus.ControlType,
                AutomationIdPattern = !string.IsNullOrWhiteSpace(focus.AutomationId) ? focus.AutomationId : null,
                ClassNamePattern = !string.IsNullOrWhiteSpace(focus.ClassName) ? focus.ClassName : null,
                ElementNamePattern = (string.IsNullOrWhiteSpace(focus.AutomationId) && !string.IsNullOrWhiteSpace(focus.ElementName)) ? focus.ElementName : null,
                Priority = 100,
                IsUserOverride = true,
                Comment = comment ?? $"Tagged element as {targetZone}"
            };
        }

        _ruleEngine?.AddOrUpdateRule(rule);

        var resolvedPane = targetPane ?? (focus.PaneLocation != WindowPaneLocation.Unknown ? focus.PaneLocation : targetZone.ToDefaultPaneLocation());
        var resolvedView = targetView ?? focus.ActiveView ?? targetZone.ToDefaultView();
        var resolvedSection = targetSection ?? focus.SectionName ?? targetZone.ToDefaultSection();

        var pathBuilder = ImmutableArray.CreateBuilder<string>(3);
        if (resolvedPane != WindowPaneLocation.Unknown) pathBuilder.Add(resolvedPane.ToString());
        if (!string.IsNullOrWhiteSpace(resolvedView)) pathBuilder.Add(resolvedView);
        if (!string.IsNullOrWhiteSpace(resolvedSection)) pathBuilder.Add(resolvedSection);

        var updatedFocus = focus with
        {
            SemanticZone = targetZone,
            PaneLocation = resolvedPane,
            ActiveView = resolvedView,
            SectionName = resolvedSection,
            SemanticPath = pathBuilder.ToImmutable()
        };
        var updatedSnapshot = snapshot with { Focus = updatedFocus };
        _stateStore.UpdateCurrentSnapshot(updatedSnapshot);

        var resultPayload = new
        {
            success = true,
            rule_id = rule.RuleId,
            target_zone = targetZone.ToString(),
            semantic_zone = targetZone.ToString(),
            target_pane = resolvedPane.ToString(),
            pane_location = resolvedPane.ToString(),
            target_view = resolvedView,
            active_view = resolvedView,
            target_section = resolvedSection,
            section_name = resolvedSection,
            semantic_path = updatedFocus.SemanticPath,
            process = window.ProcessName,
            control_type = focus.ControlType,
            automation_id = focus.AutomationId,
            scope = scope,
            priority = rule.Priority,
            message = $"Successfully tagged active control as [{targetZone}] in pane [{resolvedPane}] with priority {rule.Priority}."
        };

        return CallToolResult.SuccessText(JsonSerializer.Serialize(resultPayload, AdceJsonSerializerOptions.Default));
    }
}
