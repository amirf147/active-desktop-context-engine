// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ADCE.Extraction.Tests.Architecture;

public class ExtractorInvariantTests
{
    [Fact]
    public void Extractors_DoNotContainUnboundedFindAllDescendantsOnWindowElement()
    {
        // Locate src/ADCE.Extraction/Extractors
        var baseDir = AppContext.BaseDirectory;
        var srcDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "ADCE.Extraction", "Extractors"));

        Assert.True(Directory.Exists(srcDir), $"Extractors directory not found at: {srcDir}");

        var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(csFiles);

        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                // Disallow windowElement.FindAllDescendants
                if (trimmed.Contains("windowElement.FindAllDescendants", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail($"Architectural Invariant Violation in {Path.GetFileName(file)} at line {i + 1}: Calling 'windowElement.FindAllDescendants' is forbidden because it causes unbounded out-of-process COM tree crawling.");
                }
            }
        }
    }
}
