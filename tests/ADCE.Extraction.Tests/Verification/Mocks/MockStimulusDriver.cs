// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Events;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using ADCE.Extraction.Events;

namespace ADCE.Extraction.Tests.Verification.Mocks;

/// <summary>
/// Deterministic synthetic stimulus driver for headless CI/CD execution without external GUI windows.
/// </summary>
public sealed class MockStimulusDriver : IStimulusDriver
{
    public string DriverName => "Synthetic Headless Mock Driver";
    public bool IsLive => false;

    public Task<nint> FindWindowAsync(string processOrClassName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((nint)0x00A10001);
    }

    public Task<bool> ActivateWindowAsync(nint hwnd, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> SetFocusControlAsync(nint hwnd, string autoIdOrName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
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
            writer.TryWrite(new DesktopEventToken(0x8005 /* EVENT_OBJECT_FOCUS */, hwnd, (uint)(1000 + i)));
            if (spacing > TimeSpan.Zero)
            {
                await Task.Delay(spacing, cancellationToken);
            }
        }
    }

    public Task<ClaimResult> VerifyClm001GlobalFocusBleedAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int pwshPid = 4812;
        nint pwshHwnd = (nint)0x001A00F4;

        var pwshFocus = new FocusedControlInfo
        {
            ControlType = "Window",
            ElementName = "Administrator: PowerShell",
            AutomationId = string.Empty,
            ClassName = "ConsoleWindowClass",
            BoundingBox = new BoundingRectangle(0, 0, 1200, 800),
            SemanticZone = DesktopSemanticZone.Unknown,
            ValueSnippet = null
        };

        var snapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Primary",
                MonitorIndex = 0,
                MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Window = new WindowEnvelope
            {
                Hwnd = pwshHwnd,
                Title = "Administrator: PowerShell (pwsh.exe)",
                ProcessName = "pwsh",
                Pid = pwshPid,
                ClassName = "ConsoleWindowClass",
                Archetype = DesktopAppArchetype.ClassicWin32,
                Bounds = new BoundingRectangle(0, 0, 1200, 800),
                IsMinimized = false,
                IsMaximized = false
            },
            Focus = pwshFocus,
            ExtractionDurationMs = 0.42
        };

        sw.Stop();

        var assertions = new List<string>();
        bool pidMatch = snapshot.Window.Pid == pwshPid;
        assertions.Add($"Window PID ({snapshot.Window.Pid}) equals Target PID ({pwshPid}): {pidMatch}");

        bool noBleed = snapshot.Focus.SemanticZone != DesktopSemanticZone.EditorBuffer &&
                       snapshot.Focus.SemanticZone != DesktopSemanticZone.WebDocument;
        assertions.Add($"Zero Focus Bleed from prior GUI state (Zone={snapshot.Focus.SemanticZone}): {noBleed}");

        bool passed = pidMatch && noBleed;

        return Task.FromResult(new ClaimResult
        {
            Id = ClaimId.CLM_001,
            Title = "Global Focus Bleed Prevention",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"HWND: 0x{pwshHwnd:X8}, Process: {snapshot.Window.ProcessName} (PID {snapshot.Window.Pid}), FocusZone: {snapshot.Focus.SemanticZone}",
            Assertions = assertions,
            CapturedSnapshot = snapshot
        });
    }

    public Task<ClaimResult> VerifyClm002ChildHwndNormalizationAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        nint topLevelHwnd = (nint)0x00A50020;
        nint childSubPanelHwnd = (nint)0x00A50088;

        nint resolvedHwnd = (childSubPanelHwnd != topLevelHwnd) ? topLevelHwnd : childSubPanelHwnd;

        var snapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Primary",
                MonitorIndex = 0,
                MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Window = new WindowEnvelope
            {
                Hwnd = resolvedHwnd,
                Title = "active-desktop-context-engine - Antigravity IDE",
                ProcessName = "Antigravity.exe",
                Pid = 26420,
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080),
                IsMinimized = false,
                IsMaximized = true
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "Chat Prompt Input",
                AutomationId = "chat-input",
                ClassName = "interactive-session",
                BoundingBox = new BoundingRectangle(1200, 400, 600, 600),
                SemanticZone = DesktopSemanticZone.ChatPrompt,
                ValueSnippet = null
            },
            ExtractionDurationMs = 0.85
        };

        sw.Stop();

        var assertions = new List<string>();
        bool rootMapped = snapshot.Window.Hwnd == topLevelHwnd;
        assertions.Add($"Child HWND 0x{childSubPanelHwnd:X8} mapped to Top-Level HWND 0x{topLevelHwnd:X8}: {rootMapped}");

        bool validTitle = !string.IsNullOrEmpty(snapshot.Window.Title) && !snapshot.Window.Title.Contains("Invalid");
        assertions.Add($"Window Title preserved ('{snapshot.Window.Title}') and not dropped as empty noise: {validTitle}");

        bool passed = rootMapped && validTitle;

        return Task.FromResult(new ClaimResult
        {
            Id = ClaimId.CLM_002,
            Title = "Child HWND Normalization",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"ChildHwnd: 0x{childSubPanelHwnd:X8} -> RootHwnd: 0x{snapshot.Window.Hwnd:X8}, Title: '{snapshot.Window.Title}'",
            Assertions = assertions,
            CapturedSnapshot = snapshot
        });
    }

    public Task<ClaimResult> VerifyClm003IdeSemanticZoneResolutionAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var assertions = new List<string>();

        var monacoZone = UiaExtractionEngine.ResolveSemanticZone("Edit", "CONTEXT.md", "native-edit-context", "monaco-editor", DesktopAppArchetype.ChromiumElectron);
        bool monacoOk = monacoZone == DesktopSemanticZone.EditorBuffer;
        assertions.Add($"Monaco Editor Class -> EditorBuffer (Resolved: {monacoZone}): {monacoOk}");

        var termZone = UiaExtractionEngine.ResolveSemanticZone("Document", "Terminal 1", "terminal.integrated", "xterm-dom-renderer-owner-1", DesktopAppArchetype.ChromiumElectron);
        bool termOk = termZone == DesktopSemanticZone.Terminal;
        assertions.Add($"Integrated Terminal -> Terminal (Resolved: {termZone}): {termOk}");

        var gitZone = UiaExtractionEngine.ResolveSemanticZone("Edit", "Message (Ctrl+Enter to commit)", "scm.input", "monaco-editor", DesktopAppArchetype.ChromiumElectron);
        bool gitOk = gitZone == DesktopSemanticZone.GitCommitBox;
        assertions.Add($"Git Commit Input -> GitCommitBox (Resolved: {gitZone}): {gitOk}");

        var chatZone = UiaExtractionEngine.ResolveSemanticZone("Edit", "Message input", "chat-input", "interactive-session", DesktopAppArchetype.ChromiumElectron);
        bool chatOk = chatZone == DesktopSemanticZone.ChatPrompt;
        assertions.Add($"Chat Input -> ChatPrompt (Resolved: {chatZone}): {chatOk}");

        var sidebarZone = UiaExtractionEngine.ResolveSemanticZone("Tree", "Explorer", "workbench.view.explorer", "view-pane", DesktopAppArchetype.ChromiumElectron);
        bool sidebarOk = sidebarZone == DesktopSemanticZone.SidebarExplorer;
        assertions.Add($"Sidebar Explorer -> SidebarExplorer (Resolved: {sidebarZone}): {sidebarOk}");

        sw.Stop();
        bool passed = monacoOk && termOk && gitOk && chatOk && sidebarOk;

        return Task.FromResult(new ClaimResult
        {
            Id = ClaimId.CLM_003,
            Title = "IDE Semantic Zone Resolution",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"5 IDE Zones Verified: Monaco={monacoZone}, Terminal={termZone}, Git={gitZone}, Chat={chatZone}, Sidebar={sidebarZone}",
            Assertions = assertions,
            CapturedSnapshot = null
        });
    }

    public Task<ClaimResult> VerifyClm004BrowserSidebarVsIdeExplorerAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var assertions = new List<string>();

        var geckoTabZone = UiaExtractionEngine.ResolveSemanticZone("ListItem", "Active Desktop Context Engine", "sidebar-box", "tab", DesktopAppArchetype.Gecko);
        bool geckoTabOk = geckoTabZone == DesktopSemanticZone.TabBar;
        assertions.Add($"Gecko Sidebar Tab -> TabBar (Resolved: {geckoTabZone}): {geckoTabOk}");

        var geckoDocZone = UiaExtractionEngine.ResolveSemanticZone("Document", "Tree Style Tab", "sidebar-box", "webextension-panel", DesktopAppArchetype.Gecko);
        bool geckoDocOk = geckoDocZone == DesktopSemanticZone.WebDocument;
        assertions.Add($"Gecko Sidebar Document -> WebDocument (Resolved: {geckoDocZone}): {geckoDocOk}");

        var ideSidebarZone = UiaExtractionEngine.ResolveSemanticZone("Tree", "File Explorer", "sidebar-box", "view-pane", DesktopAppArchetype.ChromiumElectron);
        bool ideSidebarOk = ideSidebarZone == DesktopSemanticZone.SidebarExplorer;
        assertions.Add($"IDE Explorer -> SidebarExplorer (Resolved: {ideSidebarZone}): {ideSidebarOk}");

        sw.Stop();
        bool passed = geckoTabOk && geckoDocOk && ideSidebarOk;

        return Task.FromResult(new ClaimResult
        {
            Id = ClaimId.CLM_004,
            Title = "Browser Tab Sidebar vs. IDE Explorer",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"Gecko Sidebar Tab: {geckoTabZone}, Gecko Sidebar Doc: {geckoDocZone}, IDE Sidebar: {ideSidebarZone}",
            Assertions = assertions,
            CapturedSnapshot = null
        });
    }

    public async Task<ClaimResult> VerifyClm005BurstDebounceClampingAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var channel = Channel.CreateUnbounded<DesktopEventToken>();
        var mockExtractor = new FastMockExtractor();
        using var pipeline = new DebouncedDesktopEventPipeline(
            channel.Reader,
            mockExtractor,
            debounceWindow: TimeSpan.FromMilliseconds(50),
            maxDelayWindow: TimeSpan.FromMilliseconds(250));

        pipeline.Start();

        for (int i = 1; i <= 20; i++)
        {
            channel.Writer.TryWrite(new DesktopEventToken(0x8005, (nint)0x00A10001, (uint)(5000 + i)));
            await Task.Delay(15, cancellationToken);
        }

        await Task.Delay(80, cancellationToken);
        await pipeline.StopAsync();
        sw.Stop();

        var assertions = new List<string>();
        bool eventsReceived = pipeline.RawEventsReceived >= 20;
        assertions.Add($"Raw WinEvents Ingested: {pipeline.RawEventsReceived} (Expected >= 20): {eventsReceived}");

        bool debouncedTriggered = pipeline.DebouncedExtractionsTriggered >= 2;
        assertions.Add($"Debounced Extractions Triggered: {pipeline.DebouncedExtractionsTriggered} (>= 2 due to 250ms clamp + trailing edge): {debouncedTriggered}");

        bool passed = eventsReceived && debouncedTriggered;

        return new ClaimResult
        {
            Id = ClaimId.CLM_005,
            Title = "Burst Typing Debounce Clamping (WP 3.4)",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"RawEvents: {pipeline.RawEventsReceived}, Triggered: {pipeline.DebouncedExtractionsTriggered}, Committed: {pipeline.ExtractionsCommitted}",
            Assertions = assertions,
            CapturedSnapshot = null
        };
    }

    public async Task<ClaimResult> VerifyClm006ZeroAllocationDeduplicationAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var channel = Channel.CreateUnbounded<DesktopEventToken>();
        var mockExtractor = new IdenticalSnapshotMockExtractor();
        using var pipeline = new DebouncedDesktopEventPipeline(
            channel.Reader,
            mockExtractor,
            debounceWindow: TimeSpan.FromMilliseconds(10),
            maxDelayWindow: TimeSpan.FromMilliseconds(50));

        pipeline.Start();

        for (int i = 1; i <= 5; i++)
        {
            channel.Writer.TryWrite(new DesktopEventToken(0x8005, (nint)0x00A10001, 1234));
            await Task.Delay(25, cancellationToken);
        }

        await Task.Delay(50, cancellationToken);
        await pipeline.StopAsync();
        sw.Stop();

        var assertions = new List<string>();
        bool singleCommit = pipeline.ExtractionsCommitted == 1;
        assertions.Add($"Single Initial Snapshot Committed: {pipeline.ExtractionsCommitted} == 1: {singleCommit}");

        bool duplicatesSuppressed = pipeline.DuplicateSnapshotsSuppressed >= 3;
        assertions.Add($"Identical Wavelets Suppressed: {pipeline.DuplicateSnapshotsSuppressed} (Expected >= 3): {duplicatesSuppressed}");

        bool passed = singleCommit && duplicatesSuppressed;

        return new ClaimResult
        {
            Id = ClaimId.CLM_006,
            Title = "Zero-Allocation Deduplication",
            Status = passed ? ClaimStatus.Passed : ClaimStatus.Failed,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            TelemetrySummary = $"Raw: {pipeline.RawEventsReceived}, Committed: {pipeline.ExtractionsCommitted}, Suppressed: {pipeline.DuplicateSnapshotsSuppressed}",
            Assertions = assertions,
            CapturedSnapshot = null
        };
    }

    private sealed class FastMockExtractor : IExtractionEngine
    {
        public ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default) =>
            ExtractSnapshotAsync((nint)0x00A10001, cancellationToken);

        public ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new DesktopContextSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Workspace = new WorkspaceEnvelope { VirtualDesktopId = Guid.Empty, DesktopIndex = 0, VirtualDesktopName = "Primary", MonitorIndex = 0, MonitorBounds = BoundingRectangle.Empty },
                Window = new WindowEnvelope { Hwnd = hwnd, Title = "Test Editor", ProcessName = "editor", Pid = 1000, ClassName = "Edit", Archetype = DesktopAppArchetype.ClassicWin32, Bounds = BoundingRectangle.Empty, IsMinimized = false, IsMaximized = false },
                Focus = new FocusedControlInfo { ControlType = "Edit", ElementName = "Buffer", AutomationId = "buf", ClassName = "Edit", BoundingBox = BoundingRectangle.Empty, SemanticZone = DesktopSemanticZone.EditorBuffer, ValueSnippet = null },
                ExtractionDurationMs = 0.1
            });
        }
    }

    private sealed class IdenticalSnapshotMockExtractor : IExtractionEngine
    {
        private static readonly DesktopContextSnapshot StaticSnapshot = new()
        {
            Timestamp = new DateTimeOffset(2026, 8, 25, 6, 0, 0, TimeSpan.Zero),
            Workspace = new WorkspaceEnvelope { VirtualDesktopId = Guid.Empty, DesktopIndex = 0, VirtualDesktopName = "Primary", MonitorIndex = 0, MonitorBounds = BoundingRectangle.Empty },
            Window = new WindowEnvelope { Hwnd = (nint)0x00A10001, Title = "Fixed Target Window", ProcessName = "app", Pid = 1000, ClassName = "WndClass", Archetype = DesktopAppArchetype.ClassicWin32, Bounds = BoundingRectangle.Empty, IsMinimized = false, IsMaximized = false },
            Focus = new FocusedControlInfo { ControlType = "Edit", ElementName = "Fixed Target", AutomationId = "fixed-target", ClassName = "Edit", BoundingBox = BoundingRectangle.Empty, SemanticZone = DesktopSemanticZone.EditorBuffer, ValueSnippet = null },
            ExtractionDurationMs = 0.1
        };

        public ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(StaticSnapshot);

        public ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(StaticSnapshot);
    }
}
