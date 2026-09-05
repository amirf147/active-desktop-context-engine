// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
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

internal static class TerminalProfileRunner
{
    public record TelemetryAncestorNode(
        int Depth,
        string Type,
        string Name,
        string AutoId,
        string Cls
    );

    public record TerminalTelemetryStep(
        int StepNumber,
        string ZoneTag,
        string Description,
        string Stimulus,
        string ControlType,
        string ElementName,
        string AutomationId,
        string ClassName,
        Rectangle BoundingBox,
        List<TelemetryAncestorNode> AncestorChain,
        DesktopSemanticZone AdceZone,
        WindowPaneLocation AdcePane,
        string? AdceView,
        string? AdceSection,
        ImmutableArray<string> SemanticPath,
        string ScreenshotFile
    );

    public static async Task RunTerminalEmpiricalStudyAsync(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Research Spike: Windows Terminal (WinUI 3 / Cascadia) Study       ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        using var automation = new UIA3Automation();
        var cf = automation.ConditionFactory;
        using var engine = new UiaExtractionEngine();

        // 1. Locate Windows Terminal Target Window
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

            if (className.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase) &&
                title != "Default IME" && title != "MSCTFIME UI" && title != "DWM Notification Window")
            {
                candidates.Add(new TargetWindow(hWnd, string.IsNullOrWhiteSpace(title) ? "Windows Terminal" : title, className, pid));
            }
            return true;
        }, IntPtr.Zero);

        if (candidates.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] No active Windows Terminal (CASCADIA_HOSTING_WINDOW_CLASS) window found on desktop.");
            Console.ResetColor();
            return;
        }

        var target = candidates.FirstOrDefault(c => !SpikeNativeMethods.IsIconic(c.Hwnd)) ?? candidates[0];

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[TARGET ACQUIRED] Windows Terminal HWND: 0x{target.Hwnd.ToInt64():X8} (PID {target.Pid})");
        Console.WriteLine($"  Title: \"{target.Title}\"");
        Console.WriteLine($"  Class: \"{target.ClassName}\"");
        Console.ResetColor();

        // 2. Bring window to foreground
        SpikeNativeMethods.ForceForegroundWindow(target.Hwnd);
        await Task.Delay(800);

        AutomationElement? windowElement = null;
        try { windowElement = automation.FromHandle(target.Hwnd); } catch { }
        if (windowElement == null)
        {
            Console.WriteLine("[ERROR] Failed to bind AutomationElement from Windows Terminal HWND.");
            return;
        }

        // 3. Prepare Screenshot Output Directory
        string baseDir = AppContext.BaseDirectory;
        string cur = baseDir;
        string? repoRoot = null;
        for (int i = 0; i < 7; i++)
        {
            if (Directory.Exists(Path.Combine(cur, "docs")) && File.Exists(Path.Combine(cur, "ADCE.slnx")))
            {
                repoRoot = cur;
                break;
            }
            string? parent = Path.GetDirectoryName(cur);
            if (parent == null) break;
            cur = parent;
        }
        repoRoot ??= Directory.GetCurrentDirectory();
        string mediaDir = Path.Combine(repoRoot, "docs", "media", "terminal_telemetry");
        Directory.CreateDirectory(mediaDir);

        var steps = new List<TerminalTelemetryStep>();

        bool IsBlackOrEmpty(Bitmap bmp)
        {
            if (bmp.Width <= 0 || bmp.Height <= 0) return true;
            int stepX = Math.Max(1, bmp.Width / 10);
            int stepY = Math.Max(1, bmp.Height / 10);
            int coloredCount = 0;

            for (int x = stepX; x < bmp.Width - 1; x += stepX)
            {
                for (int y = stepY; y < bmp.Height - 1; y += stepY)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 0 && (c.R > 15 || c.G > 15 || c.B > 15))
                    {
                        coloredCount++;
                    }
                }
            }
            return coloredCount < 5;
        }

        Bitmap? CaptureWindowBitmap(IntPtr hWnd, Rectangle bounds)
        {
            int w = Math.Max(100, bounds.Width);
            int h = Math.Max(100, bounds.Height);

            // Attempt 1: PrintWindow with PW_RENDERFULLCONTENT
            try
            {
                var bmpPrint = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmpPrint))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        bool pwSuccess = SpikeNativeMethods.PrintWindow(hWnd, hdc, SpikeNativeMethods.PW_RENDERFULLCONTENT);
                        g.ReleaseHdc(hdc);
                        hdc = IntPtr.Zero;

                        if (pwSuccess && !IsBlackOrEmpty(bmpPrint))
                        {
                            Console.WriteLine("    [CAPTURE ENGINE] PrintWindow (PW_RENDERFULLCONTENT) rendered real UI pixels.");
                            return bmpPrint;
                        }
                    }
                    finally
                    {
                        if (hdc != IntPtr.Zero) g.ReleaseHdc(hdc);
                    }
                }
                bmpPrint.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [CAPTURE ENGINE] PrintWindow attempt threw: {ex.Message}");
            }

            // Attempt 2: Clamped GDI CopyFromScreen with window brought to top
            try
            {
                SpikeNativeMethods.ForceForegroundWindow(hWnd);
                Thread.Sleep(250);

                var bmpScreen = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmpScreen))
                {
                    int screenX = Math.Max(0, bounds.Left);
                    int screenY = Math.Max(0, bounds.Top);
                    int destX = screenX - bounds.Left;
                    int destY = screenY - bounds.Top;
                    int copyW = Math.Max(1, bounds.Width - destX);
                    int copyH = Math.Max(1, bounds.Height - destY);

                    g.CopyFromScreen(screenX, screenY, destX, destY, new Size(copyW, copyH), CopyPixelOperation.SourceCopy);
                }

                if (!IsBlackOrEmpty(bmpScreen))
                {
                    Console.WriteLine("    [CAPTURE ENGINE] Foreground CopyFromScreen rendered real UI pixels.");
                }
                else
                {
                    Console.WriteLine("    [CAPTURE ENGINE WARNING] Clamped CopyFromScreen returned dark/blank bitmap.");
                }

                return bmpScreen;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [WARN] Clamped CopyFromScreen fallback failed: {ex.Message}");
                return null;
            }
        }

        void CaptureScreenshot(AutomationElement rootWindow, Rectangle highlightRect, string filename, string badge)
        {
            try
            {
                SpikeNativeMethods.GetWindowRect(target.Hwnd, out SpikeNativeMethods.RECT winRect);
                var rootBounds = new Rectangle(winRect.Left, winRect.Top, winRect.Width, winRect.Height);

                using var bmp = CaptureWindowBitmap(target.Hwnd, rootBounds);
                if (bmp != null)
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        if (!highlightRect.IsEmpty)
                        {
                            int localX = highlightRect.X - rootBounds.X;
                            int localY = highlightRect.Y - rootBounds.Y;
                            int localW = Math.Max(4, highlightRect.Width);
                            int localH = Math.Max(4, highlightRect.Height);

                            using var pen = new Pen(Color.FromArgb(255, 0, 220, 255), 3); // Bright Cyan
                            g.DrawRectangle(pen, localX, localY, localW, localH);

                            using var brush = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
                            g.FillRectangle(brush, localX, Math.Max(0, localY - 24), Math.Min(260, localW), 24);

                            using var textBrush = new SolidBrush(Color.FromArgb(255, 0, 220, 255));
                            using var font = new Font("Segoe UI", 9, FontStyle.Bold);
                            g.DrawString(badge, font, textBrush, localX + 4, Math.Max(2, localY - 22));
                        }
                    }

                    string fullPath = Path.Combine(mediaDir, filename);
                    bmp.Save(fullPath, ImageFormat.Png);
                    Console.WriteLine($"    [SCREENSHOT SAVED] {filename} ({bmp.Width}x{bmp.Height})");
                }
                else
                {
                    Console.WriteLine($"    [SCREENSHOT SKIPPED] Bitmap null for {filename}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [SCREENSHOT ERROR] {ex.Message}");
            }
        }

        async Task<TerminalTelemetryStep> InspectControlStopAsync(
            int stepNum,
            string zoneTag,
            string description,
            string stimulus,
            AutomationElement? element,
            string screenshotFile)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\n>>> STOP {stepNum}: [{zoneTag}] - {description}");
            Console.ResetColor();

            if (element == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [WARNING] Target element for {zoneTag} was not found.");
                Console.ResetColor();
                return new TerminalTelemetryStep(
                    stepNum, zoneTag, description, stimulus, "Unknown", "Missing", "", "",
                    Rectangle.Empty, new List<TelemetryAncestorNode>(),
                    DesktopSemanticZone.Unknown, WindowPaneLocation.Unknown, null, null,
                    ImmutableArray<string>.Empty, screenshotFile);
            }

            string cType = SafeType(element).ToString();
            string name = SafeName(element);
            string autoId = SafeId(element);
            string cls = SafeClass(element);
            var rect = SafeBounds(element);

            Console.WriteLine($"  Stimulus       : {stimulus}");
            Console.WriteLine($"  UIA Control    : [{cType}] \"{name}\"");
            Console.WriteLine($"  AutomationId   : \"{autoId}\"");
            Console.WriteLine($"  ClassName      : \"{cls}\"");
            Console.WriteLine($"  Bounds         : [X={rect.X}, Y={rect.Y}, W={rect.Width}, H={rect.Height}]");

            // Harvest live ancestor chain using raw walker
            var rawWalker = automation.TreeWalkerFactory.GetRawViewWalker();
            var curr = element;
            int depth = 0;
            var ancestors = new List<TelemetryAncestorNode>();
            while (curr != null && depth < 8)
            {
                try
                {
                    var p = rawWalker.GetParent(curr);
                    if (p == null) break;
                    ancestors.Add(new TelemetryAncestorNode(depth, SafeType(p).ToString(), SafeName(p), SafeId(p), SafeClass(p)));
                    curr = p;
                    depth++;
                }
                catch { break; }
            }

            Console.WriteLine("  Ancestor Chain :");
            foreach (var a in ancestors)
            {
                Console.WriteLine($"    ^ [{a.Depth}] [{a.Type}] \"{a.Name}\" | AutoId=\"{a.AutoId}\" | Cls=\"{a.Cls}\"");
            }

            // Extract ADCE domain semantics via production engine
            var rBounds = windowElement.BoundingRectangle;
            var bRect = rBounds.IsEmpty ? BoundingRectangle.Empty :
                new BoundingRectangle((int)rBounds.X, (int)rBounds.Y, (int)rBounds.Width, (int)rBounds.Height);
            var controlInfo = engine.ExtractControlInfo(windowElement, element, DesktopAppArchetype.WinUI3Xaml, bRect);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ADCE Zone      : {controlInfo.SemanticZone}");
            Console.WriteLine($"  ADCE Pane      : {controlInfo.PaneLocation}");
            Console.WriteLine($"  ADCE View      : {controlInfo.ActiveView ?? "(null)"}");
            Console.WriteLine($"  ADCE Section   : {controlInfo.SectionName ?? "(null)"}");
            Console.WriteLine($"  Semantic Path  : [{string.Join(" > ", controlInfo.SemanticPath)}]");
            Console.ResetColor();

            // Capture highlighted screenshot
            CaptureScreenshot(windowElement, rect, screenshotFile, $"{zoneTag}: {name}");

            return new TerminalTelemetryStep(
                stepNum,
                zoneTag,
                description,
                stimulus,
                cType,
                name,
                autoId,
                cls,
                rect,
                ancestors,
                controlInfo.SemanticZone,
                controlInfo.PaneLocation,
                controlInfo.ActiveView,
                controlInfo.SectionName,
                controlInfo.SemanticPath,
                screenshotFile
            );
        }

        // 4. Discover Window Descendants
        Console.WriteLine("\n[ANALYSIS] Discovering Windows Terminal structural parts...");
        var allDesc = windowElement.FindAllDescendants();
        Console.WriteLine($"  Total Descendants Cached: {allDesc.Length}");

        // Stop 1: Terminal Tab Strip (TabItem / TabListView)
        var tabItem = allDesc.FirstOrDefault(d => SafeType(d) == ControlType.TabItem) ??
                      allDesc.FirstOrDefault(d => SafeId(d) == "TabListView" || SafeClass(d) == "ListViewItem");

        if (tabItem != null)
        {
            try { tabItem.Focus(); } catch { }
            await Task.Delay(300);
        }
        steps.Add(await InspectControlStopAsync(1, "TabBar", "Terminal Tab Strip & Open Tabs", "Focus Tab Item", tabItem, "step_01_tab_strip.png"));

        // Stop 2: Active Console Viewport (TermControl)
        var termControl = allDesc.FirstOrDefault(d => SafeClass(d) == "TermControl") ??
                          allDesc.FirstOrDefault(d => SafeName(d).Contains("PowerShell", StringComparison.OrdinalIgnoreCase) ||
                                                     SafeName(d).Contains("Command Prompt", StringComparison.OrdinalIgnoreCase) ||
                                                     SafeName(d).Contains("WSL", StringComparison.OrdinalIgnoreCase));

        if (termControl != null)
        {
            try { termControl.Focus(); } catch { }
            await Task.Delay(300);
        }
        steps.Add(await InspectControlStopAsync(2, "Terminal", "Active Shell Console Buffer Viewport", "Focus Terminal Viewport", termControl, "step_02_console_viewport.png"));

        // Stop 3: New Tab Button (NewTabButton / SplitButton)
        var newTabButton = allDesc.FirstOrDefault(d => SafeId(d) == "NewTabButton" || SafeId(d) == "AddButton") ??
                           allDesc.FirstOrDefault(d => SafeType(d) == ControlType.SplitButton || SafeName(d).Equals("New Tab", StringComparison.OrdinalIgnoreCase));

        steps.Add(await InspectControlStopAsync(3, "NewTabButton", "New Tab Split Launcher & Profile Dropdown", "Focus New Tab Button", newTabButton, "step_03_new_tab_button.png"));

        // Ensure an active live tab is focused so keystrokes are received
        try
        {
            var liveTab = allDesc.FirstOrDefault(d => SafeType(d) == ControlType.TabItem && SafeName(d).Contains("PowerShell", StringComparison.OrdinalIgnoreCase));
            if (liveTab != null)
            {
                Console.WriteLine($"[TAB SWITCH] Activating live tab '{SafeName(liveTab)}' via SelectionItem pattern...");
                if (liveTab.Patterns.SelectionItem.IsSupported)
                {
                    liveTab.Patterns.SelectionItem.Pattern.Select();
                    Console.WriteLine("  SelectionItem.Select() succeeded.");
                }
                else if (liveTab.Patterns.Invoke.IsSupported)
                {
                    liveTab.Patterns.Invoke.Pattern.Invoke();
                    Console.WriteLine("  Invoke.Invoke() succeeded.");
                }
                await Task.Delay(800);
            }
            else if (newTabButton != null && newTabButton.Patterns.Invoke.IsSupported)
            {
                Console.WriteLine("[TAB CREATE] Invoking NewTabButton via Invoke pattern...");
                newTabButton.Patterns.Invoke.Pattern.Invoke();
                await Task.Delay(1200);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TAB NOTICE] Tab switch threw: {ex.Message}");
        }

        // Re-focus active terminal
        var currentTerm = windowElement.FindFirstDescendant(cf.ByClassName("TermControl"));
        if (currentTerm != null)
        {
            try { currentTerm.Focus(); } catch { }
            await Task.Delay(300);
        }

        // Stop 4: Command Palette Overlay (Ctrl+Shift+P)
        Console.WriteLine("\n[STIMULUS] Opening Command Palette via Ctrl+Shift+P...");
        SpikeNativeMethods.ForceForegroundWindow(target.Hwnd);
        await Task.Delay(300);
        SpikeNativeMethods.SendKey(SpikeNativeMethods.VK_P, ctrl: true, shift: true, holdMs: 100);
        await Task.Delay(1200);

        var paletteDesc = windowElement.FindAllDescendants();
        Console.WriteLine($"  Descendants count after Ctrl+Shift+P: {paletteDesc.Length}");
        var paletteElement = paletteDesc.FirstOrDefault(d => SafeId(d).Contains("CommandPalette", StringComparison.OrdinalIgnoreCase) ||
                                                            SafeClass(d).Contains("CommandPalette", StringComparison.OrdinalIgnoreCase) ||
                                                            SafeId(d).Contains("SearchBox", StringComparison.OrdinalIgnoreCase) ||
                                                            SafeId(d).Contains("FilteredCommandList", StringComparison.OrdinalIgnoreCase) ||
                                                            SafeName(d).Contains("Command Palette", StringComparison.OrdinalIgnoreCase) ||
                                                            SafeClass(d).Contains("Palette", StringComparison.OrdinalIgnoreCase));

        if (paletteElement == null)
        {
            Console.WriteLine("  Palette not found directly. Checking desktop children for PID " + target.Pid);
            var desktopWindows = automation.GetDesktop().FindAllChildren(cf.ByProcessId((int)target.Pid));
            Console.WriteLine($"  Desktop children count for PID {target.Pid}: {desktopWindows.Length}");
            foreach (var dw in desktopWindows)
            {
                Console.WriteLine($"    Popup HWND=0x{dw.Properties.NativeWindowHandle.ValueOrDefault:X8}, Cls='{SafeClass(dw)}', Name='{SafeName(dw)}'");
                var popDesc = dw.FindAllDescendants();
                paletteElement = popDesc.FirstOrDefault(d => SafeId(d).Contains("CommandPalette", StringComparison.OrdinalIgnoreCase) ||
                                                             SafeClass(d).Contains("CommandPalette", StringComparison.OrdinalIgnoreCase) ||
                                                             SafeId(d).Contains("SearchBox", StringComparison.OrdinalIgnoreCase) ||
                                                             SafeName(d).Contains("Command Palette", StringComparison.OrdinalIgnoreCase));
                if (paletteElement != null) break;
            }
        }

        if (paletteElement == null)
        {
            // Log any element with 'search' or 'command' or 'box' in its name/id
            var candidatesList = paletteDesc.Where(d =>
            {
                string s = $"{SafeId(d)} {SafeClass(d)} {SafeName(d)}".ToLowerInvariant();
                return s.Contains("search") || s.Contains("command") || s.Contains("palette") || s.Contains("flyout") || s.Contains("popup") || s.Contains("list");
            }).Take(10).ToList();
            Console.WriteLine($"  Sample Candidate elements ({candidatesList.Count}):");
            foreach (var c in candidatesList)
            {
                Console.WriteLine($"    -> [{SafeType(c)}] '{SafeName(c)}' | AutoId='{SafeId(c)}' | Cls='{SafeClass(c)}'");
            }
        }

        steps.Add(await InspectControlStopAsync(4, "CommandPalette", "Command Palette & Action Filter Overlay", "Press Ctrl+Shift+P", paletteElement, "step_04_command_palette.png"));

        // Dismiss command palette
        SpikeNativeMethods.SendKey(SpikeNativeMethods.VK_ESCAPE, holdMs: 50);
        await Task.Delay(400);

        // Stop 5: Settings View (Ctrl+,)
        Console.WriteLine("\n[STIMULUS] Opening Settings View via Ctrl+,...");
        SpikeNativeMethods.ForceForegroundWindow(target.Hwnd);
        await Task.Delay(300);
        SpikeNativeMethods.SendKey(SpikeNativeMethods.VK_OEM_COMMA, ctrl: true, holdMs: 80);
        await Task.Delay(1500);

        var settingsDesc = windowElement.FindAllDescendants();
        var settingsElement = settingsDesc.FirstOrDefault(d => SafeClass(d).Contains("SettingsControl", StringComparison.OrdinalIgnoreCase) ||
                                                               SafeClass(d).Contains("SettingsPage", StringComparison.OrdinalIgnoreCase) ||
                                                               SafeClass(d).Contains("NavigationView", StringComparison.OrdinalIgnoreCase) ||
                                                               SafeClass(d).Contains("BreadcrumbBar", StringComparison.OrdinalIgnoreCase) ||
                                                               (SafeId(d).Contains("Settings", StringComparison.OrdinalIgnoreCase) && SafeType(d) != ControlType.TabItem) ||
                                                               (SafeName(d).Equals("Settings", StringComparison.OrdinalIgnoreCase) && SafeType(d) != ControlType.TabItem));

        if (settingsElement == null && newTabButton != null)
        {
            Console.WriteLine("  Settings not opened via shortcut. Trying NewTabButton dropdown menu...");
            try
            {
                if (newTabButton.Patterns.ExpandCollapse.IsSupported)
                {
                    newTabButton.Patterns.ExpandCollapse.Pattern.Expand();
                    await Task.Delay(600);
                }
                var menuItems = windowElement.FindAllDescendants(cf.ByControlType(ControlType.MenuItem));
                if (menuItems.Length == 0)
                {
                    var desktopWindows = automation.GetDesktop().FindAllChildren(cf.ByProcessId((int)target.Pid));
                    foreach (var dw in desktopWindows)
                    {
                        var popItems = dw.FindAllDescendants(cf.ByControlType(ControlType.MenuItem));
                        if (popItems.Length > 0) { menuItems = popItems; break; }
                    }
                }
                Console.WriteLine($"  Discovered {menuItems.Length} MenuItems in dropdown menu.");
                foreach (var m in menuItems)
                {
                    Console.WriteLine($"    -> MenuItem: '{SafeName(m)}' | AutoId='{SafeId(m)}'");
                }
                var sm = menuItems.FirstOrDefault(m => SafeName(m).Contains("Settings", StringComparison.OrdinalIgnoreCase));
                if (sm != null && sm.Patterns.Invoke.IsSupported)
                {
                    Console.WriteLine("  Invoking Settings MenuItem via Invoke pattern...");
                    sm.Patterns.Invoke.Pattern.Invoke();
                    await Task.Delay(1500);
                    settingsDesc = windowElement.FindAllDescendants();
                    settingsElement = settingsDesc.FirstOrDefault(d => SafeClass(d).Contains("SettingsControl", StringComparison.OrdinalIgnoreCase) ||
                                                                       SafeClass(d).Contains("SettingsPage", StringComparison.OrdinalIgnoreCase) ||
                                                                       SafeClass(d).Contains("NavigationView", StringComparison.OrdinalIgnoreCase) ||
                                                                       SafeClass(d).Contains("BreadcrumbBar", StringComparison.OrdinalIgnoreCase) ||
                                                                       (SafeId(d).Contains("Settings", StringComparison.OrdinalIgnoreCase) && SafeType(d) != ControlType.TabItem) ||
                                                                       (SafeName(d).Equals("Settings", StringComparison.OrdinalIgnoreCase) && SafeType(d) != ControlType.TabItem));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Dropdown menu attempt threw: {ex.Message}");
            }
        }

        if (settingsElement == null)
        {
            // Fallback: look for newly opened Settings tab in TabView
            var allTabs = settingsDesc.Where(d => SafeType(d) == ControlType.TabItem).ToList();
            settingsElement = allTabs.FirstOrDefault(t => SafeName(t).Contains("Settings", StringComparison.OrdinalIgnoreCase));
        }

        steps.Add(await InspectControlStopAsync(5, "Settings", "Terminal Settings & Configuration Page", "Open Settings (Ctrl+,)", settingsElement, "step_05_settings_view.png"));

        // Close settings tab (Ctrl+W)
        SpikeNativeMethods.SendKey(SpikeNativeMethods.VK_W, ctrl: true, holdMs: 50);
        await Task.Delay(500);

        // Save empirical telemetry JSON
        string telemetryJsonPath = Path.Combine(mediaDir, "telemetry.json");
        string json = JsonSerializer.Serialize(steps, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(telemetryJsonPath, json, Encoding.UTF8);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n==========================================================================");
        Console.WriteLine("  [SUCCESS] Empirical Windows Terminal Study Complete!                    ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
        Console.WriteLine($"  Telemetry JSON Written  : {telemetryJsonPath}");
        Console.WriteLine($"  Screenshots Stored in   : {mediaDir}");
        Console.WriteLine($"  Captured Stops Count    : {steps.Count}");
        Console.WriteLine($"  Curated Hierarchy Spec  : docs/app_hierarchies/03_windows_terminal.md\n");
    }

    private static string SafeName(AutomationElement? el)
    {
        if (el == null) return string.Empty;
        try { return el.Properties.Name.ValueOrDefault ?? string.Empty; } catch { return string.Empty; }
    }

    private static string SafeId(AutomationElement? el)
    {
        if (el == null) return string.Empty;
        try { return el.Properties.AutomationId.ValueOrDefault ?? string.Empty; } catch { return string.Empty; }
    }

    private static string SafeClass(AutomationElement? el)
    {
        if (el == null) return string.Empty;
        try { return el.Properties.ClassName.ValueOrDefault ?? string.Empty; } catch { return string.Empty; }
    }

    private static ControlType SafeType(AutomationElement? el)
    {
        if (el == null) return ControlType.Custom;
        try { return el.Properties.ControlType.ValueOrDefault; } catch { return ControlType.Custom; }
    }

    private static Rectangle SafeBounds(AutomationElement? el)
    {
        if (el == null) return Rectangle.Empty;
        try { return el.Properties.BoundingRectangle.ValueOrDefault; } catch { return Rectangle.Empty; }
    }
}
