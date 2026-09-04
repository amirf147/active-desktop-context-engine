// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Mcp.Server;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Xunit;

namespace ADCE.Mcp.Tests;

public class HierarchySerializationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDesktopStateStore _store;
    private readonly DesktopContextMcpHandler _handler;

    public HierarchySerializationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"adce_hier_serial_{Guid.NewGuid():N}.db");
        _store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = _testDbPath });
        _store.Initialize();
        _handler = new DesktopContextMcpHandler(_store);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
    }

    [Fact]
    public void TagActiveControlSchema_IncludesHierarchyParameters()
    {
        var tools = _handler.GetTools();
        var tagTool = tools.FirstOrDefault(t => t.Name == "tag_active_control");

        Assert.NotNull(tagTool);
        var schemaJson = JsonSerializer.Serialize(tagTool.InputSchema, AdceJsonSerializerOptions.Default);
        using var doc = JsonDocument.Parse(schemaJson);
        var properties = doc.RootElement.GetProperty("properties");

        Assert.True(properties.TryGetProperty("target_pane", out var targetPaneProp));
        Assert.Equal("string", targetPaneProp.GetProperty("type").GetString());

        Assert.True(properties.TryGetProperty("target_view", out var targetViewProp));
        Assert.Equal("string", targetViewProp.GetProperty("type").GetString());

        Assert.True(properties.TryGetProperty("target_section", out var targetSectionProp));
        Assert.Equal("string", targetSectionProp.GetProperty("type").GetString());
    }

    [Fact]
    public void SnapshotSerialization_SerializesAndDeserializesHierarchyFields()
    {
        var snapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope { VirtualDesktopId = Guid.NewGuid(), DesktopIndex = 0, VirtualDesktopName = "Code" },
            Window = new WindowEnvelope
            {
                Hwnd = 0xABCDE,
                Title = "active-desktop-context-engine - Antigravity IDE",
                ProcessName = "Antigravity.exe",
                Pid = 5432,
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "TreeItem",
                ElementName = "Outline Item",
                AutomationId = "outline.item.1",
                ClassName = "monaco-list",
                BoundingBox = new BoundingRectangle(20, 300, 200, 30),
                SemanticZone = DesktopSemanticZone.Outline,
                PaneLocation = WindowPaneLocation.PrimarySidebar,
                ActiveView = "Explorer",
                SectionName = "Outline",
                SemanticPath = ImmutableArray.Create("PrimarySidebar", "Explorer", "Outline")
            }
        };

        var json = JsonSerializer.Serialize(snapshot, AdceJsonSerializerOptions.Default);
        using var doc = JsonDocument.Parse(json);
        var focusElem = doc.RootElement.GetProperty("focus");

        Assert.Equal("primary_sidebar", focusElem.GetProperty("pane_location").GetString());
        Assert.Equal("Explorer", focusElem.GetProperty("active_view").GetString());
        Assert.Equal("Outline", focusElem.GetProperty("section_name").GetString());

        var pathArray = focusElem.GetProperty("semantic_path").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
        string[] expected = ["PrimarySidebar", "Explorer", "Outline"];
        Assert.Equal(expected, pathArray);

        var roundtripped = JsonSerializer.Deserialize<DesktopContextSnapshot>(json, AdceJsonSerializerOptions.Default);
        Assert.NotNull(roundtripped);
        Assert.Equal(WindowPaneLocation.PrimarySidebar, roundtripped.Focus.PaneLocation);
        Assert.Equal("Explorer", roundtripped.Focus.ActiveView);
        Assert.Equal("Outline", roundtripped.Focus.SectionName);
        Assert.Equal(3, roundtripped.Focus.SemanticPath.Length);
        Assert.Equal("PrimarySidebar", roundtripped.Focus.SemanticPath[0]);
        Assert.Equal("Explorer", roundtripped.Focus.SemanticPath[1]);
        Assert.Equal("Outline", roundtripped.Focus.SemanticPath[2]);
    }
}
