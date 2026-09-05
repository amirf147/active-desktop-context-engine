// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Extraction.Classifiers;
using ADCE.Extraction.Extractors;
using ADCE.Extraction.Models;
using ADCE.Extraction.Resolvers;
using ADCE.Extraction.Security;
using ADCE.Extraction.Spatial;
using ADCE.Extraction.Win32;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace ADCE.Extraction.Engine;

/// <summary>
/// Production-grade UI Automation context extraction engine implementing IExtractionEngine.
/// Orchestrates fast Win32 gating, single-roundtrip FlaUI 5 tree harvesting, specialized
/// document extractors, and modular archetype semantic zone resolvers.
/// </summary>
public sealed class UiaExtractionEngine : IExtractionEngine, IDisposable
{
    private static readonly Dictionary<DesktopAppArchetype, IArchetypeZoneResolver> Resolvers = new()
    {
        [DesktopAppArchetype.ChromiumElectron] = ChromiumElectronZoneResolver.Instance,
        [DesktopAppArchetype.Gecko] = GeckoZoneResolver.Instance,
        [DesktopAppArchetype.WinUI3Xaml] = WinUi3XamlZoneResolver.Instance,
        [DesktopAppArchetype.ClassicWin32] = ClassicWin32ZoneResolver.Instance
    };

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

        // 0. Win32 Root Window Normalization: map child HWNDs to top-level window
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

        // 2. UIPI Gating: If target runs elevated and ADCE is standard user, return Win32 shallow context
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

        // 4. Extract Focus Target
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
            // Resilient degradation
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

                if (IsSameOrChildProcess(focusedPid, windowPid, processName))
                {
                    return ExtractControlInfoCore(
                        automation, windowElement, focused, windowPid, processName, archetype,
                        enableSemanticZones, ruleEngine, windowBounds);
                }
            }

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
        try { rootHwnd = windowElement.Properties.NativeWindowHandle.ValueOrDefault; } catch { }

        // Harvest Ancestor Chain via isolated harvester module
        var ancestors = AncestorChainHarvester.Harvest(automation, focused, rootHwnd, windowPid, processName);

        var descriptor = new FocusedControlDescriptor(cType, name, autoId, cls, boundingBox, isOverlay);

        WindowPaneLocation pane = WindowPaneLocation.Unknown;
        string? activeView = null;
        string? sectionName = null;
        var zone = DesktopSemanticZone.Unknown;

        if (enableSemanticZones)
        {
            // 1. Declarative Custom Rules take precedence
            var matchedRule = ruleEngine?.FindMatchingRule(processName, cType, name, autoId, cls, ancestors.ContainerPaths);
            if (matchedRule != null)
            {
                if (matchedRule.TargetZone != DesktopSemanticZone.Unknown) zone = matchedRule.TargetZone;
                if (matchedRule.TargetPane.HasValue && matchedRule.TargetPane.Value != WindowPaneLocation.Unknown) pane = matchedRule.TargetPane.Value;
                if (!string.IsNullOrEmpty(matchedRule.TargetView)) activeView = matchedRule.TargetView;
                if (!string.IsNullOrEmpty(matchedRule.TargetSection)) sectionName = matchedRule.TargetSection;
            }

            // 2. Archetype Strategy Resolver
            if (zone == DesktopSemanticZone.Unknown && Resolvers.TryGetValue(archetype, out var resolver))
            {
                if (resolver.TryResolve(descriptor, ancestors, out var res))
                {
                    zone = res.Zone;
                    pane = res.Pane;
                    activeView = res.ActiveView;
                    sectionName = res.SectionName;
                }
            }

            // 3. Fallback Heuristics
            if (zone == DesktopSemanticZone.Unknown)
            {
                zone = ResolveSemanticZone(cType, name, autoId, cls, archetype, isOverlay);
            }
        }

        // Infer pane from zone if still unknown
        if (pane == WindowPaneLocation.Unknown && zone != DesktopSemanticZone.Unknown)
        {
            pane = SemanticZoneInference.InferPaneFromZone(zone);
        }

        // Viewport Boundary Isolation for Web Documents
        bool isInsideWebDocument = zone == DesktopSemanticZone.WebDocument ||
                                   cType.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
                                   cls.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase) ||
                                   ancestors.ContainerClasses.Any(c => c.Contains("MozillaContentWindowClass", StringComparison.OrdinalIgnoreCase)) ||
                                   ancestors.ContainerPaths.Any(p => p.Contains("tabbrowser-tabpanels", StringComparison.OrdinalIgnoreCase) ||
                                                                     p.Contains("appcontent", StringComparison.OrdinalIgnoreCase));

        if (isInsideWebDocument)
        {
            pane = WindowPaneLocation.MainContent;
            if (zone == DesktopSemanticZone.Unknown) zone = DesktopSemanticZone.WebDocument;
            activeView ??= "WebDocument";
        }
        else if (pane == WindowPaneLocation.Unknown && !windowBounds.IsEmpty && !boundingBox.IsEmpty)
        {
            pane = SpatialPaneResolver.InferPaneFromGeometry(windowBounds, boundingBox);
        }

        activeView ??= SemanticZoneInference.InferViewFromZone(zone);
        sectionName ??= SemanticZoneInference.InferSectionFromZone(zone);

        // Assemble semantic path
        var pathBuilder = System.Collections.Immutable.ImmutableArray.CreateBuilder<string>(3);
        if (pane != WindowPaneLocation.Unknown) pathBuilder.Add(pane.ToString());
        if (!string.IsNullOrWhiteSpace(activeView)) pathBuilder.Add(activeView);
        if (!string.IsNullOrWhiteSpace(sectionName)) pathBuilder.Add(sectionName);
        var semanticPath = pathBuilder.ToImmutable();

        bool isPassword = false;
        try { isPassword = focused.Properties.IsPassword.ValueOrDefault; } catch { }

        string? value = null;
        try { value = focused.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault; } catch { }

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
            ContainerPath = ancestors.ContainerPaths,
            ContainerClasses = ancestors.ContainerClasses,
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

    #region Backwards-Compatible Static Facades

    public static DesktopSemanticZone ResolveSemanticZone(
        string controlType,
        string name,
        string autoId,
        string className,
        DesktopAppArchetype archetype,
        bool isOverlay = false)
    {
        var descriptor = new FocusedControlDescriptor(controlType, name, autoId, className, BoundingRectangle.Empty, isOverlay);
        if (Resolvers.TryGetValue(archetype, out var resolver) &&
            resolver.TryResolve(descriptor, AncestorChain.Empty, out var res))
        {
            return res.Zone;
        }

        // Generic cross-archetype fallbacks
        if (isOverlay) return DesktopSemanticZone.QuickOpen;
        if (controlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) || controlType.Equals("Menu", StringComparison.OrdinalIgnoreCase))
            return DesktopSemanticZone.NavigationPanel;
        if (controlType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) || controlType.Equals("Tab", StringComparison.OrdinalIgnoreCase))
            return DesktopSemanticZone.TabBar;
        if (className.Equals("#32770", StringComparison.OrdinalIgnoreCase))
            return DesktopSemanticZone.SystemDialog;
        if (controlType.Equals("Document", StringComparison.OrdinalIgnoreCase) &&
            (archetype == DesktopAppArchetype.Gecko || archetype == DesktopAppArchetype.ChromiumElectron))
            return DesktopSemanticZone.WebDocument;

        return DesktopSemanticZone.Unknown;
    }

    public static WindowPaneLocation InferPaneFromZone(DesktopSemanticZone zone) => SemanticZoneInference.InferPaneFromZone(zone);

    public static string? InferViewFromZone(DesktopSemanticZone zone) => SemanticZoneInference.InferViewFromZone(zone);

    public static string? InferSectionFromZone(DesktopSemanticZone zone) => SemanticZoneInference.InferSectionFromZone(zone);

    public static WindowPaneLocation InferPaneFromGeometry(BoundingRectangle windowBounds, BoundingRectangle controlBounds) =>
        SpatialPaneResolver.InferPaneFromGeometry(windowBounds, controlBounds);

    #endregion

    private static AutomationElement? SafeBindWindow(UIA3Automation automation, nint hwnd)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
            return null;

        try
        {
            return automation.FromHandle(hwnd);
        }
        catch (COMException ex) when (ex.HResult is unchecked((int)0x80040201) or
                                      unchecked((int)0x80070578) or
                                      unchecked((int)0x80070005) or
                                      unchecked((int)0x80004005))
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
