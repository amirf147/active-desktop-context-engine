// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using Xunit;

namespace ADCE.Extraction.Tests;

public class UiaExtractionEngineTests
{
    [Fact]
    public async Task ExtractSnapshotAsync_HandlesInvalidHwndGracefully()
    {
        using var engine = new UiaExtractionEngine();
        var snapshot = await engine.ExtractSnapshotAsync(nint.Zero);

        Assert.NotNull(snapshot);
        Assert.Equal((nint)0, snapshot.Window.Hwnd);
        Assert.Equal(DesktopAppArchetype.Unknown, snapshot.Window.Archetype);
        Assert.Null(snapshot.IdeContext);
        Assert.Null(snapshot.BrowserContext);
        Assert.Null(snapshot.ExplorerContext);
        Assert.Null(snapshot.TerminalContext);
    }

    [Fact]
    public async Task ExtractForegroundSnapshotAsync_ReturnsValidSnapshotInstance()
    {
        using var engine = new UiaExtractionEngine();
        var snapshot = await engine.ExtractForegroundSnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Window);
        Assert.NotNull(snapshot.Workspace);
        Assert.NotNull(snapshot.Focus);
        Assert.True(snapshot.ExtractionDurationMs >= 0.0);
    }

    [Theory]
    [InlineData("Edit", "Message (Ctrl+Enter to commit)", "scm.input", "monaco-editor", DesktopAppArchetype.ChromiumElectron, false, DesktopSemanticZone.GitCommitBox)]
    [InlineData("Document", "Terminal 1", "workbench.action.terminal.focus", "xterm", DesktopAppArchetype.ChromiumElectron, false, DesktopSemanticZone.Terminal)]
    [InlineData("Edit", "Type a message", "chat-input", "interactive-session", DesktopAppArchetype.ChromiumElectron, false, DesktopSemanticZone.ChatPrompt)]
    [InlineData("Edit", "Search box", "quickInput", "quick-input-widget", DesktopAppArchetype.ChromiumElectron, true, DesktopSemanticZone.QuickOpen)]
    [InlineData("TreeItem", "Source Control", "workbench.view.scm", "monaco-list", DesktopAppArchetype.ChromiumElectron, false, DesktopSemanticZone.SidebarExplorer)]
    [InlineData("Document", "Mozilla Firefox", "urlbar-input", "MozillaWindowClass", DesktopAppArchetype.Gecko, false, DesktopSemanticZone.AddressBar)]
    public void ResolveSemanticZone_MapsToExpectedMacroAnchors(
        string cType, string name, string autoId, string className, DesktopAppArchetype archetype, bool isOverlay, DesktopSemanticZone expected)
    {
        var zone = UiaExtractionEngine.ResolveSemanticZone(cType, name, autoId, className, archetype, isOverlay);
        Assert.Equal(expected, zone);
    }

    [Fact]
    public void ExtractionEngine_CanToggleSemanticZones()
    {
        using var engine = new UiaExtractionEngine();
        Assert.True(engine.EnableSemanticZones);

        engine.EnableSemanticZones = false;
        Assert.False(engine.EnableSemanticZones);
    }

    [Fact]
    public async Task ExtractSnapshotAsync_EnrichesWorkspace_FromInjectedWorkspaceManager()
    {
        var expectedGuid = Guid.NewGuid();
        var customEnvelope = new WorkspaceEnvelope
        {
            VirtualDesktopId = expectedGuid,
            DesktopIndex = 2,
            VirtualDesktopName = "Workspace-Code",
            MonitorIndex = 1,
            MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
        };

        var mockManager = new MockWorkspaceManager(customEnvelope);
        using var engine = new UiaExtractionEngine(workspaceManager: mockManager);

        var snapshot = await engine.ExtractSnapshotAsync(nint.Zero);

        Assert.NotNull(snapshot.Workspace);
        Assert.Equal(expectedGuid, snapshot.Workspace.VirtualDesktopId);
        Assert.Equal(2, snapshot.Workspace.DesktopIndex);
        Assert.Equal("Workspace-Code", snapshot.Workspace.VirtualDesktopName);
    }

    [Fact]
    public async Task ExtractSnapshotAsync_FallsBackGracefully_OnWorkspaceManagerFailure()
    {
        var failingManager = new MockWorkspaceManager(new WorkspaceEnvelope { VirtualDesktopId = Guid.Empty, DesktopIndex = 0 }, shouldThrow: true);
        using var engine = new UiaExtractionEngine(workspaceManager: failingManager);

        var snapshot = await engine.ExtractSnapshotAsync(nint.Zero);

        Assert.NotNull(snapshot.Workspace);
        Assert.Equal(Guid.Empty, snapshot.Workspace.VirtualDesktopId);
        Assert.Equal("Current Desktop", snapshot.Workspace.VirtualDesktopName);
    }

    private sealed class MockWorkspaceManager : IWorkspaceManager
    {
        private readonly WorkspaceEnvelope _envelope;
        private readonly bool _shouldThrow;

        public MockWorkspaceManager(WorkspaceEnvelope envelope, bool shouldThrow = false)
        {
            _envelope = envelope;
            _shouldThrow = shouldThrow;
        }

        public ValueTask<WorkspaceEnvelope> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default)
        {
            if (_shouldThrow) throw new InvalidOperationException("Simulated failure");
            return ValueTask.FromResult(_envelope);
        }

        public ValueTask<WorkspaceEnvelope> GetWindowWorkspaceAsync(nint hwnd, CancellationToken cancellationToken = default)
        {
            if (_shouldThrow) throw new InvalidOperationException("Simulated failure");
            return ValueTask.FromResult(_envelope);
        }

        public ValueTask<IReadOnlyList<WorkspaceEnvelope>> GetAllWorkspacesAsync(CancellationToken cancellationToken = default)
        {
            if (_shouldThrow) throw new InvalidOperationException("Simulated failure");
            return ValueTask.FromResult<IReadOnlyList<WorkspaceEnvelope>>(new[] { _envelope });
        }
    }
}
