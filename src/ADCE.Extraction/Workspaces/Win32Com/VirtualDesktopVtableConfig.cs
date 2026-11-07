// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;

namespace ADCE.Extraction.Workspaces.Win32Com;

/// <summary>
/// Vtable slot offsets and interface identifiers mapped to specific Windows build tiers.
/// </summary>
internal sealed record VirtualDesktopVtableConfig
{
    public required int BuildMin { get; init; }
    public required int BuildMax { get; init; }
    public required string Name { get; init; }

    public required Guid ManagerIid { get; init; }
    public required Guid DesktopIid { get; init; }

    public required int SlotGetCount { get; init; }
    public required int SlotGetCurrentDesktop { get; init; }
    public required int SlotGetDesktops { get; init; }

    public required int SlotVdGetId { get; init; }
    public required int? SlotVdGetName { get; init; }

    public required bool GetCurrentTakesHwnd { get; init; }
    public required bool GetDesktopsTakesHwnd { get; init; }

    public static readonly VirtualDesktopVtableConfig Config26100 = new()
    {
        BuildMin = 26100,
        BuildMax = int.MaxValue,
        Name = "Windows 11 24H2 / Canary (Build 26100+)",
        ManagerIid = VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_26100,
        DesktopIid = VirtualDesktopGuids.IID_IVirtualDesktop_26100,
        SlotGetCount = 3,
        SlotGetCurrentDesktop = 6,
        SlotGetDesktops = 7,
        SlotVdGetId = 4,
        SlotVdGetName = 5,
        GetCurrentTakesHwnd = false,
        GetDesktopsTakesHwnd = false
    };

    public static readonly VirtualDesktopVtableConfig Config22631 = new()
    {
        BuildMin = 22631,
        BuildMax = 26099,
        Name = "Windows 11 23H2 (Build 22631)",
        ManagerIid = VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22631,
        DesktopIid = VirtualDesktopGuids.IID_IVirtualDesktop_22631,
        SlotGetCount = 3,
        SlotGetCurrentDesktop = 6,
        SlotGetDesktops = 7,
        SlotVdGetId = 4,
        SlotVdGetName = 5,
        GetCurrentTakesHwnd = false,
        GetDesktopsTakesHwnd = false
    };

    public static readonly VirtualDesktopVtableConfig Config22621 = new()
    {
        BuildMin = 22621,
        BuildMax = 22630,
        Name = "Windows 11 22H2 (Build 22621)",
        ManagerIid = VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22621,
        DesktopIid = VirtualDesktopGuids.IID_IVirtualDesktop_22621,
        SlotGetCount = 3,
        SlotGetCurrentDesktop = 6,
        SlotGetDesktops = 7,
        SlotVdGetId = 4,
        SlotVdGetName = 5,
        GetCurrentTakesHwnd = false,
        GetDesktopsTakesHwnd = false
    };

    public static readonly VirtualDesktopVtableConfig Config22000 = new()
    {
        BuildMin = 22000,
        BuildMax = 22620,
        Name = "Windows 11 21H2 (Build 22000)",
        ManagerIid = VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22000,
        DesktopIid = VirtualDesktopGuids.IID_IVirtualDesktop_22000,
        SlotGetCount = 3,
        SlotGetCurrentDesktop = 6,
        SlotGetDesktops = 7,
        SlotVdGetId = 4,
        SlotVdGetName = 6,
        GetCurrentTakesHwnd = true,
        GetDesktopsTakesHwnd = true
    };

    public static readonly VirtualDesktopVtableConfig Config19041 = new()
    {
        BuildMin = 19041,
        BuildMax = 21999,
        Name = "Windows 10 2004 - 22H2 (Build 19041)",
        ManagerIid = VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_19041,
        DesktopIid = VirtualDesktopGuids.IID_IVirtualDesktop_19041,
        SlotGetCount = 3,
        SlotGetCurrentDesktop = 6,
        SlotGetDesktops = 7,
        SlotVdGetId = 4,
        SlotVdGetName = null,
        GetCurrentTakesHwnd = false,
        GetDesktopsTakesHwnd = false
    };

    /// <summary>
    /// Resolves the vtable configuration for a specified Windows OS build number.
    /// </summary>
    public static VirtualDesktopVtableConfig? Resolve(int buildNumber)
    {
        if (buildNumber >= Config26100.BuildMin)
        {
            return Config26100;
        }
        if (buildNumber >= Config22631.BuildMin)
        {
            return Config22631;
        }
        if (buildNumber >= Config22621.BuildMin)
        {
            return Config22621;
        }
        if (buildNumber >= Config22000.BuildMin)
        {
            return Config22000;
        }
        if (buildNumber >= Config19041.BuildMin)
        {
            return Config19041;
        }
        return null;
    }

    /// <summary>
    /// Gets the vtable configuration matching the host operating system build.
    /// </summary>
    public static VirtualDesktopVtableConfig? Current => Resolve(Environment.OSVersion.Version.Build);
}
