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
    private bool _disposed;

    /// <summary>
    /// Gets or sets whether heuristic semantic zone resolution is enabled.
    /// When false, semantic zone heuristics are completely bypassed and set to Unknown/None,
    /// enabling pure explicit structural inspection.
    /// </summary>
    public bool EnableSemanticZones { get; set; } = true;

    public UiaExtractionEngine(IArchetypeClassifier? classifier = null)
    {
        _classifier = classifier ?? ArchetypeClassifier.Default;
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
        var focusInfo = ExtractFocusedControl(_automation, windowElement, pid, archetype, EnableSemanticZones);

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

    private static FocusedControlInfo ExtractFocusedControl(
        UIA3Automation automation,
        AutomationElement windowElement,
        int windowPid,
        DesktopAppArchetype archetype,
        bool enableSemanticZones = true)
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

                // Process Boundary Guard: Only accept focused element if it belongs to the active window's PID
                if (focusedPid == windowPid)
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

                    var (containerPath, containerClasses, ancestorZone) = ExtractAncestorHierarchy(
                        automation, focused, rootHwnd, windowPid, archetype, enableSemanticZones: enableSemanticZones);

                    var zone = enableSemanticZones ? ResolveSemanticZone(cType, name, autoId, cls, archetype, isOverlay) : DesktopSemanticZone.Unknown;
                    if (enableSemanticZones && zone == DesktopSemanticZone.Unknown && ancestorZone != DesktopSemanticZone.Unknown)
                    {
                        zone = ancestorZone;
                    }

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
                        ContainerPath = containerPath,
                        ContainerClasses = containerClasses,
                        IsOverlay = isOverlay,
                        ValueSnippet = sanitizedValue
                    };
                }
            }
        }
        catch { }

        return new FocusedControlInfo
        {
            ControlType = "Window",
            ElementName = windowElement.Properties.Name.ValueOrDefault ?? string.Empty,
            AutomationId = string.Empty,
            ClassName = windowElement.Properties.ClassName.ValueOrDefault ?? string.Empty,
            BoundingBox = BoundingRectangle.Empty,
            SemanticZone = DesktopSemanticZone.Unknown,
            ContainerPath = System.Collections.Immutable.ImmutableArray<string>.Empty,
            ContainerClasses = System.Collections.Immutable.ImmutableArray<string>.Empty,
            IsOverlay = false
        };
    }

    internal static (System.Collections.Immutable.ImmutableArray<string> Paths, System.Collections.Immutable.ImmutableArray<string> Classes, DesktopSemanticZone Zone) ExtractAncestorHierarchy(
        UIA3Automation automation,
        AutomationElement focusedElement,
        nint rootWindowHwnd,
        int expectedPid,
        DesktopAppArchetype archetype,
        int maxDepth = 3,
        bool enableSemanticZones = true)
    {
        var pathBuilder = System.Collections.Immutable.ImmutableArray.CreateBuilder<string>(maxDepth);
        var classBuilder = System.Collections.Immutable.ImmutableArray.CreateBuilder<string>(maxDepth);
        var resolvedZone = DesktopSemanticZone.Unknown;

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

                if (parentPid != expectedPid || (rootWindowHwnd != nint.Zero && parentHwnd == rootWindowHwnd))
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

                if (enableSemanticZones && resolvedZone == DesktopSemanticZone.Unknown)
                {
                    resolvedZone = MapContainerToMacroZone(autoId, cls, name, cTypeId, archetype);
                }

                currentNative = parentNative;
            }
        }
        catch { }

        return (pathBuilder.ToImmutable(), classBuilder.ToImmutable(), resolvedZone);
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

        if (className.Contains("monaco-editor", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("editor-container", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("monaco-pane-view", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("native-edit-context", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.EditorBuffer;
        }

        if (autoId.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("interactive-session", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("chat-input", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.ChatPrompt;
        }

        if (autoId.Contains("quickInput", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("command-palette", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("quick-input", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.QuickOpen;
        }

        if (autoId.Contains("sidebar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("workbench.view", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("activitybar", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("view-pane", StringComparison.OrdinalIgnoreCase))
        {
            if (archetype == DesktopAppArchetype.Gecko)
            {
                return DesktopSemanticZone.WebDocument;
            }

            return DesktopSemanticZone.NavigationPanel;
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
            name.Contains("Address and search bar", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.QuickOpen;
        }

        if (name.Contains("Message (Ctrl+Enter to commit", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("scm.input", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("git-commit", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.EditorBuffer;
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

        if (controlType.Equals("TreeItem", StringComparison.OrdinalIgnoreCase) ||
            controlType.Equals("Tree", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("sidebar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("explorer", StringComparison.OrdinalIgnoreCase) ||
            (archetype == DesktopAppArchetype.ChromiumElectron && name.Contains("Source Control", StringComparison.OrdinalIgnoreCase)) ||
            (archetype == DesktopAppArchetype.WinUI3Xaml && className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase)))
        {
            if (archetype == DesktopAppArchetype.Gecko)
            {
                return controlType.Equals("Document", StringComparison.OrdinalIgnoreCase)
                    ? DesktopSemanticZone.WebDocument
                    : DesktopSemanticZone.NavigationPanel;
            }

            return DesktopSemanticZone.NavigationPanel;
        }

        if ((controlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase) ||
             controlType.Equals("List", StringComparison.OrdinalIgnoreCase) ||
             className.Contains("ItemsView", StringComparison.OrdinalIgnoreCase)) &&
            (archetype == DesktopAppArchetype.WinUI3Xaml || archetype == DesktopAppArchetype.ClassicWin32))
        {
            return DesktopSemanticZone.NavigationPanel;
        }

        if (controlType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) ||
            controlType.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.NavigationPanel;
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
