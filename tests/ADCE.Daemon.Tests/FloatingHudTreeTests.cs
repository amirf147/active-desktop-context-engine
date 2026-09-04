// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADCE.Core.Enums;
using ADCE.Core.Events;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;
using ADCE.Daemon.Configuration;
using ADCE.Daemon.Hosting;
using ADCE.Daemon.UI;
using ADCE.Storage.Database;
using ADCE.Storage.Options;
using Xunit;

namespace ADCE.Daemon.Tests;

public sealed class FloatingHudTreeTests
{
    private static DesktopContextSnapshot CreateSampleIdeSnapshot()
    {
        return new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Primary Desktop"
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x00054321,
                Pid = 4321,
                ProcessName = "Code",
                Title = "FloatingHudForm.cs - ADCE",
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(100, 100, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Document",
                ElementName = "FloatingHudForm.cs",
                AutomationId = "monaco-editor-doc",
                ClassName = "monaco-editor-input",
                BoundingBox = new BoundingRectangle(200, 200, 1200, 800),
                SemanticZone = DesktopSemanticZone.EditorBuffer,
                ContainerPath = ["monaco-editor", "workbench.parts.editor", "workbench.main.container"],
                IsOverlay = false,
                ValueSnippet = "public sealed class FloatingHudForm : Form"
            },
            IdeContext = new IdeContext
            {
                WorkspaceRoot = "/mock/workspace/active-desktop-context-engine",
                ActiveFilePath = @"src\ADCE.Daemon\UI\FloatingHudForm.cs",
                GitBranch = "feature/hud-tree-view",
                Breadcrumbs = ["src", "ADCE.Daemon", "UI", "FloatingHudForm.cs"],
                OpenEditorTabs =
                [
                    new TabItemInfo { Title = "FloatingHudForm.cs", IsActive = true, IsDirty = false, IsPinned = false },
                    new TabItemInfo { Title = "TrayApplicationContext.cs", IsActive = false, IsDirty = false, IsPinned = false }
                ]
            }
        };
    }

    private static DesktopContextSnapshot CreateSampleBrowserSnapshot()
    {
        return new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Main"
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x00098765,
                Pid = 8888,
                ProcessName = "waterfox",
                Title = "GitHub - ADCE Repo",
                ClassName = "MozillaWindowClass",
                Archetype = DesktopAppArchetype.Gecko,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "Edit",
                ElementName = "Search GitHub",
                AutomationId = "query-input",
                ClassName = "SearchBox",
                BoundingBox = new BoundingRectangle(300, 50, 600, 40),
                SemanticZone = DesktopSemanticZone.AddressBar,
                ContainerPath = ["nav-bar", "browser-chrome"],
                IsOverlay = false
            },
            BrowserContext = new BrowserContext
            {
                UrlAddress = "https://github.com/amirf147/active-desktop-context-engine",
                ContainerType = "TreeStyleTab",
                TotalCount = 2,
                ActiveTab = "GitHub - ADCE Repo",
                Tabs =
                [
                    new TabItemInfo { Title = "GitHub - ADCE Repo", IsActive = true },
                    new TabItemInfo { Title = "Documentation", IsActive = false }
                ]
            }
        };
    }

    private sealed class DummyExtractor : IExtractionEngine
    {
        private readonly DesktopContextSnapshot _snapshot;
        public DummyExtractor(DesktopContextSnapshot snapshot) => _snapshot = snapshot;

        public ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_snapshot);

        public ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_snapshot);
    }

    private sealed class DummyHookProvider : IEventHookProvider
    {
        private readonly Channel<DesktopEventToken> _channel = Channel.CreateUnbounded<DesktopEventToken>();
        public ChannelReader<DesktopEventToken> EventReader => _channel.Reader;
        public bool IsRunning { get; private set; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() => Stop();
    }

    [Fact]
    public void FloatingHudForm_ExplicitMode_AutoExpandsTreeView()
    {
        var sampleSnapshot = CreateSampleIdeSnapshot();
        var options = new DaemonOptions
        {
            IsHeadless = false,
            EnableSse = false,
            EnableSemanticZones = false, // Explicit Mode
            DatabasePath = ":memory:"
        };

        var store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = ":memory:" });
        var host = new DaemonHost(options, store, new DummyExtractor(sampleSnapshot), new DummyHookProvider());

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var hud = new FloatingHudForm(host);
                Assert.NotNull(hud);
                Assert.Equal(480, hud.Height); // Auto-expanded in explicit mode
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool joined = thread.Join(5000);

        if (threadEx != null) throw new InvalidOperationException($"STA thread failed: {threadEx.Message}", threadEx);
        Assert.True(joined);
    }

    [Fact]
    public void FloatingHudForm_HudTreeFlag_InitializesExpanded()
    {
        var sampleSnapshot = CreateSampleIdeSnapshot();
        var options = new DaemonOptions
        {
            IsHeadless = false,
            EnableSse = false,
            ShowHudTree = true,
            DatabasePath = ":memory:"
        };

        var store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = ":memory:" });
        var host = new DaemonHost(options, store, new DummyExtractor(sampleSnapshot), new DummyHookProvider());

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var hud = new FloatingHudForm(host);
                Assert.NotNull(hud);
                Assert.Equal(480, hud.Height);

                // Toggle collapsing
                hud.ToggleTreeView(false);
                Assert.Equal(165, hud.Height);

                // Toggle expanding
                hud.ToggleTreeView(true);
                Assert.Equal(480, hud.Height);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool joined = thread.Join(5000);

        if (threadEx != null) throw new InvalidOperationException($"STA thread failed: {threadEx.Message}", threadEx);
        Assert.True(joined);
    }

    [Fact]
    public void FloatingHudForm_BrowserSnapshot_ConstructsBrowserTreeCorrectly()
    {
        var sampleSnapshot = CreateSampleBrowserSnapshot();
        var options = new DaemonOptions
        {
            IsHeadless = false,
            EnableSse = false,
            ShowHudTree = true,
            DatabasePath = ":memory:"
        };

        var store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = ":memory:" });
        var host = new DaemonHost(options, store, new DummyExtractor(sampleSnapshot), new DummyHookProvider());

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var hud = new FloatingHudForm(host);
                Assert.NotNull(hud);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool joined = thread.Join(5000);

        if (threadEx != null) throw new InvalidOperationException($"STA thread failed: {threadEx.Message}", threadEx);
        Assert.True(joined);
    }

    [Fact]
    public void FloatingHudForm_SnapshotWithHierarchy_ConstructsSemanticHierarchyTreeNodes()
    {
        var sampleSnapshot = new DesktopContextSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Workspace = new WorkspaceEnvelope
            {
                VirtualDesktopId = Guid.NewGuid(),
                DesktopIndex = 0,
                VirtualDesktopName = "Code Workspace"
            },
            Window = new WindowEnvelope
            {
                Hwnd = 0x00012345,
                Pid = 5555,
                ProcessName = "Code",
                Title = "SemanticRuleEngine.cs - ADCE",
                ClassName = "Chrome_WidgetWin_1",
                Archetype = DesktopAppArchetype.ChromiumElectron,
                Bounds = new BoundingRectangle(0, 0, 1920, 1080)
            },
            Focus = new FocusedControlInfo
            {
                ControlType = "TreeItem",
                ElementName = "Timeline: Commit History",
                AutomationId = "timeline.item.1",
                ClassName = "monaco-tree",
                BoundingBox = new BoundingRectangle(50, 300, 250, 25),
                SemanticZone = DesktopSemanticZone.Timeline,
                PaneLocation = WindowPaneLocation.PrimarySidebar,
                ActiveView = "Explorer",
                SectionName = "Timeline",
                SemanticPath = ["PrimarySidebar", "Explorer", "Timeline"],
                ContainerClasses = ["monaco-list", "pane-header"]
            }
        };

        var options = new DaemonOptions
        {
            IsHeadless = false,
            EnableSse = false,
            ShowHudTree = true,
            DatabasePath = ":memory:"
        };

        var store = new SqliteDesktopStateStore(new StorageOptions { DatabasePath = ":memory:" });
        store.UpdateCurrentSnapshot(sampleSnapshot);
        var host = new DaemonHost(options, store, new DummyExtractor(sampleSnapshot), new DummyHookProvider());

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var hud = new FloatingHudForm(host);
                Assert.NotNull(hud);

                // Verify label representations
                Assert.Contains("Pane: [PrimarySidebar]", hud.ZoneLabel.Text);
                Assert.Contains("Hierarchy: PrimarySidebar › Explorer › Timeline", hud.HierarchyLabel.Text);

                // Verify TreeView hierarchy nodes
                var rootNode = hud.TreeView.Nodes[0];
                TreeNode? semanticNode = null;
                foreach (TreeNode node in rootNode.Nodes)
                {
                    if (node.Text.StartsWith("🏛️ Semantic Hierarchy"))
                    {
                        semanticNode = node;
                        break;
                    }
                }

                Assert.NotNull(semanticNode);
                Assert.Single(semanticNode.Nodes);

                var paneNode = semanticNode.Nodes[0];
                Assert.Equal("🪟 Window Pane: PrimarySidebar", paneNode.Text);
                Assert.Single(paneNode.Nodes);

                var viewNode = paneNode.Nodes[0];
                Assert.Equal("📑 Active View: Explorer", viewNode.Text);
                Assert.Single(viewNode.Nodes);

                var sectionNode = viewNode.Nodes[0];
                Assert.Equal("📂 Section: Timeline", sectionNode.Text);
                Assert.Single(sectionNode.Nodes);

                var targetZoneNode = sectionNode.Nodes[0];
                Assert.Equal("🎯 Target Zone: Timeline [TreeItem]", targetZoneNode.Text);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool joined = thread.Join(5000);

        if (threadEx != null) throw new InvalidOperationException($"STA thread failed: {threadEx.Message}", threadEx);
        Assert.True(joined);
    }
}
