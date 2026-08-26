// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using ADCE.Extraction.Win32;
using Xunit;

namespace ADCE.Extraction.Tests;

public class Win32GatingTests
{
    [Fact]
    public void IsWindowValidAndVisible_ReturnsFalseForZeroHandle()
    {
        bool result = Win32Gating.IsWindowValidAndVisible(nint.Zero);
        Assert.False(result);
    }

    [Fact]
    public void GetWindowIdentityFast_ReturnsFalseForInvalidHandle()
    {
        bool result = Win32Gating.GetWindowIdentityFast(nint.Zero, out var title, out var className, out var pid, out var processName);
        Assert.False(result);
        Assert.Equal(string.Empty, title);
        Assert.Equal(string.Empty, className);
        Assert.Equal(0, pid);
        Assert.Equal(string.Empty, processName);
    }

    [Fact]
    public void CanAccessProcess_HandlesZeroCleanlyWithoutThrowing()
    {
        bool result = Win32Gating.CanAccessProcess(nint.Zero);
        Assert.False(result);
    }
}
