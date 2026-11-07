// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using ADCE.Extraction.Workspaces.Win32Com;
using Xunit;

namespace ADCE.Extraction.Tests;

public class VirtualDesktopGuidsTests
{
    [Fact]
    public void WellKnownGuids_MatchExpectedDefinitions()
    {
        Assert.Equal(new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239"), VirtualDesktopGuids.CLSID_ImmersiveShell);
        Assert.Equal(new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B"), VirtualDesktopGuids.CLSID_VirtualDesktopManagerInternal);
        Assert.Equal(new Guid("AA509085-0A56-466E-8A88-89B846E17610"), VirtualDesktopGuids.CLSID_VirtualDesktopManager);
        Assert.Equal(new Guid("A5CD92DD-0160-458F-9F0F-B27018D27541"), VirtualDesktopGuids.IID_IVirtualDesktopManager);
        Assert.Equal(new Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), VirtualDesktopGuids.IID_IServiceProvider);
        Assert.Equal(new Guid("C17949F0-3DE2-4DD0-BE55-520B342BD7AC"), VirtualDesktopGuids.IID_IVirtualDesktopNotification);
        Assert.Equal(new Guid("0CD45E71-5278-4ACD-9686-4973EABF45EA"), VirtualDesktopGuids.IID_IVirtualDesktopNotificationService);
        Assert.Equal(new Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), VirtualDesktopGuids.IID_IObjectArray);
    }

    [Fact]
    public void BuildSpecificGuids_MatchExpectedDefinitions()
    {
        // Build 26100+
        Assert.Equal(new Guid("53F5CA0B-158F-4124-900C-057158060B27"), VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_26100);
        Assert.Equal(new Guid("3F07F4BE-B107-441A-AF0F-39D82529072C"), VirtualDesktopGuids.IID_IVirtualDesktop_26100);

        // Build 22631
        Assert.Equal(new Guid("4970BA3D-FD4E-4647-BEA3-D89076EF4B9C"), VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22631);
        Assert.Equal(new Guid("3F07F4BE-B107-441A-AF0F-39D82529072C"), VirtualDesktopGuids.IID_IVirtualDesktop_22631);

        // Build 22621
        Assert.Equal(new Guid("A3175F2D-239C-4BD2-8AA0-EEBA8B0B138E"), VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22621);
        Assert.Equal(new Guid("3F07F4BE-B107-441A-AF0F-39D82529072C"), VirtualDesktopGuids.IID_IVirtualDesktop_22621);

        // Build 22000
        Assert.Equal(new Guid("B2F925B9-5A0F-4D2E-9F4D-2B1507593C10"), VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_22000);
        Assert.Equal(new Guid("4CD1900C-0156-430B-B708-EB7161624A42"), VirtualDesktopGuids.IID_IVirtualDesktop_22000);

        // Build 19041
        Assert.Equal(new Guid("F3157EB4-65E7-45B4-BA3B-626F9E595D30"), VirtualDesktopGuids.IID_IVirtualDesktopManagerInternal_19041);
        Assert.Equal(new Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4"), VirtualDesktopGuids.IID_IVirtualDesktop_19041);
    }
}
