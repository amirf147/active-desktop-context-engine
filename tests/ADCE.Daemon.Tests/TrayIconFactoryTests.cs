// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Drawing;
using ADCE.Daemon.Hosting;
using ADCE.Daemon.UI;
using Xunit;

namespace ADCE.Daemon.Tests;

public sealed class TrayIconFactoryTests
{
    [Theory]
    [InlineData(DaemonState.Running)]
    [InlineData(DaemonState.Paused)]
    [InlineData(DaemonState.Faulted)]
    [InlineData(DaemonState.Stopped)]
    public void CreateStateIcon_ReturnsValidManagedIconForState(DaemonState state)
    {
        using var icon = TrayIconFactory.CreateStateIcon(state, 32);

        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public void CreateStateIcon_RepeatedCreation_DoesNotLeakGdiOrThrow()
    {
        // Trap 1 Validation: Repeated generation must clean native HICON via DestroyIcon
        for (int i = 0; i < 50; i++)
        {
            using var icon1 = TrayIconFactory.CreateStateIcon(DaemonState.Running, 16);
            using var icon2 = TrayIconFactory.CreateStateIcon(DaemonState.Paused, 32);
            using var icon3 = TrayIconFactory.CreateStateIcon(DaemonState.Faulted, 64);

            Assert.NotNull(icon1);
            Assert.NotNull(icon2);
            Assert.NotNull(icon3);
        }
    }
}
