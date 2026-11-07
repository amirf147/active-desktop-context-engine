// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ADCE.Extraction.Workspaces;
using ADCE.Extraction.Workspaces.Win32Com;
using Xunit;

namespace ADCE.Extraction.Tests;

public class WindowsWorkspaceManagerTests
{
    [Fact]
    public async Task GetCurrentWorkspaceAsync_ReturnsValidEnvelope()
    {
        var manager = new WindowsWorkspaceManager();
        var workspace = await manager.GetCurrentWorkspaceAsync();

        Assert.NotNull(workspace);
        Assert.False(string.IsNullOrWhiteSpace(workspace.VirtualDesktopName));
        Assert.True(workspace.DesktopIndex >= 0);
        Assert.True(workspace.MonitorBounds.Width > 0);
        Assert.True(workspace.MonitorBounds.Height > 0);
    }

    [Fact]
    public async Task GetWindowWorkspaceAsync_HandlesZeroHwndGracefully()
    {
        var manager = new WindowsWorkspaceManager();
        var workspace = await manager.GetWindowWorkspaceAsync(nint.Zero);

        Assert.NotNull(workspace);
        Assert.False(string.IsNullOrWhiteSpace(workspace.VirtualDesktopName));
        Assert.True(workspace.MonitorBounds.Width > 0);
        Assert.True(workspace.MonitorBounds.Height > 0);
    }

    [Fact]
    public async Task GetAllWorkspacesAsync_ReturnsNonEmptyList()
    {
        var manager = new WindowsWorkspaceManager();
        var all = await manager.GetAllWorkspacesAsync();

        Assert.NotNull(all);
        Assert.NotEmpty(all);
        foreach (var ws in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(ws.VirtualDesktopName));
            Assert.True(ws.MonitorBounds.Width > 0);
            Assert.True(ws.MonitorBounds.Height > 0);
        }
    }

    [Fact]
    public void TransientMtaSession_LiveResolution_SucceedsOnSupportedHost()
    {
        using var session = TransientMtaSession.TryCreate();
        if (session != null)
        {
            var cur = session.GetCurrentDesktop();
            Assert.NotNull(cur);
            Assert.NotEqual(Guid.Empty, cur.Value.Id);
            Assert.False(string.IsNullOrWhiteSpace(cur.Value.Name));
            Assert.True(cur.Value.Index >= 0);

            var all = session.GetDesktops();
            Assert.NotEmpty(all);
            Assert.Contains(all, d => d.Id == cur.Value.Id);
        }
    }

    [Fact]
    public async Task WindowsWorkspaceManager_GracefulDegradation_WhenSessionUnavailable()
    {
        var failingProvider = new MockFailingSessionProvider();
        var manager = new WindowsWorkspaceManager(failingProvider);

        var current = await manager.GetCurrentWorkspaceAsync();
        Assert.NotNull(current);
        Assert.Equal(Guid.Empty, current.VirtualDesktopId);
        Assert.Equal(0, current.DesktopIndex);
        Assert.Equal("Current Desktop", current.VirtualDesktopName);

        var all = await manager.GetAllWorkspacesAsync();
        Assert.NotNull(all);
        Assert.Single(all);
        Assert.Equal(Guid.Empty, all[0].VirtualDesktopId);

        var windowWs = await manager.GetWindowWorkspaceAsync(new nint(12345));
        Assert.NotNull(windowWs);
        Assert.Equal(Guid.Empty, windowWs.VirtualDesktopId);
        Assert.Equal("Current Desktop", windowWs.VirtualDesktopName);
    }

    [Fact]
    public async Task WindowsWorkspaceManager_MockSessionProvider_MapsAllDesktopsAccurately()
    {
        var desktop1 = new VirtualDesktopInfo(Guid.NewGuid(), "Development", 0);
        var desktop2 = new VirtualDesktopInfo(Guid.NewGuid(), "Monitoring", 1);
        var mockProvider = new ConfigurableMockSessionProvider(
            current: desktop2,
            all: [desktop1, desktop2],
            windowDesktopId: desktop2.Id);

        var manager = new WindowsWorkspaceManager(mockProvider);

        var current = await manager.GetCurrentWorkspaceAsync();
        Assert.Equal(desktop2.Id, current.VirtualDesktopId);
        Assert.Equal(1, current.DesktopIndex);
        Assert.Equal("Monitoring", current.VirtualDesktopName);

        var all = await manager.GetAllWorkspacesAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(desktop1.Id, all[0].VirtualDesktopId);
        Assert.Equal("Development", all[0].VirtualDesktopName);
        Assert.Equal(0, all[0].DesktopIndex);
        Assert.Equal(desktop2.Id, all[1].VirtualDesktopId);
        Assert.Equal("Monitoring", all[1].VirtualDesktopName);
        Assert.Equal(1, all[1].DesktopIndex);

        var windowWs = await manager.GetWindowWorkspaceAsync(new nint(0xCAFE));
        Assert.Equal(desktop2.Id, windowWs.VirtualDesktopId);
        Assert.Equal("Monitoring", windowWs.VirtualDesktopName);
        Assert.Equal(1, windowWs.DesktopIndex);
    }

    [Fact]
    public async Task WindowsWorkspaceManager_GetWindowWorkspaceAsync_FallsBackWhenWindowUnmapped()
    {
        var currentDesktop = new VirtualDesktopInfo(Guid.NewGuid(), "Active Space", 0);
        var mockProvider = new ConfigurableMockSessionProvider(
            current: currentDesktop,
            all: [currentDesktop],
            windowDesktopId: null);

        var manager = new WindowsWorkspaceManager(mockProvider);
        var windowWs = await manager.GetWindowWorkspaceAsync(new nint(0xBEEF));

        Assert.Equal(currentDesktop.Id, windowWs.VirtualDesktopId);
        Assert.Equal("Active Space", windowWs.VirtualDesktopName);
        Assert.Equal(0, windowWs.DesktopIndex);
    }

    private sealed class MockFailingSessionProvider : IVirtualDesktopSessionProvider
    {
        public VirtualDesktopInfo? GetCurrentDesktop() => null;
        public IReadOnlyList<VirtualDesktopInfo> GetAllDesktops() => [];
        public Guid? GetWindowDesktopId(nint hwnd) => null;
    }

    private sealed class ConfigurableMockSessionProvider : IVirtualDesktopSessionProvider
    {
        private readonly VirtualDesktopInfo? _current;
        private readonly IReadOnlyList<VirtualDesktopInfo> _all;
        private readonly Guid? _windowDesktopId;

        public ConfigurableMockSessionProvider(
            VirtualDesktopInfo? current,
            IReadOnlyList<VirtualDesktopInfo> all,
            Guid? windowDesktopId)
        {
            _current = current;
            _all = all;
            _windowDesktopId = windowDesktopId;
        }

        public VirtualDesktopInfo? GetCurrentDesktop() => _current;
        public IReadOnlyList<VirtualDesktopInfo> GetAllDesktops() => _all;
        public Guid? GetWindowDesktopId(nint hwnd) => _windowDesktopId;
    }
}
