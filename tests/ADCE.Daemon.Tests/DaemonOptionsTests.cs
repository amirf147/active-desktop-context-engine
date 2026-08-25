// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using ADCE.Daemon.Configuration;
using Xunit;

namespace ADCE.Daemon.Tests;

public sealed class DaemonOptionsTests
{
    [Fact]
    public void Parse_DefaultArguments_SetsCanonicalDefaults()
    {
        var options = DaemonOptions.Parse(Array.Empty<string>());

        Assert.False(options.IsHeadless);
        Assert.False(options.IsStdio);
        Assert.True(options.EnableSse);
        Assert.Equal(8424, options.Port);
        Assert.Null(options.DatabasePath);
        Assert.Equal(50, options.DebounceMs);
        Assert.Equal(250, options.MaxBurstMs);
        Assert.False(options.ShowHelp);
        Assert.False(options.ShowVersion);
        Assert.False(options.ShowStatus);
        Assert.False(options.ShowHud);
    }

    [Fact]
    public void Parse_HudFlags_EnablesHud()
    {
        var opt1 = DaemonOptions.Parse(new[] { "--hud" });
        var opt2 = DaemonOptions.Parse(new[] { "-H" });

        Assert.True(opt1.ShowHud);
        Assert.True(opt2.ShowHud);
    }

    [Fact]
    public void Parse_StdioFlag_EnablesStdioAndHeadless()
    {
        var options = DaemonOptions.Parse(new[] { "--stdio" });

        Assert.True(options.IsStdio);
        Assert.True(options.IsHeadless);
    }

    [Fact]
    public void Parse_HeadlessFlags_EnablesHeadless()
    {
        var opt1 = DaemonOptions.Parse(new[] { "--headless" });
        var opt2 = DaemonOptions.Parse(new[] { "--no-tray" });
        var opt3 = DaemonOptions.Parse(new[] { "-n" });

        Assert.True(opt1.IsHeadless);
        Assert.True(opt2.IsHeadless);
        Assert.True(opt3.IsHeadless);
    }

    [Fact]
    public void Parse_CustomPort_UpdatesPortAndKeepsSseEnabled()
    {
        var options = DaemonOptions.Parse(new[] { "--port", "9123" });

        Assert.True(options.EnableSse);
        Assert.Equal(9123, options.Port);
    }

    [Fact]
    public void Parse_DisableSseFlag_DisablesSse()
    {
        var opt1 = DaemonOptions.Parse(new[] { "--no-sse" });
        var opt2 = DaemonOptions.Parse(new[] { "--disable-sse" });

        Assert.False(opt1.EnableSse);
        Assert.False(opt2.EnableSse);
    }

    [Fact]
    public void Parse_CustomDatabasePath_SetsDatabasePath()
    {
        var options = DaemonOptions.Parse(new[] { "--db-path", "test_storage/custom_adce.db" });

        Assert.Equal("test_storage/custom_adce.db", options.DatabasePath);
    }

    [Fact]
    public void Parse_DebounceAndMaxBurst_UpdatesDelays()
    {
        var options = DaemonOptions.Parse(new[] { "--debounce", "75", "--max-burst", "300" });

        Assert.Equal(75, options.DebounceMs);
        Assert.Equal(300, options.MaxBurstMs);
    }

    [Fact]
    public void Parse_HelpAndVersionFlags_SetsBooleans()
    {
        var optHelp = DaemonOptions.Parse(new[] { "--help" });
        var optVer = DaemonOptions.Parse(new[] { "-v" });
        var optStat = DaemonOptions.Parse(new[] { "--status" });

        Assert.True(optHelp.ShowHelp);
        Assert.True(optVer.ShowVersion);
        Assert.True(optStat.ShowStatus);
    }

    [Fact]
    public void ResolveEffectiveDatabasePath_Default_ReturnsLocalAppDataPath()
    {
        var options = new DaemonOptions();
        var path = options.ResolveEffectiveDatabasePath();

        Assert.NotNull(path);
        Assert.Contains("ADCE", path);
        Assert.EndsWith("adce_history.db", path);
    }

    [Fact]
    public void GetHelpText_ContainsAllCoreOptions()
    {
        var help = DaemonOptions.GetHelpText();

        Assert.Contains("--help", help);
        Assert.Contains("--stdio", help);
        Assert.Contains("--sse", help);
        Assert.Contains("--port", help);
        Assert.Contains("8424", help);
    }
}
