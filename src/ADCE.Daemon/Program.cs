// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

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
    public static async Task<int> Main(string[] args)
    {
        // 1. Enforce Per-Monitor V2 DPI awareness immediately before any UI creation
        try
        {
            NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch { }

        // 2. Parse command-line options
        var options = DaemonOptions.Parse(args);

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
            await host.StartAsync(cts.Token);
            var status = host.GetStatus();
            Console.WriteLine(JsonSerializer.Serialize(status, AdceJsonSerializerOptions.Default));
            await host.StopAsync();
            return 0;
        }

        // 5. Execution mode dispatch
        if (options.IsHeadless || options.IsStdio)
        {
            // Headless / Stdio console mode
            if (options.IsStdio)
            {
                await host.StartAsync(cts.Token);
                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
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

                await host.StartAsync(cts.Token);

                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException) { }
            }

            await host.StopAsync();
            return 0;
        }
        else
        {
            // System Tray Host GUI Mode (STA Thread Message Loop)
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Install WindowsFormsSynchronizationContext on the STA thread before any async yield
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            await host.StartAsync(cts.Token);

            using var trayContext = new TrayApplicationContext(host, options);
            Application.Run(trayContext);

            await host.StopAsync();
            return 0;
        }
    }
}
