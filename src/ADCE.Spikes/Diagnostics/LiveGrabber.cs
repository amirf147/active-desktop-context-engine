// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Extraction.Engine;
using ADCE.Spikes.Models;
using ADCE.Spikes.Native;

namespace ADCE.Spikes.Diagnostics;

internal static class LiveGrabber
{
    public static async Task RunStandaloneGrabberAsync(string? filter = null, int delaySeconds = 0)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Milestone 2: Standalone Context Grabber Live Extraction Spike     ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();

        if (delaySeconds > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            for (int i = delaySeconds; i > 0; i--)
            {
                Console.WriteLine($"[GRAB COUNTDOWN] Capturing active foreground window in {i} second{(i > 1 ? "s" : "")}... (Switch to your target app now)");
                await Task.Delay(1000);
            }
            Console.WriteLine("[GRAB COUNTDOWN] Capturing now!\n");
            Console.ResetColor();
        }

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        using var engine = new UiaExtractionEngine();

        var targets = new List<TargetWindow>();
        SpikeNativeMethods.EnumDesktopWindows(hDesktop != IntPtr.Zero ? hDesktop : IntPtr.Zero, (hWnd, lParam) =>
        {
            var sbTitle = new StringBuilder(512);
            SpikeNativeMethods.GetWindowText(hWnd, sbTitle, 512);
            var sbClass = new StringBuilder(256);
            SpikeNativeMethods.GetClassName(hWnd, sbClass, 256);
            SpikeNativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            string title = sbTitle.ToString();
            string className = sbClass.ToString();

            if (!string.IsNullOrWhiteSpace(title) || className.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase))
            {
                if (className != "Progman" && className != "WorkerW" && className != "Shell_TrayWnd" && className != "Windows.UI.Core.CoreWindow" && className != "EdgeUiInputTopWndClass")
                {
                    targets.Add(new TargetWindow(hWnd, string.IsNullOrWhiteSpace(title) ? "Windows Terminal" : title, className, pid));
                }
            }
            return true;
        }, IntPtr.Zero);

        List<TargetWindow> selectedTargets = [];
        if (!string.IsNullOrWhiteSpace(filter) && !filter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            selectedTargets = targets.Where(t => t.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                                 t.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                                 t.Pid.ToString() == filter).ToList();
        }
        else if (filter?.Equals("all", StringComparison.OrdinalIgnoreCase) == true)
        {
            selectedTargets = targets;
        }
        else
        {
            var fg = SpikeNativeMethods.GetForegroundWindow();
            if (fg != nint.Zero)
            {
                selectedTargets = [new TargetWindow(fg, "Foreground Window", "", 0)];
            }
            else if (targets.Count > 0)
            {
                selectedTargets = [targets[0]];
            }
        }

        if (selectedTargets.Count == 0)
        {
            Console.WriteLine("[WARN] No matching top-level target windows discovered.");
            return;
        }

        foreach (var target in selectedTargets)
        {
            var sw = Stopwatch.StartNew();
            var snapshot = await engine.ExtractSnapshotAsync(target.Hwnd);
            sw.Stop();

            PrintSnapshot(snapshot, sw.Elapsed.TotalMilliseconds);
        }
    }

    public static void PrintSnapshot(DesktopContextSnapshot snapshot, double totalPipeMs)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[GRAB SUCCESS] Context snapshot captured in {snapshot.ExtractionDurationMs:F2} ms (Total pipe: {totalPipeMs:F2} ms)\n");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.WriteLine(" [ENVELOPE BREAKDOWN]");
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.ResetColor();

        Console.WriteLine($" Window HWND    : 0x{snapshot.Window.Hwnd:X8} (PID: {snapshot.Window.Pid})");
        Console.WriteLine($" Window Title   : '{snapshot.Window.Title}'");
        Console.WriteLine($" Process / Class: {snapshot.Window.ProcessName} / '{snapshot.Window.ClassName}'");
        Console.WriteLine($" App Archetype  : {snapshot.Window.Archetype}");
        Console.WriteLine($" Focus Target   : [{snapshot.Focus.SemanticZone}] '{snapshot.Focus.ElementName}' ({snapshot.Focus.ControlType})");
        if (!snapshot.Focus.ContainerPath.IsDefault && !snapshot.Focus.ContainerPath.IsEmpty)
        {
            Console.WriteLine($" Container Path : {string.Join(" > ", snapshot.Focus.ContainerPath)}");
        }
        if (!snapshot.Focus.ContainerClasses.IsDefault && !snapshot.Focus.ContainerClasses.IsEmpty)
        {
            Console.WriteLine($" Container Class: {string.Join(" > ", snapshot.Focus.ContainerClasses)}");
        }
        if (snapshot.Focus.IsOverlay)
        {
            Console.WriteLine($" Overlay Modal  : [YES]");
        }

        if (snapshot.IdeContext != null)
        {
            Console.WriteLine($"\n [IDE CONTEXT]");
            if (!string.IsNullOrWhiteSpace(snapshot.IdeContext.WorkspaceRoot))
            {
                Console.WriteLine($"  Workspace Root: {snapshot.IdeContext.WorkspaceRoot}");
            }
            Console.WriteLine($"  Active File   : {snapshot.IdeContext.ActiveFilePath}");
            Console.WriteLine($"  Sidebar View  : {snapshot.IdeContext.ActiveSidebarView}");
            if (snapshot.IdeContext.IsDiffEditor)
            {
                Console.WriteLine($"  Diff Mode     : [ACTIVE DIFF]");
            }
            Console.WriteLine($"  Open Tabs ({snapshot.IdeContext.OpenEditorTabs.Length}):");
            foreach (var t in snapshot.IdeContext.OpenEditorTabs)
            {
                string active = t.IsActive ? "**[ACTIVE]**" : "        ";
                Console.WriteLine($"   - {active} {t.Title}");
            }
        }
        else if (snapshot.BrowserContext != null)
        {
            Console.WriteLine($"\n [BROWSER CONTEXT]");
            Console.WriteLine($"  Container     : {snapshot.BrowserContext.ContainerType}");
            Console.WriteLine($"  Active Tab    : {snapshot.BrowserContext.ActiveTab}");
            Console.WriteLine($"  Sanitized URL : {snapshot.BrowserContext.UrlAddress}");
            Console.WriteLine($"  Open Tabs ({snapshot.BrowserContext.Tabs.Length}):");
            foreach (var t in snapshot.BrowserContext.Tabs)
            {
                string active = t.IsActive ? "**[ACTIVE]**" : "        ";
                Console.WriteLine($"   - {active} {t.Title}");
            }
        }
        else if (snapshot.ExplorerContext != null)
        {
            Console.WriteLine($"\n [EXPLORER CONTEXT]");
            Console.WriteLine($"  Current Path  : {snapshot.ExplorerContext.CurrentPath}");
            Console.WriteLine($"  Breadcrumbs   : {string.Join(" > ", snapshot.ExplorerContext.Breadcrumbs)}");
            Console.WriteLine($"  Selected Items: {string.Join(", ", snapshot.ExplorerContext.SelectedItems)}");
        }
        else if (snapshot.TerminalContext != null)
        {
            Console.WriteLine($"\n [TERMINAL CONTEXT]");
            Console.WriteLine($"  Shell Title   : {snapshot.TerminalContext.ShellTitle}");
            Console.WriteLine($"  Open Tabs ({snapshot.TerminalContext.Tabs.Length}):");
            foreach (var t in snapshot.TerminalContext.Tabs)
            {
                Console.WriteLine($"   - {t.Title}");
            }
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n--------------------------------------------------------------------------");
        Console.WriteLine(" [MCP JSON PAYLOAD]");
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.ResetColor();

        var displayOptions = new JsonSerializerOptions(AdceJsonSerializerOptions.Default)
        {
            WriteIndented = true
        };
        string json = JsonSerializer.Serialize(snapshot, displayOptions);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(json);
        Console.ResetColor();
        Console.WriteLine();
    }
}
