// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using ADCE.Core.Enums;

namespace ADCE.Core.Events;

/// <summary>
/// Base record representing a lightweight operating system desktop event token.
/// Designed for zero-allocation enqueueing into Channels from WinEvent callbacks.
/// </summary>
public abstract record DesktopEvent
{
    /// <summary>Type classification of this desktop event.</summary>
    public abstract DesktopEventType EventType { get; }

    /// <summary>UTC timestamp when the event was received by the hook.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Emitted when the active foreground window changes (EVENT_SYSTEM_FOREGROUND).
/// </summary>
public sealed record ForegroundChangedEvent : DesktopEvent
{
    public override DesktopEventType EventType => DesktopEventType.ForegroundChanged;

    /// <summary>Window handle of the newly activated foreground window.</summary>
    public required nint Hwnd { get; init; }

    /// <summary>Process name if resolved during fast Win32 gating, or empty.</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>Win32 class name of the foreground window.</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Process ID of the foreground window.</summary>
    public int Pid { get; init; }
}

/// <summary>
/// Emitted when keyboard or accessibility focus moves to a different control (EVENT_OBJECT_FOCUS).
/// </summary>
public sealed record FocusChangedEvent : DesktopEvent
{
    public override DesktopEventType EventType => DesktopEventType.FocusChanged;

    /// <summary>Window handle containing the focused control.</summary>
    public required nint Hwnd { get; init; }

    /// <summary>UI Automation control type name if known, or empty.</summary>
    public string ControlType { get; init; } = string.Empty;

    /// <summary>Automation ID of the focused control if known, or empty.</summary>
    public string AutomationId { get; init; } = string.Empty;

    /// <summary>Accessible name of the focused control if known, or empty.</summary>
    public string ElementName { get; init; } = string.Empty;
}

/// <summary>
/// Emitted when the user switches virtual desktops.
/// </summary>
public sealed record VirtualDesktopSwitchedEvent : DesktopEvent
{
    public override DesktopEventType EventType => DesktopEventType.VirtualDesktopSwitched;

    /// <summary>GUID of the newly activated virtual desktop.</summary>
    public required Guid NewDesktopId { get; init; }

    /// <summary>Zero-indexed index of the newly activated virtual desktop.</summary>
    public required int DesktopIndex { get; init; }
}

/// <summary>
/// Emitted when the internal UI tree structure changes inside the active window.
/// </summary>
public sealed record StructureChangedEvent : DesktopEvent
{
    public override DesktopEventType EventType => DesktopEventType.StructureChanged;

    /// <summary>Window handle where the structural change occurred.</summary>
    public required nint Hwnd { get; init; }
}

/// <summary>
/// Periodic heartbeat event for pipeline liveness monitoring.
/// </summary>
public sealed record HeartbeatEvent : DesktopEvent
{
    public override DesktopEventType EventType => DesktopEventType.Heartbeat;
}
