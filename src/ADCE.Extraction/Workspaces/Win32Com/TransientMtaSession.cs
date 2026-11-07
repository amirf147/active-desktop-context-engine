// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ADCE.Extraction.Workspaces.Win32Com;

/// <summary>
/// Snapshot of virtual desktop properties resolved from shell COM objects.
/// </summary>
internal readonly record struct VirtualDesktopInfo(Guid Id, string Name, int Index);

/// <summary>
/// Encapsulates a transient call-scoped MTA COM session communicating with explorer.exe.
/// Implements the Zero-Cached-State pattern: pointers are acquired, queried, and released
/// within the invocation scope.
/// </summary>
internal sealed class TransientMtaSession : IDisposable
{
    private const uint CLSCTX_INPROC_SERVER = 0x1;
    private const uint CLSCTX_LOCAL_SERVER = 0x4;

    private readonly VirtualDesktopVtableConfig _config;
    private readonly bool _shouldUninitializeCom;
    private nint _serviceProvider;
    private nint _managerInternal;
    private bool _disposed;

    private TransientMtaSession(
        VirtualDesktopVtableConfig config,
        nint serviceProvider,
        nint managerInternal,
        bool shouldUninitializeCom)
    {
        _config = config;
        _serviceProvider = serviceProvider;
        _managerInternal = managerInternal;
        _shouldUninitializeCom = shouldUninitializeCom;
    }

    /// <summary>
    /// Attempts to establish a transient COM session with explorer.exe using the active Windows build configuration.
    /// Returns null if COM server is unavailable, build is unsupported, or Explorer is restarting.
    /// </summary>
    public static TransientMtaSession? TryCreate(VirtualDesktopVtableConfig? configOverride = null)
    {
        var config = configOverride ?? VirtualDesktopVtableConfig.Current;
        if (config == null)
        {
            return null;
        }

        int hrInit = CoInitializeEx(nint.Zero, 0x0);
        bool shouldUninit = hrInit == 0;

        nint pSp = nint.Zero;
        nint pVdm = nint.Zero;

        try
        {
            int hrSp = CoCreateInstance(
                in VirtualDesktopGuids.CLSID_ImmersiveShell,
                nint.Zero,
                CLSCTX_LOCAL_SERVER,
                in VirtualDesktopGuids.IID_IServiceProvider,
                out pSp);

            if (hrSp != 0 || pSp == nint.Zero)
            {
                if (shouldUninit)
                {
                    try { CoUninitialize(); } catch { }
                }
                return null;
            }

            unsafe
            {
                nint* vtableSp = *(nint**)pSp;
                var queryService = (delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, nint*, int>)vtableSp[3];

                Guid sidVdm = VirtualDesktopGuids.CLSID_VirtualDesktopManagerInternal;
                Guid iidVdm = config.ManagerIid;

                int hrQuery = queryService(pSp, &sidVdm, &iidVdm, &pVdm);
                if (hrQuery != 0 || pVdm == nint.Zero)
                {
                    SafeRelease(ref pSp);
                    if (shouldUninit)
                    {
                        try { CoUninitialize(); } catch { }
                    }
                    return null;
                }
            }

            return new TransientMtaSession(config, pSp, pVdm, shouldUninit);
        }
        catch
        {
            SafeRelease(ref pVdm);
            SafeRelease(ref pSp);
            if (shouldUninit)
            {
                try { CoUninitialize(); } catch { }
            }
            return null;
        }
    }

    /// <summary>
    /// Enumerates all currently active virtual desktops.
    /// </summary>
    public IReadOnlyList<VirtualDesktopInfo> GetDesktops()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint pArray = nint.Zero;
        try
        {
            unsafe
            {
                nint* vtableVdm = *(nint**)_managerInternal;
                int hrGet;
                if (_config.GetDesktopsTakesHwnd)
                {
                    var getDesktopsHwnd = (delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)vtableVdm[_config.SlotGetDesktops];
                    hrGet = getDesktopsHwnd(_managerInternal, nint.Zero, &pArray);
                }
                else
                {
                    var getDesktops = (delegate* unmanaged[Stdcall]<nint, nint*, int>)vtableVdm[_config.SlotGetDesktops];
                    hrGet = getDesktops(_managerInternal, &pArray);
                }

                if (hrGet != 0 || pArray == nint.Zero)
                {
                    return [];
                }

                nint* vtableArr = *(nint**)pArray;
                var getCount = (delegate* unmanaged[Stdcall]<nint, uint*, int>)vtableArr[3];
                uint count = 0;
                int hrCount = getCount(pArray, &count);
                if (hrCount != 0 || count == 0)
                {
                    return [];
                }

                var getAt = (delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, int>)vtableArr[4];
                var list = new List<VirtualDesktopInfo>((int)count);
                Guid desktopIid = _config.DesktopIid;

                for (uint i = 0; i < count; i++)
                {
                    nint pDesktop = nint.Zero;
                    try
                    {
                        int hrAt = getAt(pArray, i, &desktopIid, &pDesktop);
                        if (hrAt != 0 || pDesktop == nint.Zero)
                        {
                            continue;
                        }

                        nint* vtableDesktop = *(nint**)pDesktop;
                        var getId = (delegate* unmanaged[Stdcall]<nint, Guid*, int>)vtableDesktop[_config.SlotVdGetId];
                        Guid id = Guid.Empty;
                        int hrId = getId(pDesktop, &id);
                        if (hrId != 0 || id == Guid.Empty)
                        {
                            continue;
                        }

                        string name = string.Empty;
                        if (_config.SlotVdGetName.HasValue)
                        {
                            var getName = (delegate* unmanaged[Stdcall]<nint, nint*, int>)vtableDesktop[_config.SlotVdGetName.Value];
                            nint hstr = nint.Zero;
                            int hrName = getName(pDesktop, &hstr);
                            if (hrName == 0 && hstr != nint.Zero)
                            {
                                name = ReadHStringAndRelease(hstr);
                            }
                        }

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = $"Desktop {i + 1}";
                        }

                        list.Add(new VirtualDesktopInfo(id, name, (int)i));
                    }
                    finally
                    {
                        SafeRelease(ref pDesktop);
                    }
                }

                return list;
            }
        }
        catch
        {
            return [];
        }
        finally
        {
            SafeRelease(ref pArray);
        }
    }

    /// <summary>
    /// Resolves the currently active virtual desktop.
    /// </summary>
    public VirtualDesktopInfo? GetCurrentDesktop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint pDesktop = nint.Zero;
        Guid currentId = Guid.Empty;
        try
        {
            unsafe
            {
                nint* vtableVdm = *(nint**)_managerInternal;
                int hrCur;
                if (_config.GetCurrentTakesHwnd)
                {
                    var getCurHwnd = (delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)vtableVdm[_config.SlotGetCurrentDesktop];
                    hrCur = getCurHwnd(_managerInternal, nint.Zero, &pDesktop);
                }
                else
                {
                    var getCur = (delegate* unmanaged[Stdcall]<nint, nint*, int>)vtableVdm[_config.SlotGetCurrentDesktop];
                    hrCur = getCur(_managerInternal, &pDesktop);
                }

                if (hrCur != 0 || pDesktop == nint.Zero)
                {
                    return null;
                }

                nint* vtableDesktop = *(nint**)pDesktop;
                var getId = (delegate* unmanaged[Stdcall]<nint, Guid*, int>)vtableDesktop[_config.SlotVdGetId];
                int hrId = getId(pDesktop, &currentId);
                if (hrId != 0 || currentId == Guid.Empty)
                {
                    return null;
                }
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            SafeRelease(ref pDesktop);
        }

        var allDesktops = GetDesktops();
        for (int i = 0; i < allDesktops.Count; i++)
        {
            if (allDesktops[i].Id == currentId)
            {
                return allDesktops[i];
            }
        }

        return new VirtualDesktopInfo(currentId, "Desktop 1", 0);
    }

    /// <summary>
    /// Resolves the virtual desktop GUID for a top-level window handle.
    /// </summary>
    public static Guid? GetWindowDesktopId(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return null;
        }

        int hrInit = CoInitializeEx(nint.Zero, 0x0);
        bool shouldUninit = hrInit == 0;
        nint pVdm = nint.Zero;

        try
        {
            int hr = CoCreateInstance(
                in VirtualDesktopGuids.CLSID_VirtualDesktopManager,
                nint.Zero,
                CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER,
                in VirtualDesktopGuids.IID_IVirtualDesktopManager,
                out pVdm);

            if (hr != 0 || pVdm == nint.Zero)
            {
                hr = CoCreateInstance(
                    in VirtualDesktopGuids.CLSID_VirtualDesktopManager_Native,
                    nint.Zero,
                    CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER,
                    in VirtualDesktopGuids.IID_IVirtualDesktopManager_Native,
                    out pVdm);
            }

            if (hr != 0 || pVdm == nint.Zero)
            {
                return null;
            }

            unsafe
            {
                nint* vtable = *(nint**)pVdm;
                var getWindowDesktopId = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, int>)vtable[4];
                Guid desktopId = Guid.Empty;
                int hrGet = getWindowDesktopId(pVdm, hwnd, &desktopId);
                if (hrGet == 0 && desktopId != Guid.Empty)
                {
                    return desktopId;
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
            SafeRelease(ref pVdm);
            if (shouldUninit)
            {
                try { CoUninitialize(); } catch { }
            }
        }
    }

    /// <summary>
    /// Resolves the active virtual desktop inside a transient MTA session.
    /// </summary>
    public static VirtualDesktopInfo? ResolveCurrentDesktop(VirtualDesktopVtableConfig? configOverride = null)
    {
        using var session = TryCreate(configOverride);
        return session?.GetCurrentDesktop();
    }

    /// <summary>
    /// Enumerates all virtual desktops inside a transient MTA session.
    /// </summary>
    public static IReadOnlyList<VirtualDesktopInfo> ResolveAllDesktops(VirtualDesktopVtableConfig? configOverride = null)
    {
        using var session = TryCreate(configOverride);
        return session?.GetDesktops() ?? [];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SafeRelease(ref _managerInternal);
        SafeRelease(ref _serviceProvider);

        if (_shouldUninitializeCom)
        {
            try
            {
                CoUninitialize();
            }
            catch
            {
                // Suppress uninitialization failure
            }
        }
    }

    internal static void SafeRelease(ref nint pUnknown)
    {
        if (pUnknown != nint.Zero)
        {
            try
            {
                Marshal.Release(pUnknown);
            }
            catch
            {
                // Suppress RPC transport and proxy failures during release
            }
            finally
            {
                pUnknown = nint.Zero;
            }
        }
    }

    internal static unsafe string ReadHStringAndRelease(nint hstring)
    {
        if (hstring == nint.Zero)
        {
            return string.Empty;
        }

        try
        {
            uint length = 0;
            char* rawBuffer = WindowsGetStringRawBuffer(hstring, &length);
            if (rawBuffer != null && length > 0)
            {
                return new string(rawBuffer, 0, (int)length);
            }
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            try
            {
                WindowsDeleteString(hstring);
            }
            catch
            {
                // Suppress string deallocation failure
            }
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(
        in Guid rclsid,
        nint pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out nint ppv);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static unsafe extern char* WindowsGetStringRawBuffer(nint hstring, uint* length);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint hstring);
}
