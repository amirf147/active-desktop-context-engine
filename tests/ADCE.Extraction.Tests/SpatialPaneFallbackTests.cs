// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using Xunit;

namespace ADCE.Extraction.Tests;

public class SpatialPaneFallbackTests
{
    private static readonly BoundingRectangle WindowBounds = new(0, 0, 1920, 1080);

    [Fact]
    public void InferPaneFromGeometry_ActivityBarRail_ReturnsActivityBar()
    {
        var controlBounds = new BoundingRectangle(0, 50, 42, 800);
        var pane = UiaExtractionEngine.InferPaneFromGeometry(WindowBounds, controlBounds);
        Assert.Equal(WindowPaneLocation.ActivityBar, pane);
    }

    [Fact]
    public void InferPaneFromGeometry_LeftDrawer_ReturnsPrimarySidebar()
    {
        var controlBounds = new BoundingRectangle(50, 50, 300, 800);
        var pane = UiaExtractionEngine.InferPaneFromGeometry(WindowBounds, controlBounds);
        Assert.Equal(WindowPaneLocation.PrimarySidebar, pane);
    }

    [Fact]
    public void InferPaneFromGeometry_CentralRegion_ReturnsMainContent()
    {
        var controlBounds = new BoundingRectangle(700, 50, 500, 700);
        var pane = UiaExtractionEngine.InferPaneFromGeometry(WindowBounds, controlBounds);
        Assert.Equal(WindowPaneLocation.MainContent, pane);
    }

    [Fact]
    public void InferPaneFromGeometry_RightQuadrant_ReturnsAuxiliarySidebar()
    {
        var controlBounds = new BoundingRectangle(1300, 50, 600, 800);
        var pane = UiaExtractionEngine.InferPaneFromGeometry(WindowBounds, controlBounds);
        Assert.Equal(WindowPaneLocation.AuxiliarySidebar, pane);
    }

    [Fact]
    public void InferPaneFromGeometry_BottomQuadrant_ReturnsBottomPanel()
    {
        var controlBounds = new BoundingRectangle(400, 850, 800, 180);
        var pane = UiaExtractionEngine.InferPaneFromGeometry(WindowBounds, controlBounds);
        Assert.Equal(WindowPaneLocation.BottomPanel, pane);
    }

    [Fact]
    public void InferPaneFromGeometry_BottomStrip_ReturnsStatusBar()
    {
        var controlBounds = new BoundingRectangle(0, 1055, 1920, 25);
        var pane = UiaExtractionEngine.InferPaneFromGeometry(WindowBounds, controlBounds);
        Assert.Equal(WindowPaneLocation.StatusBar, pane);
    }

    [Fact]
    public void InferPaneFromGeometry_TopStrip_ReturnsTopBar()
    {
        var controlBounds = new BoundingRectangle(0, 5, 1920, 30);
        var pane = UiaExtractionEngine.InferPaneFromGeometry(WindowBounds, controlBounds);
        Assert.Equal(WindowPaneLocation.TopBar, pane);
    }

    [Fact]
    public void InferPaneFromGeometry_EmptyBounds_ReturnsUnknown()
    {
        var pane1 = UiaExtractionEngine.InferPaneFromGeometry(BoundingRectangle.Empty, new BoundingRectangle(10, 10, 100, 100));
        var pane2 = UiaExtractionEngine.InferPaneFromGeometry(WindowBounds, BoundingRectangle.Empty);

        Assert.Equal(WindowPaneLocation.Unknown, pane1);
        Assert.Equal(WindowPaneLocation.Unknown, pane2);
    }
}
