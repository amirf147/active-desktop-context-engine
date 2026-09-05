// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using ADCE.Core.Enums;
using ADCE.Core.Models;

namespace ADCE.Extraction.Spatial;

/// <summary>
/// Relative spatial geometry analyzer mapping control bounding boxes to window layout quadrants.
/// Used strictly as a fallback when accessibility container metadata is exhausted.
/// </summary>
public static class SpatialPaneResolver
{
    public static WindowPaneLocation InferPaneFromGeometry(BoundingRectangle windowBounds, BoundingRectangle controlBounds)
    {
        if (windowBounds.IsEmpty || windowBounds.Width <= 0 || windowBounds.Height <= 0 ||
            controlBounds.IsEmpty || controlBounds.Width <= 0 || controlBounds.Height <= 0)
        {
            return WindowPaneLocation.Unknown;
        }

        double relX = (controlBounds.Left - windowBounds.Left) / (double)windowBounds.Width;
        double relY = (controlBounds.Top - windowBounds.Top) / (double)windowBounds.Height;

        // Check status bar at bottom (height <= 35 and within 40px of bottom or relY >= 0.95)
        if (relY >= 0.95 || (controlBounds.Height <= 35 && (windowBounds.Bottom - controlBounds.Bottom) <= 40))
        {
            return WindowPaneLocation.StatusBar;
        }

        // Check bottom panel (e.g. terminal / output at bottom quadrant)
        if (relY >= 0.75)
        {
            return WindowPaneLocation.BottomPanel;
        }

        // Check top bar (e.g. tabs or title bar)
        if (relY < 0.05 && controlBounds.Height <= 45)
        {
            return WindowPaneLocation.TopBar;
        }

        // Check Activity Bar (narrow vertical rail on far-left)
        if (relX < 0.035 && controlBounds.Width <= 60)
        {
            return WindowPaneLocation.ActivityBar;
        }

        // Check Primary Sidebar (left ~30%)
        if (relX < 0.30)
        {
            return WindowPaneLocation.PrimarySidebar;
        }

        // Check Auxiliary Sidebar (right ~35%)
        if (relX >= 0.65)
        {
            return WindowPaneLocation.AuxiliarySidebar;
        }

        // Main content (center)
        if (relX >= 0.30 && relX < 0.65)
        {
            return WindowPaneLocation.MainContent;
        }

        return WindowPaneLocation.Unknown;
    }
}
