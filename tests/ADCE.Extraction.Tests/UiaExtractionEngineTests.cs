// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Extraction.Engine;
using Xunit;

namespace ADCE.Extraction.Tests;

public class UiaExtractionEngineTests
{
    [Fact]
    public async Task ExtractSnapshotAsync_HandlesInvalidHwndGracefully()
    {
        using var engine = new UiaExtractionEngine();
        var snapshot = await engine.ExtractSnapshotAsync(nint.Zero);

        Assert.NotNull(snapshot);
        Assert.Equal((nint)0, snapshot.Window.Hwnd);
        Assert.Equal(DesktopAppArchetype.Unknown, snapshot.Window.Archetype);
        Assert.Null(snapshot.IdeContext);
        Assert.Null(snapshot.BrowserContext);
        Assert.Null(snapshot.ExplorerContext);
        Assert.Null(snapshot.TerminalContext);
    }

    [Fact]
    public async Task ExtractForegroundSnapshotAsync_ReturnsValidSnapshotInstance()
    {
        using var engine = new UiaExtractionEngine();
        var snapshot = await engine.ExtractForegroundSnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Window);
        Assert.NotNull(snapshot.Workspace);
        Assert.NotNull(snapshot.Focus);
        Assert.True(snapshot.ExtractionDurationMs >= 0.0);
    }
}
