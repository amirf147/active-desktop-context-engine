// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ADCE.Daemon.Hosting;
using ADCE.Daemon.Native;

namespace ADCE.Daemon.UI;

/// <summary>
/// High-DPI dynamic system tray icon factory with deterministic GDI handle destruction.
/// Prevents unmanaged GDI table exhaustion by calling DestroyIcon immediately after cloning.
/// </summary>
public static class TrayIconFactory
{
    /// <summary>
    /// Creates a crisp, high-DPI icon representing the specified daemon operational state.
    /// </summary>
    /// <param name="state">Operational state (Running, Paused, Faulted, etc.).</param>
    /// <param name="size">Icon size in pixels (default: 32x32).</param>
    /// <returns>A managed <see cref="Icon"/> with zero lingering unmanaged GDI handle leaks.</returns>
    public static Icon CreateStateIcon(DaemonState state, int size = 32)
    {
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Background circle/rounded rect
        using var bgBrush = new SolidBrush(Color.FromArgb(230, 20, 24, 30));
        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

        // State-specific colors and glyphs
        Color accentColor = state switch
        {
            DaemonState.Running => Color.FromArgb(0, 210, 255),  // Vibrant Cyan
            DaemonState.Paused => Color.FromArgb(255, 180, 0),   // Amber
            DaemonState.Faulted => Color.FromArgb(255, 60, 60),  // Red
            _ => Color.FromArgb(140, 150, 165)                   // Muted Slate
        };

        using var accentPen = new Pen(accentColor, Math.Max(2f, size / 12f));
        using var accentBrush = new SolidBrush(accentColor);

        if (state == DaemonState.Running)
        {
            // Outer target ring
            float margin = size * 0.22f;
            g.DrawEllipse(accentPen, margin, margin, size - 2 * margin, size - 2 * margin);

            // Center target dot
            float dotMargin = size * 0.38f;
            g.FillEllipse(accentBrush, dotMargin, dotMargin, size - 2 * dotMargin, size - 2 * dotMargin);
        }
        else if (state == DaemonState.Paused)
        {
            // Two vertical pause bars
            float barW = Math.Max(2f, size * 0.12f);
            float barH = size * 0.44f;
            float topY = (size - barH) / 2f;
            float leftX1 = size * 0.32f;
            float leftX2 = size * 0.56f;

            g.FillRectangle(accentBrush, leftX1, topY, barW, barH);
            g.FillRectangle(accentBrush, leftX2, topY, barW, barH);
        }
        else
        {
            // Center indicator dot
            float dotMargin = size * 0.32f;
            g.FillEllipse(accentBrush, dotMargin, dotMargin, size - 2 * dotMargin, size - 2 * dotMargin);
        }

        // Convert Bitmap to managed Icon with deterministic native HICON handle destruction
        nint hIcon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            // Trap 1 Prevention: Release native Win32 HICON handle back to GDI table immediately
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
