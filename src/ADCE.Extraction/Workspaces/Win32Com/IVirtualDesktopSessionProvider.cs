// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;

namespace ADCE.Extraction.Workspaces.Win32Com;

/// <summary>
/// Abstraction for querying virtual desktop state from the host shell.
/// Enables deterministic unit testing without requiring an active Windows Explorer instance.
/// </summary>
internal interface IVirtualDesktopSessionProvider
{
    VirtualDesktopInfo? GetCurrentDesktop();
    IReadOnlyList<VirtualDesktopInfo> GetAllDesktops();
    Guid? GetWindowDesktopId(nint hwnd);
}

/// <summary>
/// Production session provider communicating directly with explorer.exe via TransientMtaSession.
/// </summary>
internal sealed class DefaultVirtualDesktopSessionProvider : IVirtualDesktopSessionProvider
{
    public static readonly DefaultVirtualDesktopSessionProvider Instance = new();

    public VirtualDesktopInfo? GetCurrentDesktop() => TransientMtaSession.ResolveCurrentDesktop();

    public IReadOnlyList<VirtualDesktopInfo> GetAllDesktops() => TransientMtaSession.ResolveAllDesktops();

    public Guid? GetWindowDesktopId(nint hwnd) => TransientMtaSession.GetWindowDesktopId(hwnd);
}
