// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace ADCE.Spikes;

public class Program
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessWindowStation(IntPtr hWinSta);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public record TargetWindow(IntPtr Hwnd, string Title, string ClassName, uint Pid);
    public record TabInfo(int Index, string Title, bool IsActive);

    public static void Main()
    {
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Micro-Spike 1: FlaUI 5 / .NET 10 UIA3 Real-World Telemetry         ");
        Console.WriteLine("==========================================================================");
        Console.WriteLine($"Runtime   : .NET {Environment.Version} ({(Environment.Is64BitProcess ? "x64" : "x86")})");
        Console.WriteLine($"Timestamp : {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}\n");

        var swInit = Stopwatch.StartNew();
        using var automation = new UIA3Automation();
        swInit.Stop();
        Console.WriteLine($"[INIT] UIA3Automation initialized in {swInit.Elapsed.TotalMilliseconds:F2} ms\n");

        var hWinSta = OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SetProcessWindowStation(hWinSta);
        var hDesktop = OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SetThreadDesktop(hDesktop);

        var targets = new List<TargetWindow>();
        EnumDesktopWindows(hDesktop != IntPtr.Zero ? hDesktop : IntPtr.Zero, (hWnd, lParam) =>
        {
            var sbTitle = new StringBuilder(512);
            GetWindowText(hWnd, sbTitle, 512);
            var sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, 256);
            GetWindowThreadProcessId(hWnd, out uint pid);
            string title = sbTitle.ToString();
            string className = sbClass.ToString();

            if (!string.IsNullOrWhiteSpace(title) &&
                (className == "MozillaWindowClass" || className == "Chrome_WidgetWin_1"))
            {
                targets.Add(new TargetWindow(hWnd, title, className, pid));
            }
            return true;
        }, IntPtr.Zero);

        Console.WriteLine($"[WIN32] Discovered {targets.Count} candidate browser/IDE window(s).\n");

        foreach (var target in targets)
        {
            BenchmarkTarget(automation, target, iterations: 10);
        }

        Console.WriteLine("==========================================================================");
        Console.WriteLine("  MICRO-SPIKE 1 COMPLETE: EMPIRICAL FINDINGS SAVED");
        Console.WriteLine("==========================================================================");
    }

    private static void BenchmarkTarget(UIA3Automation automation, TargetWindow target, int iterations = 1)
    {
        string targetType = target.ClassName switch
        {
            "MozillaWindowClass" => "WATERFOX/FIREFOX",
            "Chrome_WidgetWin_1" => target.Title.Contains("Antigravity") ? "ANTIGRAVITY IDE" :
                                    target.Title.Contains("Visual Studio Code") ? "VS CODE" : "ELECTRON/CHROME",
            _ => "UNKNOWN"
        };

        var bindTimes = new List<double>();
        var containerTimes = new List<double>();
        var extractTimes = new List<double>();
        var tabsExtracted = new List<TabInfo>();
        string foundContainerLabel = "";
        string foundAutoId = "";

        for (int i = 0; i < iterations; i++)
        {
            var swBind = Stopwatch.StartNew();
            AutomationElement? windowElement = null;
            try
            {
                windowElement = automation.FromHandle(target.Hwnd);
            }
            catch { }
            swBind.Stop();
            bindTimes.Add(swBind.Elapsed.TotalMilliseconds);

            if (windowElement == null) continue;

            var cf = automation.ConditionFactory;
            AutomationElement? container = null;
            string containerLabel = "";

            var swContainer = Stopwatch.StartNew();
            if (target.ClassName == "MozillaWindowClass")
            {
                container = windowElement.FindFirstDescendant(cf.ByClassName("tabs normal"));
                containerLabel = "tabs normal";
                if (container == null)
                {
                    container = windowElement.FindFirstDescendant(cf.ByClassName("tabbrowser-tabs"));
                    containerLabel = "tabbrowser-tabs";
                }
            }
            else if (target.ClassName == "Chrome_WidgetWin_1")
            {
                container = windowElement.FindFirstDescendant(cf.ByClassName("tabs-container"));
                containerLabel = "tabs-container";

                if (container == null)
                {
                    var tabControls = windowElement.FindAllDescendants(cf.ByControlType(ControlType.Tab));
                    foreach (var tc in tabControls)
                    {
                        string cls = tc.Properties.ClassName.ValueOrDefault ?? "";
                        if (cls.Contains("tabs-container"))
                        {
                            container = tc;
                            containerLabel = $"Tab (Class: '{cls}')";
                            break;
                        }
                    }
                }
            }
            swContainer.Stop();
            containerTimes.Add(swContainer.Elapsed.TotalMilliseconds);

            if (container == null)
            {
                if (i == 0)
                {
                    Console.WriteLine("--------------------------------------------------------------------------");
                    Console.WriteLine($" TARGET: [{targetType}] 0x{target.Hwnd.ToInt64():X8} (PID {target.Pid})");
                    Console.WriteLine($" Title : '{target.Title}'");
                    Console.WriteLine($" Class : '{target.ClassName}'");
                    Console.WriteLine("--------------------------------------------------------------------------");
                    Console.WriteLine($"[BIND] Bound AutomationElement in {swBind.Elapsed.TotalMilliseconds:F2} ms");
                    Console.WriteLine($"[CONTAINER] Tab container not found in {swContainer.Elapsed.TotalMilliseconds:F2} ms (Non-editor/utility window or different structure)\n");
                }
                return;
            }

            foundContainerLabel = containerLabel;
            foundAutoId = container.Properties.AutomationId.ValueOrDefault ?? "";

            // Tab Extraction
            var swExtract = Stopwatch.StartNew();
            var currentTabs = new List<TabInfo>();

            try
            {
                var tabElements = (target.ClassName == "MozillaWindowClass" && containerLabel == "tabs normal")
                    ? container.FindAllChildren(cf.ByControlType(ControlType.ListItem))
                    : container.FindAllChildren(cf.ByControlType(ControlType.TabItem));

                if (tabElements.Length == 0)
                {
                    tabElements = container.FindAllChildren();
                }

                int idx = 1;
                foreach (var tab in tabElements)
                {
                    string name = tab.Properties.Name.ValueOrDefault ?? "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string cls = tab.Properties.ClassName.ValueOrDefault ?? "";
                    bool isSelected = false;
                    try
                    {
                        var selPattern = tab.Patterns.SelectionItem.PatternOrDefault;
                        if (selPattern != null)
                        {
                            isSelected = selPattern.IsSelected.Value;
                        }
                        else
                        {
                            isSelected = cls.Contains("active") || cls.Contains("selected");
                        }
                    }
                    catch
                    {
                        isSelected = cls.Contains("active") || cls.Contains("selected");
                    }

                    currentTabs.Add(new TabInfo(idx++, name, isSelected));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXTRACTION ERROR] {ex.GetType().Name}: {ex.Message}");
            }
            swExtract.Stop();
            extractTimes.Add(swExtract.Elapsed.TotalMilliseconds);

            if (i == 0) tabsExtracted = currentTabs;
        }

        Console.WriteLine("--------------------------------------------------------------------------");
        Console.WriteLine($" TARGET: [{targetType}] 0x{target.Hwnd.ToInt64():X8} (PID {target.Pid})");
        Console.WriteLine($" Title : '{target.Title}'");
        Console.WriteLine($" Class : '{target.ClassName}'");
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.WriteLine($"[BIND] Bound AutomationElement: Median {GetMedian(bindTimes):F2} ms (Min: {bindTimes.Min():F2} ms, P95: {GetP95(bindTimes):F2} ms, Max: {bindTimes.Max():F2} ms)");
        Console.WriteLine($"[CONTAINER] Found container '{foundContainerLabel}' (AutoId: '{foundAutoId}'): Median {GetMedian(containerTimes):F2} ms (Min: {containerTimes.Min():F2} ms, P95: {GetP95(containerTimes):F2} ms, Max: {containerTimes.Max():F2} ms)");
        
        double medianExtract = GetMedian(extractTimes);
        double perTabUs = tabsExtracted.Count > 0 ? (medianExtract * 1000.0) / tabsExtracted.Count : 0.0;
        Console.WriteLine($"[EXTRACTION] Extracted {tabsExtracted.Count} named tabs: Median {medianExtract:F2} ms ({perTabUs:F1} µs/tab) (Min: {extractTimes.Min():F2} ms, P95: {GetP95(extractTimes):F2} ms, Max: {extractTimes.Max():F2} ms)\n");

        if (tabsExtracted.Count > 0)
        {
            Console.WriteLine("  Extracted Tabs Table:");
            Console.WriteLine("  | Index | Active | Title |");
            Console.WriteLine("  |-------|--------|-------|");
            foreach (var t in tabsExtracted)
            {
                string activeMarker = t.IsActive ? "  **[ACTIVE]**  " : "            ";
                Console.WriteLine($"  | {t.Index,5} | {activeMarker} | {t.Title} |");
            }
        }
        Console.WriteLine();
    }

    private static double GetMedian(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 != 0 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static double GetP95(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int idx = (int)Math.Ceiling(0.95 * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }

    private static void DumpTree(AutomationElement el, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        try
        {
            var children = el.FindAllChildren();
            foreach (var c in children)
            {
                string name = c.Properties.Name.ValueOrDefault ?? "";
                string autoId = c.Properties.AutomationId.ValueOrDefault ?? "";
                string cls = c.Properties.ClassName.ValueOrDefault ?? "";
                var type = c.Properties.ControlType.ValueOrDefault;
                string indent = new string(' ', (depth + 1) * 2);
                Console.WriteLine($"{indent}[D{depth+1}][{type}] AutoId='{autoId}' Class='{cls}' Name='{name}'");
                DumpTree(c, depth + 1, maxDepth);
            }
        }
        catch { }
    }
}
