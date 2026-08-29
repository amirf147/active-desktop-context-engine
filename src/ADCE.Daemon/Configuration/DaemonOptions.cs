// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.IO;

namespace ADCE.Daemon.Configuration;

/// <summary>
/// Strongly-typed command-line configuration options for the ADCE Daemon.
/// </summary>
public sealed class DaemonOptions
{
    public const int DefaultPort = 8424;
    public const int DefaultDebounceMs = 50;
    public const int DefaultMaxBurstMs = 250;

    /// <summary>
    /// Gets whether to run in headless mode without a system tray icon.
    /// </summary>
    public bool IsHeadless { get; init; }

    /// <summary>
    /// Gets whether to run the MCP server over standard I/O (Stdio).
    /// </summary>
    public bool IsStdio { get; init; }

    /// <summary>
    /// Gets whether to enable the HTTP/SSE MCP transport.
    /// </summary>
    public bool EnableSse { get; init; } = true;

    /// <summary>
    /// Gets the HTTP/SSE port number. Default is 8424.
    /// </summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>
    /// Gets the custom database path for SQLite WAL persistence.
    /// If null, defaults to the user local application data directory (ADCE/adce_history.db).
    /// </summary>
    public string? DatabasePath { get; init; }

    /// <summary>
    /// Gets the event debouncing window in milliseconds. Default is 50 ms.
    /// </summary>
    public int DebounceMs { get; init; } = DefaultDebounceMs;

    /// <summary>
    /// Gets the maximum burst delay clamp in milliseconds. Default is 250 ms.
    /// </summary>
    public int MaxBurstMs { get; init; } = DefaultMaxBurstMs;

    /// <summary>
    /// Gets whether the user requested the help screen.
    /// </summary>
    public bool ShowHelp { get; init; }

    /// <summary>
    /// Gets whether the user requested the version information.
    /// </summary>
    public bool ShowVersion { get; init; }

    /// <summary>
    /// Gets whether the user requested a one-shot status query.
    /// </summary>
    public bool ShowStatus { get; init; }

    /// <summary>
    /// Gets whether to show the non-activating floating HUD overlay.
    /// </summary>
    public bool ShowHud { get; init; }

    /// <summary>
    /// Gets whether to automatically expand the DOM & Structural Tree View inside the HUD.
    /// </summary>
    public bool ShowHudTree { get; init; }

    /// <summary>
    /// Gets whether the user requested recent diagnostic logs and system health telemetry.
    /// </summary>
    public bool ShowLogs { get; init; }

    /// <summary>
    /// Gets the configured log severity level (Debug, Info, Warn, Error, None).
    /// </summary>
    public ADCE.Core.Logging.AdceLogLevel LogLevel { get; init; } = ADCE.Core.Logging.AdceLogLevel.Info;

    /// <summary>
    /// Gets whether heuristic semantic zone resolution is enabled (default: true).
    /// When set to false via --explicit-only or --no-zones, zone heuristics are bypassed for pure explicit structural inspection.
    /// </summary>
    public bool EnableSemanticZones { get; init; } = true;

    /// <summary>
    /// Resolves the effective SQLite database file path.
    /// </summary>
    public string ResolveEffectiveDatabasePath()
    {
        if (DatabasePath != null && (DatabasePath.Equals(":memory:", StringComparison.OrdinalIgnoreCase) ||
                                     DatabasePath.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase)))
        {
            return ":memory:";
        }

        if (!string.IsNullOrWhiteSpace(DatabasePath))
        {
            return Path.GetFullPath(DatabasePath);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var adceDir = Path.Combine(localAppData, "ADCE");
        return Path.Combine(adceDir, "adce_history.db");
    }

    /// <summary>
    /// Parses command-line arguments into a <see cref="DaemonOptions"/> instance.
    /// </summary>
    public static DaemonOptions Parse(string[] args)
    {
        bool isHeadless = false;
        bool isStdio = false;
        bool enableSse = true;
        int port = DefaultPort;
        string? dbPath = null;
        int debounceMs = DefaultDebounceMs;
        int maxBurstMs = DefaultMaxBurstMs;
        bool showHelp = false;
        bool showVersion = false;
        bool showStatus = false;
        bool showHud = false;
        bool showHudTree = false;
        bool showLogs = false;
        bool enableSemanticZones = true;
        ADCE.Core.Logging.AdceLogLevel logLevel = ADCE.Core.Logging.AdceLogLevel.Info;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg == "-h" ||
                arg == "-?" ||
                arg == "/?")
            {
                showHelp = true;
            }
            else if (arg.Equals("--version", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
            {
                showVersion = true;
            }
            else if (arg.Equals("--status", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                showStatus = true;
            }
            else if (arg.Equals("--logs", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--diagnostics", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-l", StringComparison.OrdinalIgnoreCase))
            {
                showLogs = true;
            }
            else if (arg.Equals("--no-zones", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--explicit-only", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--disable-zones", StringComparison.OrdinalIgnoreCase))
            {
                enableSemanticZones = false;
            }
            else if (arg.Equals("--zones", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--enable-zones", StringComparison.OrdinalIgnoreCase))
            {
                enableSemanticZones = true;
            }
            else if (arg.Equals("--log-level", StringComparison.OrdinalIgnoreCase) &&
                     i + 1 < args.Length)
            {
                if (Enum.TryParse<ADCE.Core.Logging.AdceLogLevel>(args[i + 1], ignoreCase: true, out var lvl))
                {
                    logLevel = lvl;
                }
                i++;
            }
            else if (arg.Equals("--hud-tree", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--tree", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--dom-tree", StringComparison.OrdinalIgnoreCase))
            {
                showHud = true;
                showHudTree = true;
            }
            else if (arg.Equals("--hud", StringComparison.OrdinalIgnoreCase) ||
                     arg == "-H")
            {
                showHud = true;
            }
            else if (arg.Equals("--headless", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--no-tray", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-n", StringComparison.OrdinalIgnoreCase))
            {
                isHeadless = true;
            }
            else if (arg.Equals("--stdio", StringComparison.OrdinalIgnoreCase))
            {
                isStdio = true;
                isHeadless = true;
            }
            else if (arg.Equals("--no-sse", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--disable-sse", StringComparison.OrdinalIgnoreCase))
            {
                enableSse = false;
            }
            else if (arg.Equals("--sse", StringComparison.OrdinalIgnoreCase))
            {
                enableSse = true;
            }
            else if ((arg.Equals("--port", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-p", StringComparison.OrdinalIgnoreCase)) &&
                     i + 1 < args.Length && int.TryParse(args[i + 1], out int p) && p > 0 && p <= 65535)
            {
                port = p;
                enableSse = true;
                i++;
            }
            else if ((arg.Equals("--db-path", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--database", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--storage", StringComparison.OrdinalIgnoreCase)) &&
                     i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                dbPath = args[i + 1];
                i++;
            }
            else if (arg.Equals("--debounce", StringComparison.OrdinalIgnoreCase) &&
                     i + 1 < args.Length && int.TryParse(args[i + 1], out int d) && d >= 0)
            {
                debounceMs = d;
                i++;
            }
            else if (arg.Equals("--max-burst", StringComparison.OrdinalIgnoreCase) &&
                     i + 1 < args.Length && int.TryParse(args[i + 1], out int b) && b >= 0)
            {
                maxBurstMs = b;
                i++;
            }
        }

        return new DaemonOptions
        {
            IsHeadless = isHeadless,
            IsStdio = isStdio,
            EnableSse = enableSse,
            Port = port,
            DatabasePath = dbPath,
            DebounceMs = debounceMs,
            MaxBurstMs = maxBurstMs,
            ShowHelp = showHelp,
            ShowVersion = showVersion,
            ShowStatus = showStatus,
            ShowHud = showHud,
            ShowHudTree = showHudTree,
            ShowLogs = showLogs,
            LogLevel = logLevel,
            EnableSemanticZones = enableSemanticZones
        };
    }

    /// <summary>
    /// Generates human-readable help and command line usage documentation.
    /// </summary>
    public static string GetHelpText()
    {
        return """
        Active Desktop Context Engine (ADCE) - Windows Daemon Host
        Usage: ADCE.Daemon.exe [options]

        Options:
          -h, --help               Show command line help and exit
          -v, --version            Show version information and exit
          -s, --status             Query live daemon status and exit
          -l, --logs, --diagnostics Dump recent diagnostic logs and system health
          --log-level <level>      Set logging severity (Debug, Info, Warn, Error, None)
          --explicit-only, --no-zones Disable semantic zone heuristics (pure explicit structural mode)
          -H, --hud                Launch with non-activating floating HUD overlay
          --hud-tree, --tree       Launch floating HUD with DOM & Structural Tree View expanded
          -n, --headless, --no-tray Run daemon in background console mode without tray icon
          --stdio                  Host MCP server over standard I/O (Stdio child process)
          --sse                    Enable MCP server over HTTP/SSE (enabled by default)
          --no-sse, --disable-sse  Disable HTTP/SSE MCP transport
          -p, --port <port>        Set HTTP/SSE port number (default: 8424)
          --db-path <path>         Set custom path for SQLite WAL history database
          --debounce <ms>          Set event debounce quiet window in ms (default: 50)
          --max-burst <ms>         Set maximum burst clamp delay in ms (default: 250)

        Examples:
          ADCE.Daemon.exe                   # Launch with System Tray icon and SSE server on port 8424
          ADCE.Daemon.exe --hud             # Launch with System Tray and live non-activating floating HUD
          ADCE.Daemon.exe --hud-tree        # Launch HUD with live hierarchical DOM/Structural Tree View
          ADCE.Daemon.exe --hud --explicit-only # Launch HUD in pure explicit structural inspection mode
          ADCE.Daemon.exe --logs            # View latest diagnostic logs and troubleshoot issues
          ADCE.Daemon.exe --log-level Debug # Launch with verbose UIA extraction debug logging
          ADCE.Daemon.exe --stdio           # Launch as MCP server child process over Stdio
          ADCE.Daemon.exe --no-tray         # Launch as headless background console daemon
        """;
    }
}
