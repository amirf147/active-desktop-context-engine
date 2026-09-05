// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ADCE.Spikes.Profiling;

public record SurfaceAncestorNode(
    int Depth,
    string Type,
    string Name,
    string AutoId,
    string Cls
);

public record SurfaceTelemetryStep(
    int Step,
    string ZoneTag,
    string Description,
    string Stimulus,
    string ControlType,
    string ElementName,
    string AutomationId,
    string ClassName,
    Rectangle Bounds,
    List<SurfaceAncestorNode> AncestorChain,
    DesktopSemanticZone SemanticZone,
    WindowPaneLocation PaneLocation,
    string? ActiveView,
    string? SectionName,
    ImmutableArray<string> SemanticPath,
    string ScreenshotFile
);

/// <summary>
/// Reusable empirical surface telemetry harvester and classifier.
/// </summary>
internal static class SurfaceHarvester
{
    public static SurfaceTelemetryStep HarvestStep(
        AutomationBase automation,
        UiaExtractionEngine engine,
        IntPtr targetHwnd,
        AutomationElement windowElement,
        AutomationElement? element,
        int stepNum,
        string zoneTag,
        string description,
        string stimulus,
        string screenshotFilePath,
        DesktopAppArchetype archetype)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n>>> SURFACE {stepNum}: [{zoneTag}] - {description}");
        Console.ResetColor();

        if (element == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [WARNING] Target element for {zoneTag} was not found.");
            Console.ResetColor();

            return new SurfaceTelemetryStep(
                stepNum, zoneTag, description, stimulus, "Unknown", "Unobserved", "", "",
                Rectangle.Empty, new List<SurfaceAncestorNode>(),
                DesktopSemanticZone.Unknown, WindowPaneLocation.Unknown, null, null,
                ImmutableArray<string>.Empty, Path.GetFileName(screenshotFilePath));
        }

        string cType = SafeType(element).ToString();
        string name = SafeName(element);
        string autoId = SafeId(element);
        string cls = SafeClass(element);
        var rect = SafeBounds(element);

        Console.WriteLine($"  Stimulus       : {stimulus}");
        Console.WriteLine($"  UIA Control    : [{cType}] \"{name}\"");
        Console.WriteLine($"  AutomationId   : \"{autoId}\"");
        Console.WriteLine($"  ClassName      : \"{cls}\"");
        Console.WriteLine($"  Bounds         : [X={rect.X}, Y={rect.Y}, W={rect.Width}, H={rect.Height}]");

        // Harvest physical ancestor chain via raw view walker
        var rawWalker = automation.TreeWalkerFactory.GetRawViewWalker();
        var curr = element;
        int depth = 0;
        var ancestors = new List<SurfaceAncestorNode>();
        while (curr != null && depth < 8)
        {
            try
            {
                var p = rawWalker.GetParent(curr);
                if (p == null) break;
                ancestors.Add(new SurfaceAncestorNode(depth, SafeType(p).ToString(), SafeName(p), SafeId(p), SafeClass(p)));
                curr = p;
                depth++;
            }
            catch { break; }
        }

        Console.WriteLine("  Ancestor Chain :");
        foreach (var a in ancestors)
        {
            Console.WriteLine($"    ^ [{a.Depth}] [{a.Type}] \"{a.Name}\" | AutoId=\"{a.AutoId}\" | Cls=\"{a.Cls}\"");
        }

        // Extract ADCE domain semantics via extraction engine
        var rBounds = windowElement.BoundingRectangle;
        var bRect = rBounds.IsEmpty ? BoundingRectangle.Empty :
            new BoundingRectangle((int)rBounds.X, (int)rBounds.Y, (int)rBounds.Width, (int)rBounds.Height);
        var controlInfo = engine.ExtractControlInfo(windowElement, element, archetype, bRect);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ADCE Zone      : {controlInfo.SemanticZone}");
        Console.WriteLine($"  ADCE Pane      : {controlInfo.PaneLocation}");
        Console.WriteLine($"  ADCE View      : {controlInfo.ActiveView ?? "(null)"}");
        Console.WriteLine($"  ADCE Section   : {controlInfo.SectionName ?? "(null)"}");
        Console.WriteLine($"  Semantic Path  : [{string.Join(" > ", controlInfo.SemanticPath)}]");
        Console.ResetColor();

        // Capture annotated screenshot
        SurfaceVisualizer.CaptureAndAnnotate(targetHwnd, rect, screenshotFilePath, $"{zoneTag}: {name}");

        return new SurfaceTelemetryStep(
            stepNum,
            zoneTag,
            description,
            stimulus,
            cType,
            name,
            autoId,
            cls,
            rect,
            ancestors,
            controlInfo.SemanticZone,
            controlInfo.PaneLocation,
            controlInfo.ActiveView,
            controlInfo.SectionName,
            controlInfo.SemanticPath,
            Path.GetFileName(screenshotFilePath)
        );
    }

    public static string SafeName(AutomationElement? el)
    {
        if (el == null) return string.Empty;
        try { return el.Properties.Name.ValueOrDefault ?? string.Empty; } catch { return string.Empty; }
    }

    public static string SafeId(AutomationElement? el)
    {
        if (el == null) return string.Empty;
        try { return el.Properties.AutomationId.ValueOrDefault ?? string.Empty; } catch { return string.Empty; }
    }

    public static string SafeClass(AutomationElement? el)
    {
        if (el == null) return string.Empty;
        try { return el.Properties.ClassName.ValueOrDefault ?? string.Empty; } catch { return string.Empty; }
    }

    public static ControlType SafeType(AutomationElement? el)
    {
        if (el == null) return ControlType.Custom;
        try { return el.Properties.ControlType.ValueOrDefault; } catch { return ControlType.Custom; }
    }

    public static Rectangle SafeBounds(AutomationElement? el)
    {
        if (el == null) return Rectangle.Empty;
        try { return el.Properties.BoundingRectangle.ValueOrDefault; } catch { return Rectangle.Empty; }
    }
}
