// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Threading.Tasks;
using ADCE.Extraction.Workspaces;
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
        Assert.Equal("Current Desktop", workspace.VirtualDesktopName);
        Assert.Equal(0, workspace.DesktopIndex);
        Assert.True(workspace.MonitorBounds.Width > 0);
        Assert.True(workspace.MonitorBounds.Height > 0);
    }

    [Fact]
    public async Task GetWindowWorkspaceAsync_HandlesZeroHwndGracefully()
    {
        var manager = new WindowsWorkspaceManager();
        var workspace = await manager.GetWindowWorkspaceAsync(nint.Zero);

        Assert.NotNull(workspace);
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
    }
}
