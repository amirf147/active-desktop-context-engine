// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ADCE.Spikes.Native;

namespace ADCE.Spikes.Diagnostics;

/// <summary>
/// Empirical diagnostic probe testing real-world Windows OS boundaries:
/// DWM cloaking (virtual desktop shifts), process integrity (UIPI), and sub-AUMID (~Wh~) window topology.
/// </summary>
internal static partial class StressProbe
{
    private const int DWMWA_CLOAKED = 14;
    private const uint DWM_CLOAKED_APP = 1;
    private const uint DWM_CLOAKED_SHELL = 2;
    private const uint DWM_CLOAKED_INHERITED = 4;

    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenIntegrityLevel = 25;

    private static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    [GeneratedRegex(@"~Wh~w([0-9a-fA-F]+)", RegexOptions.Compiled)]
    private static partial Regex SubAumidRegex();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out uint pvAttribute, int cbAttribute);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, out IPropertyStore propertyStore);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr pSid, uint nSubAuthority);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr pSid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;

        public PROPERTYKEY(Guid fmtid, uint pid)
        {
            this.fmtid = fmtid;
            this.pid = pid;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        int GetCount(out uint count);
        int GetAt(uint iProp, out PROPERTYKEY pkey);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        int Commit();
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    private record WindowProbeResult(
        IntPtr Hwnd,
        uint Pid,
        string ProcessName,
        string Title,
        string ClassName,
        bool IsForeground,
        string CloakStatus,
        string IntegrityLevel,
        string Aumid,
        IntPtr? ParentHwndFromAumid
    );

    public static void RunStressProbe()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Empirical OS Stress & Blind-Spot Diagnostic Probe                  ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
        Console.WriteLine("Testing Live OS Signals: DWM Cloaking, UIPI Integrity, and Sub-AUMIDs (~Wh~)\n");

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        var fg = SpikeNativeMethods.GetForegroundWindow();
        var results = new List<WindowProbeResult>();

        SpikeNativeMethods.EnumDesktopWindows(hDesktop != IntPtr.Zero ? hDesktop : IntPtr.Zero, (hWnd, lParam) =>
        {
            var sbTitle = new StringBuilder(512);
            SpikeNativeMethods.GetWindowText(hWnd, sbTitle, 512);
            var sbClass = new StringBuilder(256);
            SpikeNativeMethods.GetClassName(hWnd, sbClass, 256);
            SpikeNativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);

            string title = sbTitle.ToString();
            string className = sbClass.ToString();


            if (string.IsNullOrWhiteSpace(title) && !className.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Skip common invisible system artifacts
            if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow" or "IME" or "MSCTFIME UI")
            {
                return true;
            }

            string procName = "unknown";
            string integrity = "Unknown";
            string aumid = string.Empty;
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                procName = proc.ProcessName;
                integrity = GetProcessIntegrityLevel(proc.Handle);
                aumid = GetWindowAumid(hWnd, proc.Handle);
            }
            catch (Exception ex)
            {
                integrity = $"Denied ({ex.GetType().Name})";
                aumid = GetWindowAumid(hWnd, IntPtr.Zero);
            }

            string cloak = GetDwmCloakStatus(hWnd);
            IntPtr? parentHwnd = ParseParentHwndFromAumid(aumid);


            results.Add(new WindowProbeResult(
                hWnd,
                pid,
                procName,
                string.IsNullOrWhiteSpace(title) ? "[Windows Terminal / Cascadia]" : title,
                className,
                hWnd == fg,
                cloak,
                integrity,
                aumid,
                parentHwnd
            ));

            return true;
        }, IntPtr.Zero);

        // Display results
        Console.WriteLine($"{"HWND",-10} {"PID",-7} {"Proc",-16} {"Integrity",-12} {"DWM Cloaking",-16} {"Title"}");
        Console.WriteLine(new string('-', 95));

        foreach (var r in results)
        {
            if (r.IsForeground)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(">> ");
            }
            else
            {
                Console.ResetColor();
                Console.Write("   ");
            }

            string truncatedTitle = r.Title.Length > 35 ? r.Title[..32] + "..." : r.Title;
            Console.WriteLine($"0x{r.Hwnd.ToInt64():X8} {r.Pid,-7} {r.ProcessName,-16} {r.IntegrityLevel,-12} {r.CloakStatus,-16} {truncatedTitle}");

            if (!string.IsNullOrEmpty(r.Aumid))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"      └─ AUMID: {r.Aumid}");
                if (r.ParentHwndFromAumid.HasValue)
                {
                    bool validHwnd = IsWindow(r.ParentHwndFromAumid.Value);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"  [~Wh~ Sub-Window ➔ Parent HWND: 0x{r.ParentHwndFromAumid.Value.ToInt64():X8} (Valid: {validHwnd})]");
                }
                Console.WriteLine();
                Console.ResetColor();
            }
        }

        Console.ResetColor();
        Console.WriteLine("\nSummary Analysis:");
        int cloakedShellCount = results.Count(r => r.CloakStatus.Contains("Shell (Other Desktop)"));
        int elevatedCount = results.Count(r => r.IntegrityLevel.Contains("High (Admin)"));
        int subWindowCount = results.Count(r => r.ParentHwndFromAumid.HasValue);

        Console.WriteLine($"• Total Windows Probed:          {results.Count}");
        Console.WriteLine($"• Cloaked on Other Desktops:    {cloakedShellCount} (Filtered at Layer 0 without spatial math)");
        Console.WriteLine($"• Elevated (UIPI Barriers):     {elevatedCount}");
        Console.WriteLine($"• Sub-Windows Detected (~Wh~):  {subWindowCount}");
    }

    private static string GetDwmCloakStatus(IntPtr hwnd)
    {
        int hr = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out uint cloaked, sizeof(uint));
        if (hr != 0)
        {
            return "N/A";
        }

        return cloaked switch
        {
            0 => "Active",
            DWM_CLOAKED_APP => "App Cloaked",
            DWM_CLOAKED_SHELL => "Shell (Other Desktop)",
            DWM_CLOAKED_INHERITED => "Inherited Cloak",
            _ => $"Cloaked ({cloaked})"
        };
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr hProcess, ref uint AppUserModelIdLength, StringBuilder sbAppUserModelId);

    private static string GetWindowAumid(IntPtr hwnd, IntPtr hProcess)
    {
        // 1. Try window-level property store (used for sub-windows and explicit taskbar identities)
        try
        {
            var iid = IID_IPropertyStore;
            int hr = SHGetPropertyStoreForWindow(hwnd, ref iid, out var store);
            if (hr == 0 && store != null)
            {
                var key = PKEY_AppUserModel_ID;
                hr = store.GetValue(ref key, out var pv);
                if (hr == 0 && pv.vt == 31 /* VT_LPWSTR */ && pv.p != IntPtr.Zero)
                {
                    string? val = Marshal.PtrToStringUni(pv.p);
                    PropVariantClear(ref pv);
                    if (!string.IsNullOrEmpty(val)) return val;
                }
                PropVariantClear(ref pv);
            }
        }
        catch { }

        // 2. Try process-level package AUMID (for UWP / MSIX / packaged apps like Terminal, Settings)
        if (hProcess != IntPtr.Zero)
        {
            try
            {
                uint length = 0;
                int err = GetApplicationUserModelId(hProcess, ref length, null!);
                if (length > 0)
                {
                    var sb = new StringBuilder((int)length);
                    err = GetApplicationUserModelId(hProcess, ref length, sb);
                    if (err == 0 && sb.Length > 0)
                    {
                        return sb.ToString();
                    }
                }
            }
            catch { }
        }

        return string.Empty;
    }


    private static IntPtr? ParseParentHwndFromAumid(string aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return null;

        var match = SubAumidRegex().Match(aumid);
        if (match.Success && match.Groups.Count > 1)
        {
            string hex = match.Groups[1].Value;
            if (long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hwndLong))
            {
                return new IntPtr(hwndLong);
            }
        }

        return null;
    }

    private static string GetProcessIntegrityLevel(IntPtr processHandle)
    {
        if (!OpenProcessToken(processHandle, TOKEN_QUERY, out var tokenHandle))
        {
            return "Denied";
        }

        try
        {
            GetTokenInformation(tokenHandle, TokenIntegrityLevel, IntPtr.Zero, 0, out uint length);
            if (length == 0) return "Unknown";

            var buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (!GetTokenInformation(tokenHandle, TokenIntegrityLevel, buffer, length, out _))
                {
                    return "Error";
                }

                // TOKEN_MANDATORY_LABEL layout: SID_AND_ATTRIBUTES -> Sid (IntPtr at offset 0)
                var pSid = Marshal.ReadIntPtr(buffer);
                if (pSid == IntPtr.Zero) return "Unknown";

                var countPtr = GetSidSubAuthorityCount(pSid);
                if (countPtr == IntPtr.Zero) return "Unknown";

                byte count = Marshal.ReadByte(countPtr);
                if (count == 0) return "Unknown";

                var subAuthorityPtr = GetSidSubAuthority(pSid, (uint)(count - 1));
                if (subAuthorityPtr == IntPtr.Zero) return "Unknown";

                int rid = Marshal.ReadInt32(subAuthorityPtr);
                return rid switch
                {
                    < 0x2000 => "Low",
                    < 0x3000 => "Medium",
                    < 0x4000 => "High (Admin)",
                    >= 0x4000 => "System",
                };
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }
}
