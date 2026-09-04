// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Mcp.Server;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Xunit;

namespace ADCE.Mcp.Tests;

public class TagActiveControlHierarchyTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDesktopStateStore _store;

    public TagActiveControlHierarchyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"adce_mcp_tag_hierarchy_test_{Guid.NewGuid():N}.db");
        _store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = _dbPath });
        _store.Initialize();
    }

    public void Dispose()
    {
        _store.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task TagActiveControl_WithHierarchyArguments_UpdatesSnapshotAndRule()
    {
        var initialSnapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope { VirtualDesktopId = Guid.NewGuid(), DesktopIndex = 0, VirtualDesktopName = "Main" },
            Window = new WindowEnvelope
            {
                Hwnd = 0x54321,
                Title = "active-desktop-context-engine - Antigravity IDE",
                ProcessName = "Antigravity.exe",
                Pid = 2002,
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "TreeItem",
                ElementName = "Timeline: History",
                AutomationId = "timeline.item",
                ClassName = "monaco-tree",
                BoundingBox = new BoundingRectangle(50, 400, 250, 30),
                SemanticZone = DesktopSemanticZone.SidebarExplorer
            }
        };

        _store.UpdateCurrentSnapshot(initialSnapshot);

        var handler = new DesktopContextMcpHandler(_store);

        var argsJson = JsonDocument.Parse("""
        {
            "target_zone": "Timeline",
            "target_pane": "PrimarySidebar",
            "target_view": "Explorer",
            "target_section": "Timeline",
            "scope": "element",
            "comment": "Explicitly tag as Timeline section in Explorer sidebar"
        }
        """).RootElement;

        var result = await handler.CallToolAsync("tag_active_control", argsJson);

        Assert.False(result.IsError);
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.Content);

        var jsonText = result.Content[0].Text ?? "{}";
        using var responseDoc = JsonDocument.Parse(jsonText);
        var root = responseDoc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("Timeline", root.GetProperty("semantic_zone").GetString());
        Assert.Equal("PrimarySidebar", root.GetProperty("pane_location").GetString());
        Assert.Equal("Explorer", root.GetProperty("active_view").GetString());
        Assert.Equal("Timeline", root.GetProperty("section_name").GetString());

        var updatedSnapshot = _store.GetCurrentSnapshot();
        Assert.NotNull(updatedSnapshot);
        Assert.Equal(DesktopSemanticZone.Timeline, updatedSnapshot.Focus.SemanticZone);
        Assert.Equal(WindowPaneLocation.PrimarySidebar, updatedSnapshot.Focus.PaneLocation);
        Assert.Equal("Explorer", updatedSnapshot.Focus.ActiveView);
        Assert.Equal("Timeline", updatedSnapshot.Focus.SectionName);
        Assert.Equal(3, updatedSnapshot.Focus.SemanticPath.Length);
        Assert.Equal("PrimarySidebar", updatedSnapshot.Focus.SemanticPath[0]);
        Assert.Equal("Explorer", updatedSnapshot.Focus.SemanticPath[1]);
        Assert.Equal("Timeline", updatedSnapshot.Focus.SemanticPath[2]);
    }

    [Fact]
    public async Task TagActiveControl_WithInvalidTargetPane_ReturnsError()
    {
        var initialSnapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope { VirtualDesktopId = Guid.NewGuid(), DesktopIndex = 0, VirtualDesktopName = "Main" },
            Window = new WindowEnvelope
            {
                Hwnd = 0x12345,
                Title = "Test",
                ProcessName = "Test.exe",
                Pid = 1001,
                ClassName = "Test",
                Archetype = DesktopAppArchetype.ClassicWin32,
                Bounds = new BoundingRectangle(0, 0, 800, 600)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "Test",
                AutomationId = "txt",
                ClassName = "Edit",
                BoundingBox = BoundingRectangle.Empty,
                SemanticZone = DesktopSemanticZone.Unknown
            }
        };

        _store.UpdateCurrentSnapshot(initialSnapshot);

        var handler = new DesktopContextMcpHandler(_store);

        var argsJson = JsonDocument.Parse("""
        {
            "target_zone": "EditorBuffer",
            "target_pane": "ImaginaryPane"
        }
        """).RootElement;

        var result = await handler.CallToolAsync("tag_active_control", argsJson);

        Assert.True(result.IsError);
        Assert.Contains("Invalid target_pane", result.Content[0].Text);
    }
}
