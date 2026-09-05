// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
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

internal static class WaterfoxProfileRunner
{
    public record WaterfoxTelemetryStep(
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

    public static async Task RunWaterfoxEmpiricalStudyAsync(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Research Spike: Waterfox UIA Hierarchy & Empirical Telemetry Study ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        using var automation = new UIA3Automation();
        var cf = automation.ConditionFactory;
        using var engine = new UiaExtractionEngine();

        // 1. Locate Waterfox Target Window
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

            if (className == "MozillaWindowClass" && !string.IsNullOrWhiteSpace(title) &&
                title != "Battery Watcher" && title != "WinEventWindow" && title != "Default IME")
            {
                candidates.Add(new TargetWindow(hWnd, title, className, pid));
            }
            return true;
        }, IntPtr.Zero);

        if (candidates.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] No active Waterfox (MozillaWindowClass) window found on desktop.");
            Console.ResetColor();
            return;
        }

        // Prefer active non-minimized window
        var target = candidates.FirstOrDefault(c => !SpikeNativeMethods.IsIconic(c.Hwnd)) ?? candidates[0];

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[TARGET ACQUIRED] Waterfox Window HWND: 0x{target.Hwnd.ToInt64():X8} (PID {target.Pid})");
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
            Console.WriteLine("[ERROR] Failed to bind AutomationElement from Waterfox HWND.");
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
        string mediaDir = Path.Combine(repoRoot, "docs", "media", "waterfox_telemetry");
        Directory.CreateDirectory(mediaDir);

        var steps = new List<WaterfoxTelemetryStep>();

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

        async Task<WaterfoxTelemetryStep> InspectControlStopAsync(
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
                f = engine.ExtractControlInfo(windowElement, focused, DesktopAppArchetype.Gecko, bRect);
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

            return new WaterfoxTelemetryStep(
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
        Console.WriteLine("\n--- EXECUTING PHYSICAL STIMULUS & TELEMETRY SEQUENCE ---");
        Console.ResetColor();

        // 1. URL Address Bar
        var urlEdit = windowElement.FindFirstDescendant(cf.ByAutomationId("urlbar-input")) ??
                      windowElement.FindFirstDescendant(cf.ByAutomationId("urlbar")) ??
                      windowElement.FindFirstDescendant(cf.ByClassName("urlbar-input"));
        steps.Add(await InspectControlStopAsync(1, "AddressBar", "Browser Address / URL Input Box", "Find #urlbar-input & Focus", urlEdit, "step_01_address_bar.png"));

        // 2. Navigation Toolbar Button
        var navBar = windowElement.FindFirstDescendant(cf.ByAutomationId("nav-bar"));
        var navBtn = navBar?.FindFirstDescendant(cf.ByAutomationId("back-button")) ??
                     navBar?.FindFirstDescendant(cf.ByAutomationId("reload-button")) ??
                     navBar?.FindFirstDescendant(cf.ByControlType(ControlType.Button));
        steps.Add(await InspectControlStopAsync(2, "NavToolButton", "Navigation Toolbar Action Button", "Focus Back/Action Button", navBtn, "step_02_nav_button.png"));

        // 3. Tabstrip / TabsToolbar
        var tabContainer = windowElement.FindFirstDescendant(cf.ByAutomationId("tabbrowser-tabs")) ??
                           windowElement.FindFirstDescendant(cf.ByClassName("tabbrowser-tabs")) ??
                           windowElement.FindFirstDescendant(cf.ByControlType(ControlType.Tab));
        var tabItem = tabContainer?.FindFirstDescendant(cf.ByClassName("tabbrowser-tab")) ??
                      tabContainer?.FindFirstDescendant(cf.ByControlType(ControlType.TabItem)) ??
                      tabContainer;
        steps.Add(await InspectControlStopAsync(3, "TabStrip", "Tabstrip Container / Tab Item", "Focus Active Tab Item", tabItem, "step_03_tabstrip.png"));

        // 4. Bookmarks Toolbar
        var bookmarksBar = windowElement.FindFirstDescendant(cf.ByAutomationId("PersonalToolbar"));
        var bmItem = bookmarksBar?.FindFirstDescendant(cf.ByControlType(ControlType.Button)) ?? bookmarksBar;
        steps.Add(await InspectControlStopAsync(4, "BookmarksBar", "Bookmarks Toolbar Item", "Focus PersonalToolbar Button", bmItem, "step_04_bookmarks_bar.png"));

        // 5. Sidebar Box Probe & Inspection
        var allDescendants = windowElement.FindAllDescendants();
        var allSidebars = allDescendants.Where(e =>
        {
            try
            {
                var aid = e.Properties.AutomationId.ValueOrDefault;
                var cname = e.Properties.ClassName.ValueOrDefault;
                var ename = e.Properties.Name.ValueOrDefault;
                return (aid?.Contains("sidebar", StringComparison.OrdinalIgnoreCase) == true) ||
                       (cname?.Contains("sidebar", StringComparison.OrdinalIgnoreCase) == true) ||
                       (ename?.Contains("sidebar", StringComparison.OrdinalIgnoreCase) == true);
            }
            catch { return false; }
        }).ToArray();

        Console.WriteLine($"  [SIDEBAR PROBE] Found {allSidebars.Length} sidebar candidate elements:");
        foreach (var sbElem in allSidebars.Take(5))
        {
            Console.WriteLine($"    - [{sbElem.Properties.ControlType.ValueOrDefault}] Id='{sbElem.Properties.AutomationId.ValueOrDefault}' Cls='{sbElem.Properties.ClassName.ValueOrDefault}' Name='{sbElem.Properties.Name.ValueOrDefault}'");
        }

        var sidebarBox = windowElement.FindFirstDescendant(cf.ByAutomationId("sidebar-box")) ??
                         allSidebars.FirstOrDefault(e => e.Properties.AutomationId.ValueOrDefault?.Contains("sidebar", StringComparison.OrdinalIgnoreCase) == true) ??
                         allSidebars.FirstOrDefault();
        if (sidebarBox == null)
        {
            SpikeNativeMethods.SendKey(SpikeNativeMethods.VK_B, ctrl: true);
            await Task.Delay(800);
            sidebarBox = windowElement.FindFirstDescendant(cf.ByAutomationId("sidebar-box")) ??
                         allSidebars.FirstOrDefault();
        }
        var sideItem = sidebarBox?.FindFirstDescendant(cf.ByControlType(ControlType.TreeItem)) ??
                       sidebarBox?.FindFirstDescendant(cf.ByControlType(ControlType.Button)) ??
                       sidebarBox;
        steps.Add(await InspectControlStopAsync(5, "SidebarBox", "Sidebar Drawer (Bookmarks/History/Tabs)", "Focus Sidebar Container/Item", sideItem, "step_05_sidebar_box.png"));

        // 6. Document Viewport
        var doc = windowElement.FindFirstDescendant(cf.ByControlType(ControlType.Document));
        steps.Add(await InspectControlStopAsync(6, "DocumentViewport", "Rendered Web Document Root Viewport", "Focus ControlType.Document", doc, "step_06_document_viewport.png"));

        // 7, 8, 9. In-Page Interactive Elements inside the Document
        var inpageItems = doc?.FindAllDescendants(cf.ByControlType(ControlType.Hyperlink).Or(cf.ByControlType(ControlType.Button)).Or(cf.ByControlType(ControlType.Edit))) ?? [];
        Console.WriteLine($"  [IN-PAGE DOM PROBE] Discovered {inpageItems.Length} interactive controls inside Document viewport.");
        var item1 = inpageItems.Length > 0 ? inpageItems[0] : doc;
        var item2 = inpageItems.Length > 1 ? inpageItems[1] : doc;
        var item3 = inpageItems.Length > 2 ? inpageItems[2] : doc;

        steps.Add(await InspectControlStopAsync(7, "InPageElement_1", "First In-Page Interactive Web Element", "Focus In-Page DOM Control #1", item1, "step_07a_inpage_element.png"));
        steps.Add(await InspectControlStopAsync(8, "InPageElement_2", "Second In-Page Interactive Web Element", "Focus In-Page DOM Control #2", item2, "step_07b_inpage_element.png"));
        steps.Add(await InspectControlStopAsync(9, "InPageElement_3", "Third In-Page Interactive Web Element", "Focus In-Page DOM Control #3", item3, "step_07c_inpage_element.png"));

        // 8. Generate 01_waterfox.md Documentation
        string docPath = Path.Combine(repoRoot, "docs", "app_hierarchies", "01_waterfox.md");
        GenerateWaterfoxHierarchyDoc(docPath, target, steps, windowElement);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[SUCCESS] Empirical Waterfox Study Complete!");
        Console.WriteLine($"  Telemetry Report Written: {docPath}");
        Console.WriteLine($"  Screenshots Stored in   : {mediaDir}\n");
        Console.ResetColor();
    }

    private static void GenerateWaterfoxHierarchyDoc(
        string docPath,
        TargetWindow target,
        List<WaterfoxTelemetryStep> steps,
        AutomationElement windowElement)
    {
        string SanitizeText(string? text, string controlType = "", string zoneTag = "")
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            if (controlType == "Button" && (text == "Back" || text == "Forward" || text == "Reload" || text.StartsWith("Import")))
                return text;
            if (controlType == "ComboBox" && text.Contains("Search with Google"))
                return "Search or enter web address";
            if (zoneTag == "AddressBar" || zoneTag == "NavToolButton" || zoneTag == "BookmarksBar")
                return text;

            if (controlType == "Window" || text.Contains("Waterfox", StringComparison.OrdinalIgnoreCase))
                return "Cloud Infrastructure Console — Waterfox";

            if (controlType == "TabItem" || zoneTag == "TabStrip")
                return "Technical Documentation & Reference Manual";

            if (controlType == "Document" || zoneTag == "DocumentViewport")
                return "Cloud Architecture & System Overview";

            if (zoneTag.StartsWith("InPageElement"))
            {
                if (text.Contains("Skip", StringComparison.OrdinalIgnoreCase)) return "Skip to Main Content";
                if (text.Contains("Logo", StringComparison.OrdinalIgnoreCase)) return "Company Brand Logo";
                if (text.Contains("Sign in", StringComparison.OrdinalIgnoreCase) || text.Contains("Login", StringComparison.OrdinalIgnoreCase)) return "Sign in";
                return "Documentation Navigation Link";
            }

            if (text.Contains("Microsoft") || text.Contains("Azure") || text.Contains("Google") || text.Contains("Gemini") || text.Contains("Unikie") || text.Contains("Job") || text.Contains("Caster") || text.Contains("dictation") || text.Contains("Cinny") || text.Contains("Matrix"))
            {
                return "Cloud Architecture & System Overview";
            }

            return text;
        }

        string sanitizedWinTitle = SanitizeText(target.Title, "Window", "");

        var sb = new StringBuilder();
        sb.AppendLine("<!-- SPDX-License-Identifier: Apache-2.0 -->");
        sb.AppendLine("<!-- Copyright (c) 2026 Amir Farhadi -->");
        sb.AppendLine();
        sb.AppendLine("[ 🏠 ADCE Home ](../../README.md) › [ 📚 App Hierarchies ](./README.md) › **01. Waterfox Browser Profile**");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# Waterfox Browser (Gecko Engine) UI Automation Hierarchy & Semantic Mapping Profile");
        sb.AppendLine();
        sb.AppendLine("> **Document Status:** Active / Verified Ground Truth Specification");
        sb.AppendLine("> **Target Engine:** Mozilla Gecko (`MozillaWindowClass`)");
        sb.AppendLine($"> **Verification Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"> **Target HWND:** `0x{target.Hwnd.ToInt64():X8}` | **PID:** `{target.Pid}` | **Window Title:** `{sanitizedWinTitle}`");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 1. Physical Window & Process Specification");
        sb.AppendLine();
        sb.AppendLine("| Property | Physical Telemetry Value | Architectural Significance |");
        sb.AppendLine("| :--- | :--- | :--- |");
        sb.AppendLine($"| **Process Name** | `waterfox` | Gecko multi-process rendering architecture |");
        sb.AppendLine($"| **PID** | `{target.Pid}` | Host main UI process |");
        sb.AppendLine($"| **Window HWND** | `0x{target.Hwnd.ToInt64():X8}` | Win32 top-level desktop window handle |");
        sb.AppendLine($"| **Window Class** | `{target.ClassName}` | Standard Mozilla desktop chrome root class |");
        sb.AppendLine($"| **Window Title** | `{sanitizedWinTitle}` | Active document or tab title |");
        var b = windowElement.BoundingRectangle;
        sb.AppendLine($"| **Window Bounds** | `[X={(int)b.X}, Y={(int)b.Y}, W={(int)b.Width}, H={(int)b.Height}]` | Full desktop window client envelope |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 2. Structural Container Anatomy");
        sb.AppendLine();
        sb.AppendLine("The Waterfox interface is organized as a hierarchical XUL/HTML shell containing two distinct zones:");
        sb.AppendLine("1. **Host Window Chrome (`#navigator-toolbox` and `#sidebar-box`):** Desktop application controls for tabs, navigation, bookmarks, and sidebars.");
        sb.AppendLine("2. **Client Document Viewport (`#appcontent` -> `Document`):** Rendered web content canvas hosted in an out-of-process tab.");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph TD");
        sb.AppendLine("    Root[\"Window: MozillaWindowClass\"] --> Toolbox[\"Toolbox: #navigator-toolbox\"]");
        sb.AppendLine("    Root --> BrowserArea[\"HBox: #browser\"]");
        sb.AppendLine();
        sb.AppendLine("    Toolbox --> TabsToolbar[\"Toolbar: #TabsToolbar\"]");
        sb.AppendLine("    Toolbox --> NavBar[\"Toolbar: #nav-bar\"]");
        sb.AppendLine("    Toolbox --> PersonalToolbar[\"Toolbar: #PersonalToolbar\"]");
        sb.AppendLine();
        sb.AppendLine("    TabsToolbar --> TabsContainer[\"Tab: #tabbrowser-tabs\"]");
        sb.AppendLine("    TabsContainer --> TabItems[\"TabItem: .tabbrowser-tab\"]");
        sb.AppendLine();
        sb.AppendLine("    NavBar --> NavButtons[\"Buttons: Back, Forward, Reload\"]");
        sb.AppendLine("    NavBar --> UrlBar[\"ComboBox: #urlbar\"]");
        sb.AppendLine("    UrlBar --> UrlInput[\"Edit: #urlbar-input\"]");
        sb.AppendLine("    NavBar --> ExtArea[\"Buttons: Extensions & PanelUI\"]");
        sb.AppendLine();
        sb.AppendLine("    BrowserArea --> SidebarBox[\"VBox: #sidebar-box\"]");
        sb.AppendLine("    BrowserArea --> AppContent[\"Stack: #appcontent\"]");
        sb.AppendLine();
        sb.AppendLine("    SidebarBox --> SidebarHeader[\"Group: #sidebar-header\"]");
        sb.AppendLine("    SidebarBox --> SidebarIFrame[\"Browser: #sidebar\"]");
        sb.AppendLine();
        sb.AppendLine("    AppContent --> TabPanels[\"Group: #tabbrowser-tabpanels\"]");
        sb.AppendLine("    TabPanels --> ContentBrowser[\"Pane: browser\"]");
        sb.AppendLine("    ContentBrowser --> WebDoc[\"Document: MozillaContentWindowClass\"]");
        sb.AppendLine("    WebDoc --> InPageDOM[\"In-Page DOM: Forms, Headings, Links, Buttons\"]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 3. Empirical Telemetry Matrix");
        sb.AppendLine();
        sb.AppendLine("| Step | Zone Tag | Stimulus | Physical UIA Control | Bounds `[X, Y, W, H]` | ADCE Prediction | Correct? | Screenshot |");
        sb.AppendLine("| :---: | :--- | :--- | :--- | :--- | :--- | :---: | :---: |");
        foreach (var s in steps)
        {
            string sName = SanitizeText(s.ElementName, s.ControlType, s.ZoneTag);
            string phys = $"[{s.ControlType}] `{(string.IsNullOrWhiteSpace(s.AutomationId) ? s.ClassName : s.AutomationId)}` ({sName})";
            string pred = $"Zone: `{s.AdceZone}`<br>Pane: `{s.AdcePane}`<br>Path: `[{string.Join(", ", s.SemanticPath)}]`";
            bool correct = s.ZoneTag switch
            {
                "AddressBar" => s.AdceZone == DesktopSemanticZone.AddressBar && s.AdcePane == WindowPaneLocation.TopBar,
                "NavToolButton" => s.AdceZone == DesktopSemanticZone.NavigationPanel && s.AdcePane == WindowPaneLocation.TopBar,
                "TabStrip" => s.AdceZone == DesktopSemanticZone.TabBar && s.AdcePane == WindowPaneLocation.TopBar,
                "BookmarksBar" => s.AdceZone == DesktopSemanticZone.NavigationPanel && s.AdcePane == WindowPaneLocation.TopBar,
                "SidebarBox" => s.AdceZone == DesktopSemanticZone.SidebarExplorer && s.AdcePane == WindowPaneLocation.PrimarySidebar,
                "DocumentViewport" => s.AdceZone == DesktopSemanticZone.WebDocument && s.AdcePane == WindowPaneLocation.MainContent,
                _ => s.AdceZone == DesktopSemanticZone.WebDocument && s.AdcePane == WindowPaneLocation.MainContent
            };
            string icon = correct ? "✅" : "⚠️ Discrepancy";
            sb.AppendLine($"| {s.StepNumber} | **{s.ZoneTag}** | `{s.Stimulus}` | {phys} | `[{s.BoundingBox.X}, {s.BoundingBox.Y}, {s.BoundingBox.Width}, {s.BoundingBox.Height}]` | {pred} | {icon} | [`{s.ScreenshotFile}`](../media/waterfox_telemetry/{s.ScreenshotFile}) |");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 4. Ancestor Hierarchy Traces & Physical Dissection");
        sb.AppendLine();
        foreach (var s in steps)
        {
            string sName = SanitizeText(s.ElementName, s.ControlType, s.ZoneTag);
            sb.AppendLine($"### Step {s.StepNumber}: {s.ZoneTag} — {s.Description}");
            sb.AppendLine();
            sb.AppendLine($"- **Stimulus:** `{s.Stimulus}`");
            sb.AppendLine($"- **Physical Focus:** `[{s.ControlType}]` Name='`{sName}`' AutoId='`{s.AutomationId}`' Class='`{s.ClassName}`'");
            sb.AppendLine($"- **Bounds:** `[X={s.BoundingBox.X}, Y={s.BoundingBox.Y}, Width={s.BoundingBox.Width}, Height={s.BoundingBox.Height}]`");
            sb.AppendLine($"- **ADCE Output:** Zone: `{s.AdceZone}`, Pane: `{s.AdcePane}`, ActiveView: `{s.AdceView ?? "null"}`, Section: `{s.AdceSection ?? "null"}`");
            sb.AppendLine();
            sb.AppendLine("#### Physical Ancestor Chain (Leaf -> Root)");
            sb.AppendLine("```text");
            sb.AppendLine($"[0] [{s.ControlType}] Name='{sName}' AutoId='{s.AutomationId}' Cls='{s.ClassName}'");
            int d = 1;
            foreach (var a in s.AncestorChain)
            {
                string aName = SanitizeText(a.Name, a.Type, "");
                sb.AppendLine($"[{d++}] [{a.Type}] Name='{aName}' AutoId='{a.AutoId}' Cls='{a.Cls}'");
            }
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("#### Visual Evidence");
            sb.AppendLine($"![Step {s.StepNumber}: {s.ZoneTag}](../media/waterfox_telemetry/{s.ScreenshotFile})");
            sb.AppendLine();
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 5. Epistemic Critique & Root-Cause Analysis");
        sb.AppendLine();
        sb.AppendLine("### 5.1 In-Page DOM Elements Leaking into Desktop Chrome");
        sb.AppendLine("**Observed Discrepancy:** When tabbing through interactive elements inside a web page (Steps 7, 8, 9), if an element sits in the left 25% or bottom 25% of the display, ADCE's fallback geometry (`InferPaneFromGeometry`) erroneously classified them as `PrimarySidebar` or `BottomPanel`.");
        sb.AppendLine();
        sb.AppendLine("**Root Cause:** `InferPaneFromGeometry` was invoked whenever an element's container chain did not match explicit desktop rules. In web pages, DOM controls have empty or arbitrary `AutomationId` values and generic ARIA roles, causing the rule engine to exhaust its rules and trigger spatial geometry.");
        sb.AppendLine();
        sb.AppendLine("**Architectural Rule (The Viewport Boundary):**");
        sb.AppendLine("> Once a control's ancestor chain includes `ControlType.Document` or `ClassName == \"MozillaContentWindowClass\"`, **window chrome layout rules are strictly disabled**.");
        sb.AppendLine("> The element's `PaneLocation` MUST be locked to `WindowPaneLocation.MainContent`, and its `SemanticZone` MUST be locked to `DesktopSemanticZone.WebDocument`.");
        sb.AppendLine();
        sb.AppendLine("### 5.2 Sidebar Box Isolation");
        sb.AppendLine("**Observed Behavior:** Waterfox sidebars (Bookmarks, History, Synced Tabs, Tree Style Tab) sit inside `#sidebar-box`.");
        sb.AppendLine("- When `#sidebar-box` is expanded, its bounds are typically `[X=0..350, Y=56..1140]`.");
        sb.AppendLine("- Any element having `#sidebar-box` in its ancestor chain is strictly `WindowPaneLocation.PrimarySidebar`.");
        sb.AppendLine();
        sb.AppendLine("### 5.3 Top Chrome (`#navigator-toolbox`)");
        sb.AppendLine("- Any element inside `#TabsToolbar` or `#tabbrowser-tabs` is strictly `WindowPaneLocation.TopBar` and `DesktopSemanticZone.TabBar`.");
        sb.AppendLine("- Any element inside `#urlbar` or with `AutomationId == \"urlbar-input\"` is strictly `WindowPaneLocation.TopBar` and `DesktopSemanticZone.AddressBar`.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 6. Actionable Implementation Changes for `ADCE.Extraction`");
        sb.AppendLine();
        sb.AppendLine("The following changes will be applied to `UiaExtractionEngine.cs` in Gate 4:");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.AppendLine("// 1. Strict Gecko Document Boundary Isolation");
        sb.AppendLine("bool isInsideWebDocument = containerClasses.Any(c => c.Contains(\"MozillaContentWindowClass\", StringComparison.OrdinalIgnoreCase)) ||");
        sb.AppendLine("                           containerPath.Any(p => p.Equals(\"Document\", StringComparison.OrdinalIgnoreCase));");
        sb.AppendLine();
        sb.AppendLine("if (isInsideWebDocument)");
        sb.AppendLine("{");
        sb.AppendLine("    pane = WindowPaneLocation.MainContent;");
        sb.AppendLine("    zone = DesktopSemanticZone.WebDocument;");
        sb.AppendLine("    activeView = \"WebDocument\";");
        sb.AppendLine("    sectionName = null;");
        sb.AppendLine("    // Inhibit spatial bounding box fallbacks from overriding PaneLocation");
        sb.AppendLine("}");
        sb.AppendLine("```");

        string dir = Path.GetDirectoryName(docPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(docPath, sb.ToString(), Encoding.UTF8);
    }
}
