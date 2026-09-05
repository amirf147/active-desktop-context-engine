// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.IO;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using ADCE.Spikes.Native;
using FlaUI.Core;
using FlaUI.UIA3;

namespace ADCE.Spikes.Profiling;

/// <summary>
/// Interactive delayed surface sampler. Allows the operator to activate complex,
/// modal, or elevated UI surfaces manually while the engine captures the resulting
/// physical focus, ancestor chain, bounding box, and visual screenshot.
/// </summary>
internal static class InteractiveSurfaceSampler
{
    public static async Task<SurfaceTelemetryStep?> SampleActiveFocusAsync(
        int delaySeconds,
        int stepNum,
        string zoneTag,
        string description,
        string stimulus,
        string outputScreenshotPath,
        DesktopAppArchetype archetype)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n[INTERACTIVE SAMPLER] Step {stepNum}: {description}");
        Console.WriteLine($"  Stimulus: {stimulus}");
        for (int i = delaySeconds; i > 0; i--)
        {
            Console.WriteLine($"  Capturing active surface in {i}s... (Activate the target surface now)");
            await Task.Delay(1000);
        }
        Console.WriteLine("  Sampling active focused element now!\n");
        Console.ResetColor();

        using var automation = new UIA3Automation();
        using var engine = new UiaExtractionEngine();

        var focused = automation.FocusedElement();
        if (focused == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  [ERROR] No active focused element detected on desktop.");
            Console.ResetColor();
            return null;
        }

        var fgHwnd = SpikeNativeMethods.GetForegroundWindow();
        var rootWindow = automation.FromHandle(fgHwnd);
        if (rootWindow == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [ERROR] Could not resolve root window from foreground HWND 0x{fgHwnd:X8}");
            Console.ResetColor();
            return null;
        }

        return SurfaceHarvester.HarvestStep(
            automation,
            engine,
            fgHwnd,
            rootWindow,
            focused,
            stepNum,
            zoneTag,
            description,
            stimulus,
            outputScreenshotPath,
            archetype
        );
    }
}
