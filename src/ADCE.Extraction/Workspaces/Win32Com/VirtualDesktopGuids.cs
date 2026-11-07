// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;

namespace ADCE.Extraction.Workspaces.Win32Com;

/// <summary>
/// Curated COM class identifiers (CLSIDs) and interface identifiers (IIDs) for Windows Virtual Desktops.
/// </summary>
internal static class VirtualDesktopGuids
{
    /// <summary>Well-known COM class identifier for ImmersiveShell.</summary>
    public static readonly Guid CLSID_ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");

    /// <summary>Service and class identifier for IVirtualDesktopManagerInternal.</summary>
    public static readonly Guid CLSID_VirtualDesktopManagerInternal = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");

    /// <summary>Public COM class identifier for VirtualDesktopManager.</summary>
    public static readonly Guid CLSID_VirtualDesktopManager = new("AA509085-0A56-466E-8A88-89B846E17610");

    /// <summary>Native Windows registered class identifier for VirtualDesktopManager.</summary>
    public static readonly Guid CLSID_VirtualDesktopManager_Native = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    /// <summary>Public COM interface identifier for IVirtualDesktopManager.</summary>
    public static readonly Guid IID_IVirtualDesktopManager = new("A5CD92DD-0160-458F-9F0F-B27018D27541");

    /// <summary>Native Windows registered interface identifier for IVirtualDesktopManager.</summary>
    public static readonly Guid IID_IVirtualDesktopManager_Native = new("A5CD92FF-29BE-454C-8D04-D82879FB3F1B");

    /// <summary>Standard OLE IServiceProvider interface identifier.</summary>
    public static readonly Guid IID_IServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");

    /// <summary>Interface identifier for IVirtualDesktopNotification (Win10 fallback default).</summary>
    public static readonly Guid IID_IVirtualDesktopNotification = new("C17949F0-3DE2-4DD0-BE55-520B342BD7AC");

    /// <summary>Interface identifier for IVirtualDesktopNotification on Windows 11.</summary>
    public static readonly Guid IID_IVirtualDesktopNotification_Win11 = new("B9E5E94D-233E-49AB-AF5C-2B4541C3AADE");

    /// <summary>Interface identifier for IVirtualDesktopNotification on Windows 10.</summary>
    public static readonly Guid IID_IVirtualDesktopNotification_Win10 = new("C17949F0-3DE2-4DD0-BE55-520B342BD7AC");

    /// <summary>Service identifier for IVirtualDesktopNotificationService on Windows 11.</summary>
    public static readonly Guid CLSID_VirtualDesktopNotificationService = new("A501FDEC-4A09-464C-AE4E-1B9C21B84918");

    /// <summary>Service identifier for IVirtualDesktopNotificationService on Windows 10.</summary>
    public static readonly Guid CLSID_VirtualDesktopNotificationService_Win10 = new("0CD45E71-5278-4ACD-9686-4973EABF45EA");

    /// <summary>Interface identifier for IVirtualDesktopNotificationService (Win10 default).</summary>
    public static readonly Guid IID_IVirtualDesktopNotificationService = new("0CD45E71-5278-4ACD-9686-4973EABF45EA");

    /// <summary>Interface identifier for IVirtualDesktopNotificationService on Windows 11.</summary>
    public static readonly Guid IID_IVirtualDesktopNotificationService_Win11 = new("0CD45E71-D927-4F15-8B0A-8FEF525337BF");

    /// <summary>Interface identifier for IVirtualDesktopNotificationService on Windows 10.</summary>
    public static readonly Guid IID_IVirtualDesktopNotificationService_Win10 = new("0CD45E71-5278-4ACD-9686-4973EABF45EA");

    /// <summary>Standard Shell IObjectArray interface identifier.</summary>
    public static readonly Guid IID_IObjectArray = new("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");

    // Build-specific manager and desktop interface identifiers
    // Build 26100+ (Windows 11 24H2 / Canary)
    public static readonly Guid IID_IVirtualDesktopManagerInternal_26100 = new("53F5CA0B-158F-4124-900C-057158060B27");
    public static readonly Guid IID_IVirtualDesktop_26100 = new("3F07F4BE-B107-441A-AF0F-39D82529072C");

    // Build 22631 (Windows 11 23H2)
    public static readonly Guid IID_IVirtualDesktopManagerInternal_22631 = new("4970BA3D-FD4E-4647-BEA3-D89076EF4B9C");
    public static readonly Guid IID_IVirtualDesktop_22631 = new("3F07F4BE-B107-441A-AF0F-39D82529072C");

    // Build 22621 (Windows 11 22H2)
    public static readonly Guid IID_IVirtualDesktopManagerInternal_22621 = new("A3175F2D-239C-4BD2-8AA0-EEBA8B0B138E");
    public static readonly Guid IID_IVirtualDesktop_22621 = new("3F07F4BE-B107-441A-AF0F-39D82529072C");

    // Build 22000 (Windows 11 21H2)
    public static readonly Guid IID_IVirtualDesktopManagerInternal_22000 = new("B2F925B9-5A0F-4D2E-9F4D-2B1507593C10");
    public static readonly Guid IID_IVirtualDesktop_22000 = new("4CD1900C-0156-430B-B708-EB7161624A42");

    // Build 19041 (Windows 10 2004 - 22H2)
    public static readonly Guid IID_IVirtualDesktopManagerInternal_19041 = new("F3157EB4-65E7-45B4-BA3B-626F9E595D30");
    public static readonly Guid IID_IVirtualDesktop_19041 = new("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4");
}
