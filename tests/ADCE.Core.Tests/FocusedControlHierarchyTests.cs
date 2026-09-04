// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Collections.Immutable;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using Xunit;

namespace ADCE.Core.Tests;

public class FocusedControlHierarchyTests
{
    [Fact]
    public void Equals_WhenHierarchyFieldsMatch_ReturnsTrue()
    {
        var info1 = new FocusedControlInfo
        {
            ControlType = "Edit",
            ElementName = "Commit message",
            BoundingBox = new BoundingRectangle(10, 10, 200, 50),
            AutomationId = "scm.input",
            ClassName = "native-edit-context",
            SemanticZone = DesktopSemanticZone.GitCommitBox,
            PaneLocation = WindowPaneLocation.PrimarySidebar,
            ActiveView = "SourceControl",
            SectionName = "CommitBox",
            SemanticPath = ImmutableArray.Create("PrimarySidebar", "SourceControl", "CommitBox"),
            ContainerPath = ImmutableArray.Create("scm-editor-container", "workbench.view.scm")
        };

        var info2 = new FocusedControlInfo
        {
            ControlType = "Edit",
            ElementName = "Commit message",
            BoundingBox = new BoundingRectangle(10, 10, 200, 50),
            AutomationId = "scm.input",
            ClassName = "native-edit-context",
            SemanticZone = DesktopSemanticZone.GitCommitBox,
            PaneLocation = WindowPaneLocation.PrimarySidebar,
            ActiveView = "SourceControl",
            SectionName = "CommitBox",
            SemanticPath = ImmutableArray.Create("PrimarySidebar", "SourceControl", "CommitBox"),
            ContainerPath = ImmutableArray.Create("scm-editor-container", "workbench.view.scm")
        };

        Assert.Equal(info1, info2);
        Assert.Equal(info1.GetHashCode(), info2.GetHashCode());
    }

    private static FocusedControlInfo CreateControl(
        string controlType = "Edit",
        string elementName = "test",
        WindowPaneLocation paneLocation = WindowPaneLocation.Unknown,
        string? activeView = null,
        string? sectionName = null,
        ImmutableArray<string> semanticPath = default)
    {
        return new FocusedControlInfo
        {
            ControlType = controlType,
            ElementName = elementName,
            BoundingBox = new BoundingRectangle(0, 0, 100, 20),
            PaneLocation = paneLocation,
            ActiveView = activeView,
            SectionName = sectionName,
            SemanticPath = semanticPath.IsDefault ? ImmutableArray<string>.Empty : semanticPath
        };
    }

    [Fact]
    public void Equals_WhenPaneDiffers_ReturnsFalse()
    {
        var info1 = CreateControl(paneLocation: WindowPaneLocation.PrimarySidebar, semanticPath: ImmutableArray.Create("PrimarySidebar"));
        var info2 = CreateControl(paneLocation: WindowPaneLocation.AuxiliarySidebar, semanticPath: ImmutableArray.Create("PrimarySidebar"));

        Assert.NotEqual(info1, info2);
    }

    [Fact]
    public void Equals_WhenActiveViewDiffers_ReturnsFalse()
    {
        var info1 = CreateControl(controlType: "TreeItem", activeView: "Explorer");
        var info2 = CreateControl(controlType: "TreeItem", activeView: "SourceControl");

        Assert.NotEqual(info1, info2);
    }

    [Fact]
    public void Equals_WhenSectionDiffers_ReturnsFalse()
    {
        var info1 = CreateControl(controlType: "TreeItem", sectionName: "Timeline");
        var info2 = CreateControl(controlType: "TreeItem", sectionName: "Outline");

        Assert.NotEqual(info1, info2);
    }

    [Fact]
    public void Equals_WhenSemanticPathDiffers_ReturnsFalse()
    {
        var info1 = CreateControl(semanticPath: ImmutableArray.Create("PrimarySidebar", "Explorer", "Timeline"));
        var info2 = CreateControl(semanticPath: ImmutableArray.Create("PrimarySidebar", "Explorer", "Outline"));

        Assert.NotEqual(info1, info2);
    }

    [Fact]
    public void GetHashCode_HandlesDefaultImmutableArrayWithoutException()
    {
        var info = new FocusedControlInfo
        {
            ControlType = "Window",
            ElementName = "test",
            BoundingBox = new BoundingRectangle(0, 0, 100, 20),
            SemanticPath = default
        };

        int hash = info.GetHashCode();
        Assert.NotEqual(0, hash);
    }
}
