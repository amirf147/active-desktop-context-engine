// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Threading.Tasks;
using ADCE.Extraction.Events;
using Xunit;

namespace ADCE.Extraction.Tests;

public class WinEventHookTests
{
    [Fact]
    public void Lifecycle_StartAndStop_TransitionsIsRunningCorrectly()
    {
        using var hook = new WinEventHookProvider(64);

        Assert.False(hook.IsRunning);

        hook.Start();
        Assert.True(hook.IsRunning);

        hook.Stop();
        Assert.False(hook.IsRunning);
    }

    [Fact]
    public void Start_MultipleCallsAreIdempotent()
    {
        using var hook = new WinEventHookProvider(64);

        hook.Start();
        hook.Start();
        Assert.True(hook.IsRunning);

        hook.Stop();
        Assert.False(hook.IsRunning);
    }

    [Fact]
    public void Stop_MultipleCallsAreIdempotent()
    {
        using var hook = new WinEventHookProvider(64);

        hook.Start();
        hook.Stop();
        hook.Stop();
        Assert.False(hook.IsRunning);
    }

    [Fact]
    public void Dispose_ClosesChannelReader()
    {
        var hook = new WinEventHookProvider(64);
        hook.Start();
        hook.Dispose();

        Assert.False(hook.IsRunning);
        Assert.True(hook.EventReader.Completion.IsCompleted);
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        var hook = new WinEventHookProvider(64);
        hook.Dispose();

        Assert.Throws<ObjectDisposedException>(() => hook.Start());
    }
}
