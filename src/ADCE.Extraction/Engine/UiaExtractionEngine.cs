// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

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

        // 4. Extract Focus Target
        var focusInfo = ExtractFocusedControl(_automation, windowElement);

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

    private static FocusedControlInfo ExtractFocusedControl(UIA3Automation automation, AutomationElement windowElement)
    {
        try
        {
            var focused = automation.FocusedElement();
            if (focused != null)
            {
                string cType = focused.Properties.ControlType.ValueOrDefault.ToString();
                string name = focused.Properties.Name.ValueOrDefault ?? string.Empty;
                string autoId = focused.Properties.AutomationId.ValueOrDefault ?? string.Empty;
                string cls = focused.Properties.ClassName.ValueOrDefault ?? string.Empty;
                var rect = focused.Properties.BoundingRectangle.ValueOrDefault;

                var boundingBox = rect.IsEmpty ? BoundingRectangle.Empty :
                    new BoundingRectangle((int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height);

                var zone = ResolveSemanticZone(cType, name, autoId, cls);

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
                    ValueSnippet = sanitizedValue
                };
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
            SemanticZone = DesktopSemanticZone.Unknown
        };
    }

    private static DesktopSemanticZone ResolveSemanticZone(string controlType, string name, string autoId, string className)
    {
        if (className.Contains("monaco-editor", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("native-edit-context", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.EditorCodeBuffer;
        }

        if (autoId.Contains("urlbar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("Address", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.AddressBar;
        }

        if (name.Contains("Message (Ctrl+Enter to commit", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("scm.input", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.GitCommitBox;
        }

        if (autoId.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
            controlType.Equals("Document", StringComparison.OrdinalIgnoreCase) && className.Contains("terminal", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.IntegratedTerminal;
        }

        if (autoId.Contains("sidebar", StringComparison.OrdinalIgnoreCase) ||
            autoId.Contains("explorer", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.SidebarExplorer;
        }

        if (controlType.Equals("TabItem", StringComparison.OrdinalIgnoreCase) ||
            controlType.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSemanticZone.TabBar;
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
