// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using ADCE.Spikes.Models;
using ADCE.Spikes.Native;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace ADCE.Spikes.Profiling;

/// <summary>
/// Empirical profiling runner for Windows Terminal (WinUI 3 / XAML Islands).
/// Uses modular SurfaceHarvester and SurfaceVisualizer primitives.
/// </summary>
internal static class TerminalProfileRunner
{
    public static async Task RunTerminalEmpiricalStudyAsync(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Research Spike: Windows Terminal (WinUI 3 / Cascadia) Study       ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();

        string mediaDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "media", "terminal_telemetry"));
        if (!Directory.Exists(mediaDir)) Directory.CreateDirectory(mediaDir);

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        using var automation = new UIA3Automation();
        using var engine = new UiaExtractionEngine();

        // 1. Locate Active Windows Terminal Window
        var candidates = new List<TargetWindow>();
        SpikeNativeMethods.EnumDesktopWindows(hDesktop != IntPtr.Zero ? hDesktop : IntPtr.Zero, (hWnd, lParam) =>
        {
            var sbTitle = new StringBuilder(512);
            SpikeNativeMethods.GetWindowText(hWnd, sbTitle, 512);
            var sbClass = new StringBuilder(256);
            SpikeNativeMethods.GetClassName(hWnd, sbClass, 256);
            SpikeNativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            string title = sbTitle.ToString();
            string className = sbClass.ToString();

            if (className.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(new TargetWindow(hWnd, string.IsNullOrWhiteSpace(title) ? "Windows Terminal" : title, className, pid));
            }
            return true;
        }, IntPtr.Zero);

        if (candidates.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] No active visible Windows Terminal window found on desktop.");
            Console.ResetColor();
            return;
        }

        // Select the candidate with the most UIA descendants (the active interactive session)
        TargetWindow target = candidates[0];
        int maxDesc = -1;
        foreach (var c in candidates)
        {
            try
            {
                var el = automation.FromHandle(c.Hwnd);
                int dCount = el.FindAllDescendants().Length;
                if (dCount > maxDesc)
                {
                    maxDesc = dCount;
                    target = c;
                }
            }
            catch { }
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[TARGET ACQUIRED] Windows Terminal HWND: 0x{target.Hwnd.ToInt64():X8} (PID {target.Pid})");
        Console.WriteLine($"  Title: \"{target.Title}\"");
        Console.WriteLine($"  Class: \"{target.ClassName}\"");
        Console.ResetColor();

        // 2. Bring window to foreground
        SpikeNativeMethods.ForceForegroundWindow(target.Hwnd);
        await Task.Delay(300);

        var windowElement = automation.FromHandle(target.Hwnd);
        if (windowElement == null)
        {
            Console.WriteLine("[ERROR] Could not attach UIA3 to target HWND.");
            return;
        }

        var steps = new List<SurfaceTelemetryStep>();

        // 3. Discover Window Descendants
        Console.WriteLine("\n[ANALYSIS] Discovering Windows Terminal structural parts...");
        var allDesc = windowElement.FindAllDescendants();
        Console.WriteLine($"  Total Descendants Discovered: {allDesc.Length}");

        // Surface 1: Terminal Tab Strip (TabItem / TabListView)
        var tabItem = allDesc.FirstOrDefault(d => SurfaceHarvester.SafeType(d) == ControlType.TabItem) ??
                      allDesc.FirstOrDefault(d => SurfaceHarvester.SafeId(d) == "TabListView" || SurfaceHarvester.SafeClass(d) == "ListViewItem");

        if (tabItem != null)
        {
            try { tabItem.Focus(); } catch { }
            await Task.Delay(200);
        }

        string s1Path = Path.Combine(mediaDir, "step_01_tab_strip.png");
        steps.Add(SurfaceHarvester.HarvestStep(
            automation, engine, target.Hwnd, windowElement, tabItem,
            1, "TabBar", "Terminal Tab Strip & Open Tabs", "Focus Tab Item",
            s1Path, DesktopAppArchetype.WinUI3Xaml));

        // Surface 2: Active Console Viewport (TermControl)
        var termControl = allDesc.FirstOrDefault(d => SurfaceHarvester.SafeClass(d) == "TermControl") ??
                          allDesc.FirstOrDefault(d => SurfaceHarvester.SafeName(d).Contains("PowerShell", StringComparison.OrdinalIgnoreCase) ||
                                                     SurfaceHarvester.SafeName(d).Contains("Command Prompt", StringComparison.OrdinalIgnoreCase) ||
                                                     SurfaceHarvester.SafeName(d).Contains("WSL", StringComparison.OrdinalIgnoreCase));

        if (termControl != null)
        {
            try { termControl.Focus(); } catch { }
            await Task.Delay(200);
        }

        string s2Path = Path.Combine(mediaDir, "step_02_console_viewport.png");
        steps.Add(SurfaceHarvester.HarvestStep(
            automation, engine, target.Hwnd, windowElement, termControl,
            2, "Terminal", "Active Shell Console Buffer Viewport", "Focus Terminal Viewport",
            s2Path, DesktopAppArchetype.WinUI3Xaml));

        // Surface 3: New Tab Button (NewTabButton / SplitButton)
        var newTabButton = allDesc.FirstOrDefault(d => SurfaceHarvester.SafeId(d) == "NewTabButton" || SurfaceHarvester.SafeId(d) == "AddButton") ??
                           allDesc.FirstOrDefault(d => SurfaceHarvester.SafeType(d) == ControlType.SplitButton || SurfaceHarvester.SafeName(d).Equals("New Tab", StringComparison.OrdinalIgnoreCase));

        string s3Path = Path.Combine(mediaDir, "step_03_new_tab_button.png");
        steps.Add(SurfaceHarvester.HarvestStep(
            automation, engine, target.Hwnd, windowElement, newTabButton,
            3, "NewTabButton", "New Tab Split Launcher Primary Action", "Focus New Tab Button",
            s3Path, DesktopAppArchetype.WinUI3Xaml));

        // Surface 4: Caret Dropdown Menu
        // Check if flyout is already open or inspectable
        var menuItems = windowElement.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem));
        if (menuItems.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n>>> SURFACE 4: [CaretMenu] - Dropdown Profile & Actions Flyout");
            Console.WriteLine("  [NOTICE] Caret dropdown menu requires manual actuation or interactive sampling.");
            Console.WriteLine("  To capture live, run: dotnet run --project src/ADCE.Spikes -- --sample-surface 4 3");
            Console.ResetColor();

            steps.Add(new SurfaceTelemetryStep(
                4, "CaretMenu", "Dropdown Profile & Actions Flyout", "Click Caret Button",
                "MenuItem", "Pending Capture", "", "",
                Rectangle.Empty, new List<SurfaceAncestorNode>(),
                DesktopSemanticZone.NavigationPanel, WindowPaneLocation.OverlayModal,
                "ProfileMenu", null, System.Collections.Immutable.ImmutableArray<string>.Empty,
                "step_04_caret_menu.png"));
        }
        else
        {
            string s4Path = Path.Combine(mediaDir, "step_04_caret_menu.png");
            steps.Add(SurfaceHarvester.HarvestStep(
                automation, engine, target.Hwnd, windowElement, menuItems[0],
                4, "CaretMenu", "Dropdown Profile & Actions Flyout", "Open Caret Menu",
                s4Path, DesktopAppArchetype.WinUI3Xaml));
        }

        // Surface 5: Settings Workspace
        var settingsElement = allDesc.FirstOrDefault(d => SurfaceHarvester.SafeClass(d).Contains("SettingsControl", StringComparison.OrdinalIgnoreCase) ||
                                                          SurfaceHarvester.SafeClass(d).Contains("NavigationView", StringComparison.OrdinalIgnoreCase));

        if (settingsElement == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n>>> SURFACE 5: [Settings] - Terminal Settings & Configuration Workspace");
            Console.WriteLine("  [NOTICE] Settings workspace is currently closed.");
            Console.WriteLine("  To capture live, run: dotnet run --project src/ADCE.Spikes -- --sample-surface 5 3");
            Console.ResetColor();

            steps.Add(new SurfaceTelemetryStep(
                5, "Settings", "Terminal Settings & Configuration Workspace", "Open Settings (Ctrl+,)",
                "Custom", "Pending Capture", "", "",
                Rectangle.Empty, new List<SurfaceAncestorNode>(),
                DesktopSemanticZone.NavigationPanel, WindowPaneLocation.MainContent,
                "Settings", null, System.Collections.Immutable.ImmutableArray<string>.Empty,
                "step_05_settings_workspace.png"));
        }
        else
        {
            string s5Path = Path.Combine(mediaDir, "step_05_settings_workspace.png");
            steps.Add(SurfaceHarvester.HarvestStep(
                automation, engine, target.Hwnd, windowElement, settingsElement,
                5, "Settings", "Terminal Settings & Configuration Workspace", "Open Settings",
                s5Path, DesktopAppArchetype.WinUI3Xaml));
        }

        // Save telemetry JSON
        string telemetryJsonPath = Path.Combine(mediaDir, "telemetry.json");
        string json = JsonSerializer.Serialize(steps, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(telemetryJsonPath, json, Encoding.UTF8);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n==========================================================================");
        Console.WriteLine("  Empirical Windows Terminal Profiling Pass Complete                      ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
        Console.WriteLine($"  Telemetry JSON Written  : {telemetryJsonPath}");
        Console.WriteLine($"  Screenshots Stored in   : {mediaDir}");
        Console.WriteLine($"  Physical Steps Recorded : {steps.Count(s => !s.Bounds.IsEmpty)} of {steps.Count}");
    }
}
