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

internal static class AntigravityProfileRunner
{
    public record AntigravityTelemetryStep(
        int StepNumber,
        string ZoneTag,
        string Description,
        string Stimulus,
        string ControlType,
        string ElementName,
        string AutomationId,
        string ClassName,
        Rectangle BoundingBox,
        List<(int Depth, string Type, string Name, string AutoId, string Cls)> AncestorChain,
        DesktopSemanticZone AdceZone,
        WindowPaneLocation AdcePane,
        string? AdceView,
        string? AdceSection,
        ImmutableArray<string> SemanticPath,
        string ScreenshotFile
    );

    public static async Task RunAntigravityEmpiricalStudyAsync(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Research Spike: Antigravity IDE (Monaco/Electron) Empirical Study  ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        using var automation = new UIA3Automation();
        var cf = automation.ConditionFactory;
        using var engine = new UiaExtractionEngine();

        // 1. Locate Antigravity IDE Target Window
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

            if (className == "Chrome_WidgetWin_1" &&
                (title.Contains("Antigravity", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("active-desktop-context-engine", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(title) &&
                title != "Default IME" && title != "MSCTFIME UI" && title != "DWM Notification Window")
            {
                candidates.Add(new TargetWindow(hWnd, title, className, pid));
            }
            return true;
        }, IntPtr.Zero);

        if (candidates.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] No active Antigravity IDE (Chrome_WidgetWin_1) window found on desktop.");
            Console.ResetColor();
            return;
        }

        // Prefer the active repository workspace window, then non-minimized
        var target = candidates.FirstOrDefault(c => c.Title.Contains("active-desktop-context-engine", StringComparison.OrdinalIgnoreCase) && !SpikeNativeMethods.IsIconic(c.Hwnd))
                     ?? candidates.FirstOrDefault(c => !SpikeNativeMethods.IsIconic(c.Hwnd))
                     ?? candidates[0];

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[TARGET ACQUIRED] Antigravity IDE Window HWND: 0x{target.Hwnd.ToInt64():X8} (PID {target.Pid})");
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
            Console.WriteLine("[ERROR] Failed to bind AutomationElement from Antigravity IDE HWND.");
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
        string mediaDir = Path.Combine(repoRoot, "docs", "media", "antigravity_telemetry");
        Directory.CreateDirectory(mediaDir);

        var steps = new List<AntigravityTelemetryStep>();

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

            // Attempt 1: PrintWindow with PW_RENDERFULLCONTENT (0x00000002)
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

                            using var pen = new Pen(Color.Red, 3);
                            g.DrawRectangle(pen, localX, localY, localW, localH);

                            using var brush = new SolidBrush(Color.FromArgb(220, 220, 0, 0));
                            using var font = new Font("Segoe UI", 10, FontStyle.Bold);
                            var badgeSize = g.MeasureString(badge, font);
                            int badgeY = Math.Max(0, localY - (int)badgeSize.Height - 4);
                            g.FillRectangle(brush, localX, badgeY, (int)badgeSize.Width + 8, (int)badgeSize.Height + 4);
                            g.DrawString(badge, font, Brushes.White, localX + 4, badgeY + 2);
                        }
                    }

                    string fullPath = Path.Combine(mediaDir, filename);
                    bmp.Save(fullPath, ImageFormat.Png);
                    long fileLen = new FileInfo(fullPath).Length;
                    Console.WriteLine($"  [SCREENSHOT SAVED] {filename} ({fileLen / 1024} KB)");
                }
                else
                {
                    Console.WriteLine($"  [WARN] Failed to capture window bitmap for {filename}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [WARN] Capture failed: {ex.Message}");
            }
        }

        async Task<AntigravityTelemetryStep> InspectControlStopAsync(
            int stepNum,
            string zoneTag,
            string desc,
            string stimulus,
            AutomationElement? targetElement,
            string shotFile)
        {
            SpikeNativeMethods.ForceForegroundWindow(target.Hwnd);
            if (targetElement != null)
            {
                try { targetElement.Focus(); } catch { }
            }
            await Task.Delay(350);

            var focused = targetElement ?? automation.FocusedElement();
            string cType = focused?.Properties.ControlType.ValueOrDefault.ToString() ?? "Unknown";
            string name = focused?.Properties.Name.ValueOrDefault ?? string.Empty;
            string autoId = focused?.Properties.AutomationId.ValueOrDefault ?? string.Empty;
            string cls = focused?.Properties.ClassName.ValueOrDefault ?? string.Empty;
            var r = focused?.Properties.BoundingRectangle.ValueOrDefault ?? Rectangle.Empty;
            var sysRect = r.IsEmpty ? Rectangle.Empty : new Rectangle((int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height);

            // Collect ancestor chain
            var ancestors = new List<(int Depth, string Type, string Name, string AutoId, string Cls)>();
            if (focused != null)
            {
                try
                {
                    var walker = automation.TreeWalkerFactory.GetControlViewWalker();
                    var curr = walker.GetParent(focused);
                    int d = 0;
                    while (curr != null && d < 12)
                    {
                        string pType = curr.Properties.ControlType.ValueOrDefault.ToString();
                        string pName = curr.Properties.Name.ValueOrDefault ?? "";
                        string pId = curr.Properties.AutomationId.ValueOrDefault ?? "";
                        string pCls = curr.Properties.ClassName.ValueOrDefault ?? "";
                        ancestors.Add((d, pType, pName, pId, pCls));

                        if (pType == "Window") break;
                        curr = walker.GetParent(curr);
                        d++;
                    }
                }
                catch { }
            }

            // Run ADCE extraction engine
            FocusedControlInfo f;
            if (focused != null)
            {
                var rBounds = windowElement.BoundingRectangle;
                var bRect = rBounds.IsEmpty ? BoundingRectangle.Empty :
                    new BoundingRectangle((int)rBounds.X, (int)rBounds.Y, (int)rBounds.Width, (int)rBounds.Height);
                f = engine.ExtractControlInfo(windowElement, focused, DesktopAppArchetype.ChromiumElectron, bRect);
            }
            else
            {
                var snapshot = await engine.ExtractSnapshotAsync(target.Hwnd);
                f = snapshot.Focus;
            }

            CaptureScreenshot(windowElement, sysRect, shotFile, $"{stepNum}: {zoneTag}");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[STEP {stepNum:D2}: {zoneTag}] {desc}");
            Console.ResetColor();
            Console.WriteLine($"  Physical Focus : [{cType}] Name='{name}' AutoId='{autoId}' Cls='{cls}'");
            Console.WriteLine($"  Bounds         : [X={sysRect.X}, Y={sysRect.Y}, W={sysRect.Width}, H={sysRect.Height}]");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"  ADCE Projected : Zone={f.SemanticZone}, Pane={f.PaneLocation}, View='{f.ActiveView}', Section='{f.SectionName}', Path=[{string.Join(", ", f.SemanticPath)}]");
            Console.ResetColor();
            Console.WriteLine($"  Ancestors      : {string.Join(" > ", ancestors.Select(a => $"{a.Type}(Id='{a.AutoId}', Cls='{a.Cls}')"))}");
            Console.WriteLine($"  Screenshot     : {shotFile}\n");

            return new AntigravityTelemetryStep(
                stepNum,
                zoneTag,
                desc,
                stimulus,
                cType,
                name,
                autoId,
                cls,
                sysRect,
                ancestors,
                f.SemanticZone,
                f.PaneLocation,
                f.ActiveView,
                f.SectionName,
                f.SemanticPath,
                shotFile
            );
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n--- DISCOVERING ANTIGRAVITY WORKBENCH PARTS & CONTROLS ---");
        Console.ResetColor();

        // Discover workbench parts
        var allDesc = windowElement.FindAllDescendants();
        Console.WriteLine($"Discovered {allDesc.Length} total descendants in Antigravity IDE.");

        string SafeId(AutomationElement e) { try { return e.Properties.AutomationId.ValueOrDefault ?? string.Empty; } catch { return string.Empty; } }
        string SafeClass(AutomationElement e) { try { return e.Properties.ClassName.ValueOrDefault ?? string.Empty; } catch { return string.Empty; } }
        string SafeName(AutomationElement e) { try { return e.Properties.Name.ValueOrDefault ?? string.Empty; } catch { return string.Empty; } }
        ControlType SafeType(AutomationElement e) { try { return e.Properties.ControlType.ValueOrDefault; } catch { return ControlType.Custom; } }

        Console.WriteLine("\n--- DIAGNOSTIC PROBE: MATCHING ELEMENTS ---");
        var matched = allDesc.Where(d =>
        {
            string s = $"{SafeId(d)} {SafeClass(d)} {SafeName(d)}".ToLowerInvariant();
            return s.Contains("sidebar") || s.Contains("explorer") || s.Contains("activity") ||
                   s.Contains("auxiliary") || s.Contains("chat") || s.Contains("artifact") ||
                   s.Contains("terminal") || s.Contains("statusbar") || s.Contains("tab");
        }).Take(30).ToList();

        foreach (var m in matched)
        {
            var b = m.BoundingRectangle;
            Console.WriteLine($"  [{SafeType(m)}] Id='{SafeId(m)}' Cls='{SafeClass(m)}' Name='{SafeName(m)}' | [X={b.X}, Y={b.Y}, W={b.Width}, H={b.Height}]");
        }
        Console.WriteLine("-------------------------------------------\n");

        // 1. Activity Bar (Buttons: Explorer, Search, SCM, Extensions, Antigravity)
        var actItem = allDesc.FirstOrDefault(d => SafeName(d).Contains("Explorer (Ctrl+Shift+E)")) ??
                      allDesc.FirstOrDefault(d => SafeClass(d).Contains("codicon-explorer-view-icon")) ??
                      allDesc.FirstOrDefault(d => SafeClass(d).Contains("action-item icon"));

        steps.Add(await InspectControlStopAsync(1, "ActivityBar", "Activity Bar Action Launcher Button", "Focus Activity Bar Button", actItem, "step_01_activity_bar.png"));

        // 2. Primary Sidebar (File Explorer Tree)
        SpikeNativeMethods.SendKey(SpikeNativeMethods.VK_E, ctrl: true, shift: true);
        await Task.Delay(600);
        var postExplorerDesc = windowElement.FindAllDescendants();

        var sideTreeItem = postExplorerDesc.FirstOrDefault(d => SafeType(d) == ControlType.TreeItem) ??
                           postExplorerDesc.FirstOrDefault(d => SafeClass(d).Contains("explorer-item") || SafeClass(d).Contains("monaco-list-row")) ??
                           postExplorerDesc.FirstOrDefault(d => SafeName(d).Contains("README") && SafeType(d) != ControlType.TabItem) ??
                           actItem;

        steps.Add(await InspectControlStopAsync(2, "SidebarExplorer", "Primary Sidebar File Explorer Tree Item", "Focus File Explorer Item (Ctrl+Shift+E)", sideTreeItem, "step_02_sidebar_explorer.png"));

        // 3. Editor TabStrip (TabItem)
        var allTabs = postExplorerDesc.Where(d => SafeType(d) == ControlType.TabItem).ToList();
        var tabItem = allTabs.FirstOrDefault(d => SafeName(d).Contains("README.md", StringComparison.OrdinalIgnoreCase)) ??
                      allTabs.FirstOrDefault(d => SafeName(d).EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                                  SafeName(d).EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                                                  !SafeName(d).Contains("Implementation Plan", StringComparison.OrdinalIgnoreCase)) ??
                      allTabs.FirstOrDefault();

        if (tabItem != null)
        {
            try
            {
                if (tabItem.Patterns.SelectionItem.IsSupported)
                    tabItem.Patterns.SelectionItem.Pattern.Select();
                else if (tabItem.Patterns.Invoke.IsSupported)
                    tabItem.Patterns.Invoke.Pattern.Invoke();
                else
                    tabItem.Focus();
            }
            catch { }
            await Task.Delay(500);
        }

        steps.Add(await InspectControlStopAsync(3, "EditorTabStrip", "Editor Area Active Document Tab", "Focus Active Editor TabItem", tabItem, "step_03_editor_tabstrip.png"));

        // 4. Code Editor Viewport (Monaco Editor Buffer)
        var codeDesc = windowElement.FindAllDescendants();
        var monacoEdit = codeDesc.FirstOrDefault(d => (SafeType(d) == ControlType.Edit || SafeType(d) == ControlType.Document) &&
                                                      SafeClass(d).Contains("native-edit-context") &&
                                                      !SafeClass(d).Contains("diff-editor")) ??
                         codeDesc.FirstOrDefault(d => SafeClass(d).Contains("native-edit-context")) ??
                         codeDesc.FirstOrDefault(d => SafeClass(d).Contains("monaco-editor"));

        steps.Add(await InspectControlStopAsync(4, "EditorBuffer", "Monaco Code Editor Document Buffer", "Focus Monaco Editor Text Area", monacoEdit, "step_04_editor_buffer.png"));

        // 5. Breadcrumbs / Navigation Path
        var breadcrumb = codeDesc.FirstOrDefault(d => SafeClass(d).Contains("monaco-breadcrumbs") ||
                                                      SafeClass(d).Contains("breadcrumb")) ??
                         codeDesc.FirstOrDefault(d => SafeClass(d).Contains("editor-breadcrumb"));
        if (breadcrumb == null)
        {
            SpikeNativeMethods.SendKey(SpikeNativeMethods.VK_OEM_1, ctrl: true, shift: true);
            await Task.Delay(500);
            var bcDesc = windowElement.FindAllDescendants();
            breadcrumb = bcDesc.FirstOrDefault(d => SafeClass(d).Contains("monaco-breadcrumbs") ||
                                                    SafeClass(d).Contains("breadcrumb")) ??
                         bcDesc.FirstOrDefault(d => SafeType(d) == ControlType.Group && SafeName(d).Contains(">"));
        }

        var targetBreadcrumb = breadcrumb;
        if (breadcrumb != null)
        {
            try
            {
                var children = breadcrumb.FindAllChildren();
                if (children.Length > 0)
                {
                    targetBreadcrumb = children.LastOrDefault(c => !string.IsNullOrWhiteSpace(SafeName(c))) ?? children[0];
                }
            }
            catch { }
        }

        steps.Add(await InspectControlStopAsync(5, "Breadcrumbs", "Editor Path Breadcrumb Navigation", "Focus Editor Breadcrumb Item", targetBreadcrumb, "step_05_breadcrumbs.png"));

        // 6. Auxiliary Sidebar (Antigravity Agent Panel Action)
        var agentBtn = postExplorerDesc.FirstOrDefault(d => SafeName(d).Contains("Toggle Agent") || SafeClass(d).Contains("codicon-layout-sidebar-right")) ??
                       allDesc.FirstOrDefault(d => SafeName(d).Contains("Toggle Agent"));

        steps.Add(await InspectControlStopAsync(6, "AgentPanelToggle", "Antigravity Agent Panel Action Toggle", "Focus Toggle Agent Button (Ctrl+Alt+B)", agentBtn, "step_06_agent_panel_toggle.png"));

        // 7. Auxiliary Bar Header (Artifact Viewer / Conversation Header)
        var artifactTab = postExplorerDesc.FirstOrDefault(d => SafeClass(d).Contains("codicon-jetski-artifacts")) ??
                          postExplorerDesc.FirstOrDefault(d => SafeName(d).Contains("Implementation Plan") && SafeClass(d).Contains("tab-label")) ??
                          tabItem;

        steps.Add(await InspectControlStopAsync(7, "ArtifactViewerHeader", "Antigravity Artifact Viewer Header", "Focus Artifact Viewer Panel Header", artifactTab, "step_07_artifact_header.png"));

        // 8. Terminal / Bottom Panel
        var termDesc = windowElement.FindAllDescendants();
        var termTab = termDesc.FirstOrDefault(d => SafeClass(d).Contains("single-terminal-tab") ||
                                                   SafeClass(d).Contains("xterm") ||
                                                   (SafeType(d) == ControlType.TabItem && SafeName(d).Contains("Terminal", StringComparison.OrdinalIgnoreCase)) ||
                                                   SafeName(d).Equals("Terminal", StringComparison.OrdinalIgnoreCase) ||
                                                   SafeId(d).Contains("terminal", StringComparison.OrdinalIgnoreCase));
        if (termTab == null)
        {
            SpikeNativeMethods.SendKey(SpikeNativeMethods.VK_J, ctrl: true);
            await Task.Delay(800);
            termDesc = windowElement.FindAllDescendants();
            termTab = termDesc.FirstOrDefault(d => SafeClass(d).Contains("single-terminal-tab") ||
                                                   SafeClass(d).Contains("xterm") ||
                                                   (SafeType(d) == ControlType.TabItem && SafeName(d).Contains("Terminal", StringComparison.OrdinalIgnoreCase)) ||
                                                   SafeName(d).Equals("Terminal", StringComparison.OrdinalIgnoreCase) ||
                                                   SafeId(d).Contains("terminal", StringComparison.OrdinalIgnoreCase));
        }

        if (termTab != null)
        {
            try
            {
                if (termTab.Patterns.SelectionItem.IsSupported)
                    termTab.Patterns.SelectionItem.Pattern.Select();
                else if (termTab.Patterns.Invoke.IsSupported)
                    termTab.Patterns.Invoke.Pattern.Invoke();
                else
                    termTab.Focus();
            }
            catch { }
            await Task.Delay(400);
        }

        steps.Add(await InspectControlStopAsync(8, "IntegratedTerminal", "Bottom Panel Integrated Terminal / Console", "Focus Terminal Viewport (Ctrl+J)", termTab, "step_08_terminal_panel.png"));

        // 9. Status Bar (Bottom Status Indicators)
        var statusItem = postExplorerDesc.FirstOrDefault(d => SafeClass(d).Contains("statusbar-item-label") || SafeId(d).Contains("status.host")) ??
                         allDesc.FirstOrDefault(d => SafeClass(d).Contains("statusbar-item-label") || SafeId(d).Contains("status.host"));

        steps.Add(await InspectControlStopAsync(9, "StatusBar", "Workbench Status Bar & Telemetry Indicator", "Focus Status Bar Indicator", statusItem, "step_09_statusbar.png"));

        // 8. Save empirical telemetry JSON alongside screenshots
        string telemetryJsonPath = Path.Combine(mediaDir, "telemetry.json");
        string json = JsonSerializer.Serialize(steps, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(telemetryJsonPath, json, Encoding.UTF8);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n==========================================================================");
        Console.WriteLine("  [SUCCESS] Empirical Antigravity IDE Study Complete!                     ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
        Console.WriteLine($"  Telemetry JSON Written  : {telemetryJsonPath}");
        Console.WriteLine($"  Screenshots Stored in   : {mediaDir}");
        Console.WriteLine($"  Captured Stops Count    : {steps.Count}");
        Console.WriteLine($"  Curated Hierarchy Spec  : docs/app_hierarchies/02_antigravity_ide.md\n");
    }
}
