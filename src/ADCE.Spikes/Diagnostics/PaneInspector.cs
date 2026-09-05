// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADCE.Spikes.Models;
using ADCE.Spikes.Native;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace ADCE.Spikes.Diagnostics;

internal static class PaneInspector
{
    public static void RunPaneInspectionSpike(string? filter)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Research Spike: Physical Window Panes & Hierarchy Inspection       ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        using var automation = new UIA3Automation();
        var cf = automation.ConditionFactory;

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

        var selected = string.IsNullOrWhiteSpace(filter)
            ? targets
            : targets.Where(t => t.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                 t.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                 t.Pid.ToString() == filter).ToList();

        if (selected.Count == 0)
        {
            Console.WriteLine($"[WARN] No target window matched filter '{filter}'. Available: {string.Join(", ", targets.Select(t => t.Title))}");
            return;
        }

        foreach (var target in selected)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n>>> INSPECTING WINDOW: '{target.Title}' (PID: {target.Pid}, HWND: 0x{target.Hwnd:X8}, Class: '{target.ClassName}')");
            Console.ResetColor();

            AutomationElement? window = null;
            try { window = automation.FromHandle(target.Hwnd); } catch { }
            if (window == null) continue;

            var wBounds = window.BoundingRectangle;
            Console.WriteLine($"  Window Bounds: [X={wBounds.X}, Y={wBounds.Y}, W={wBounds.Width}, H={wBounds.Height}]");

            // Check for Electron workbench parts
            string[] workbenchParts = [
                "workbench.parts.activitybar",
                "workbench.parts.sidebar",
                "workbench.parts.editor",
                "workbench.parts.auxiliarybar",
                "workbench.parts.panel",
                "workbench.parts.statusbar",
                "workbench.parts.titlebar"
            ];

            foreach (var partId in workbenchParts)
            {
                var part = window.FindFirstDescendant(cf.ByAutomationId(partId));
                if (part != null)
                {
                    var pb = part.BoundingRectangle;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  [PART] {partId} | [{part.ControlType}] Cls='{part.ClassName}' Name='{part.Name}' | [X={pb.X}, Y={pb.Y}, W={pb.Width}, H={pb.Height}]");
                    Console.ResetColor();

                    if (partId == "workbench.parts.sidebar")
                    {
                        var headers = part.FindAllDescendants(cf.ByClassName("pane-header"));
                        Console.WriteLine($"    Found {headers.Length} accordion pane headers in Sidebar:");
                        foreach (var h in headers)
                        {
                            var hb = h.BoundingRectangle;
                            Console.WriteLine($"      -> Header: '{h.Name}' | Cls='{h.ClassName}' | [X={hb.X}, Y={hb.Y}, W={hb.Width}, H={hb.Height}]");
                        }
                    }
                    else if (partId == "workbench.parts.auxiliarybar")
                    {
                        var headers = part.FindAllDescendants(cf.ByClassName("pane-header"));
                        Console.WriteLine($"    Found {headers.Length} accordion pane headers in AuxiliaryBar:");
                        foreach (var h in headers)
                        {
                            Console.WriteLine($"      -> Aux Header: '{h.Name}' | Cls='{h.ClassName}'");
                        }
                    }
                    else if (partId == "workbench.parts.activitybar")
                    {
                        var items = part.FindAllChildren();
                        Console.WriteLine($"    ActivityBar children count: {items.Length}");
                        foreach (var it in items)
                        {
                            Console.WriteLine($"      -> [{it.ControlType}] '{it.Name}' | Cls='{it.ClassName}' | AutoId='{it.AutomationId}'");
                        }
                    }
                }
            }

            // Check for Gecko / Waterfox parts
            if (target.ClassName == "MozillaWindowClass")
            {
                var navBar = window.FindFirstDescendant(cf.ByAutomationId("nav-bar"));
                if (navBar != null)
                {
                    Console.WriteLine($"  [GECKO PART] nav-bar | Bounds: {navBar.BoundingRectangle}");
                }
                var sidebarBox = window.FindFirstDescendant(cf.ByAutomationId("sidebar-box"));
                if (sidebarBox != null)
                {
                    Console.WriteLine($"  [GECKO PART] sidebar-box | Bounds: {sidebarBox.BoundingRectangle} | Name='{sidebarBox.Name}'");
                    var sideChildren = sidebarBox.FindAllChildren();
                    foreach (var sc in sideChildren)
                    {
                        Console.WriteLine($"    -> [{sc.ControlType}] '{sc.Name}' | Cls='{sc.ClassName}' | AutoId='{sc.AutomationId}'");
                    }
                }
                var tabsToolbar = window.FindFirstDescendant(cf.ByAutomationId("TabsToolbar"));
                if (tabsToolbar != null)
                {
                    Console.WriteLine($"  [GECKO PART] TabsToolbar | Bounds: {tabsToolbar.BoundingRectangle}");
                }
            }

            // Check for Windows Terminal / Cascadia parts
            if (target.ClassName.StartsWith("CASCADIA"))
            {
                var tabView = window.FindFirstDescendant(cf.ByAutomationId("TabView"));
                if (tabView != null)
                {
                    Console.WriteLine($"  [CASCADIA PART] TabView | Bounds: {tabView.BoundingRectangle}");
                }
                var termControls = window.FindAllDescendants(cf.ByClassName("TermControl"));
                Console.WriteLine($"  [CASCADIA] TermControl count: {termControls.Length}");
                foreach (var tc in termControls)
                {
                    Console.WriteLine($"    -> TermControl: '{tc.Name}' | AutoId='{tc.AutomationId}' | Bounds: {tc.BoundingRectangle}");
                }
            }
        }

        // Dump Current Focus and full ancestor hierarchy
        try
        {
            var focused = automation.FocusedElement();
            if (focused != null)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("\n--------------------------------------------------------------------------");
                Console.WriteLine(" CURRENT DESKTOP FOCUSED ELEMENT & ANCESTOR HIERARCHY");
                Console.WriteLine("--------------------------------------------------------------------------");
                Console.ResetColor();

                var fb = focused.BoundingRectangle;
                Console.WriteLine($"Focused: [{focused.ControlType}] '{focused.Name}' | AutoId='{focused.AutomationId}' | Cls='{focused.ClassName}' | Bounds=[X={fb.X}, Y={fb.Y}, W={fb.Width}, H={fb.Height}]");

                var walker = automation.TreeWalkerFactory.GetRawViewWalker();
                var curr = focused;
                int depth = 0;
                while (curr != null && depth < 10)
                {
                    var parent = walker.GetParent(curr);
                    if (parent == null) break;
                    var pb = parent.BoundingRectangle;
                    Console.WriteLine($"  ^ Parent [{depth}]: [{parent.ControlType}] '{parent.Name}' | AutoId='{parent.AutomationId}' | Cls='{parent.ClassName}' | Bounds=[X={pb.X}, Y={pb.Y}, W={pb.Width}, H={pb.Height}]");
                    curr = parent;
                    depth++;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Focus extraction error: {ex.Message}");
        }
    }
}
