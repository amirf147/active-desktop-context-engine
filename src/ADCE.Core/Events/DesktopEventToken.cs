// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Runtime.InteropServices;

namespace ADCE.Core.Events;

/// <summary>
/// 16-byte unmanaged value token representing a raw operating system desktop event.
/// Engineered for zero-heap-allocation ingress from SetWinEventHook into Channels.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DesktopEventToken(
    ushort EventType,
    nint Hwnd,
    uint TimestampMs
)
{
    /// <summary>Empty / uninitialized event token.</summary>
    public static readonly DesktopEventToken Empty = default;

    /// <summary>Checks if this event token has a valid HWND.</summary>
    public bool IsValid => Hwnd != nint.Zero;
}
