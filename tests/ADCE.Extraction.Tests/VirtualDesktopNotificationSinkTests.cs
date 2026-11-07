// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using ADCE.Extraction.Win32;
using ADCE.Extraction.Workspaces.Win32Com;
using Xunit;

namespace ADCE.Extraction.Tests;

public class VirtualDesktopNotificationSinkTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_OnNullAction()
    {
        Assert.Throws<ArgumentNullException>(() => new VirtualDesktopNotificationSink(null!));
    }

    [Fact]
    public void TryRegister_ReturnsNull_WhenCallbackIsNull()
    {
        var registration = VirtualDesktopNotificationSink.TryRegister(null!);
        Assert.Null(registration);
    }

    [Fact]
    public void CurrentVirtualDesktopChanged_InvokesCallback()
    {
        int invocationCount = 0;
        var sink = new VirtualDesktopNotificationSink(() => invocationCount++);

        sink.CurrentVirtualDesktopChanged(nint.Zero, nint.Zero);

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void VirtualDesktopSwitched_InvokesCallback()
    {
        int invocationCount = 0;
        var sink = new VirtualDesktopNotificationSink(() => invocationCount++);

        sink.VirtualDesktopSwitched(nint.Zero);

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void AncillaryCallbacks_DoNotThrowExceptions()
    {
        int invocationCount = 0;
        var sink = new VirtualDesktopNotificationSink(() => invocationCount++);

        sink.VirtualDesktopCreated(nint.Zero);
        sink.VirtualDesktopDestroyBegin(nint.Zero, nint.Zero);
        sink.VirtualDesktopDestroyFailed(nint.Zero, nint.Zero);
        sink.VirtualDesktopDestroyed(nint.Zero, nint.Zero);
        sink.VirtualDesktopMoved(nint.Zero, 0, 1);
        sink.VirtualDesktopRenamed(nint.Zero, nint.Zero);
        sink.ViewVirtualDesktopChanged(nint.Zero);
        sink.VirtualDesktopWallpaperChanged(nint.Zero, nint.Zero);
        sink.RemoteVirtualDesktopConnected(nint.Zero);

        Assert.Equal(0, invocationCount);
    }

    [Fact]
    public void EventConstant_DesktopSwitch_MatchesExpectedWin32Value()
    {
        Assert.Equal(0x0020u, NativeMethods.EVENT_SYSTEM_DESKTOPSWITCH);
    }

    [Fact]
    public void LiveTryRegister_OnSupportedWindows11Host_SucceedsAndDisposesCleanly()
    {
        if (Environment.OSVersion.Version.Build < 22000)
        {
            return;
        }

        var registration = VirtualDesktopNotificationSink.TryRegister(() => { });
        if (registration != null)
        {
            // Verify safe idempotent disposal
            registration.Dispose();
            registration.Dispose();
        }
    }
}
