// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.IO;
using System.Threading;
using ADCE.Core.Logging;
using Xunit;

namespace ADCE.Core.Tests;

public class AdceLoggerTests
{
    [Fact]
    public void AdceLogger_MemoryBuffer_CapturesFormattedEntries()
    {
        using var logger = new AdceLogger(logFilePath: null, minimumLevel: AdceLogLevel.Debug);

        logger.Debug("TestTag", "Debug message");
        logger.Info("TestTag", "Info message");
        logger.Warn("TestTag", "Warn message", new InvalidOperationException("Simulated warn"));
        logger.Error("TestTag", "Error message", new Exception("Simulated error"));

        var logs = logger.GetRecentLogs();

        Assert.Equal(4, logs.Length);
        Assert.Contains("[DEBUG] [TestTag] Debug message", logs[0]);
        Assert.Contains("[INFO ] [TestTag] Info message", logs[1]);
        Assert.Contains("[WARN ] [TestTag] Warn message", logs[2]);
        Assert.Contains("InvalidOperationException", logs[2]);
        Assert.Contains("[ERROR] [TestTag] Error message", logs[3]);
    }

    [Fact]
    public void AdceLogger_MinimumLevelFiltering_DropsLowerSeverityMessages()
    {
        using var logger = new AdceLogger(logFilePath: null, minimumLevel: AdceLogLevel.Warn);

        logger.Debug("FilterTag", "Dropped debug");
        logger.Info("FilterTag", "Dropped info");
        logger.Warn("FilterTag", "Kept warn");
        logger.Error("FilterTag", "Kept error");

        var logs = logger.GetRecentLogs();

        Assert.Equal(2, logs.Length);
        Assert.Contains("[WARN ]", logs[0]);
        Assert.Contains("[ERROR]", logs[1]);
    }

    [Fact]
    public void AdceLogger_FileWriting_EmitsLinesToDisk()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"adce_test_log_{Guid.NewGuid():N}.log");

        try
        {
            using (var logger = new AdceLogger(tempFile, AdceLogLevel.Info))
            {
                logger.Info("DiskTest", "File persistence test line");
            }

            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("[INFO ] [DiskTest] File persistence test line", content);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
