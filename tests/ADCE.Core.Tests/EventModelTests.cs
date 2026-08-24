// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using ADCE.Core.Enums;
using ADCE.Core.Events;
using Xunit;

namespace ADCE.Core.Tests;

public class EventModelTests
{
    [Fact]
    public void ForegroundChangedEvent_InitializesCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new ForegroundChangedEvent
        {
            Hwnd = 0x00DB083E,
            ProcessName = "Antigravity.exe",
            ClassName = "Chrome_WidgetWin_1",
            Pid = 1234,
            Timestamp = now
        };

        Assert.Equal(DesktopEventType.ForegroundChanged, evt.EventType);
        Assert.Equal((nint)0x00DB083E, evt.Hwnd);
        Assert.Equal("Antigravity.exe", evt.ProcessName);
        Assert.Equal("Chrome_WidgetWin_1", evt.ClassName);
        Assert.Equal(1234, evt.Pid);
        Assert.Equal(now, evt.Timestamp);
    }

    [Fact]
    public void FocusChangedEvent_InitializesCorrectly()
    {
        var evt = new FocusChangedEvent
        {
            Hwnd = 0x00123456,
            ControlType = "Edit",
            AutomationId = "urlbar-input",
            ElementName = "Address Bar"
        };

        Assert.Equal(DesktopEventType.FocusChanged, evt.EventType);
        Assert.Equal((nint)0x00123456, evt.Hwnd);
        Assert.Equal("Edit", evt.ControlType);
        Assert.Equal("urlbar-input", evt.AutomationId);
        Assert.Equal("Address Bar", evt.ElementName);
    }

    [Fact]
    public void VirtualDesktopSwitchedEvent_InitializesCorrectly()
    {
        var guid = Guid.NewGuid();
        var evt = new VirtualDesktopSwitchedEvent
        {
            NewDesktopId = guid,
            DesktopIndex = 2
        };

        Assert.Equal(DesktopEventType.VirtualDesktopSwitched, evt.EventType);
        Assert.Equal(guid, evt.NewDesktopId);
        Assert.Equal(2, evt.DesktopIndex);
    }

    [Fact]
    public void StructureChangedEvent_InitializesCorrectly()
    {
        var evt = new StructureChangedEvent
        {
            Hwnd = 0x00554433
        };

        Assert.Equal(DesktopEventType.StructureChanged, evt.EventType);
        Assert.Equal((nint)0x00554433, evt.Hwnd);
    }

    [Fact]
    public void HeartbeatEvent_InitializesCorrectly()
    {
        var evt = new HeartbeatEvent();
        Assert.Equal(DesktopEventType.Heartbeat, evt.EventType);
    }

    [Fact]
    public void DesktopEventToken_PropertiesAndEquality()
    {
        var tokenA = new DesktopEventToken(0x0003, 0x00DB083E, 123456);
        var tokenB = new DesktopEventToken(0x0003, 0x00DB083E, 123456);
        var tokenEmpty = DesktopEventToken.Empty;

        Assert.True(tokenA.IsValid);
        Assert.False(tokenEmpty.IsValid);
        Assert.Equal(tokenA, tokenB);
        Assert.Equal((ushort)0x0003, tokenA.EventType);
        Assert.Equal((nint)0x00DB083E, tokenA.Hwnd);
        Assert.Equal(123456u, tokenA.TimestampMs);
    }
}
