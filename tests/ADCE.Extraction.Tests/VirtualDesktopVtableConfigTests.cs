// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using ADCE.Extraction.Workspaces.Win32Com;
using Xunit;

namespace ADCE.Extraction.Tests;

public class VirtualDesktopVtableConfigTests
{
    [Fact]
    public void Resolve_Build26100Plus_ReturnsExpectedOffsets()
    {
        var config = VirtualDesktopVtableConfig.Resolve(26300);

        Assert.NotNull(config);
        Assert.Equal(26100, config.BuildMin);
        Assert.Equal(int.MaxValue, config.BuildMax);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_26100, config.ManagerIid);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktop_26100, config.DesktopIid);
        Assert.Equal(3, config.SlotGetCount);
        Assert.Equal(6, config.SlotGetCurrentDesktop);
        Assert.Equal(7, config.SlotGetDesktops);
        Assert.Equal(4, config.SlotVdGetId);
        Assert.Equal(5, config.SlotVdGetName);
        Assert.False(config.GetCurrentTakesHwnd);
        Assert.False(config.GetDesktopsTakesHwnd);
    }

    [Fact]
    public void Resolve_Build22631_ReturnsExpectedOffsets()
    {
        var config = VirtualDesktopVtableConfig.Resolve(22631);

        Assert.NotNull(config);
        Assert.Equal(22631, config.BuildMin);
        Assert.Equal(26099, config.BuildMax);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22631, config.ManagerIid);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktop_22631, config.DesktopIid);
        Assert.Equal(3, config.SlotGetCount);
        Assert.Equal(6, config.SlotGetCurrentDesktop);
        Assert.Equal(7, config.SlotGetDesktops);
        Assert.Equal(4, config.SlotVdGetId);
        Assert.Equal(5, config.SlotVdGetName);
        Assert.False(config.GetCurrentTakesHwnd);
        Assert.False(config.GetDesktopsTakesHwnd);
    }

    [Fact]
    public void Resolve_Build22621_ReturnsExpectedOffsets()
    {
        var config = VirtualDesktopVtableConfig.Resolve(22621);

        Assert.NotNull(config);
        Assert.Equal(22621, config.BuildMin);
        Assert.Equal(22630, config.BuildMax);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22621, config.ManagerIid);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktop_22621, config.DesktopIid);
        Assert.Equal(3, config.SlotGetCount);
        Assert.Equal(6, config.SlotGetCurrentDesktop);
        Assert.Equal(7, config.SlotGetDesktops);
        Assert.Equal(4, config.SlotVdGetId);
        Assert.Equal(5, config.SlotVdGetName);
        Assert.False(config.GetCurrentTakesHwnd);
        Assert.False(config.GetDesktopsTakesHwnd);
    }

    [Fact]
    public void Resolve_Build22000_ReturnsExpectedOffsets()
    {
        var config = VirtualDesktopVtableConfig.Resolve(22000);

        Assert.NotNull(config);
        Assert.Equal(22000, config.BuildMin);
        Assert.Equal(22620, config.BuildMax);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22000, config.ManagerIid);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktop_22000, config.DesktopIid);
        Assert.Equal(3, config.SlotGetCount);
        Assert.Equal(6, config.SlotGetCurrentDesktop);
        Assert.Equal(7, config.SlotGetDesktops);
        Assert.Equal(4, config.SlotVdGetId);
        Assert.Equal(6, config.SlotVdGetName);
        Assert.True(config.GetCurrentTakesHwnd);
        Assert.True(config.GetDesktopsTakesHwnd);
    }

    [Fact]
    public void Resolve_Build19041_ReturnsExpectedOffsets()
    {
        var config = VirtualDesktopVtableConfig.Resolve(19045);

        Assert.NotNull(config);
        Assert.Equal(19041, config.BuildMin);
        Assert.Equal(21999, config.BuildMax);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_19041, config.ManagerIid);
        Assert.Equal(VirtualDesktopGuids.IID_IVirtualDesktop_19041, config.DesktopIid);
        Assert.Equal(3, config.SlotGetCount);
        Assert.Equal(6, config.SlotGetCurrentDesktop);
        Assert.Equal(7, config.SlotGetDesktops);
        Assert.Equal(4, config.SlotVdGetId);
        Assert.Null(config.SlotVdGetName);
        Assert.False(config.GetCurrentTakesHwnd);
        Assert.False(config.GetDesktopsTakesHwnd);
    }

    [Fact]
    public void Resolve_UnsupportedLegacyBuild_ReturnsNull()
    {
        var config = VirtualDesktopVtableConfig.Resolve(10240);

        Assert.Null(config);
    }
}
