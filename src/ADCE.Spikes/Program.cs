// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Linq;
using System.Threading.Tasks;
using ADCE.Spikes.Diagnostics;
using ADCE.Spikes.Milestones;
using ADCE.Spikes.Profiling;
using ADCE.Spikes.Transport;

namespace ADCE.Spikes;

/// <summary>
/// Lean CLI dispatcher for ADCE empirical diagnostics, application profiling suites,
/// verification harness, and historical milestone benchmarks.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        // 0. Help Menu
        if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            PrintHelp();
            return;
        }

        // 1. Daily Diagnostics & Developer Tooling
        if (args.Any(a => a.Equals("--grab", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("--extract", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("-g", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("--grab-delay", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("--delay", StringComparison.OrdinalIgnoreCase)))
        {
            int delaySeconds = 0;
            int delayIdx = Array.FindIndex(args, a => a.Equals("--grab-delay", StringComparison.OrdinalIgnoreCase) ||
                                                     a.Equals("--delay", StringComparison.OrdinalIgnoreCase));
            if (delayIdx >= 0 && delayIdx + 1 < args.Length && int.TryParse(args[delayIdx + 1], out int d))
            {
                delaySeconds = d;
            }
            else if (delayIdx >= 0)
            {
                delaySeconds = 3;
            }

            int grabIdx = Array.FindIndex(args, a => a.Equals("--grab", StringComparison.OrdinalIgnoreCase) ||
                                                    a.Equals("--extract", StringComparison.OrdinalIgnoreCase) ||
                                                    a.Equals("-g", StringComparison.OrdinalIgnoreCase));
            string? filter = (grabIdx >= 0 && grabIdx + 1 < args.Length && !args[grabIdx + 1].StartsWith("-")) ? args[grabIdx + 1] : null;

            await LiveGrabber.RunStandaloneGrabberAsync(filter, delaySeconds);
        }
        else if (args.Any(a => a.Equals("--apps", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--list-apps", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--windows", StringComparison.OrdinalIgnoreCase)))
        {
            AppEnumerator.RunListOpenApps();
        }
        else if (args.Any(a => a.Equals("--inspect-panes", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--panes", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("-p", StringComparison.OrdinalIgnoreCase)))
        {
            int pIdx = Array.FindIndex(args, a => a.Equals("--inspect-panes", StringComparison.OrdinalIgnoreCase) ||
                                                  a.Equals("--panes", StringComparison.OrdinalIgnoreCase) ||
                                                  a.Equals("-p", StringComparison.OrdinalIgnoreCase));
            string? filter = (pIdx >= 0 && pIdx + 1 < args.Length && !args[pIdx + 1].StartsWith("-")) ? args[pIdx + 1] : null;
            PaneInspector.RunPaneInspectionSpike(filter);
        }
        else if (args.Any(a => a.Equals("--timeline", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--history", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("-t", StringComparison.OrdinalIgnoreCase)))
        {
            int countIdx = Array.FindIndex(args, a => a.Equals("--timeline", StringComparison.OrdinalIgnoreCase) ||
                                                      a.Equals("--history", StringComparison.OrdinalIgnoreCase) ||
                                                      a.Equals("-t", StringComparison.OrdinalIgnoreCase));
            int limit = (countIdx >= 0 && countIdx + 1 < args.Length && int.TryParse(args[countIdx + 1], out int lim)) ? lim : 20;

            int dbIdx = Array.FindIndex(args, a => a.Equals("--db-path", StringComparison.OrdinalIgnoreCase) ||
                                                   a.Equals("--db", StringComparison.OrdinalIgnoreCase));
            string? customDbPath = (dbIdx >= 0 && dbIdx + 1 < args.Length && !args[dbIdx + 1].StartsWith("-")) ? args[dbIdx + 1] : null;

            await TimelineVisualizer.RunDatabaseTimelineSpikeAsync(limit, customDbPath);
        }
        else if (args.Any(a => a.Equals("--analyze", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--report", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("-a", StringComparison.OrdinalIgnoreCase)))
        {
            int dbIdx = Array.FindIndex(args, a => a.Equals("--db-path", StringComparison.OrdinalIgnoreCase) ||
                                                   a.Equals("--db", StringComparison.OrdinalIgnoreCase));
            string? customDbPath = (dbIdx >= 0 && dbIdx + 1 < args.Length && !args[dbIdx + 1].StartsWith("-")) ? args[dbIdx + 1] : null;

            await SessionAnalyzer.RunDeepAnalysisSpikeAsync(customDbPath);
        }

        // 2. Application Empirical Profiling Suites
        else if (args.Any(a => a.Equals("--waterfox-study", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--waterfox", StringComparison.OrdinalIgnoreCase)))
        {
            await WaterfoxProfileRunner.RunWaterfoxEmpiricalStudyAsync(args);
        }
        else if (args.Any(a => a.Equals("--antigravity-study", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--antigravity", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--ide", StringComparison.OrdinalIgnoreCase)))
        {
            await AntigravityProfileRunner.RunAntigravityEmpiricalStudyAsync(args);
        }

        // 3. Claim Verification Matrix [Legacy / Deprecated]
        else if (args.Any(a => a.Equals("--verify-mocks", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--mock-verify", StringComparison.OrdinalIgnoreCase)))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("[LEGACY] Claim Verification Matrix is deprecated meta-tooling. Active testing lives in: 'dotnet test'\n");
            Console.ResetColor();
            int vIdx = Array.FindIndex(args, a => a.Equals("--verify", StringComparison.OrdinalIgnoreCase) || a.Equals("-v", StringComparison.OrdinalIgnoreCase));
            string? singleClaim = (vIdx >= 0 && vIdx + 1 < args.Length && !args[vIdx + 1].StartsWith("-")) ? args[vIdx + 1] : null;
            await MilestoneSpikes.RunClaimVerificationSuiteAsync(liveMode: false, singleClaim);
        }
        else if (args.Any(a => a.Equals("--verify-all", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--verify", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("-v", StringComparison.OrdinalIgnoreCase)))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("[LEGACY] Claim Verification Matrix is deprecated meta-tooling. Active testing lives in: 'dotnet test'\n");
            Console.ResetColor();
            int vIdx = Array.FindIndex(args, a => a.Equals("--verify", StringComparison.OrdinalIgnoreCase) || a.Equals("-v", StringComparison.OrdinalIgnoreCase));
            string? singleClaim = (vIdx >= 0 && vIdx + 1 < args.Length && !args[vIdx + 1].StartsWith("-")) ? args[vIdx + 1] : null;
            await MilestoneSpikes.RunClaimVerificationSuiteAsync(liveMode: true, singleClaim);
        }
        else if (args.Any(a => a.Equals("--verify-spike", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--spike4.5", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--spike45", StringComparison.OrdinalIgnoreCase)))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("[LEGACY] Milestone 4.5 stimulus-response spike is a historical research artifact.\n");
            Console.ResetColor();
            await MilestoneSpikes.RunGate3EmpiricalMicroSpikeAsync();
        }

        // 4. Model Context Protocol (MCP) Transports
        else if (args.Any(a => a.Equals("--mcp-test", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--mcp", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--spike5", StringComparison.OrdinalIgnoreCase)))
        {
            await McpDiagnosticHost.RunMcpTestSpikeAsync();
        }
        else if (args.Any(a => a.Equals("--mcp-stdio", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--stdio", StringComparison.OrdinalIgnoreCase)))
        {
            await McpDiagnosticHost.RunMcpStdioAsync();
        }
        else if (args.Any(a => a.Equals("--mcp-sse", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--sse", StringComparison.OrdinalIgnoreCase)))
        {
            int portIdx = Array.FindIndex(args, a => a.Equals("--port", StringComparison.OrdinalIgnoreCase) ||
                                                     a.Equals("-p", StringComparison.OrdinalIgnoreCase));
            int port = (portIdx >= 0 && portIdx + 1 < args.Length && int.TryParse(args[portIdx + 1], out int p)) ? p : 8424;
            await McpDiagnosticHost.RunMcpSseAsync(port);
        }

        // 5. Milestone Verification Spikes & Benchmarks
        else if (args.Any(a => a.Equals("--daemon", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--daemon-spike", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--spike6", StringComparison.OrdinalIgnoreCase)))
        {
            await MilestoneSpikes.RunDaemonSpikeAsync();
        }
        else if (args.Any(a => a.Equals("--storage", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--store", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--spike4", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("-s", StringComparison.OrdinalIgnoreCase)))
        {
            await MilestoneSpikes.RunStorageSpikeAsync();
        }
        else if (args.Any(a => a.Equals("--events", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--listen", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--spike3", StringComparison.OrdinalIgnoreCase)))
        {
            int durIdx = Array.FindIndex(args, a => a.Equals("--duration", StringComparison.OrdinalIgnoreCase) ||
                                                    a.Equals("-d", StringComparison.OrdinalIgnoreCase));
            int durationSeconds = (durIdx >= 0 && durIdx + 1 < args.Length && int.TryParse(args[durIdx + 1], out int d)) ? d : 5;
            await MilestoneSpikes.RunEventPipelineSpikeAsync(durationSeconds);
        }
        else if (args.Any(a => a.Equals("--flaui-benchmark", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--benchmark", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("--spike1", StringComparison.OrdinalIgnoreCase)))
        {
            MilestoneSpikes.RunFlaUiBenchmark();
        }
        else
        {
            // Default: Milestone 1 Core Models & Serialization Demo
            MilestoneSpikes.RunMilestone1CoreDemo();
        }
    }

    private static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  Active Desktop Context Engine (ADCE) — CLI Diagnostics & Spike Tools    ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
        Console.WriteLine("Usage: dotnet run --project src/ADCE.Spikes -- [command] [options]\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("DAILY DIAGNOSTICS & DEVTOOLS:");
        Console.ResetColor();
        Console.WriteLine("  --grab [filter] [--delay N]  Capture active foreground context snapshot");
        Console.WriteLine("  --apps                       List open windows and classify UI archetypes");
        Console.WriteLine("  --panes [filter]             Inspect physical window panes & container hierarchy");
        Console.WriteLine("  --timeline [N] [--db path]   Visualize recent SQLite time-series transitions");
        Console.WriteLine("  --analyze [--db path]        Deep statistical audit of stored transitions\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("EMPIRICAL PROFILING SUITES:");
        Console.ResetColor();
        Console.WriteLine("  --waterfox-study             Run Gecko 9-stop study and update 01_waterfox.md");
        Console.WriteLine("  --antigravity-study          Run Monaco 9-stop study and update telemetry.json\n");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("CLAIM VERIFICATION [LEGACY / DEPRECATED]:");
        Console.ResetColor();
        Console.WriteLine("  --verify-mocks               [Legacy] Run mock claim suite (Note: use 'dotnet test')");
        Console.WriteLine("  --verify-all                 [Legacy] Run live interactive claim verification (CLM-001..CLM-006)");
        Console.WriteLine("  --verify [CLM-xxx]           [Legacy] Run single claim test against live OS");
        Console.WriteLine("  --verify-spike               [Legacy] Run Gate 3 stimulus-response micro-spike\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("MODEL CONTEXT PROTOCOL (MCP):");
        Console.ResetColor();
        Console.WriteLine("  --mcp-stdio                  Host MCP server over standard input/output");
        Console.WriteLine("  --mcp-sse [--port N]         Host MCP server over HTTP/SSE (default: 8424)");
        Console.WriteLine("  --mcp-test                   Run automated JSON-RPC protocol test suite\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("HISTORICAL MILESTONE BENCHMARKS:");
        Console.ResetColor();
        Console.WriteLine("  --benchmark                  Benchmark UIA3 CacheRequest vs live COM queries");
        Console.WriteLine("  --events [--duration N]      Listen for foreground WinEvents with debouncing");
        Console.WriteLine("  --storage                    Benchmark SQLite WAL and L1 cache throughput");
        Console.WriteLine("  --daemon                     Test in-process daemon host and tray icon factory");
        Console.WriteLine("  (no args)                    Run Milestone 1 Core Models & Serialization Demo\n");
    }
}
