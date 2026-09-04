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

public class TagActiveControlTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDesktopStateStore _store;

    public TagActiveControlTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"adce_mcp_tag_test_{Guid.NewGuid():N}.db");
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
    public async Task TagActiveControl_ValidArguments_AddsRuleAndUpdatesCurrentSnapshot()
    {
        var initialSnapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope { VirtualDesktopId = Guid.NewGuid(), DesktopIndex = 0, VirtualDesktopName = "Main" },
            Window = new WindowEnvelope
            {
                Hwnd = 0x12345,
                Title = "active-desktop-context-engine - Antigravity IDE",
                ProcessName = "Antigravity.exe",
                Pid = 1001,
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "Message (Ctrl+Enter to commit)",
                AutomationId = "scm.input",
                ClassName = "monaco-editor",
                BoundingBox = new BoundingRectangle(10, 10, 200, 50),
                SemanticZone = DesktopSemanticZone.EditorBuffer
            }
        };

        _store.UpdateCurrentSnapshot(initialSnapshot);

        var handler = new DesktopContextMcpHandler(_store);

        var argsJson = JsonDocument.Parse("""
        {
            "target_zone": "GitCommitBox",
            "scope": "element",
            "comment": "Tag SCM input as GitCommitBox"
        }
        """).RootElement;

        var result = await handler.CallToolAsync("tag_active_control", argsJson);

        Assert.False(result.IsError);
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.Content);

        var updatedSnapshot = _store.GetCurrentSnapshot();
        Assert.NotNull(updatedSnapshot);
        Assert.Equal(DesktopSemanticZone.GitCommitBox, updatedSnapshot.Focus.SemanticZone);
    }

    [Fact]
    public async Task TagActiveControl_InvalidZone_ReturnsError()
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
            "target_zone": "NonExistentZone"
        }
        """).RootElement;

        var result = await handler.CallToolAsync("tag_active_control", argsJson);

        Assert.True(result.IsError);
        Assert.Contains("Invalid target_zone", result.Content[0].Text);
    }
}
