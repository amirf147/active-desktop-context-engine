// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Extraction.Classifiers;
using ADCE.Extraction.Extractors;
using ADCE.Extraction.Security;
using ADCE.Extraction.Win32;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace ADCE.Extraction.Engine;

/// <summary>
/// Production-grade UI Automation context extraction engine implementing IExtractionEngine.
/// Features single-roundtrip FlaUI 5 batch caching, 50ms COM transaction timeouts,
/// UIPI privilege gating, and privacy redaction.
/// </summary>
public sealed class UiaExtractionEngine : IExtractionEngine, IDisposable
{
    private readonly UIA3Automation _automation;
    private readonly IArchetypeClassifier _classifier;
    private readonly ISemanticRuleEngine _ruleEngine;
    private bool _disposed;

    /// <summary>
    /// Gets the active semantic rule engine.
    /// </summary>
    public ISemanticRuleEngine RuleEngine => _ruleEngine;

    /// <summary>
    /// Gets or sets whether heuristic semantic zone resolution is enabled.
    /// When false, semantic zone heuristics are completely bypassed and set to Unknown/None,
    /// enabling pure explicit structural inspection.
    /// </summary>
    public bool EnableSemanticZones { get; set; } = true;

    public UiaExtractionEngine(IArchetypeClassifier? classifier = null, ISemanticRuleEngine? ruleEngine = null)
    {
        _classifier = classifier ?? ArchetypeClassifier.Default;
        _ruleEngine = ruleEngine ?? new Rules.SemanticRuleEngine();
        _automation = new UIA3Automation();
        ConfigureTransactionTimeouts(_automation, 50);
    }

    public ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default)
    {
        nint fgHwnd = NativeMethods.GetForegroundWindow();
        if (fgHwnd == nint.Zero)
        {
            return ValueTask.FromResult(CreateEmptySnapshot(nint.Zero, "No Active Window", string.Empty, 0, string.Empty, DesktopAppArchetype.Unknown, 0.0));
        }

        return ExtractSnapshotAsync(fgHwnd, cancellationToken);
    }

    public ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 0. Win32 Root Window Normalization: map child HWNDs (e.g. Electron sub-surfaces) to top-level window
        if (hwnd != nint.Zero && NativeMethods.IsWindow(hwnd))
        {
            nint rootHwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOTOWNER);
            if (rootHwnd != nint.Zero && NativeMethods.IsWindow(rootHwnd))
            {
                hwnd = rootHwnd;
            }
        }

        // 1. Fast Win32 Gating (< 0.5 ms)
        if (!Win32Gating.GetWindowIdentityFast(hwnd, out string title, out string className, out int pid, out string processName))
        {
            sw.Stop();
            return ValueTask.FromResult(CreateEmptySnapshot(hwnd, "Invalid Window Handle", string.Empty, 0, string.Empty, DesktopAppArchetype.Unknown, sw.Elapsed.TotalMilliseconds));
        }

        var bounds = Win32Gating.GetWindowBounds(hwnd);
        var archetype = _classifier.Classify(className, processName, title);

        // 2. UIPI Gating: If target runs elevated and ADCE is standard user, return Win32 shallow context without hanging in COM
        if (!Win32Gating.CanAccessProcess(hwnd))
        {
            sw.Stop();
            return ValueTask.FromResult(CreateShallowSnapshot(hwnd, title, className, pid, processName, archetype, bounds, sw.Elapsed.TotalMilliseconds));
        }

        // 3. Safe Window Binding
        var windowElement = SafeBindWindow(_automation, hwnd);
        if (windowElement == null)
        {
            sw.Stop();
            return ValueTask.FromResult(CreateShallowSnapshot(hwnd, title, className, pid, processName, archetype, bounds, sw.Elapsed.TotalMilliseconds));
        }

        // 4. Extract Focus Target (process-scoped to prevent global UIA focus bleed from other windows)
        var focusInfo = ExtractFocusedControl(_automation, windowElement, pid, processName, archetype, EnableSemanticZones, _ruleEngine, bounds);

        // 5. Specialized Multi-Zone Extraction based on Archetype
        IdeContext? ideContext = null;
        BrowserContext? browserContext = null;
        ExplorerContext? explorerContext = null;
        TerminalContext? terminalContext = null;

        try
        {
            switch (archetype)
            {
                case DesktopAppArchetype.ChromiumElectron when className.Contains("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase):
                    if (title.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("Antigravity", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("Cursor", StringComparison.OrdinalIgnoreCase) ||
                        processName.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
                        processName.Contains("Antigravity", StringComparison.OrdinalIgnoreCase) ||
                        processName.Contains("Cursor", StringComparison.OrdinalIgnoreCase))
                    {
                        ideContext = MonacoIdeExtractor.Extract(windowElement, _automation);
                    }
                    break;

                case DesktopAppArchetype.Gecko:
                    browserContext = GeckoBrowserExtractor.Extract(windowElement, _automation);
                    break;

                case DesktopAppArchetype.WinUI3Xaml when className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase):
                    explorerContext = WinUIExplorerExtractor.Extract(windowElement, _automation);
                    break;

                case DesktopAppArchetype.WinUI3Xaml when className.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase):
                case DesktopAppArchetype.ClassicWin32 when className.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase):
                    terminalContext = TerminalExtractor.Extract(windowElement, _automation);
                    break;
            }
        }
        catch (Exception)
        {
            // Resilient degradation: zone extraction failure never crashes root envelope capture
        }

        sw.Stop();

        var snapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.Empty,
                DesktopIndex = 0,
                VirtualDesktopName = "Current Desktop",
                MonitorIndex = 0,
                MonitorBounds = bounds
            },
            Window = new WindowEnvelope
            {
                Hwnd = hwnd,
                Title = title,
                ProcessName = processName,
                Pid = pid,
                ClassName = className,
                Archetype = archetype,
                Bounds = bounds,
                IsMinimized = bounds.IsEmpty,
                IsMaximized = false
            },
            Focus = focusInfo,
            IdeContext = ideContext,
            BrowserContext = browserContext,
            ExplorerContext = explorerContext,
            TerminalContext = terminalContext,
            ExtractionDurationMs = sw.Elapsed.TotalMilliseconds
        };

        return ValueTask.FromResult(snapshot);
    }

    internal static bool IsSameOrChildProcess(int controlPid, int windowPid, string windowProcessName)
    {
        if (controlPid == windowPid) return true;
        if (controlPid <= 0) return false;
        if (string.IsNullOrEmpty(windowProcessName)) return false;

        try
        {
            using var proc = Process.GetProcessById(controlPid);
            return proc.ProcessName.Equals(windowProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Evaluates ADCE semantic zone and pane classification on an explicit target control.
    /// </summary>
    public FocusedControlInfo ExtractControlInfo(
        AutomationElement windowElement,
        AutomationElement control,
        DesktopAppArchetype archetype,
        BoundingRectangle windowBounds = default)
    {
        int pid = 0;
        string procName = string.Empty;
        try
        {
            pid = windowElement.Properties.ProcessId.ValueOrDefault;
            if (pid > 0)
            {
                using var p = Process.GetProcessById(pid);
                procName = p.ProcessName;
            }
        }
        catch { }

        if (windowBounds.IsEmpty)
        {
            try
            {
                var r = windowElement.Properties.BoundingRectangle.ValueOrDefault;
                if (!r.IsEmpty)
                {
                    windowBounds = new BoundingRectangle((int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height);
                }
            }
            catch { }
        }

        return ExtractControlInfoCore(
            _automation, windowElement, control, pid, procName, archetype,
            EnableSemanticZones, _ruleEngine, windowBounds);
    }

    private static FocusedControlInfo ExtractFocusedControl(
        UIA3Automation automation,
        AutomationElement windowElement,
        int windowPid,
        string processName,
        DesktopAppArchetype archetype,
        bool enableSemanticZones = true,
        ISemanticRuleEngine? ruleEngine = null,
        BoundingRectangle windowBounds = default)
    {
        try
        {
            var focused = automation.FocusedElement();
            if (focused != null)
            {
                int focusedPid = 0;
                try
                {
                    focusedPid = focused.Properties.ProcessId.ValueOrDefault;
                }
                catch { }

                // Process Boundary Guard: Accept focused element if it belongs to the active window or child renderer
                if (IsSameOrChildProcess(focusedPid, windowPid, processName))
                {
                    return ExtractControlInfoCore(
                        automation, windowElement, focused, windowPid, processName, archetype,
                        enableSemanticZones, ruleEngine, windowBounds);
                }
            }

            // Fallback: If global foreground focus is not in the target process, look for internal keyboard focus
            try
            {
                var cond = new FlaUI.Core.Conditions.PropertyCondition(automation.PropertyLibrary.Element.HasKeyboardFocus, true);
                var internalFocus = windowElement.FindFirstDescendant(cond);
                if (internalFocus != null)
                {
                    return ExtractControlInfoCore(
                        automation, windowElement, internalFocus, windowPid, processName, archetype,
                        enableSemanticZones, ruleEngine, windowBounds);
                }
            }
            catch { }
        }
        catch { }

        return CreateDefaultFocusedControlInfo(windowElement, windowBounds);
    }

    private static FocusedControlInfo ExtractControlInfoCore(
        UIA3Automation automation,
        AutomationElement windowElement,
        AutomationElement focused,
        int windowPid,
        string processName,
        DesktopAppArchetype archetype,
        bool enableSemanticZones = true,
        ISemanticRuleEngine? ruleEngine = null,
        BoundingRectangle windowBounds = default)
    {
        string cType = focused.Properties.ControlType.ValueOrDefault.ToString();
        string name = focused.Properties.Name.ValueOrDefault ?? string.Empty;
        string autoId = focused.Properties.AutomationId.ValueOrDefault ?? string.Empty;
        string cls = focused.Properties.ClassName.ValueOrDefault ?? string.Empty;
        var rect = focused.Properties.BoundingRectangle.ValueOrDefault;

        var boundingBox = rect.IsEmpty ? BoundingRectangle.Empty :
            new BoundingRectangle((int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height);

        bool isOverlay = autoId.Contains("quickInput", StringComparison.OrdinalIgnoreCase) ||
                         autoId.Contains("command-palette", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("quick-input", StringComparison.OrdinalIgnoreCase) ||
                         name.Equals("Search box", StringComparison.OrdinalIgnoreCase);

        nint rootHwnd = nint.Zero;
        try
        {
            rootHwnd = windowElement.Properties.NativeWindowHandle.ValueOrDefault;
        }
        catch { }

        var (containerPath, containerClasses, ancestorZone, ancestorPane, ancestorView, ancestorSection) = ExtractAncestorHierarchy(
            automation, focused, rootHwnd, windowPid, processName, archetype, maxDepth: 8, enableSemanticZones: enableSemanticZones);

        WindowPaneLocation pane = ancestorPane;
        string? activeView = ancestorView;
        string? sectionName = ancestorSection;
        var zone = DesktopSemanticZone.Unknown;

        // Direct signatures on focused control itself
        if (isOverlay)
        {
            pane = WindowPaneLocation.OverlayModal;
            activeView = "QuickOpen";
        }
        else if (autoId.Contains("antigravity.agentSidePanelInputBox", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("Message input", StringComparison.OrdinalIgnoreCase))
        {
            pane = WindowPaneLocation.AuxiliarySidebar;
            activeView = "Chat";
            sectionName = "ChatPrompt";
            zone = DesktopSemanticZone.ChatPrompt;
        }
        else if (name.Contains("Message (Ctrl+Enter to commit", StringComparison.OrdinalIgnoreCase) ||
                 autoId.Contains("scm.input", StringComparison.OrdinalIgnoreCase))
        {
            pane = WindowPaneLocation.PrimarySidebar;
            activeView = "SourceControl";
            sectionName = "CommitBox";
            zone = DesktopSemanticZone.GitCommitBox;
        }
        else if (cls.Contains("pane-header", StringComparison.OrdinalIgnoreCase) ||
                 (cType.Equals("Button", StringComparison.OrdinalIgnoreCase) && name.Contains("Section", StringComparison.OrdinalIgnoreCase)))
        {
            pane = WindowPaneLocation.PrimarySidebar;
            activeView = "Explorer";
            if (name.Contains("Timeline", StringComparison.OrdinalIgnoreCase))
            {
                sectionName = "Timeline";
                zone = DesktopSemanticZone.Timeline;
            }
            else if (name.Contains("Outline", StringComparison.OrdinalIgnoreCase))
            {
                sectionName = "Outline";
                zone = DesktopSemanticZone.Outline;
            }
            else if (name.StartsWith("Explorer Section:", StringComparison.OrdinalIgnoreCase))
            {
                string parsed = name["Explorer Section:".Length..].Trim();
                sectionName = string.IsNullOrEmpty(parsed) ? "Explorer" : parsed;
                zone = DesktopSemanticZone.SidebarExplorer;
            }
        }
        else if (name.Contains("Toggle Agent", StringComparison.OrdinalIgnoreCase) ||
                 cls.Contains("codicon-layout-sidebar-right", StringComparison.OrdinalIgnoreCase) ||
                 cls.Contains("antigravity-agent-side-panel", StringComparison.OrdinalIgnoreCase) ||
                 autoId.Contains("antigravity.agentSidePanelInputBox", StringComparison.OrdinalIgnoreCase))
        {
            pane = WindowPaneLocation.AuxiliarySidebar;
            activeView = "Chat";
            zone = DesktopSemanticZone.ChatConversation;
        }
        else if (cls.Contains("activitybar", StringComparison.OrdinalIgnoreCase) ||
                 autoId.Contains("workbench.parts.activitybar", StringComparison.OrdinalIgnoreCase) ||
                 cls.Contains("codicon-explorer-view-icon", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("Explorer (Ctrl+Shift+E)", StringComparison.OrdinalIgnoreCase))
        {
            pane = WindowPaneLocation.ActivityBar;
            activeView = "ActivityBar";
            zone = DesktopSemanticZone.ActivityBar;
        }
        else if (cls.Contains("monaco-breadcrumbs", StringComparison.OrdinalIgnoreCase) ||
                 autoId.Contains("breadcrumbs", StringComparison.OrdinalIgnoreCase))
        {
            pane = WindowPaneLocation.MainContent;
            activeView = "Editor";
            sectionName = "Breadcrumbs";
            zone = DesktopSemanticZone.NavigationPanel;
        }
        else if (cls.Contains("single-terminal-tab", StringComparison.OrdinalIgnoreCase) ||
                 cls.Contains("xterm", StringComparison.OrdinalIgnoreCase) ||
                 autoId.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("Focus Terminal", StringComparison.OrdinalIgnoreCase) ||
                 (cType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) && name.Equals("Terminal", StringComparison.OrdinalIgnoreCase)))
        {
            pane = WindowPaneLocation.BottomPanel;
            activeView = "Terminal";
            zone = DesktopSemanticZone.Terminal;
        }
        else if (archetype == DesktopAppArchetype.Gecko)
        {
            if (autoId.Contains("urlbar-input", StringComparison.OrdinalIgnoreCase) ||
                autoId.Equals("urlbar", StringComparison.OrdinalIgnoreCase))
            {
                pane = WindowPaneLocation.TopBar;
                activeView = "NavigationBar";
                zone = DesktopSemanticZone.AddressBar;
            }
            else if (autoId.Contains("back-button", StringComparison.OrdinalIgnoreCase) ||
                     autoId.Contains("forward-button", StringComparison.OrdinalIgnoreCase) ||
                     autoId.Contains("reload-button", StringComparison.OrdinalIgnoreCase))
            {
                pane = WindowPaneLocation.TopBar;
                activeView = "NavigationBar";
                zone = DesktopSemanticZone.NavigationPanel;
            }
            else if (autoId.Contains("tabbrowser-tab", StringComparison.OrdinalIgnoreCase) ||
                     cls.Contains("tabbrowser-tab", StringComparison.OrdinalIgnoreCase) ||
                     cType.Equals("TabItem", StringComparison.OrdinalIgnoreCase))
            {
                pane = WindowPaneLocation.TopBar;
                activeView = "TabStrip";
                zone = DesktopSemanticZone.TabBar;
            }
            else if (autoId.Contains("sidebar-box", StringComparison.OrdinalIgnoreCase))
            {
                pane = WindowPaneLocation.PrimarySidebar;
                activeView = "Sidebar";
                zone = DesktopSemanticZone.SidebarExplorer;
            }
            else if (cType.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
                     cls.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase))
            {
                pane = WindowPaneLocation.MainContent;
                activeView = "WebDocument";
                zone = DesktopSemanticZone.WebDocument;
            }
        }

        if (enableSemanticZones)
        {
            // 1. Declarative custom rules take precedence (user overrides & seeded rules)
            var matchedRule = ruleEngine?.FindMatchingRule(processName, cType, name, autoId, cls, containerPath);
            if (matchedRule != null)
            {
                if (matchedRule.TargetZone != DesktopSemanticZone.Unknown)
                {
                    zone = matchedRule.TargetZone;
                }
                if (matchedRule.TargetPane.HasValue && matchedRule.TargetPane.Value != WindowPaneLocation.Unknown)
                {
                    pane = matchedRule.TargetPane.Value;
                }
                if (!string.IsNullOrEmpty(matchedRule.TargetView))
                {
                    activeView = matchedRule.TargetView;
                }
                if (!string.IsNullOrEmpty(matchedRule.TargetSection))
                {
                    sectionName = matchedRule.TargetSection;
                }
            }

            // 2. Fall back to archetype heuristics
            if (zone == DesktopSemanticZone.Unknown)
            {
                zone = ResolveSemanticZone(cType, name, autoId, cls, archetype, isOverlay);
            }

            // 3. Fall back to ancestor zone
            if (zone == DesktopSemanticZone.Unknown && ancestorZone != DesktopSemanticZone.Unknown)
            {
                zone = ancestorZone;
            }
        }

        // Infer pane from zone if still unknown
        if (pane == WindowPaneLocation.Unknown && zone != DesktopSemanticZone.Unknown)
        {
            pane = InferPaneFromZone(zone);
        }

        // Viewport Boundary Isolation:
        // Any element inside a WebDocument (or whose ancestor is MozillaContentWindowClass/tabbrowser-tabpanels/appcontent)
        // belongs strictly to MainContent and WebDocument. This prevents spatial bounding box heuristics from falsely claiming
        // in-page DOM elements as PrimarySidebar or BottomPanel!
        bool isInsideWebDocument = zone == DesktopSemanticZone.WebDocument ||
                                   cType.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
                                   cls.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase) ||
                                   containerClasses.Any(c => c.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase)) ||
                                   containerPath.Any(p => p.Contains("tabbrowser-tabpanels", StringComparison.OrdinalIgnoreCase) ||
                                                          p.Contains("appcontent", StringComparison.OrdinalIgnoreCase));

        if (isInsideWebDocument)
        {
            pane = WindowPaneLocation.MainContent;
            if (zone == DesktopSemanticZone.Unknown)
            {
                zone = DesktopSemanticZone.WebDocument;
            }
            if (string.IsNullOrEmpty(activeView))
            {
                activeView = "WebDocument";
            }
        }
        else
        {
            // Spatial relative geometry fallback only for non-document host chrome controls
            if (pane == WindowPaneLocation.Unknown && !windowBounds.IsEmpty && !boundingBox.IsEmpty)
            {
                pane = InferPaneFromGeometry(windowBounds, boundingBox);
            }
        }

        // Infer view and section from zone if missing
        if (string.IsNullOrEmpty(activeView) && zone != DesktopSemanticZone.Unknown)
        {
            activeView = InferViewFromZone(zone);
        }

        if (string.IsNullOrEmpty(sectionName) && zone != DesktopSemanticZone.Unknown)
        {
            sectionName = InferSectionFromZone(zone);
        }

        // Assemble semantic path: [Pane, ActiveView, SectionName]
        var pathBuilder = System.Collections.Immutable.ImmutableArray.CreateBuilder<string>(3);
        if (pane != WindowPaneLocation.Unknown)
        {
            pathBuilder.Add(pane.ToString());
        }
        if (!string.IsNullOrWhiteSpace(activeView))
        {
            pathBuilder.Add(activeView);
        }
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            pathBuilder.Add(sectionName);
        }
        var semanticPath = pathBuilder.ToImmutable();

        bool isPassword = false;
        try
        {
            isPassword = focused.Properties.IsPassword.ValueOrDefault;
        }
        catch { }

        string? value = null;
        try
        {
            value = focused.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
        }
        catch { }

        string? sanitizedValue = ContextPrivacySanitizer.SanitizeBuffer(value, name, isPassword);

        return new FocusedControlInfo
        {
            ControlType = cType,
            ElementName = name,
            AutomationId = autoId,
            ClassName = cls,
            BoundingBox = boundingBox,
            SemanticZone = zone,
            PaneLocation = pane,
            ActiveView = activeView,
            SectionName = sectionName,
            SemanticPath = semanticPath,
            ContainerPath = containerPath,
            ContainerClasses = containerClasses,
            IsOverlay = isOverlay,
            ValueSnippet = sanitizedValue
        };
    }

    private static FocusedControlInfo CreateDefaultFocusedControlInfo(AutomationElement windowElement, BoundingRectangle windowBounds)
    {
        return new FocusedControlInfo
        {
            ControlType = "Window",
            ElementName = windowElement.Properties.Name.ValueOrDefault ?? string.Empty,
            AutomationId = string.Empty,
            ClassName = windowElement.Properties.ClassName.ValueOrDefault ?? string.Empty,
            BoundingBox = windowBounds.IsEmpty ? BoundingRectangle.Empty : windowBounds,
            SemanticZone = DesktopSemanticZone.Unknown,
            PaneLocation = WindowPaneLocation.Unknown,
            ActiveView = null,
            SectionName = null,
            SemanticPath = System.Collections.Immutable.ImmutableArray<string>.Empty,
            ContainerPath = System.Collections.Immutable.ImmutableArray<string>.Empty,
            ContainerClasses = System.Collections.Immutable.ImmutableArray<string>.Empty,
            IsOverlay = false
        };
    }

    internal static (
        System.Collections.Immutable.ImmutableArray<string> Paths,
        System.Collections.Immutable.ImmutableArray<string> Classes,
        DesktopSemanticZone Zone,
        WindowPaneLocation Pane,
        string? ActiveView,
        string? SectionName) ExtractAncestorHierarchy(
        UIA3Automation automation,
        AutomationElement focusedElement,
        nint rootWindowHwnd,
        int expectedPid,
        string expectedProcessName,
        DesktopAppArchetype archetype,
        int maxDepth = 8,
        bool enableSemanticZones = true)
    {
        var pathBuilder = System.Collections.Immutable.ImmutableArray.CreateBuilder<string>(maxDepth);
        var classBuilder = System.Collections.Immutable.ImmutableArray.CreateBuilder<string>(maxDepth);
        var resolvedZone = DesktopSemanticZone.Unknown;
        var resolvedPane = WindowPaneLocation.Unknown;
        string? resolvedView = null;
        string? resolvedSection = null;

        try
        {
            var nativeAutomation = (Interop.UIAutomationClient.IUIAutomation)automation.NativeAutomation;
            var nativeWalker = nativeAutomation.RawViewWalker;

            var cacheRequest = nativeAutomation.CreateCacheRequest();
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.AutomationId.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.ClassName.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.ControlType.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.Name.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.ProcessId.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.NativeWindowHandle.Id);
            cacheRequest.TreeScope = Interop.UIAutomationClient.TreeScope.TreeScope_Element;

            var currentNative = ((FlaUI.UIA3.UIA3FrameworkAutomationElement)focusedElement.FrameworkAutomationElement).NativeElement;

            for (int depth = 0; depth < maxDepth; depth++)
            {
                Interop.UIAutomationClient.IUIAutomationElement? parentNative = null;
                try
                {
                    parentNative = nativeWalker.GetParentElementBuildCache(currentNative, cacheRequest);
                }
                catch (COMException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (parentNative == null) break;

                int parentPid = 0;
                nint parentHwnd = nint.Zero;
                try
                {
                    parentPid = (int)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.ProcessId.Id);
                    parentHwnd = (nint)(int)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.NativeWindowHandle.Id);
                }
                catch { }

                if (!IsSameOrChildProcess(parentPid, expectedPid, expectedProcessName) || (rootWindowHwnd != nint.Zero && parentHwnd == rootWindowHwnd))
                {
                    break;
                }

                string autoId = string.Empty;
                string cls = string.Empty;
                string name = string.Empty;
                int cTypeId = 0;
                try
                {
                    autoId = (string)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.AutomationId.Id) ?? string.Empty;
                    cls = (string)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.ClassName.Id) ?? string.Empty;
                    name = (string)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.Name.Id) ?? string.Empty;
                    cTypeId = (int)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.ControlType.Id);
                }
                catch { }

                if (!string.IsNullOrWhiteSpace(cls) && !IsNoiseWrapperClass(cls))
                {
                    classBuilder.Add(cls);
                }

                if (!string.IsNullOrWhiteSpace(autoId))
                {
                    pathBuilder.Add(autoId);
                }

                // Gecko Chrome & Web Document Hierarchy Rules:
                if (archetype == DesktopAppArchetype.Gecko)
                {
                    if (cTypeId == 50030 || // UIA_DocumentControlTypeId
                        cls.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase) ||
                        autoId.Contains("tabbrowser-tabpanels", StringComparison.OrdinalIgnoreCase) ||
                        autoId.Contains("appcontent", StringComparison.OrdinalIgnoreCase))
                    {
                        if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.MainContent;
                        resolvedView ??= "WebDocument";
                        if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                            resolvedZone = DesktopSemanticZone.WebDocument;
                    }
                    else if (autoId.Contains("sidebar-box", StringComparison.OrdinalIgnoreCase) ||
                             autoId.Contains("sidebar-header", StringComparison.OrdinalIgnoreCase) ||
                             autoId.Equals("sidebar", StringComparison.OrdinalIgnoreCase) ||
                             cls.Contains("sidebar-box", StringComparison.OrdinalIgnoreCase))
                    {
                        if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.PrimarySidebar;
                        resolvedView ??= "Sidebar";
                        if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                            resolvedZone = DesktopSemanticZone.SidebarExplorer;
                    }
                    else if (autoId.Contains("TabsToolbar", StringComparison.OrdinalIgnoreCase) ||
                             autoId.Contains("tabbrowser-tabs", StringComparison.OrdinalIgnoreCase))
                    {
                        if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.TopBar;
                        resolvedView ??= "TabStrip";
                        if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                            resolvedZone = DesktopSemanticZone.TabBar;
                    }
                    else if (autoId.Contains("PersonalToolbar", StringComparison.OrdinalIgnoreCase) ||
                             autoId.Contains("PlacesToolbar", StringComparison.OrdinalIgnoreCase))
                    {
                        if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.TopBar;
                        resolvedView ??= "BookmarksToolbar";
                        if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                            resolvedZone = DesktopSemanticZone.NavigationPanel;
                    }
                    else if (autoId.Contains("nav-bar", StringComparison.OrdinalIgnoreCase) ||
                             autoId.Contains("urlbar", StringComparison.OrdinalIgnoreCase) ||
                             autoId.Contains("navigator-toolbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.TopBar;
                        resolvedView ??= "NavigationBar";
                        if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                        {
                            if (autoId.Contains("urlbar", StringComparison.OrdinalIgnoreCase))
                                resolvedZone = DesktopSemanticZone.AddressBar;
                            else
                                resolvedZone = DesktopSemanticZone.NavigationPanel;
                        }
                    }
                }

                // Structural and semantic container inspection:
                if (cls.Contains("scm-editor-container", StringComparison.OrdinalIgnoreCase) ||
                    autoId.Contains("workbench.view.scm", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.PrimarySidebar;
                    resolvedView ??= "SourceControl";
                    resolvedSection ??= "CommitBox";
                    if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                        resolvedZone = DesktopSemanticZone.GitCommitBox;
                }
                else if (autoId.Contains("antigravity.agentSidePanelInputBox", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("antigravity-agent-side-panel", StringComparison.OrdinalIgnoreCase) ||
                         autoId.Contains("workbench.parts.auxiliarybar", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Toggle Agent", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("codicon-layout-sidebar-right", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.AuxiliarySidebar;
                    resolvedView ??= "Chat";
                    if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                        resolvedZone = DesktopSemanticZone.ChatConversation;
                }
                else if (autoId.Equals("conversation", StringComparison.OrdinalIgnoreCase) ||
                         name.Equals("Agent Conversation", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.AuxiliarySidebar;
                    resolvedView ??= "Chat";
                    resolvedSection ??= "Conversation";
                    if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                        resolvedZone = DesktopSemanticZone.ChatConversation;
                }
                else if (cls.Contains("pane-header", StringComparison.OrdinalIgnoreCase) ||
                         (name.Contains("Section", StringComparison.OrdinalIgnoreCase) && (cls.Contains("pane") || cls.Contains("header"))))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.PrimarySidebar;
                    if (name.Contains("Timeline", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedView ??= "Explorer";
                        resolvedSection ??= "Timeline";
                        if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                            resolvedZone = DesktopSemanticZone.Timeline;
                    }
                    else if (name.Contains("Outline", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedView ??= "Explorer";
                        resolvedSection ??= "Outline";
                        if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                            resolvedZone = DesktopSemanticZone.Outline;
                    }
                    else if (name.StartsWith("Explorer Section:", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedView ??= "Explorer";
                        string parsed = name["Explorer Section:".Length..].Trim();
                        resolvedSection ??= string.IsNullOrEmpty(parsed) ? "Explorer" : parsed;
                        if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                            resolvedZone = DesktopSemanticZone.SidebarExplorer;
                    }
                    else if (name.EndsWith("Section", StringComparison.OrdinalIgnoreCase))
                    {
                        string parsed = name[..^"Section".Length].Trim();
                        resolvedSection ??= string.IsNullOrEmpty(parsed) ? name : parsed;
                    }
                }
                else if (autoId.Contains("workbench.parts.sidebar", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("part sidebar", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.PrimarySidebar;
                    if (autoId.Contains("workbench.view.explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedView ??= "Explorer";
                    }
                }
                else if (autoId.Contains("workbench.parts.activitybar", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("activitybar", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("codicon-explorer-view-icon", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Explorer (Ctrl+Shift+E)", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.ActivityBar;
                    resolvedView ??= "ActivityBar";
                    if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                        resolvedZone = DesktopSemanticZone.ActivityBar;
                }
                else if (autoId.Contains("workbench.parts.editor", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("monaco-editor", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("tabs-container", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("monaco-breadcrumbs", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("codicon-jetski-artifacts", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.MainContent;
                    resolvedView ??= "Editor";
                    if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown && !cls.Contains("scm-editor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (cls.Contains("monaco-breadcrumbs", StringComparison.OrdinalIgnoreCase))
                            resolvedZone = DesktopSemanticZone.NavigationPanel;
                        else if (cls.Contains("tab", StringComparison.OrdinalIgnoreCase) || cls.Contains("tabs-container", StringComparison.OrdinalIgnoreCase))
                            resolvedZone = DesktopSemanticZone.TabBar;
                        else
                            resolvedZone = DesktopSemanticZone.EditorBuffer;
                    }
                }
                else if (autoId.Contains("workbench.parts.panel", StringComparison.OrdinalIgnoreCase) ||
                         autoId.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("single-terminal-tab", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Focus Terminal", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("xterm", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.BottomPanel;
                    resolvedView ??= "Terminal";
                    if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                        resolvedZone = DesktopSemanticZone.Terminal;
                }
                else if (autoId.Contains("workbench.parts.statusbar", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("status-bar", StringComparison.OrdinalIgnoreCase) ||
                         cls.Contains("statusbar", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolvedPane == WindowPaneLocation.Unknown) resolvedPane = WindowPaneLocation.StatusBar;
                    resolvedView ??= "StatusBar";
                    if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                        resolvedZone = DesktopSemanticZone.StatusBar;
                }

                if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                {
                    resolvedZone = MapContainerToMacroZone(autoId, cls, name, cTypeId, archetype);
                }

                currentNative = parentNative;
            }
        }
        catch { }

        return (pathBuilder.ToImmutable(), classBuilder.ToImmutable(), resolvedZone, resolvedPane, resolvedView, resolvedSection);
    }

    private static bool IsNoiseWrapperClass(string cls)
    {
        return cls.Contains("view-lines", StringComparison.OrdinalIgnoreCase) ||
               cls.Contains("overflow-guard", StringComparison.OrdinalIgnoreCase) ||
               cls.Contains("monaco-scrollable-element", StringComparison.OrdinalIgnoreCase) ||
               cls.Contains("split-view-view", StringComparison.OrdinalIgnoreCase) ||
               cls.Contains("split-view-container", StringComparison.OrdinalIgnoreCase);
    }

    private static DesktopSemanticZone MapContainerToMacroZone(
        string autoId, string className, string name, int controlTypeId, DesktopAppArchetype archetype)
    {
        if (autoId.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("xterm", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Terminal", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.Terminal;
        }

        if (className.Contains("scm-editor-container", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("workbench.view.scm", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.GitCommitBox;
        }

        if (name.Contains("Timeline", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.Timeline;
        }

        if (name.Contains("Outline", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.Outline;
        }

        if (autoId.Equals("conversation", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Agent Conversation", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.ChatConversation;
        }

        if (autoId.Contains("antigravity.agentSidePanelInputBox", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("interactive-session", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("chat-input", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.ChatPrompt;
        }

        if (className.Contains("monaco-editor", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("editor-container", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("monaco-pane-view", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("native-edit-context", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.EditorBuffer;
        }

        if (autoId.Contains("quickInput", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("command-palette", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("quick-input", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.QuickOpen;
        }

        if (className.Contains("activitybar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("workbench.parts.activitybar", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.ActivityBar;
        }

        if (autoId.Contains("sidebar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("workbench.view", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("view-pane", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.SidebarExplorer;
        }

        if (className.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("tabbrowser-tabpanels", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("appcontent", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.WebDocument;
        }

        return DesktopSemanticZone.Unknown;
    }

    internal static DesktopSemanticZone ResolveSemanticZone(
        string controlType,
        string name,
        string autoId,
        string className,
        DesktopAppArchetype archetype,
        bool isOverlay = false)
    {
        if (isOverlay)
        {
            return DesktopSemanticZone.QuickOpen;
        }

        if (autoId.Contains("urlbar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("Address", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Address and search bar", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Search with Google or enter address", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.AddressBar;
        }

        if (name.Contains("Message (Ctrl+Enter to commit", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("scm.input", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("git-commit", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.GitCommitBox;
        }

        if (name.Contains("Message input", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Message history", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("chat-input", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("interactive-session", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("chat-input", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("interactive-session", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("voice memo", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Stop recording", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.ChatPrompt;
        }

        if (autoId.Equals("conversation", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Agent Conversation", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.ChatConversation;
        }

        if (autoId.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Terminal", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("terminal accessibility", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase) ||
            className.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase) ||
            (controlType.Equals("Document", StringComparison.OrdinalIgnoreCase) && className.Contains("terminal", StringComparison.OrdinalIgnoreCase)))
        {
            return DesktopSemanticZone.Terminal;
        }

        if (className.Contains("monaco-editor", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("native-edit-context", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.EditorBuffer;
        }

        if (name.Contains("Timeline Section", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("timeline", StringComparison.OrdinalIgnoreCase) ||
            (controlType.Equals("TreeItem", StringComparison.OrdinalIgnoreCase) && name.Equals("Timeline", StringComparison.OrdinalIgnoreCase)))
        {
            return DesktopSemanticZone.Timeline;
        }

        if (name.Contains("Outline Section", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("outline", StringComparison.OrdinalIgnoreCase) ||
            (controlType.Equals("TreeItem", StringComparison.OrdinalIgnoreCase) && name.Equals("Outline", StringComparison.OrdinalIgnoreCase)))
        {
            return DesktopSemanticZone.Outline;
        }

        if (className.Contains("activitybar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("workbench.parts.activitybar", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.ActivityBar;
        }

        if (archetype == DesktopAppArchetype.Gecko)
        {
            if (autoId.Contains("tabbrowser-tab", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("tabbrowser-tab", StringComparison.OrdinalIgnoreCase) ||
                className.Equals("tab", StringComparison.OrdinalIgnoreCase) ||
                controlType.Equals("TabItem", StringComparison.OrdinalIgnoreCase))
            {
                return DesktopSemanticZone.TabBar;
            }

            if (autoId.Contains("back-button", StringComparison.OrdinalIgnoreCase) ||
                autoId.Contains("forward-button", StringComparison.OrdinalIgnoreCase) ||
                autoId.Contains("reload-button", StringComparison.OrdinalIgnoreCase) ||
                autoId.Contains("PersonalToolbar", StringComparison.OrdinalIgnoreCase))
            {
                return DesktopSemanticZone.NavigationPanel;
            }
        }

        if (controlType.Equals("TreeItem", StringComparison.OrdinalIgnoreCase) ||
            controlType.Equals("Tree", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("sidebar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("explorer", StringComparison.OrdinalIgnoreCase) ||
            (archetype == DesktopAppArchetype.ChromiumElectron && name.Contains("Source Control", StringComparison.OrdinalIgnoreCase)) ||
            (archetype == DesktopAppArchetype.WinUI3Xaml && className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase)))
        {
            if (archetype == DesktopAppArchetype.Gecko)
            {
                if (controlType.Equals("Document", StringComparison.OrdinalIgnoreCase))
                    return DesktopSemanticZone.WebDocument;
                if (className.Contains("tab", StringComparison.OrdinalIgnoreCase) || autoId.Contains("tab", StringComparison.OrdinalIgnoreCase))
                    return DesktopSemanticZone.TabBar;
                return DesktopSemanticZone.SidebarExplorer;
            }

            return DesktopSemanticZone.SidebarExplorer;
        }

        if ((controlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase) ||
             controlType.Equals("List", StringComparison.OrdinalIgnoreCase) ||
             className.Contains("ItemsView", StringComparison.OrdinalIgnoreCase)) &&
            (archetype == DesktopAppArchetype.WinUI3Xaml || archetype == DesktopAppArchetype.ClassicWin32))
        {
            return DesktopSemanticZone.ShellItemList;
        }

        if (controlType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) ||
            controlType.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.TabBar;
        }

        if (autoId.Contains("status", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("status-bar", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("statusbar", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.StatusBar;
        }

        if (autoId.Contains("command-palette", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Command Palette", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.CommandPalette;
        }

        if (controlType.Equals("Document", StringComparison.OrdinalIgnoreCase) &&
            (archetype == DesktopAppArchetype.Gecko || archetype == DesktopAppArchetype.ChromiumElectron))
        {
            return DesktopSemanticZone.WebDocument;
        }

        if (className.Equals("#32770", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.SystemDialog;
        }

        return DesktopSemanticZone.Unknown;
    }

    internal static WindowPaneLocation InferPaneFromZone(DesktopSemanticZone zone)
    {
        return zone switch
        {
            DesktopSemanticZone.GitCommitBox => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.SidebarExplorer => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.Timeline => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.Outline => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.ShellItemList => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.NavigationPanel => WindowPaneLocation.PrimarySidebar,
            DesktopSemanticZone.EditorBuffer => WindowPaneLocation.MainContent,
            DesktopSemanticZone.WebDocument => WindowPaneLocation.MainContent,
            DesktopSemanticZone.ChatPrompt => WindowPaneLocation.AuxiliarySidebar,
            DesktopSemanticZone.ChatConversation => WindowPaneLocation.AuxiliarySidebar,
            DesktopSemanticZone.Terminal => WindowPaneLocation.BottomPanel,
            DesktopSemanticZone.ActivityBar => WindowPaneLocation.ActivityBar,
            DesktopSemanticZone.AddressBar => WindowPaneLocation.TopBar,
            DesktopSemanticZone.TabBar => WindowPaneLocation.TopBar,
            DesktopSemanticZone.StatusBar => WindowPaneLocation.StatusBar,
            DesktopSemanticZone.QuickOpen or DesktopSemanticZone.CommandPalette or DesktopSemanticZone.SystemDialog => WindowPaneLocation.OverlayModal,
            _ => WindowPaneLocation.Unknown
        };
    }

    internal static string? InferViewFromZone(DesktopSemanticZone zone)
    {
        return zone switch
        {
            DesktopSemanticZone.GitCommitBox => "SourceControl",
            DesktopSemanticZone.Timeline => "Explorer",
            DesktopSemanticZone.Outline => "Explorer",
            DesktopSemanticZone.SidebarExplorer => "Explorer",
            DesktopSemanticZone.ChatPrompt => "Chat",
            DesktopSemanticZone.ChatConversation => "Chat",
            DesktopSemanticZone.EditorBuffer => "Editor",
            DesktopSemanticZone.Terminal => "Terminal",
            DesktopSemanticZone.ActivityBar => "ActivityBar",
            DesktopSemanticZone.StatusBar => "StatusBar",
            DesktopSemanticZone.QuickOpen or DesktopSemanticZone.CommandPalette => "QuickOpen",
            DesktopSemanticZone.AddressBar => "NavigationBar",
            DesktopSemanticZone.TabBar => "TabStrip",
            DesktopSemanticZone.WebDocument => "WebDocument",
            DesktopSemanticZone.NavigationPanel => "NavigationBar",
            _ => null
        };
    }

    internal static string? InferSectionFromZone(DesktopSemanticZone zone)
    {
        return zone switch
        {
            DesktopSemanticZone.GitCommitBox => "CommitBox",
            DesktopSemanticZone.Timeline => "Timeline",
            DesktopSemanticZone.Outline => "Outline",
            DesktopSemanticZone.ChatPrompt => "ChatPrompt",
            DesktopSemanticZone.ChatConversation => "Conversation",
            _ => null
        };
    }

    internal static WindowPaneLocation InferPaneFromGeometry(BoundingRectangle windowBounds, BoundingRectangle controlBounds)
    {
        if (windowBounds.IsEmpty || windowBounds.Width <= 0 || windowBounds.Height <= 0 ||
            controlBounds.IsEmpty || controlBounds.Width <= 0 || controlBounds.Height <= 0)
        {
            return WindowPaneLocation.Unknown;
        }

        double relX = (controlBounds.Left - windowBounds.Left) / (double)windowBounds.Width;
        double relY = (controlBounds.Top - windowBounds.Top) / (double)windowBounds.Height;

        // Check status bar at bottom (height <= 35 and within 40px of bottom or relY >= 0.95)
        if (relY >= 0.95 || (controlBounds.Height <= 35 && (windowBounds.Bottom - controlBounds.Bottom) <= 40))
        {
            return WindowPaneLocation.StatusBar;
        }

        // Check bottom panel (e.g. terminal / output at bottom quadrant)
        if (relY >= 0.75)
        {
            return WindowPaneLocation.BottomPanel;
        }

        // Check top bar (e.g. tabs or title bar)
        if (relY < 0.05 && controlBounds.Height <= 45)
        {
            return WindowPaneLocation.TopBar;
        }

        // Check Activity Bar (narrow vertical rail on far-left)
        if (relX < 0.035 && controlBounds.Width <= 60)
        {
            return WindowPaneLocation.ActivityBar;
        }

        // Check Primary Sidebar (left ~30%)
        if (relX < 0.30)
        {
            return WindowPaneLocation.PrimarySidebar;
        }

        // Check Auxiliary Sidebar (right ~35%)
        if (relX >= 0.65)
        {
            return WindowPaneLocation.AuxiliarySidebar;
        }

        // Main content (center)
        if (relX >= 0.30 && relX < 0.65)
        {
            return WindowPaneLocation.MainContent;
        }

        return WindowPaneLocation.Unknown;
    }

    private static AutomationElement? SafeBindWindow(UIA3Automation automation, nint hwnd)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
            return null;

        try
        {
            return automation.FromHandle(hwnd);
        }
        catch (COMException ex) when (ex.HResult is unchecked((int)0x80040201) /* UIA_E_ELEMENTNOTAVAILABLE */ or
                                      unchecked((int)0x80070578) /* ERROR_INVALID_WINDOW_HANDLE */ or
                                      unchecked((int)0x80070005) /* E_ACCESSDENIED */ or
                                      unchecked((int)0x80004005) /* E_FAIL */)
        {
            return null;
        }
    }

    private static void ConfigureTransactionTimeouts(UIA3Automation automation, uint timeoutMs)
    {
        try
        {
            var native = automation.NativeAutomation;
            if (native is Interop.UIAutomationClient.IUIAutomation2 native2)
            {
                native2.TransactionTimeout = timeoutMs;
                native2.ConnectionTimeout = timeoutMs;
                native2.AutoSetFocus = 0;
            }
        }
        catch { }
    }

    private static DesktopContextSnapshot CreateShallowSnapshot(
        nint hwnd, string title, string className, int pid, string processName,
        DesktopAppArchetype archetype, BoundingRectangle bounds, double durationMs)
    {
        return new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.Empty,
                DesktopIndex = 0,
                VirtualDesktopName = "Current Desktop",
                MonitorIndex = 0,
                MonitorBounds = bounds
            },
            Window = new WindowEnvelope
            {
                Hwnd = hwnd,
                Title = title,
                ProcessName = processName,
                Pid = pid,
                ClassName = className,
                Archetype = archetype,
                Bounds = bounds,
                IsMinimized = false,
                IsMaximized = false
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Window",
                ElementName = title,
                AutomationId = string.Empty,
                ClassName = className,
                BoundingBox = bounds,
                SemanticZone = DesktopSemanticZone.Unknown
            },
            ExtractionDurationMs = durationMs
        };
    }

    private static DesktopContextSnapshot CreateEmptySnapshot(
        nint hwnd, string title, string className, int pid, string processName,
        DesktopAppArchetype archetype, double durationMs)
    {
        return CreateShallowSnapshot(hwnd, title, className, pid, processName, archetype, BoundingRectangle.Empty, durationMs);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _automation.Dispose();
            _disposed = true;
        }
    }
}
