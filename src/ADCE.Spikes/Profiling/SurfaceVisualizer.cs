// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using ADCE.Spikes.Native;

namespace ADCE.Spikes.Profiling;

/// <summary>
/// Reusable screenshot capture and visual annotation engine for empirical application profiling.
/// </summary>
internal static class SurfaceVisualizer
{
    public static Bitmap? CaptureWindowBitmap(IntPtr hWnd, Rectangle bounds)
    {
        int w = Math.Max(1, bounds.Width);
        int h = Math.Max(1, bounds.Height);

        // Attempt 1: Win32 PrintWindow with PW_RENDERFULLCONTENT
        try
        {
            var bmpPrint = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmpPrint))
            {
                IntPtr hdc = g.GetHdc();
                try
                {
                    bool pwSuccess = SpikeNativeMethods.PrintWindow(hWnd, hdc, SpikeNativeMethods.PW_RENDERFULLCONTENT);
                    g.ReleaseHdc(hdc);
                    hdc = IntPtr.Zero;

                    if (pwSuccess && !IsBlackOrEmpty(bmpPrint))
                    {
                        return bmpPrint;
                    }
                }
                finally
                {
                    if (hdc != IntPtr.Zero) g.ReleaseHdc(hdc);
                }
            }
            bmpPrint.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [VISUALIZER] PrintWindow threw: {ex.Message}");
        }

        // Attempt 2: Clamped GDI CopyFromScreen with window brought to top
        try
        {
            SpikeNativeMethods.ForceForegroundWindow(hWnd);
            Thread.Sleep(200);

            var bmpScreen = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmpScreen))
            {
                int screenX = Math.Max(0, bounds.Left);
                int screenY = Math.Max(0, bounds.Top);
                int destX = screenX - bounds.Left;
                int destY = screenY - bounds.Top;
                int copyW = Math.Max(1, bounds.Width - destX);
                int copyH = Math.Max(1, bounds.Height - destY);

                g.CopyFromScreen(screenX, screenY, destX, destY, new Size(copyW, copyH), CopyPixelOperation.SourceCopy);
            }

            if (!IsBlackOrEmpty(bmpScreen))
            {
                return bmpScreen;
            }
            bmpScreen.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [VISUALIZER] CopyFromScreen fallback threw: {ex.Message}");
        }

        return null;
    }

    public static bool CaptureAndAnnotate(IntPtr hWnd, Rectangle highlightRect, string outputFilePath, string badgeLabel)
    {
        try
        {
            SpikeNativeMethods.GetWindowRect(hWnd, out SpikeNativeMethods.RECT winRect);
            var rootBounds = new Rectangle(winRect.Left, winRect.Top, winRect.Width, winRect.Height);

            using var bmp = CaptureWindowBitmap(hWnd, rootBounds);
            if (bmp == null)
            {
                Console.WriteLine($"    [VISUALIZER WARNING] Failed to capture bitmap for {Path.GetFileName(outputFilePath)}");
                return false;
            }

            using (var g = Graphics.FromImage(bmp))
            {
                if (!highlightRect.IsEmpty)
                {
                    int localX = highlightRect.X - rootBounds.X;
                    int localY = highlightRect.Y - rootBounds.Y;
                    var localBox = new Rectangle(localX, localY, highlightRect.Width, highlightRect.Height);

                    // Draw semi-transparent cyan fill
                    using (var fillBrush = new SolidBrush(Color.FromArgb(45, 0, 210, 255)))
                    {
                        g.FillRectangle(fillBrush, localBox);
                    }

                    // Draw cyan border
                    using (var cyanPen = new Pen(Color.FromArgb(0, 230, 255), 2.5f))
                    {
                        g.DrawRectangle(cyanPen, localBox);
                    }

                    // Draw high-contrast badge
                    using var badgeFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                    string text = $" {badgeLabel} ";
                    var textSize = g.MeasureString(text, badgeFont);

                    int badgeX = Math.Max(4, Math.Min(localX, bmp.Width - (int)textSize.Width - 6));
                    int badgeY = localY >= (int)textSize.Height + 6 ? localY - (int)textSize.Height - 4 : localY + highlightRect.Height + 4;
                    badgeY = Math.Max(4, Math.Min(badgeY, bmp.Height - (int)textSize.Height - 6));

                    var badgeRect = new Rectangle(badgeX, badgeY, (int)textSize.Width, (int)textSize.Height);
                    using var badgeBg = new SolidBrush(Color.FromArgb(220, 15, 23, 42));
                    g.FillRectangle(badgeBg, badgeRect);
                    using var badgeBorder = new Pen(Color.FromArgb(0, 230, 255), 1f);
                    g.DrawRectangle(badgeBorder, badgeRect);
                    using var textBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
                    g.DrawString(text, badgeFont, textBrush, badgeX, badgeY);
                }
            }

            string? dir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bmp.Save(outputFilePath, ImageFormat.Png);
            Console.WriteLine($"    [VISUALIZER] Saved screenshot: {Path.GetFileName(outputFilePath)} ({bmp.Width}x{bmp.Height})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [VISUALIZER ERROR] Exception while saving {Path.GetFileName(outputFilePath)}: {ex.Message}");
            return false;
        }
    }

    private static bool IsBlackOrEmpty(Bitmap bmp)
    {
        try
        {
            int stepX = Math.Max(1, bmp.Width / 4);
            int stepY = Math.Max(1, bmp.Height / 4);
            int nonBlack = 0;

            for (int x = stepX / 2; x < bmp.Width; x += stepX)
            {
                for (int y = stepY / 2; y < bmp.Height; y += stepY)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.R > 10 || c.G > 10 || c.B > 10) nonBlack++;
                }
            }
            return nonBlack == 0;
        }
        catch { return false; }
    }
}
