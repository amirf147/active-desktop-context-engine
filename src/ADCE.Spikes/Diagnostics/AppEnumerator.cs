// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ADCE.Core.Enums;
using ADCE.Extraction.Classifiers;
using ADCE.Spikes.Native;

namespace ADCE.Spikes.Diagnostics;

internal static class AppEnumerator
{
    private record WindowAppEntry(
        IntPtr Hwnd,
        uint Pid,
        string ProcessName,
        string Title,
        string ClassName,
        DesktopAppArchetype Archetype,
        bool IsForeground,
        bool IsVisible,
        bool IsMinimized
    );

    public static void RunListOpenApps()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Desktop Discovery: Currently Open Top-Level Applications          ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        var classifier = ArchetypeClassifier.Default;
        var fg = SpikeNativeMethods.GetForegroundWindow();

        var list = new List<WindowAppEntry>();

        SpikeNativeMethods.EnumDesktopWindows(hDesktop != IntPtr.Zero ? hDesktop : IntPtr.Zero, (hWnd, lParam) =>
        {
            var sbTitle = new StringBuilder(512);
            SpikeNativeMethods.GetWindowText(hWnd, sbTitle, 512);
            var sbClass = new StringBuilder(256);
            SpikeNativeMethods.GetClassName(hWnd, sbClass, 256);
            SpikeNativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            string title = sbTitle.ToString();
            string className = sbClass.ToString();

            if (string.IsNullOrWhiteSpace(title) && !className.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd" ||
                className == "Windows.UI.Core.CoreWindow" || className == "Internet Explorer_Hidden" ||
                className == "EdgeUiInputTopWndClass" || className == "IME" || className == "MSCTFIME UI" ||
                className == "Xaml_WindowedPopupClass" || className == "OleDdeWndClass" ||
                className == "CiceroUIWndFrame" || className == "GDI+ Hook Window Class" ||
                className.Contains("Hidden", StringComparison.OrdinalIgnoreCase) ||
                title == "Default IME" || title == "MSCTFIME UI" || title == "DWM Notification Window" ||
                title.StartsWith("GDI+ Window", StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith(".NET-Broadcast", StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("SystemResource", StringComparison.OrdinalIgnoreCase) ||
                title == "Hidden Window" || title == "CiceroUIWndFrame" || title == "PopupHost" ||
                title == "Battery Watcher" || title == "WinEventWindow")
            {
                return true;
            }

            string procName = "unknown";
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                procName = proc.ProcessName;
            }
            catch { }

            bool isVis = SpikeNativeMethods.IsWindowVisible(hWnd);
            bool isMin = SpikeNativeMethods.IsIconic(hWnd);
            bool isFg = hWnd == fg;

            var archetype = classifier.Classify(className, procName, title);

            list.Add(new WindowAppEntry(
                hWnd,
                pid,
                procName,
                string.IsNullOrWhiteSpace(title) ? "[Windows Terminal]" : title,
                className,
                archetype,
                isFg,
                isVis,
                isMin
            ));

            return true;
        }, IntPtr.Zero);

        Console.WriteLine($"Total Windows Discovered: {list.Count}\n");

        var ordered = list
            .OrderByDescending(w => w.IsForeground)
            .ThenBy(w => w.IsMinimized)
            .ThenBy(w => w.ProcessName)
            .ToList();

        foreach (var w in ordered)
        {
            var color = w.IsForeground ? ConsoleColor.Green : (w.IsMinimized ? ConsoleColor.DarkGray : ConsoleColor.White);
            Console.ForegroundColor = color;
            string status = w.IsForeground ? "[FOREGROUND]" : (w.IsMinimized ? "[MINIMIZED]" : "[VISIBLE]   ");
            Console.WriteLine($"{status} PID: {w.Pid,-6} | Proc: {w.ProcessName,-16} | Archetype: {w.Archetype,-16} | HWND: 0x{w.Hwnd.ToInt64():X8}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"           Title: \"{w.Title}\"");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"           Class: \"{w.ClassName}\"");
            Console.WriteLine();
        }
        Console.ResetColor();
    }
}
