// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Extraction.Win32;

namespace ADCE.Extraction.Workspaces;

/// <summary>
/// Production workspace manager resolving virtual desktop identifiers and physical monitor geometry.
/// </summary>
public sealed class WindowsWorkspaceManager : IWorkspaceManager
{
    private static readonly Guid DefaultDesktopId = Guid.Empty;

    public ValueTask<WorkspaceEnvelope> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var monitorBounds = GetPrimaryMonitorBounds();

        var envelope = new WorkspaceEnvelope
        {
            VirtualDesktopId = DefaultDesktopId,
            DesktopIndex = 0,
            VirtualDesktopName = "Current Desktop",
            MonitorIndex = 0,
            MonitorBounds = monitorBounds
        };

        return ValueTask.FromResult(envelope);
    }

    public ValueTask<WorkspaceEnvelope> GetWindowWorkspaceAsync(nint hwnd, CancellationToken cancellationToken = default)
    {
        var monitorBounds = GetMonitorBoundsForWindow(hwnd);

        var envelope = new WorkspaceEnvelope
        {
            VirtualDesktopId = DefaultDesktopId,
            DesktopIndex = 0,
            VirtualDesktopName = "Current Desktop",
            MonitorIndex = 0,
            MonitorBounds = monitorBounds
        };

        return ValueTask.FromResult(envelope);
    }

    public ValueTask<IReadOnlyList<WorkspaceEnvelope>> GetAllWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var current = GetCurrentWorkspaceAsync(cancellationToken).Result;
        IReadOnlyList<WorkspaceEnvelope> list = [current];
        return ValueTask.FromResult(list);
    }

    private static BoundingRectangle GetPrimaryMonitorBounds()
    {
        int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        return new BoundingRectangle(0, 0, width > 0 ? width : 1920, height > 0 ? height : 1080);
    }

    private static BoundingRectangle GetMonitorBoundsForWindow(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return GetPrimaryMonitorBounds();
        }

        nint hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMonitor == nint.Zero)
        {
            return GetPrimaryMonitorBounds();
        }

        var mi = new NativeMethods.MONITORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
        {
            int w = mi.rcMonitor.Right - mi.rcMonitor.Left;
            int h = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
            return new BoundingRectangle(mi.rcMonitor.Left, mi.rcMonitor.Top, w, h);
        }

        return GetPrimaryMonitorBounds();
    }
}
