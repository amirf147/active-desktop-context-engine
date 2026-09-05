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

        var hWinSta = SpikeNativeMethods.OpenWindowStation("WinSta0", false, 0x37F);
        if (hWinSta != IntPtr.Zero) SpikeNativeMethods.SetProcessWindowStation(hWinSta);
        var hDesktop = SpikeNativeMethods.OpenDesktop("Default", 0, false, 0x1FF);
        if (hDesktop != IntPtr.Zero) SpikeNativeMethods.SetThreadDesktop(hDesktop);

        // Locate active Cascadia window for WinUI3Xaml sampling
        IntPtr cascadiaHwnd = IntPtr.Zero;
        SpikeNativeMethods.EnumDesktopWindows(hDesktop != IntPtr.Zero ? hDesktop : IntPtr.Zero, (hWnd, lParam) =>
        {
            var sbClass = new System.Text.StringBuilder(256);
            SpikeNativeMethods.GetClassName(hWnd, sbClass, 256);
            if (sbClass.ToString().StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase))
            {
                cascadiaHwnd = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (cascadiaHwnd != IntPtr.Zero)
        {
            SpikeNativeMethods.ForceForegroundWindow(cascadiaHwnd);
            await Task.Delay(200);
        }


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
        FlaUI.Core.AutomationElements.AutomationElement? rootWindow = null;
        if (fgHwnd != IntPtr.Zero)
        {
            try { rootWindow = automation.FromHandle(fgHwnd); } catch { }
        }

        if (rootWindow == null)
        {
            // Walk up raw tree walker from focused element to find root window
            var walker = automation.TreeWalkerFactory.GetRawViewWalker();
            var curr = focused;
            while (curr != null)
            {
                try
                {
                    if (curr.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.Window)
                    {
                        rootWindow = curr;
                        fgHwnd = curr.Properties.NativeWindowHandle.ValueOrDefault;
                        break;
                    }
                    curr = walker.GetParent(curr);
                }
                catch { break; }
            }
        }

        if (rootWindow == null)
        {
            // Fallback: search desktop for Cascadia window
            cascadiaHwnd = IntPtr.Zero;
            SpikeNativeMethods.EnumDesktopWindows(hDesktop != IntPtr.Zero ? hDesktop : IntPtr.Zero, (hWnd, lParam) =>
            {
                var sbClass = new System.Text.StringBuilder(256);
                SpikeNativeMethods.GetClassName(hWnd, sbClass, 256);
                if (sbClass.ToString().StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase))
                {
                    cascadiaHwnd = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (cascadiaHwnd != IntPtr.Zero)
            {
                fgHwnd = cascadiaHwnd;
                try { rootWindow = automation.FromHandle(cascadiaHwnd); } catch { }
            }
        }

        if (rootWindow == null)
        {
            rootWindow = automation.GetDesktop();
            fgHwnd = rootWindow.Properties.NativeWindowHandle.ValueOrDefault;
        }

        var stepResult = SurfaceHarvester.HarvestStep(
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

        // Update telemetry.json with the freshly captured step
        try
        {
            string mediaDir = Path.GetDirectoryName(outputScreenshotPath) ?? "";
            string jsonPath = Path.Combine(mediaDir, "telemetry.json");
            var existingSteps = new List<SurfaceTelemetryStep>();
            if (File.Exists(jsonPath))
            {
                string existingJson = File.ReadAllText(jsonPath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<List<SurfaceTelemetryStep>>(existingJson);
                if (loaded != null) existingSteps = loaded;
            }

            existingSteps.RemoveAll(s => s.Step == stepNum);
            existingSteps.Add(stepResult);
            existingSteps.Sort((a, b) => a.Step.CompareTo(b.Step));

            string outJson = System.Text.Json.JsonSerializer.Serialize(existingSteps, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, outJson);
            Console.WriteLine($"  [SAMPLER] Updated {jsonPath} with Step {stepNum}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [WARNING] Could not update telemetry.json: {ex.Message}");
        }

        return stepResult;
    }
}
