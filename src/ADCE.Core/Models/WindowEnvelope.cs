// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Text.Json.Serialization;
using ADCE.Core.Enums;
using ADCE.Core.Serialization;

namespace ADCE.Core.Models;

/// <summary>
/// Represents top-level window identity, process metadata, and screen coordinates.
/// </summary>
public sealed record WindowEnvelope
{
    /// <summary>Native Win32 window handle (HWND).</summary>
    [JsonConverter(typeof(HwndJsonConverter))]
    public required nint Hwnd { get; init; }

    /// <summary>Text from the top-level window title bar.</summary>
    public required string Title { get; init; }

    /// <summary>Executable process name (e.g. "Antigravity.exe", "waterfox.exe").</summary>
    public required string ProcessName { get; init; }

    /// <summary>Operating system Process ID (PID).</summary>
    public required int Pid { get; init; }

    /// <summary>Win32 registered window class name (e.g. "Chrome_WidgetWin_1", "MozillaWindowClass").</summary>
    public required string ClassName { get; init; }

    /// <summary>Architectural UI framework archetype classified for this window.</summary>
    public DesktopAppArchetype Archetype { get; init; } = DesktopAppArchetype.Unknown;

    /// <summary>Top-level window bounds on screen.</summary>
    public BoundingRectangle Bounds { get; init; } = BoundingRectangle.Empty;

    /// <summary>Indicates whether the window is currently minimized (iconic).</summary>
    public bool IsMinimized { get; init; }

    /// <summary>Indicates whether the window is currently maximized (zoomed).</summary>
    public bool IsMaximized { get; init; }
}
