// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADCE.Core.Serialization;
using ADCE.Daemon.Configuration;
using ADCE.Daemon.Hosting;
using ADCE.Daemon.Native;
using ADCE.Daemon.UI;

namespace ADCE.Daemon;

public static class Program
{
    private const string SingleInstanceMutexName = @"Local\ADCE_Daemon_SingleInstance_Mutex";

    [STAThread]
    public static int Main(string[] args)
    {
        // 1. Enforce Per-Monitor V2 DPI awareness immediately before any UI creation
        try
        {
            NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch { }

        // Setup global unhandled crash sinks
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ADCE.Core.Logging.AdceLogger.Default.Error("Fatal", "AppDomain unhandled crash", ex);
            }
        };

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            ADCE.Core.Logging.AdceLogger.Default.Error("Fatal", "WinForms UI thread unhandled exception", e.Exception);
        };

        try
        {
            // 2. Parse command-line options
            var options = DaemonOptions.Parse(args);
            ADCE.Core.Logging.AdceLogger.Default.MinimumLevel = options.LogLevel;

            // 3. Handle interactive CLI commands (Trap 3: Attach parent console in WinExe mode)
            if (options.ShowHelp)
            {
                NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                Console.WriteLine(DaemonOptions.GetHelpText());
                return 0;
            }

            if (options.ShowVersion)
            {
                NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                var ver = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
                Console.WriteLine($"ADCE Daemon v{ver} (.NET 10 x64 / FlaUI 5.0.0)");
                return 0;
            }

            if (options.ShowLogs)
            {
                NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("==========================================================================");
                Console.WriteLine("  ADCE DAEMON DIAGNOSTIC TELEMETRY & RECENT LOGS                          ");
                Console.WriteLine("==========================================================================");
                Console.ResetColor();

                var logPath = ADCE.Core.Logging.AdceLogger.Default.LogFilePath;
                Console.WriteLine($"Log File Location: {logPath}");
                Console.WriteLine("--------------------------------------------------------------------------");

                if (!string.IsNullOrWhiteSpace(logPath) && File.Exists(logPath))
                {
                    try
                    {
                        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(stream);
                        var lines = new System.Collections.Generic.List<string>();
                        while (reader.ReadLine() is { } line)
                        {
                            lines.Add(line);
                        }

                        int start = Math.Max(0, lines.Count - 50);
                        for (int i = start; i < lines.Count; i++)
                        {
                            var l = lines[i];
                            if (l.Contains("[ERROR]")) Console.ForegroundColor = ConsoleColor.Red;
                            else if (l.Contains("[WARN ]")) Console.ForegroundColor = ConsoleColor.Yellow;
                            else if (l.Contains("[DEBUG]")) Console.ForegroundColor = ConsoleColor.DarkGray;
                            else Console.ForegroundColor = ConsoleColor.White;

                            Console.WriteLine(l);
                        }
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Error reading log file]: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("(No log entries recorded yet.)");
                }
                Console.WriteLine("==========================================================================");
                return 0;
            }

            // 4. Single-Instance Named Mutex Guard (Trap 2: Prevent multiple tray instances)
            using var mutex = new Mutex(true, SingleInstanceMutexName, out bool isNewInstance);
            if (!isNewInstance && !options.IsStdio)
            {
                NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[ADCE.Daemon] Another instance of ADCE Daemon is already active.");
                Console.ResetColor();
                return 0;
            }

            using var cts = new CancellationTokenSource();
            var host = new DaemonHost(options);

            // Handle Console Ctrl+C / ProcessExit for graceful headless shutdown
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                cts.Cancel();
                host.Dispose();
            };

            if (options.ShowStatus)
            {
                NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                host.StartAsync(cts.Token).GetAwaiter().GetResult();
                var status = host.GetStatus();
                Console.WriteLine(JsonSerializer.Serialize(status, AdceJsonSerializerOptions.Default));
                host.StopAsync().GetAwaiter().GetResult();
                return 0;
            }

            // 5. Execution mode dispatch
            if (options.IsHeadless || options.IsStdio)
            {
                // Headless / Stdio console mode
                if (options.IsStdio)
                {
                    host.StartAsync(cts.Token).GetAwaiter().GetResult();
                    try
                    {
                        Task.Delay(Timeout.Infinite, cts.Token).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException) { }
                }
                else
                {
                    NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[ADCE.Daemon] Active Desktop Context Engine started in headless mode.");
                    if (options.EnableSse)
                    {
                        Console.WriteLine($"[ADCE.Daemon] MCP SSE endpoint listening at: http://localhost:{options.Port}/sse");
                    }
                    Console.WriteLine("[ADCE.Daemon] Press Ctrl+C to terminate.");
                    Console.ResetColor();

                    host.StartAsync(cts.Token).GetAwaiter().GetResult();

                    try
                    {
                        Task.Delay(Timeout.Infinite, cts.Token).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException) { }
                }

                host.StopAsync().GetAwaiter().GetResult();
                return 0;
            }
            else
            {
                // System Tray Host GUI Mode (Guaranteed STA Thread Message Loop)
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Install WindowsFormsSynchronizationContext on the STA thread
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                host.StartAsync(cts.Token).GetAwaiter().GetResult();

                using var trayContext = new TrayApplicationContext(host, options);
                Application.Run(trayContext);

                host.StopAsync().GetAwaiter().GetResult();
                return 0;
            }
        }
        catch (Exception ex)
        {
            ADCE.Core.Logging.AdceLogger.Default.Error("Fatal", "Fatal crash in Main entry point", ex);
            NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ADCE.Daemon Fatal Error]: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
