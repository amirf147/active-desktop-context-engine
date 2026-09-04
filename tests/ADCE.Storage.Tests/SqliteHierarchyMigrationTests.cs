// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ADCE.Storage.Tests;

public class SqliteHierarchyMigrationTests : IDisposable
{
    private readonly string _testDbPath;

    public SqliteHierarchyMigrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"adce_migr_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            string walPath = $"{_testDbPath}-wal";
            if (File.Exists(walPath)) File.Delete(walPath);
            string shmPath = $"{_testDbPath}-shm";
            if (File.Exists(shmPath)) File.Delete(shmPath);
        }
        catch { }
    }

    [Fact]
    public async Task PersistAndRetrieve_SavesHierarchyFieldsToDatabase()
    {
        var options = new StorageOptions { DatabasePath = _testDbPath };
        await using var store = new SqliteDesktopStateStore(options);
        await store.InitializeAsync();

        var snapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Primary",
                MonitorIndex = 0,
                MonitorBounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x00123456,
                Title = "active-desktop-context-engine - Antigravity IDE",
                ProcessName = "Antigravity.exe",
                Pid = 1234,
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080),
                IsMinimized = false,
                IsMaximized = true
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "TreeItem",
                ElementName = "Timeline",
                AutomationId = "timeline.section",
                ClassName = "monaco-tree",
                BoundingBox = new BoundingRectangle(10, 200, 300, 40),
                SemanticZone = DesktopSemanticZone.Timeline,
                PaneLocation = WindowPaneLocation.PrimarySidebar,
                ActiveView = "Explorer",
                SectionName = "Timeline",
                SemanticPath = ImmutableArray.Create("PrimarySidebar", "Explorer", "Timeline")
            }
        };

        store.UpdateCurrentSnapshot(snapshot);
        await Task.Delay(250);

        var history = (await store.GetHistoryAsync(DateTimeOffset.UtcNow.AddMinutes(-5), 10).ToListAsync());
        Assert.NotEmpty(history);
        var retrieved = history.First();

        Assert.Equal(WindowPaneLocation.PrimarySidebar, retrieved.Focus.PaneLocation);
        Assert.Equal("Explorer", retrieved.Focus.ActiveView);
        Assert.Equal("Timeline", retrieved.Focus.SectionName);
        Assert.Equal(3, retrieved.Focus.SemanticPath.Length);
        Assert.Equal("PrimarySidebar", retrieved.Focus.SemanticPath[0]);
        Assert.Equal("Explorer", retrieved.Focus.SemanticPath[1]);
        Assert.Equal("Timeline", retrieved.Focus.SemanticPath[2]);
    }

    [Fact]
    public async Task Initialize_OnLegacyDatabaseWithoutHierarchyColumns_MigratesAndAllowsInserts()
    {
        // 1. Create a legacy table schema without pane_location, active_view, section_name, semantic_path
        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        await using (var conn = new SqliteConnection(connStr))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            var dummyLegacySnapshot = new DesktopContextSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2),
                Workspace = new WorkspaceEnvelope { VirtualDesktopId = Guid.NewGuid(), DesktopIndex = 0, VirtualDesktopName = "Default" },
                Window = new WindowEnvelope
                {
                    Hwnd = 123,
                    Title = "Legacy Window",
                    ProcessName = "legacy.exe",
                    Pid = 999,
                    ClassName = "LegacyClass",
                    Archetype = DesktopAppArchetype.ClassicWin32,
                    Bounds = new BoundingRectangle(0, 0, 800, 600)
                },
                Focus = new FocusedControlInfo
                {
                    ControlType = "Window",
                    ElementName = "Root",
                    BoundingBox = new BoundingRectangle(0, 0, 100, 20),
                    SemanticZone = DesktopSemanticZone.Unknown
                }
            };
            string dummyJson = System.Text.Json.JsonSerializer.Serialize(dummyLegacySnapshot, AdceJsonSerializerOptions.Default);
            long legacyUnixMs = dummyLegacySnapshot.Timestamp.ToUnixTimeMilliseconds();

            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS desktop_snapshots (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp_utc TEXT NOT NULL,
                    timestamp_unix_ms INTEGER NOT NULL,
                    hwnd INTEGER NOT NULL,
                    window_title TEXT NOT NULL,
                    process_name TEXT NOT NULL,
                    class_name TEXT NOT NULL,
                    archetype INTEGER NOT NULL,
                    focus_control_type TEXT,
                    focus_element_name TEXT,
                    focus_semantic_zone INTEGER NOT NULL,
                    active_file_or_tab TEXT,
                    container_path TEXT DEFAULT '',
                    container_classes TEXT DEFAULT '',
                    snapshot_json TEXT NOT NULL
                );
                INSERT INTO desktop_snapshots (
                    timestamp_utc, timestamp_unix_ms,
                    hwnd, window_title, process_name, class_name, archetype,
                    focus_control_type, focus_element_name, focus_semantic_zone,
                    active_file_or_tab, container_path, container_classes, snapshot_json
                ) VALUES (
                    datetime('now'), @unix_ms,
                    123, 'Legacy Window', 'legacy.exe', 'LegacyClass', 0,
                    'Window', 'Root', 0,
                    'test.txt', '[]', '[]', @json
                );
            """;
            cmd.Parameters.AddWithValue("@unix_ms", legacyUnixMs);
            cmd.Parameters.AddWithValue("@json", dummyJson);
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Open via SqliteDesktopStateStore which triggers migration in Initialize()
        var options = new StorageOptions { DatabasePath = _testDbPath };
        await using var store = new SqliteDesktopStateStore(options);
        await store.InitializeAsync();

        // 3. Verify columns now exist in table schema
        await using (var conn = new SqliteConnection(connStr))
        {
            await conn.OpenAsync();
            using var pragmaCmd = conn.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA table_info(desktop_snapshots);";
            using var reader = await pragmaCmd.ExecuteReaderAsync();
            var columns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            Assert.Contains("pane_location", columns);
            Assert.Contains("active_view", columns);
            Assert.Contains("section_name", columns);
            Assert.Contains("semantic_path", columns);
        }

        // 4. Verify new snapshots with hierarchy insert cleanly
        var newSnapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope { VirtualDesktopId = Guid.NewGuid(), DesktopIndex = 0, VirtualDesktopName = "Main" },
            Window = new WindowEnvelope
            {
                Hwnd = 0x9999,
                Title = "New Window",
                ProcessName = "new.exe",
                Pid = 888,
                ClassName = "NewClass",
                Archetype = DesktopAppArchetype.ClassicWin32,
                Bounds = new BoundingRectangle(0, 0, 1024, 768)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "Input",
                BoundingBox = new BoundingRectangle(0, 0, 100, 20),
                SemanticZone = DesktopSemanticZone.ChatConversation,
                PaneLocation = WindowPaneLocation.AuxiliarySidebar,
                ActiveView = "Chat",
                SectionName = "Conversation",
                SemanticPath = ImmutableArray.Create("AuxiliarySidebar", "Chat", "Conversation")
            }
        };

        store.UpdateCurrentSnapshot(newSnapshot);
        await Task.Delay(250);

        var history = (await store.GetHistoryAsync(DateTimeOffset.UtcNow.AddMinutes(-5), 5).ToListAsync());
        Assert.True(history.Count >= 2);
    }
}
