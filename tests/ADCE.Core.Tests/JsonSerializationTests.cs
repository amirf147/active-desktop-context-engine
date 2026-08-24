// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Text.Json;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using Xunit;

namespace ADCE.Core.Tests;

public class JsonSerializationTests
{
    [Fact]
    public void McpSchema_RepresentativeExample_DeserializesSuccessfully()
    {
        // Representative JSON payload from docs/MCP_SCHEMA_SPEC.md section 3
        string sampleJson = """
        {
          "timestamp": "2026-08-24T06:20:00.000Z",
          "workspace": {
            "virtual_desktop_id": "3f2a1b0c-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
            "virtual_desktop_name": "Development",
            "desktop_index": 1
          },
          "window": {
            "hwnd": "0x00DB083E",
            "title": "active-desktop-context-engine - Antigravity IDE",
            "process_name": "Antigravity.exe",
            "pid": 26420,
            "class_name": "Chrome_WidgetWin_1"
          },
          "ide_context": {
            "active_file_path": "docs/CONTEXT.md",
            "active_sidebar_view": "Explorer (Ctrl+Shift+E)",
            "open_editor_tabs": [
              { "title": "CONTEXT.md", "is_active": true },
              { "title": "UInspect.md", "is_active": false },
              { "title": "README.md", "is_active": false }
            ],
            "edit_buffer": "CONTEXT.md"
          },
          "focus": {
            "control_type": "Edit",
            "element_name": "CONTEXT.md",
            "automation_id": "",
            "bounding_box": { "left": 400, "top": 120, "width": 1200, "height": 800 }
          }
        }
        """;

        var options = AdceJsonSerializerOptions.Default;
        var snapshot = JsonSerializer.Deserialize<DesktopContextSnapshot>(sampleJson, options);

        Assert.NotNull(snapshot);
        Assert.Equal(Guid.Parse("3f2a1b0c-4d5e-6f7a-8b9c-0d1e2f3a4b5c"), snapshot.Workspace.VirtualDesktopId);
        Assert.Equal("Development", snapshot.Workspace.VirtualDesktopName);
        Assert.Equal(1, snapshot.Workspace.DesktopIndex);

        Assert.Equal((nint)0x00DB083E, snapshot.Window.Hwnd);
        Assert.Equal("active-desktop-context-engine - Antigravity IDE", snapshot.Window.Title);
        Assert.Equal("Antigravity.exe", snapshot.Window.ProcessName);
        Assert.Equal(26420, snapshot.Window.Pid);
        Assert.Equal("Chrome_WidgetWin_1", snapshot.Window.ClassName);

        Assert.NotNull(snapshot.IdeContext);
        Assert.Equal("docs/CONTEXT.md", snapshot.IdeContext.ActiveFilePath);
        Assert.Equal("Explorer (Ctrl+Shift+E)", snapshot.IdeContext.ActiveSidebarView);
        Assert.Equal(3, snapshot.IdeContext.OpenEditorTabs.Length);
        Assert.True(snapshot.IdeContext.OpenEditorTabs[0].IsActive);
        Assert.False(snapshot.IdeContext.OpenEditorTabs[1].IsActive);

        Assert.NotNull(snapshot.Focus);
        Assert.Equal("Edit", snapshot.Focus.ControlType);
        Assert.Equal("CONTEXT.md", snapshot.Focus.ElementName);
        Assert.Equal(400, snapshot.Focus.BoundingBox.Left);
        Assert.Equal(120, snapshot.Focus.BoundingBox.Top);
        Assert.Equal(1200, snapshot.Focus.BoundingBox.Width);
        Assert.Equal(800, snapshot.Focus.BoundingBox.Height);
    }

    [Fact]
    public void Serialization_ProducesSnakeCaseProperties()
    {
        var snapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.Parse("2026-08-24T06:20:00Z"),
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Primary"
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x00AABBCC,
                Title = "Test Window",
                ProcessName = "Test.exe",
                Pid = 1234,
                ClassName = "TestClass",
                Archetype = DesktopAppArchetype.ChromiumElectron
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Button",
                ElementName = "Submit",
                BoundingBox = new BoundingRectangle(10, 20, 100, 50),
                SemanticZone = DesktopSemanticZone.CommandPalette
            }
        };

        var options = AdceJsonSerializerOptions.Default;
        string json = JsonSerializer.Serialize(snapshot, options);

        // Verify snake_case field names
        Assert.Contains("\"virtual_desktop_id\":", json);
        Assert.Contains("\"virtual_desktop_name\":", json);
        Assert.Contains("\"desktop_index\":", json);
        Assert.Contains("\"process_name\":", json);
        Assert.Contains("\"class_name\":", json);
        Assert.Contains("\"control_type\":", json);
        Assert.Contains("\"element_name\":", json);
        Assert.Contains("\"bounding_box\":", json);

        // Null contexts must be omitted
        Assert.DoesNotContain("\"ide_context\":", json);
        Assert.DoesNotContain("\"browser_context\":", json);
    }

    [Fact]
    public void BrowserContext_RoundTripSerialization_IsLossless()
    {
        var snapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x12345678,
                Title = "Waterfox",
                ProcessName = "waterfox.exe",
                Pid = 5000,
                ClassName = "MozillaWindowClass",
                Archetype = DesktopAppArchetype.Gecko
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "ListItem",
                ElementName = "Active Tab",
                BoundingBox = new BoundingRectangle(0, 0, 200, 30)
            },
            BrowserContext = new BrowserContext
            {
                ContainerType = "TreeStyleTab",
                TotalCount = 2,
                ActiveTab = "ADCE Spec",
                UrlAddress = "https://example.com/adce",
                Tabs =
                [
                    new() { Index = 1, Title = "ADCE Spec", IsActive = true, IsPinned = false },
                    new() { Index = 2, Title = "GitHub PR", IsActive = false, IsPinned = true }
                ]
            }
        };

        var options = AdceJsonSerializerOptions.Default;
        string json = JsonSerializer.Serialize(snapshot, options);
        var deserialized = JsonSerializer.Deserialize<DesktopContextSnapshot>(json, options);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.BrowserContext);
        Assert.Equal("TreeStyleTab", deserialized.BrowserContext.ContainerType);
        Assert.Equal(2, deserialized.BrowserContext.TotalCount);
        Assert.Equal("ADCE Spec", deserialized.BrowserContext.ActiveTab);
        Assert.Equal(2, deserialized.BrowserContext.Tabs.Length);
        Assert.True(deserialized.BrowserContext.Tabs[0].IsActive);
        Assert.True(deserialized.BrowserContext.Tabs[1].IsPinned);
    }
}
