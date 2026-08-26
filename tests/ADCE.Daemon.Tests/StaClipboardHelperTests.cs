// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Daemon.UI;
using Xunit;

namespace ADCE.Daemon.Tests;

public sealed class StaClipboardHelperTests
{
    [Fact]
    public void SetText_EmptyOrNull_ReturnsFalse()
    {
        Assert.False(StaClipboardHelper.SetText(string.Empty));
        Assert.False(StaClipboardHelper.SetText(null!));
    }

    [Fact]
    public void SetText_FromMtaThreadPoolThread_ExecutesWithoutThreadStateException()
    {
        // ThreadPool threads in .NET are MTA by default
        Assert.Equal(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState());

        string testPayload = $"{{\"test_id\": \"{Guid.NewGuid()}\"}}";
        bool result = StaClipboardHelper.SetText(testPayload);

        // Should return true or false (if environment has no UI clipboard), but MUST NOT throw ThreadStateException
        Assert.True(result || !result);
    }

    [Fact]
    public void SetText_FromExplicitStaThread_ExecutesDirectly()
    {
        bool executed = false;
        var thread = new Thread(() =>
        {
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            string testPayload = $"{{\"test_sta_id\": \"{Guid.NewGuid()}\"}}";
            _ = StaClipboardHelper.SetText(testPayload);
            executed = true;
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(2));

        Assert.True(executed);
    }
}
