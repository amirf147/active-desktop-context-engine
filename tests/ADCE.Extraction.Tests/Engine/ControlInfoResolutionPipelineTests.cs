// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Collections.Immutable;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Extraction.Engine;
using ADCE.Extraction.Models;
using Xunit;

namespace ADCE.Extraction.Tests.Engine;

/// <summary>
/// Unit tests verifying end-to-end control info resolution, provenance tracking,
/// Notepad classification, and fine-grained in-browser DOM perception.
/// </summary>
public class ControlInfoResolutionPipelineTests
{
    [Fact]
    public void ResolveControlInfo_Windows11Notepad_ResolvesEditorBuffer()
    {
        // Windows 11 Notepad text editor hosts a Direct2D RichEdit control exposing ControlType.Document
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Document",
            Name: "Untitled - Notepad",
            AutomationId: "",
            ClassName: "RichEditD2DPT",
            BoundingBox: new BoundingRectangle(50, 50, 800, 600),
            IsOverlay: false);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            AncestorChain.Empty,
            processName: "Notepad",
            archetype: DesktopAppArchetype.ClassicWin32,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.EditorBuffer, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("Editor", focus.ActiveView);
        Assert.Equal("Resolver:ClassicWin32", focus.ResolutionSource);
    }

    [Fact]
    public void ResolveControlInfo_ClassicWin32Notepad_ResolvesEditorBuffer()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Edit",
            Name: "Text Editor",
            AutomationId: "15",
            ClassName: "Edit",
            BoundingBox: new BoundingRectangle(10, 10, 600, 400),
            IsOverlay: false);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            AncestorChain.Empty,
            processName: "notepad",
            archetype: DesktopAppArchetype.ClassicWin32,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.EditorBuffer, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("Editor", focus.ActiveView);
        Assert.Equal("Resolver:ClassicWin32", focus.ResolutionSource);
    }

    [Fact]
    public void ResolveControlInfo_WinUi3TextBox_ResolvesEditorBuffer()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Edit",
            Name: "Input Area",
            AutomationId: "TextEditorBox",
            ClassName: "RichEditBox",
            BoundingBox: new BoundingRectangle(0, 0, 500, 300),
            IsOverlay: false);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            AncestorChain.Empty,
            processName: "ModernEditor",
            archetype: DesktopAppArchetype.WinUI3Xaml,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.EditorBuffer, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("Resolver:WinUI3Xaml", focus.ResolutionSource);
    }

    [Fact]
    public void ResolveControlInfo_GeckoInPageChatPrompt_ResolvesChatPrompt()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Edit",
            Name: "Message ChatGPT",
            AutomationId: "prompt-textarea",
            ClassName: "chat-input",
            BoundingBox: new BoundingRectangle(200, 600, 600, 80),
            IsOverlay: false);

        var ancestors = new AncestorChain(
            ContainerPaths: ImmutableArray.Create("appcontent"),
            ContainerClasses: ImmutableArray.Create("MozillaContentWindowClass"),
            Nodes: ImmutableArray<AncestorNode>.Empty);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            ancestors,
            processName: "waterfox",
            archetype: DesktopAppArchetype.Gecko,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.ChatPrompt, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("WebDocument", focus.ActiveView);
        Assert.Equal("ChatPrompt", focus.SectionName);
        Assert.Equal("Resolver:Gecko", focus.ResolutionSource);
    }

    [Fact]
    public void ResolveControlInfo_GeckoInPageSearch_ResolvesQuickOpen()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Edit",
            Name: "Search Google or type a URL",
            AutomationId: "search-input",
            ClassName: "searchbox",
            BoundingBox: new BoundingRectangle(300, 300, 400, 40),
            IsOverlay: false);

        var ancestors = new AncestorChain(
            ContainerPaths: ImmutableArray<string>.Empty,
            ContainerClasses: ImmutableArray.Create("MozillaContentWindowClass"),
            Nodes: ImmutableArray<AncestorNode>.Empty);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            ancestors,
            processName: "waterfox",
            archetype: DesktopAppArchetype.Gecko,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.QuickOpen, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("WebDocument", focus.ActiveView);
        Assert.Equal("Search", focus.SectionName);
    }

    [Fact]
    public void ResolveControlInfo_GeckoInPageCodeEditor_ResolvesEditorBuffer()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Edit",
            Name: "",
            AutomationId: "code-buffer",
            ClassName: "cm-content",
            BoundingBox: new BoundingRectangle(100, 100, 800, 500),
            IsOverlay: false);

        var ancestors = new AncestorChain(
            ContainerPaths: ImmutableArray.Create("tabbrowser-tabpanels"),
            ContainerClasses: ImmutableArray<string>.Empty,
            Nodes: ImmutableArray<AncestorNode>.Empty);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            ancestors,
            processName: "firefox",
            archetype: DesktopAppArchetype.Gecko,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.EditorBuffer, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("WebDocument", focus.ActiveView);
        Assert.Equal("CodeEditor", focus.SectionName);
    }

    [Fact]
    public void ResolveControlInfo_GeckoWebDocumentCanvas_ResolvesWebDocument()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Document",
            Name: "Documentation Page",
            AutomationId: "",
            ClassName: "MozillaContentWindowClass",
            BoundingBox: new BoundingRectangle(0, 80, 1920, 1000),
            IsOverlay: false);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            AncestorChain.Empty,
            processName: "waterfox",
            archetype: DesktopAppArchetype.Gecko,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.WebDocument, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("WebDocument", focus.ActiveView);
    }

    [Fact]
    public void ResolveControlInfo_ChromiumOmnibox_ResolvesAddressBar()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Edit",
            Name: "Address and search bar",
            AutomationId: "view_1004",
            ClassName: "OmniboxViewViews",
            BoundingBox: new BoundingRectangle(200, 40, 800, 30),
            IsOverlay: false);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            AncestorChain.Empty,
            processName: "chrome",
            archetype: DesktopAppArchetype.ChromiumElectron,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.AddressBar, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.TopBar, focus.PaneLocation);
        Assert.Equal("NavigationBar", focus.ActiveView);
    }

    [Fact]
    public void ResolveControlInfo_ChromiumInPageChatPrompt_ResolvesChatPrompt()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Edit",
            Name: "Ask Claude a question...",
            AutomationId: "prompt-composer",
            ClassName: "chat-input",
            BoundingBox: new BoundingRectangle(250, 700, 500, 60),
            IsOverlay: false);

        var ancestors = new AncestorChain(
            ContainerPaths: ImmutableArray<string>.Empty,
            ContainerClasses: ImmutableArray.Create("Chrome_RenderWidgetHostHWND"),
            Nodes: ImmutableArray<AncestorNode>.Empty);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            ancestors,
            processName: "msedge",
            archetype: DesktopAppArchetype.ChromiumElectron,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.ChatPrompt, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("WebDocument", focus.ActiveView);
        Assert.Equal("ChatPrompt", focus.SectionName);
    }

    [Fact]
    public void ResolveControlInfo_ChromiumInPageDocument_ResolvesWebDocument()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Document",
            Name: "Active Web Page",
            AutomationId: "",
            ClassName: "Chrome_RenderWidgetHostHWND",
            BoundingBox: new BoundingRectangle(0, 100, 1200, 800),
            IsOverlay: false);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            AncestorChain.Empty,
            processName: "chrome",
            archetype: DesktopAppArchetype.ChromiumElectron,
            enableSemanticZones: true);

        Assert.Equal(DesktopSemanticZone.WebDocument, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.MainContent, focus.PaneLocation);
        Assert.Equal("WebDocument", focus.ActiveView);
    }

    [Fact]
    public void ResolveControlInfo_WhenSemanticZonesDisabled_ReturnsExplicitOnlyWithUnknownZone()
    {
        var descriptor = new FocusedControlDescriptor(
            ControlType: "Edit",
            Name: "Message input",
            AutomationId: "chat-input",
            ClassName: "interactive-session",
            BoundingBox: new BoundingRectangle(10, 10, 100, 50),
            IsOverlay: false);

        var focus = UiaExtractionEngine.ResolveControlInfo(
            descriptor,
            AncestorChain.Empty,
            processName: "Antigravity",
            archetype: DesktopAppArchetype.ChromiumElectron,
            enableSemanticZones: false);

        Assert.Equal(DesktopSemanticZone.Unknown, focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.Unknown, focus.PaneLocation);
        Assert.Equal("ExplicitOnly", focus.ResolutionSource);
        Assert.Null(focus.ActiveView);
        Assert.Null(focus.SectionName);
    }
}
