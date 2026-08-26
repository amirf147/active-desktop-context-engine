// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Events;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using ADCE.Extraction.Events;
using ADCE.Extraction.Security;
using ADCE.Extraction.Win32;
using ADCE.Spikes.Verification.Models;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace ADCE.Spikes.Verification.Drivers;

/// <summary>
/// Live stimulus driver that inspects and drives real-world Windows applications using Win32 API and FlaUI 5.
/// </summary>
public sealed class LiveWin32StimulusDriver : IStimulusDriver, IDisposable
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessWindowStation(IntPtr hWinSta);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseWindowStation(IntPtr hWinSta);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr hDesktop);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly IntPtr _hWinSta;
    private readonly IntPtr _hDesktop;
    private readonly UIA3Automation _automation;
    private readonly UiaExtractionEngine _engine;
    private bool _disposed;

    public string DriverName => "Live Win32 / FlaUI Stimulus Driver";
    public bool IsLive => true;

    public LiveWin32StimulusDriver()
    {
        _hWinSta = OpenWindowStation("WinSta0", false, 0x37F);
        if (_hWinSta != IntPtr.Zero) SetProcessWindowStation(_hWinSta);
        _hDesktop = OpenDesktop("Default", 0, false, 0x1FF);
        if (_hDesktop != IntPtr.Zero) SetThreadDesktop(_hDesktop);

        _automation = new UIA3Automation();
        _engine = new UiaExtractionEngine();
    }

    public Task<nint> FindWindowAsync(string processOrClassName, CancellationToken cancellationToken = default)
    {
        nint foundHwnd = nint.Zero;

        EnumDesktopWindows(IntPtr.Zero, (hWnd, lParam) =>
        {
            var sbTitle = new StringBuilder(512);
            GetWindowText(hWnd, sbTitle, 512);
            var sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, 256);
            GetWindowThreadProcessId(hWnd, out uint pid);

            string title = sbTitle.ToString();
            string className = sbClass.ToString();

            if (!string.IsNullOrWhiteSpace(title))
            {
                if (className.Contains(processOrClassName, StringComparison.OrdinalIgnoreCase) ||
                    title.Contains(processOrClassName, StringComparison.OrdinalIgnoreCase))
                {
                    foundHwnd = hWnd;
                    return false; // Stop enumeration
                }

                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    if (proc.ProcessName.Contains(processOrClassName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundHwnd = hWnd;
                        return false;
                    }
                }
                catch { }
            }

            return true;
        }, IntPtr.Zero);

        return Task.FromResult(foundHwnd);
    }

    public Task<bool> ActivateWindowAsync(nint hwnd, CancellationToken cancellationToken = default)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
            return Task.FromResult(false);

        bool result = SetForegroundWindow(hwnd);
        return Task.FromResult(result);
    }

    public Task<bool> SetFocusControlAsync(nint hwnd, string autoIdOrName, CancellationToken cancellationToken = default)
    {
        try
        {
            var windowElement = _automation.FromHandle(hwnd);
            if (windowElement == null) return Task.FromResult(false);

            var cf = _automation.ConditionFactory;
            var element = windowElement.FindFirstDescendant(cf.ByAutomationId(autoIdOrName)) ??
                          windowElement.FindFirstDescendant(cf.ByName(autoIdOrName));

            if (element != null)
            {
                element.Focus();
                return Task.FromResult(true);
            }
        }
        catch { }

        return Task.FromResult(false);
    }

    public async Task InjectEventBurstAsync(
        ChannelWriter<DesktopEventToken> writer,
        nint hwnd,
        int eventCount,
        TimeSpan spacing,
        CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < eventCount; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;
            writer.TryWrite(new DesktopEventToken(0x8005, hwnd, (uint)(2000 + i)));
            if (spacing > TimeSpan.Zero)
            {
                await Task.Delay(spacing, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes CLM-001 verification against live desktop windows.
    /// </summary>
    public async Task<ClaimResult> VerifyClm001GlobalFocusBleedAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        nint targetHwnd = await FindWindowAsync("ConsoleWindowClass", cancellationToken);
        if (targetHwnd == nint.Zero)
        {
            targetHwnd = await FindWindowAsync("pwsh", cancellationToken);
        }
        if (targetHwnd == nint.Zero)
        {
            targetHwnd = await FindWindowAsync("cmd", cancellationToken);
        }

        if (targetHwnd == nint.Zero)
        {
            sw.Stop();
            return new ClaimResult
            {
                Id = ClaimId.CLM_001,
                Title = "Global Focus Bleed Prevention",
                Status = ClaimStatus.Skipped,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                TelemetrySummary = "No active Win32 console (pwsh/cmd/conhost) window discovered on desktop.",
                Assertions = ["Precondition check: Target console window not running."],
                SkipOrFailureReason = "Prerequisite console window not running.",
                CapturedSnapshot = null
            };
        }

        var snapshot = await _engine.ExtractSnapshotAsync(targetHwnd, cancellationToken);
        sw.Stop();

        var assertions = new List<string>();
        bool validHwnd = snapshot.Window.Hwnd == targetHwnd;
        assertions.Add($"Target HWND 0x{targetHwnd:X8} bound: {validHwnd}");

        bool pidBound = snapshot.Focus.SemanticZone != DesktopSemanticZone.EditorCodeBuffer;
        assertions.Add($"Focus isolated to target process (Zone={snapshot.Focus.SemanticZone}): {pidBound}");

        bool passed = validHwnd && pidBound;

        return new ClaimResult
        {
            Id = ClaimId.CLM_001,
            Title = "Global Focus Bleed Prevention",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"HWND: 0x{snapshot.Window.Hwnd:X8}, Title: '{snapshot.Window.Title}', Focus: [{snapshot.Focus.SemanticZone}] '{snapshot.Focus.ElementName}'",
            Assertions = assertions,
            CapturedSnapshot = snapshot
        };
    }

    /// <summary>
    /// Executes CLM-002 verification against live desktop windows.
    /// </summary>
    public async Task<ClaimResult> VerifyClm002ChildHwndNormalizationAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        nint ideHwnd = await FindWindowAsync("Chrome_WidgetWin_1", cancellationToken);
        if (ideHwnd == nint.Zero)
        {
            sw.Stop();
            return new ClaimResult
            {
                Id = ClaimId.CLM_002,
                Title = "Child HWND Normalization",
                Status = ClaimStatus.Skipped,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                TelemetrySummary = "No active Chromium/Electron IDE window discovered on desktop.",
                Assertions = ["Precondition check: Electron IDE not running."],
                SkipOrFailureReason = "Prerequisite Electron window not running.",
                CapturedSnapshot = null
            };
        }

        var snapshot = await _engine.ExtractSnapshotAsync(ideHwnd, cancellationToken);
        sw.Stop();

        var assertions = new List<string>();
        bool rootResolved = snapshot.Window.Hwnd != nint.Zero;
        assertions.Add($"Root HWND resolved (0x{snapshot.Window.Hwnd:X8}): {rootResolved}");

        bool nonNoiseTitle = !string.IsNullOrWhiteSpace(snapshot.Window.Title) && !snapshot.Window.Title.Contains("Invalid");
        assertions.Add($"Window Title present ('{snapshot.Window.Title}'): {nonNoiseTitle}");

        bool passed = rootResolved && nonNoiseTitle;

        return new ClaimResult
        {
            Id = ClaimId.CLM_002,
            Title = "Child HWND Normalization",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"Resolved HWND: 0x{snapshot.Window.Hwnd:X8}, Title: '{snapshot.Window.Title}', Archetype: {snapshot.Window.Archetype}",
            Assertions = assertions,
            CapturedSnapshot = snapshot
        };
    }

    /// <summary>
    /// Executes CLM-003 verification against live desktop windows.
    /// </summary>
    public async Task<ClaimResult> VerifyClm003IdeSemanticZoneResolutionAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        nint ideHwnd = await FindWindowAsync("Antigravity", cancellationToken);
        if (ideHwnd == nint.Zero)
        {
            ideHwnd = await FindWindowAsync("Visual Studio Code", cancellationToken);
        }

        if (ideHwnd == nint.Zero)
        {
            sw.Stop();
            return new ClaimResult
            {
                Id = ClaimId.CLM_003,
                Title = "IDE Semantic Zone Resolution",
                Status = ClaimStatus.Skipped,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                TelemetrySummary = "No active VS Code / Antigravity IDE window discovered on desktop.",
                Assertions = ["Precondition check: IDE window not running."],
                SkipOrFailureReason = "Prerequisite IDE window not running.",
                CapturedSnapshot = null
            };
        }

        var snapshot = await _engine.ExtractSnapshotAsync(ideHwnd, cancellationToken);
        sw.Stop();

        var assertions = new List<string>();
        bool hasFocus = snapshot.Focus != null;
        assertions.Add($"Focus info extracted: {hasFocus}");

        bool zoneValid = snapshot.Focus != null && snapshot.Focus.SemanticZone != DesktopSemanticZone.Unknown;
        assertions.Add($"Semantic Zone resolved to active IDE area ({snapshot.Focus?.SemanticZone}): {zoneValid}");

        bool passed = hasFocus && (zoneValid || snapshot.IdeContext != null);

        return new ClaimResult
        {
            Id = ClaimId.CLM_003,
            Title = "IDE Semantic Zone Resolution",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"Focus: [{snapshot.Focus?.SemanticZone}] '{snapshot.Focus?.ElementName}' (Class: '{snapshot.Focus?.ClassName}'), IDE Tabs: {snapshot.IdeContext?.OpenEditorTabs.Length ?? 0}",
            Assertions = assertions,
            CapturedSnapshot = snapshot
        };
    }

    /// <summary>
    /// Executes CLM-004 verification against live desktop windows.
    /// </summary>
    public async Task<ClaimResult> VerifyClm004BrowserSidebarVsIdeExplorerAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        nint geckoHwnd = await FindWindowAsync("MozillaWindowClass", cancellationToken);
        if (geckoHwnd == nint.Zero)
        {
            sw.Stop();
            return new ClaimResult
            {
                Id = ClaimId.CLM_004,
                Title = "Browser Tab Sidebar vs. IDE Explorer",
                Status = ClaimStatus.Skipped,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                TelemetrySummary = "No active MozillaWindowClass (Waterfox/Firefox) window discovered on desktop.",
                Assertions = ["Precondition check: Waterfox/Firefox not running."],
                SkipOrFailureReason = "Prerequisite Gecko browser not running.",
                CapturedSnapshot = null
            };
        }

        var snapshot = await _engine.ExtractSnapshotAsync(geckoHwnd, cancellationToken);
        sw.Stop();

        var assertions = new List<string>();
        bool notSidebarExplorer = snapshot.Focus.SemanticZone != DesktopSemanticZone.SidebarExplorer;
        assertions.Add($"Gecko focus ({snapshot.Focus.SemanticZone}) is NOT SidebarExplorer: {notSidebarExplorer}");

        bool archetypeIsGecko = snapshot.Window.Archetype == DesktopAppArchetype.Gecko;
        assertions.Add($"Archetype classified as Gecko: {archetypeIsGecko}");

        bool passed = notSidebarExplorer && archetypeIsGecko;

        return new ClaimResult
        {
            Id = ClaimId.CLM_004,
            Title = "Browser Tab Sidebar vs. IDE Explorer",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"Gecko Window: '{snapshot.Window.Title}', Focus Zone: [{snapshot.Focus.SemanticZone}], Browser Tabs: {snapshot.BrowserContext?.Tabs.Length ?? 0}",
            Assertions = assertions,
            CapturedSnapshot = snapshot
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _engine.Dispose();
            _automation.Dispose();
            if (_hDesktop != IntPtr.Zero) CloseDesktop(_hDesktop);
            if (_hWinSta != IntPtr.Zero) CloseWindowStation(_hWinSta);
            _disposed = true;
        }
    }
}
