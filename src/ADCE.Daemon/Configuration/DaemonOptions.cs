// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

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
            ShowHud = showHud
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
          -H, --hud                Launch with non-activating floating HUD overlay
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
          ADCE.Daemon.exe --stdio           # Launch as MCP server child process over Stdio
          ADCE.Daemon.exe -p 9000           # Launch with SSE server on custom port 9000
          ADCE.Daemon.exe --no-tray         # Launch as headless background console daemon
        """;
    }
}
