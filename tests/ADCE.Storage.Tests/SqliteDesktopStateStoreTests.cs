// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Xunit;

namespace ADCE.Storage.Tests;

public class SqliteDesktopStateStoreTests : IDisposable
{
    private readonly string _testDbPath;

    public SqliteDesktopStateStoreTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"adce_test_{Guid.NewGuid():N}.db");
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
    public async Task InitializeAsync_CreatesDatabaseFileAndTables()
    {
        var options = new StorageOptions { DatabasePath = _testDbPath };
        await using var store = new SqliteDesktopStateStore(options);
        await store.InitializeAsync();

        Assert.True(File.Exists(_testDbPath));
        Assert.Null(store.GetCurrentSnapshot());
    }

    [Fact]
    public async Task UpdateCurrentSnapshot_UpdatesL1CacheInstantly()
    {
        var options = new StorageOptions { DatabasePath = _testDbPath };
        await using var store = new SqliteDesktopStateStore(options);
        await store.InitializeAsync();

        var snapshot = CreateTestSnapshot("Antigravity - Main", "Antigravity.exe", "src/App.cs", DesktopSemanticZone.EditorCodeBuffer);
        store.UpdateCurrentSnapshot(snapshot);

        var cached = store.GetCurrentSnapshot();
        Assert.NotNull(cached);
        Assert.Same(snapshot, cached);
        Assert.Equal("Antigravity - Main", cached.Window.Title);
    }

    [Fact]
    public async Task UpdateCurrentSnapshot_FlushesToSqlite_RetrievableViaGetHistoryAsync()
    {
        var options = new StorageOptions { DatabasePath = _testDbPath };
        var store = new SqliteDesktopStateStore(options);
        await store.InitializeAsync();

        var time1 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var time2 = DateTimeOffset.UtcNow.AddMinutes(-1);

        var snap1 = CreateTestSnapshot("Window 1", "app1.exe", "tab1", DesktopSemanticZone.EditorCodeBuffer, time1);
        var snap2 = CreateTestSnapshot("Window 2", "app2.exe", "tab2", DesktopSemanticZone.AddressBar, time2);

        store.UpdateCurrentSnapshot(snap1);
        store.UpdateCurrentSnapshot(snap2);

        // Flush background writer
        await store.DisposeAsync();

        // Query with fresh reader store
        await using var queryStore = new SqliteDesktopStateStore(options);
        await queryStore.InitializeAsync();

        var history = new List<DesktopContextSnapshot>();
        await foreach (var item in queryStore.GetHistoryAsync(DateTimeOffset.UtcNow.AddMinutes(-5), limit: 10))
        {
            history.Add(item);
        }

        Assert.Equal(2, history.Count);
        Assert.Equal("Window 2", history[0].Window.Title); // Newest first
        Assert.Equal("Window 1", history[1].Window.Title);
    }

    [Fact]
    public async Task SearchHistoryAsync_MatchesTitle_Process_Tab_OrFocusElement()
    {
        var options = new StorageOptions { DatabasePath = _testDbPath };
        var store = new SqliteDesktopStateStore(options);
        await store.InitializeAsync();

        store.UpdateCurrentSnapshot(CreateTestSnapshot("Waterfox Browser", "waterfox.exe", "https://github.com/adce", DesktopSemanticZone.AddressBar));
        store.UpdateCurrentSnapshot(CreateTestSnapshot("VS Code - Project", "Code.exe", "docs/CONTEXT.md", DesktopSemanticZone.EditorCodeBuffer));
        store.UpdateCurrentSnapshot(CreateTestSnapshot("Terminal Shell", "WindowsTerminal.exe", "pwsh", DesktopSemanticZone.IntegratedTerminal));

        await store.DisposeAsync();

        await using var queryStore = new SqliteDesktopStateStore(options);
        await queryStore.InitializeAsync();

        // 1. Search by document/tab keyword
        var contextMatches = new List<DesktopContextSnapshot>();
        await foreach (var item in queryStore.SearchHistoryAsync("CONTEXT.md"))
        {
            contextMatches.Add(item);
        }
        Assert.Single(contextMatches);
        Assert.Equal("VS Code - Project", contextMatches[0].Window.Title);

        // 2. Search by process keyword
        var browserMatches = new List<DesktopContextSnapshot>();
        await foreach (var item in queryStore.SearchHistoryAsync("waterfox"))
        {
            browserMatches.Add(item);
        }
        Assert.Single(browserMatches);
        Assert.Equal("Waterfox Browser", browserMatches[0].Window.Title);

        // 3. Search non-matching term
        var noMatches = new List<DesktopContextSnapshot>();
        await foreach (var item in queryStore.SearchHistoryAsync("NonExistentTerm"))
        {
            noMatches.Add(item);
        }
        Assert.Empty(noMatches);
    }

    [Fact]
    public async Task SearchHistoryAsync_WhitespaceQuery_ReturnsEmptyEnumerable()
    {
        var options = new StorageOptions { DatabasePath = _testDbPath };
        await using var store = new SqliteDesktopStateStore(options);
        await store.InitializeAsync();

        var results = new List<DesktopContextSnapshot>();
        await foreach (var item in store.SearchHistoryAsync("   "))
        {
            results.Add(item);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task MaintenancePruning_ClampsMaxRetentionCount()
    {
        var options = new StorageOptions
        {
            DatabasePath = _testDbPath,
            MaxRetentionCount = 10,
            MaintenanceCommitCadence = 5 // Trigger maintenance every 5 commits
        };

        var store = new SqliteDesktopStateStore(options);
        await store.InitializeAsync();

        for (int i = 1; i <= 25; i++)
        {
            store.UpdateCurrentSnapshot(CreateTestSnapshot($"Snapshot #{i}", "test.exe", $"item_{i}", DesktopSemanticZone.EditorCodeBuffer, DateTimeOffset.UtcNow.AddSeconds(-30 + i)));
        }

        await store.DisposeAsync();

        await using var queryStore = new SqliteDesktopStateStore(options);
        await queryStore.InitializeAsync();

        var allItems = new List<DesktopContextSnapshot>();
        await foreach (var item in queryStore.GetHistoryAsync(DateTimeOffset.UtcNow.AddHours(-1), limit: 100))
        {
            allItems.Add(item);
        }

        // Total retained items must not exceed MaxRetentionCount (10)
        Assert.InRange(allItems.Count, 1, 10);
        Assert.Equal("Snapshot #25", allItems[0].Window.Title); // Newest is preserved
    }

    private static DesktopContextSnapshot CreateTestSnapshot(
        string title, string processName, string activeFileOrTab, DesktopSemanticZone zone, DateTimeOffset? timestamp = null)
    {
        return new DesktopContextSnapshot
        {
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
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
                Title = title,
                ProcessName = processName,
                Pid = 1234,
                ClassName = "SampleClass",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080),
                IsMinimized = false,
                IsMaximized = true
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = activeFileOrTab,
                AutomationId = "editor",
                ClassName = "monaco-editor",
                BoundingBox = new BoundingRectangle(100, 100, 800, 600),
                SemanticZone = zone,
                ValueSnippet = null
            },
            IdeContext = processName.Contains("Antigravity", StringComparison.OrdinalIgnoreCase) || processName.Contains("Code", StringComparison.OrdinalIgnoreCase)
                ? new IdeContext
                {
                    ActiveFilePath = activeFileOrTab,
                    ActiveSidebarView = "Explorer",
                    GitBranch = "main",
                    EditBuffer = activeFileOrTab,
                    Breadcrumbs = ["src", activeFileOrTab],
                    OpenEditorTabs = [new() { Index = 1, Title = activeFileOrTab, IsActive = true, IsDirty = false }]
                }
                : null,
            ExtractionDurationMs = 1.0
        };
    }
}
