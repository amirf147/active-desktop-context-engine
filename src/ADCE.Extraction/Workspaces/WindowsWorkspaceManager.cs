// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Extraction.Win32;
using ADCE.Extraction.Workspaces.Win32Com;

namespace ADCE.Extraction.Workspaces;

/// <summary>
/// Production workspace manager resolving virtual desktop identifiers and physical monitor geometry.
/// </summary>
public sealed class WindowsWorkspaceManager : IWorkspaceManager
{
    private static readonly Guid DefaultDesktopId = Guid.Empty;
    private const string DefaultDesktopName = "Current Desktop";
    private readonly IVirtualDesktopSessionProvider _sessionProvider;

    public WindowsWorkspaceManager()
        : this(DefaultVirtualDesktopSessionProvider.Instance)
    {
    }

    internal WindowsWorkspaceManager(IVirtualDesktopSessionProvider sessionProvider)
    {
        _sessionProvider = sessionProvider ?? DefaultVirtualDesktopSessionProvider.Instance;
    }

    public ValueTask<WorkspaceEnvelope> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monitorBounds = GetPrimaryMonitorBounds();

        VirtualDesktopInfo? currentDesktop = null;
        try
        {
            currentDesktop = _sessionProvider.GetCurrentDesktop();
        }
        catch
        {
            // Graceful degradation when shell COM interface is unavailable
        }

        var envelope = new WorkspaceEnvelope
        {
            VirtualDesktopId = currentDesktop?.Id ?? DefaultDesktopId,
            DesktopIndex = currentDesktop?.Index ?? 0,
            VirtualDesktopName = !string.IsNullOrWhiteSpace(currentDesktop?.Name)
                ? currentDesktop.Value.Name
                : DefaultDesktopName,
            MonitorIndex = 0,
            MonitorBounds = monitorBounds
        };

        return ValueTask.FromResult(envelope);
    }

    public async ValueTask<WorkspaceEnvelope> GetWindowWorkspaceAsync(nint hwnd, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var monitorBounds = GetMonitorBoundsForWindow(hwnd);

        if (hwnd == nint.Zero)
        {
            var cur = await GetCurrentWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            return cur with { MonitorBounds = monitorBounds };
        }

        Guid? windowDesktopId = null;
        try
        {
            windowDesktopId = _sessionProvider.GetWindowDesktopId(hwnd);
        }
        catch
        {
            // Graceful degradation
        }

        if (!windowDesktopId.HasValue || windowDesktopId.Value == Guid.Empty)
        {
            var cur = await GetCurrentWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            return cur with { MonitorBounds = monitorBounds };
        }

        IReadOnlyList<VirtualDesktopInfo> desktops = [];
        try
        {
            desktops = _sessionProvider.GetAllDesktops();
        }
        catch
        {
            // Graceful degradation
        }

        VirtualDesktopInfo? matchedDesktop = null;
        for (int i = 0; i < desktops.Count; i++)
        {
            if (desktops[i].Id == windowDesktopId.Value)
            {
                matchedDesktop = desktops[i];
                break;
            }
        }

        var envelope = new WorkspaceEnvelope
        {
            VirtualDesktopId = windowDesktopId.Value,
            DesktopIndex = matchedDesktop?.Index ?? 0,
            VirtualDesktopName = !string.IsNullOrWhiteSpace(matchedDesktop?.Name)
                ? matchedDesktop.Value.Name
                : DefaultDesktopName,
            MonitorIndex = 0,
            MonitorBounds = monitorBounds
        };

        return envelope;
    }

    public async ValueTask<IReadOnlyList<WorkspaceEnvelope>> GetAllWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primaryMonitorBounds = GetPrimaryMonitorBounds();

        IReadOnlyList<VirtualDesktopInfo> desktops = [];
        try
        {
            desktops = _sessionProvider.GetAllDesktops();
        }
        catch
        {
            // Graceful degradation
        }

        if (desktops.Count == 0)
        {
            var current = await GetCurrentWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            return [current];
        }

        var envelopes = new WorkspaceEnvelope[desktops.Count];
        for (int i = 0; i < desktops.Count; i++)
        {
            var d = desktops[i];
            envelopes[i] = new WorkspaceEnvelope
            {
                VirtualDesktopId = d.Id,
                DesktopIndex = d.Index,
                VirtualDesktopName = !string.IsNullOrWhiteSpace(d.Name) ? d.Name : $"Desktop {d.Index + 1}",
                MonitorIndex = 0,
                MonitorBounds = primaryMonitorBounds
            };
        }

        return envelopes;
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
