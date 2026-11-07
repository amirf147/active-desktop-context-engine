// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Runtime.InteropServices;

namespace ADCE.Extraction.Workspaces.Win32Com;

/// <summary>
/// Windows 11 COM notification interface for virtual desktop events.
/// All [In] parameters are typed as unmanaged nint pointers to prevent runtime RCW construction
/// and uphold the shell-owned refcount safety invariant.
/// </summary>
[ComImport]
[Guid("B9E5E94D-233E-49AB-AF5C-2B4541C3AADE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopNotificationWin11
{
    void VirtualDesktopCreated(nint pDesktop);
    void VirtualDesktopDestroyBegin(nint pDesktopDestroyed, nint pDesktopFallback);
    void VirtualDesktopDestroyFailed(nint pDesktopDestroyed, nint pDesktopFallback);
    void VirtualDesktopDestroyed(nint pDesktopDestroyed, nint pDesktopFallback);
    void VirtualDesktopMoved(nint pDesktop, int nIndexFrom, int nIndexTo);
    void VirtualDesktopRenamed(nint pDesktop, nint chName);
    void ViewVirtualDesktopChanged(nint pView);
    void CurrentVirtualDesktopChanged(nint pDesktopOld, nint pDesktopNew);
    void VirtualDesktopWallpaperChanged(nint pDesktop, nint chPath);
    void VirtualDesktopSwitched(nint pDesktop);
    void RemoteVirtualDesktopConnected(nint pDesktop);
}

/// <summary>
/// Windows 10 COM notification interface for virtual desktop events.
/// </summary>
[ComImport]
[Guid("C17949F0-3DE2-4DD0-BE55-520B342BD7AC")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopNotificationWin10
{
    void VirtualDesktopCreated(nint pDesktop);
    void VirtualDesktopDestroyBegin(nint pDesktopDestroyed, nint pDesktopFallback);
    void VirtualDesktopDestroyFailed(nint pDesktopDestroyed, nint pDesktopFallback);
    void VirtualDesktopDestroyed(nint pDesktopDestroyed, nint pDesktopFallback);
    void ViewVirtualDesktopChanged(nint pView);
    void CurrentVirtualDesktopChanged(nint pDesktopOld, nint pDesktopNew);
}

/// <summary>
/// Windows 11 registration service interface.
/// </summary>
[ComImport]
[Guid("0CD45E71-D927-4F15-8B0A-8FEF525337BF")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopNotificationServiceWin11
{
    uint Register(IVirtualDesktopNotificationWin11 pNotification);
    void Unregister(uint dwCookie);
}

/// <summary>
/// Windows 10 registration service interface.
/// </summary>
[ComImport]
[Guid("0CD45E71-5278-4ACD-9686-4973EABF45EA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopNotificationServiceWin10
{
    uint Register(IVirtualDesktopNotificationWin10 pNotification);
    void Unregister(uint dwCookie);
}

/// <summary>
/// Active COM sink receiving virtual desktop transition notifications from explorer.exe.
/// Implements both Windows 11 and Windows 10 notification contracts with zero-allocation pointer handling.
/// </summary>
internal sealed class VirtualDesktopNotificationSink : IVirtualDesktopNotificationWin11, IVirtualDesktopNotificationWin10
{
    private readonly Action _onDesktopChanged;

    public VirtualDesktopNotificationSink(Action onDesktopChanged)
    {
        _onDesktopChanged = onDesktopChanged ?? throw new ArgumentNullException(nameof(onDesktopChanged));
    }

    // Windows 11 and Windows 10 shared callbacks
    public void VirtualDesktopCreated(nint pDesktop) { }
    public void VirtualDesktopDestroyBegin(nint pDesktopDestroyed, nint pDesktopFallback) { }
    public void VirtualDesktopDestroyFailed(nint pDesktopDestroyed, nint pDesktopFallback) { }
    public void VirtualDesktopDestroyed(nint pDesktopDestroyed, nint pDesktopFallback) { }
    public void VirtualDesktopMoved(nint pDesktop, int nIndexFrom, int nIndexTo) { }
    public void VirtualDesktopRenamed(nint pDesktop, nint chName) { }
    public void ViewVirtualDesktopChanged(nint pView) { }

    public void CurrentVirtualDesktopChanged(nint pDesktopOld, nint pDesktopNew)
    {
        _onDesktopChanged();
    }

    public void VirtualDesktopWallpaperChanged(nint pDesktop, nint chPath) { }

    public void VirtualDesktopSwitched(nint pDesktop)
    {
        _onDesktopChanged();
    }

    public void RemoteVirtualDesktopConnected(nint pDesktop) { }

    /// <summary>
    /// Registers the notification sink against the shell notification service.
    /// Returns null if COM registration fails or Explorer is restarting.
    /// </summary>
    public static VirtualDesktopNotificationRegistration? TryRegister(Action onDesktopChanged)
    {
        if (onDesktopChanged == null)
        {
            return null;
        }

        nint pSp = nint.Zero;
        nint pSvc = nint.Zero;

        try
        {
            int hrSp = CoCreateInstance(
                in VirtualDesktopGuids.CLSID_ImmersiveShell,
                nint.Zero,
                4, // CLSCTX_LOCAL_SERVER
                in VirtualDesktopGuids.IID_IServiceProvider,
                out pSp);

            if (hrSp != 0 || pSp == nint.Zero)
            {
                return null;
            }

            unsafe
            {
                nint* vtableSp = *(nint**)pSp;
                var queryService = (delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, nint*, int>)vtableSp[3];

                int build = Environment.OSVersion.Version.Build;
                bool isWin11 = build >= 22000;

                if (isWin11)
                {
                    Guid sid11 = VirtualDesktopGuids.CLSID_VirtualDesktopNotificationService;
                    Guid iid11 = VirtualDesktopGuids.IID_IVirtualDesktopNotificationService_Win11;

                    int hr11 = queryService(pSp, &sid11, &iid11, &pSvc);
                    if (hr11 == 0 && pSvc != nint.Zero)
                    {
                        var service = (IVirtualDesktopNotificationServiceWin11)Marshal.GetObjectForIUnknown(pSvc);
                        var sink = new VirtualDesktopNotificationSink(onDesktopChanged);
                        uint cookie = service.Register(sink);
                        return new VirtualDesktopNotificationRegistration(cookie, service, isWin11: true);
                    }
                }

                // Fallback to Win10 notification service
                Guid sid10 = VirtualDesktopGuids.CLSID_VirtualDesktopNotificationService_Win10;
                Guid iid10 = VirtualDesktopGuids.IID_IVirtualDesktopNotificationService_Win10;

                int hr10 = queryService(pSp, &sid10, &iid10, &pSvc);
                if (hr10 == 0 && pSvc != nint.Zero)
                {
                    var service = (IVirtualDesktopNotificationServiceWin10)Marshal.GetObjectForIUnknown(pSvc);
                    var sink = new VirtualDesktopNotificationSink(onDesktopChanged);
                    uint cookie = service.Register(sink);
                    return new VirtualDesktopNotificationRegistration(cookie, service, isWin11: false);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (pSvc != nint.Zero)
            {
                try { Marshal.Release(pSvc); } catch { }
            }
            if (pSp != nint.Zero)
            {
                try { Marshal.Release(pSp); } catch { }
            }
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(
        in Guid rclsid,
        nint pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out nint ppv);
}

/// <summary>
/// Disposable lifetime handle for an active virtual desktop notification sink registration.
/// </summary>
internal sealed class VirtualDesktopNotificationRegistration : IDisposable
{
    private readonly uint _cookie;
    private readonly object _service;
    private readonly bool _isWin11;
    private bool _disposed;

    internal VirtualDesktopNotificationRegistration(uint cookie, object service, bool isWin11)
    {
        _cookie = cookie;
        _service = service;
        _isWin11 = isWin11;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_isWin11 && _service is IVirtualDesktopNotificationServiceWin11 svc11)
            {
                svc11.Unregister(_cookie);
            }
            else if (_service is IVirtualDesktopNotificationServiceWin10 svc10)
            {
                svc10.Unregister(_cookie);
            }
        }
        catch
        {
            // Suppress errors during teardown
        }
    }
}
