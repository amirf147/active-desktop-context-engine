// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Windows.Forms;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Daemon.Hosting;

namespace ADCE.Daemon.UI;

/// <summary>
/// Lightweight, non-activating floating HUD overlay for real-time visual telemetry and explicit DOM tree inspection.
/// Utilizes WS_EX_NOACTIVATE, WS_EX_TOPMOST, and ShowWithoutActivation to observe desktop
/// transitions in real-time without ever stealing keyboard focus from active target applications.
/// </summary>
public sealed class FloatingHudForm : Form
{
    private const int CollapsedHeight = 165;
    private const int ExpandedHeight = 480;
    private const int DefaultWidth = 520;

    private readonly DaemonHost _host;
    private readonly ToolTip _hudToolTip = new();
    private Label _processLabel = null!;
    private Label _latencyLabel = null!;
    private Button _treeToggleButton = null!;
    private Label _titleLabel = null!;
    private Label _focusLabel = null!;
    private Label _hierarchyLabel = null!;
    private Label _zoneLabel = null!;
    private Label _detailLabel = null!;
    private NoActivateTreeView _structuralTreeView = null!;

    internal Label HierarchyLabel => _hierarchyLabel;
    internal Label ZoneLabel => _zoneLabel;
    internal NoActivateTreeView TreeView => _structuralTreeView;

    private bool _isTreeExpanded;
    private Point _dragStartPoint;
    private bool _isDragging;

    /// <summary>
    /// Explicitly tells Windows Forms not to activate this window when shown.
    /// </summary>
    protected override bool ShowWithoutActivation => true;

    /// <summary>
    /// Applies Win32 extended window styles:
    /// - WS_EX_NOACTIVATE (0x08000000): Never takes OS focus when clicked or shown.
    /// - WS_EX_TOPMOST (0x00000008): Stays pinned floating on top of all windows.
    /// - WS_EX_TOOLWINDOW (0x00000080): Excluded from Alt+Tab task switcher and taskbar.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.NativeMethods.SetWindowPos(
            Handle,
            Native.NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            Native.NativeMethods.SWP_NOMOVE | Native.NativeMethods.SWP_NOSIZE | Native.NativeMethods.SWP_NOACTIVATE | Native.NativeMethods.SWP_SHOWWINDOW);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.NativeMethods.SetWindowPos(
            Handle,
            Native.NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            Native.NativeMethods.SWP_NOMOVE | Native.NativeMethods.SWP_NOSIZE | Native.NativeMethods.SWP_NOACTIVATE | Native.NativeMethods.SWP_SHOWWINDOW);
    }

    public FloatingHudForm(DaemonHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

        // Default to expanded tree view if --hud-tree or explicit-only mode is active
        _isTreeExpanded = _host.Options.ShowHudTree || !_host.EnableSemanticZones;

        InitializeHudComponents();

        // Wire host state change
        _host.SnapshotChanged += OnSnapshotChanged;

        var initial = _host.GetCurrentSnapshot();
        if (initial != null)
        {
            UpdateSnapshotDisplay(initial);
        }
    }

    private void InitializeHudComponents()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Size = new Size(DefaultWidth, _isTreeExpanded ? ExpandedHeight : CollapsedHeight);

        // Position in top-right of primary screen
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(workingArea.Right - Width - 25, workingArea.Top + 25);

        BackColor = Color.FromArgb(20, 24, 32);
        ForeColor = Color.FromArgb(235, 240, 250);

        _processLabel = new Label
        {
            Text = "ADCE HUD [Initializing...]",
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 215, 255), // Cyan
            Location = new Point(12, 10),
            AutoSize = true
        };

        _latencyLabel = new Label
        {
            Text = "< 1 ms",
            Font = new Font("Consolas", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(140, 160, 185),
            Location = new Point(295, 12),
            AutoSize = true
        };

        _treeToggleButton = new Button
        {
            Text = _isTreeExpanded ? "▲ Hide Tree" : "▼ DOM Tree",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 215, 255),
            BackColor = Color.FromArgb(30, 38, 52),
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            Location = new Point(418, 8),
            Size = new Size(90, 22),
            Cursor = Cursors.Hand
        };
        _treeToggleButton.FlatAppearance.BorderSize = 1;
        _treeToggleButton.FlatAppearance.BorderColor = Color.FromArgb(0, 160, 200);
        _treeToggleButton.Click += (s, e) => ToggleTreeView();

        _titleLabel = new Label
        {
            Text = "Window: (Waiting for active window)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(220, 228, 240),
            Location = new Point(12, 32),
            Size = new Size(495, 18),
            AutoEllipsis = true
        };

        _focusLabel = new Label
        {
            Text = "Focus: [None]",
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 190, 60), // Amber
            Location = new Point(12, 53),
            Size = new Size(495, 18),
            AutoEllipsis = true
        };

        _zoneLabel = new Label
        {
            Text = "Mode: [Explicit Structural Inspection]",
            Font = new Font("Consolas", 8.0f, FontStyle.Regular),
            ForeColor = Color.FromArgb(160, 230, 150), // Light green
            Location = new Point(12, 74),
            Size = new Size(495, 18),
            AutoEllipsis = true
        };

        _hierarchyLabel = new Label
        {
            Text = "Hierarchy: (Root)",
            Font = new Font("Consolas", 8.0f, FontStyle.Regular),
            ForeColor = Color.FromArgb(255, 215, 100), // Amber / gold
            Location = new Point(12, 95),
            Size = new Size(495, 18),
            AutoEllipsis = true
        };

        _detailLabel = new Label
        {
            Text = "Workspace: (Default)",
            Font = new Font("Consolas", 8.0f, FontStyle.Regular),
            ForeColor = Color.FromArgb(170, 185, 205),
            Location = new Point(12, 116),
            Size = new Size(495, 36),
            AutoEllipsis = true
        };

        _structuralTreeView = new NoActivateTreeView
        {
            Location = new Point(12, 160),
            Size = new Size(DefaultWidth - 24, ExpandedHeight - 172),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Visible = _isTreeExpanded
        };

        Controls.Add(_processLabel);
        Controls.Add(_latencyLabel);
        Controls.Add(_treeToggleButton);
        Controls.Add(_titleLabel);
        Controls.Add(_focusLabel);
        Controls.Add(_hierarchyLabel);
        Controls.Add(_zoneLabel);
        Controls.Add(_detailLabel);
        Controls.Add(_structuralTreeView);

        // Mouse drag and double click handlers without focus stealing
        MouseDown += OnHudMouseDown;
        MouseMove += OnHudMouseMove;
        MouseUp += OnHudMouseUp;
        DoubleClick += OnHudDoubleClick;

        foreach (Control ctrl in Controls)
        {
            if (ctrl is NoActivateTreeView || ctrl is Button) continue;

            ctrl.MouseDown += OnHudMouseDown;
            ctrl.MouseMove += OnHudMouseMove;
            ctrl.MouseUp += OnHudMouseUp;
            ctrl.DoubleClick += OnHudDoubleClick;
        }
    }

    /// <summary>
    /// Toggles or sets the visibility of the DOM & Structural Tree View.
    /// </summary>
    public void ToggleTreeView(bool? expand = null)
    {
        _isTreeExpanded = expand ?? !_isTreeExpanded;
        Height = _isTreeExpanded ? ExpandedHeight : CollapsedHeight;
        _structuralTreeView.Visible = _isTreeExpanded;
        _treeToggleButton.Text = _isTreeExpanded ? "▲ Hide Tree" : "▼ DOM Tree";

        if (_isTreeExpanded)
        {
            var snapshot = _host.GetCurrentSnapshot();
            if (snapshot != null)
            {
                PopulateStructuralTreeView(snapshot);
            }
        }

        Invalidate();
    }

    private void OnSnapshotChanged(DesktopContextSnapshot snapshot)
    {
        if (IsDisposed || !IsHandleCreated) return;

        try
        {
            BeginInvoke(() => UpdateSnapshotDisplay(snapshot));
        }
        catch { }
    }

    private void UpdateSnapshotDisplay(DesktopContextSnapshot snapshot)
    {
        if (IsDisposed) return;

        string proc = snapshot.Window?.ProcessName ?? "Unknown";
        int pid = snapshot.Window?.Pid ?? 0;
        string archetype = snapshot.Window?.Archetype.ToString() ?? "Unknown";
        _processLabel.Text = $"{proc} (PID {pid}) [{archetype}]";

        string modeTag = _host.EnableSemanticZones ? "Heuristics" : "Explicit";
        _latencyLabel.Text = $"{snapshot.ExtractionDurationMs:F1} ms | {modeTag}";

        string title = snapshot.Window?.Title ?? "No Window Title";
        _titleLabel.Text = $"Window: {title} (HWND 0x{snapshot.Window?.Hwnd:X8})";

        string elemName = snapshot.Focus?.ElementName ?? string.Empty;
        string ctrlType = snapshot.Focus?.ControlType ?? "Window";
        string autoId = snapshot.Focus?.AutomationId ?? string.Empty;
        string idStr = string.IsNullOrWhiteSpace(autoId) ? string.Empty : $" (ID: {autoId})";
        string nameStr = string.IsNullOrWhiteSpace(elemName) ? "(unnamed)" : $"\"{elemName}\"";
        _focusLabel.Text = $"Focus: [{ctrlType}] {nameStr}{idStr}";

        // Mode, Semantic Zone, and Macro Window Pane Badge
        string paneStr = (snapshot.Focus != null && snapshot.Focus.PaneLocation != WindowPaneLocation.Unknown)
            ? $" | Pane: [{snapshot.Focus.PaneLocation}]"
            : string.Empty;

        if (_host.EnableSemanticZones)
        {
            string zone = snapshot.Focus?.SemanticZone.ToString() ?? "Unknown";
            string overlayStr = snapshot.Focus?.IsOverlay == true ? " | Overlay: True" : string.Empty;
            _zoneLabel.Text = $"Zone: [{zone}]{paneStr}{overlayStr}";
            _zoneLabel.ForeColor = Color.FromArgb(160, 230, 150);
        }
        else
        {
            string overlayStr = snapshot.Focus?.IsOverlay == true ? " (Overlay: True)" : string.Empty;
            _zoneLabel.Text = $"[Explicit Inspection Mode]{paneStr}{overlayStr}";
            _zoneLabel.ForeColor = Color.FromArgb(200, 180, 255); // Lavender
        }

        // Structural & Semantic Hierarchy Path
        if (snapshot.Focus != null && snapshot.Focus.SemanticPath.Length > 0)
        {
            var path = string.Join(" › ", snapshot.Focus.SemanticPath);
            _hierarchyLabel.Text = $"Hierarchy: {path}";
            _hierarchyLabel.ForeColor = Color.FromArgb(255, 215, 100); // Amber / gold

            if (snapshot.Focus.ContainerClasses.Length > 0)
            {
                _hudToolTip.SetToolTip(_hierarchyLabel, $"Classes: {string.Join(" > ", snapshot.Focus.ContainerClasses)}");
            }
            else if (snapshot.Focus.ContainerPath.Length > 0)
            {
                _hudToolTip.SetToolTip(_hierarchyLabel, $"Container IDs: {string.Join(" > ", snapshot.Focus.ContainerPath)}");
            }
            else
            {
                _hudToolTip.SetToolTip(_hierarchyLabel, $"Path: {path}");
            }
        }
        else if (snapshot.Focus != null && snapshot.Focus.ContainerClasses.Length > 0)
        {
            var classes = string.Join(" > ", snapshot.Focus.ContainerClasses);
            _hierarchyLabel.Text = $"Classes: {classes}";
            _hierarchyLabel.ForeColor = Color.FromArgb(100, 200, 255);
            _hudToolTip.SetToolTip(_hierarchyLabel, classes);
        }
        else if (snapshot.Focus != null && snapshot.Focus.ContainerPath.Length > 0)
        {
            var path = string.Join(" > ", snapshot.Focus.ContainerPath);
            _hierarchyLabel.Text = $"Container IDs: {path}";
            _hierarchyLabel.ForeColor = Color.FromArgb(100, 200, 255);
            _hudToolTip.SetToolTip(_hierarchyLabel, path);
        }
        else
        {
            _hierarchyLabel.Text = "Hierarchy: (Direct Window Child / Root)";
            _hierarchyLabel.ForeColor = Color.FromArgb(100, 200, 255);
            _hudToolTip.SetToolTip(_hierarchyLabel, "No intermediate parent containers detected");
        }

        // Domain Specific Context
        if (snapshot.IdeContext != null)
        {
            string activeFile = string.IsNullOrWhiteSpace(snapshot.IdeContext.ActiveFilePath) ? "None" : System.IO.Path.GetFileName(snapshot.IdeContext.ActiveFilePath);
            string ws = string.IsNullOrWhiteSpace(snapshot.IdeContext.WorkspaceRoot) ? "None" : snapshot.IdeContext.WorkspaceRoot;
            string diff = snapshot.IdeContext.IsDiffEditor ? " [Diff]" : string.Empty;
            _detailLabel.Text = $"IDE Root: {ws} | Active: {activeFile}{diff}\nTabs ({snapshot.IdeContext.OpenEditorTabs.Length}) | Breadcrumbs ({snapshot.IdeContext.Breadcrumbs.Length})";
        }
        else if (snapshot.BrowserContext != null)
        {
            string activeTab = string.IsNullOrWhiteSpace(snapshot.BrowserContext.ActiveTab) ? "None" : snapshot.BrowserContext.ActiveTab;
            string url = string.IsNullOrWhiteSpace(snapshot.BrowserContext.UrlAddress) ? "(No URL)" : snapshot.BrowserContext.UrlAddress;
            _detailLabel.Text = $"Browser: {snapshot.BrowserContext.Tabs.Length} tabs | Active: {activeTab}\nURL: {url}";
        }
        else if (snapshot.TerminalContext != null)
        {
            string shell = snapshot.TerminalContext.ShellTitle ?? "Terminal";
            _detailLabel.Text = $"Shell: {shell} | Tabs: {snapshot.TerminalContext.Tabs.Length}";
        }
        else
        {
            _detailLabel.Text = $"Desktop: {snapshot.Workspace?.VirtualDesktopName ?? "Default"}";
        }

        // Populate TreeView if currently visible/expanded
        if (_isTreeExpanded)
        {
            PopulateStructuralTreeView(snapshot);
        }
    }

    private void PopulateStructuralTreeView(DesktopContextSnapshot snapshot)
    {
        _structuralTreeView.BeginUpdate();
        try
        {
            _structuralTreeView.Nodes.Clear();

            string proc = snapshot.Window?.ProcessName ?? "Unknown";
            int pid = snapshot.Window?.Pid ?? 0;
            string title = snapshot.Window?.Title ?? "Untitled";
            var rootNode = new TreeNode($"🪟 {proc} (PID {pid}) - \"{title}\"")
            {
                ForeColor = Color.FromArgb(0, 215, 255)
            };

            // Window Metadata
            if (snapshot.Window != null)
            {
                rootNode.Nodes.Add(new TreeNode($"Archetype: {snapshot.Window.Archetype} | HWND: 0x{snapshot.Window.Hwnd:X8} | Class: {snapshot.Window.ClassName}")
                {
                    ForeColor = Color.FromArgb(160, 180, 205)
                });
                rootNode.Nodes.Add(new TreeNode($"Bounds: Left={snapshot.Window.Bounds.Left}, Top={snapshot.Window.Bounds.Top}, Width={snapshot.Window.Bounds.Width}, Height={snapshot.Window.Bounds.Height}")
                {
                    ForeColor = Color.FromArgb(160, 180, 205)
                });
            }

            // Virtual Desktop / Workspace Node
            if (snapshot.Workspace != null)
            {
                rootNode.Nodes.Add(new TreeNode($"🖥️ Virtual Desktop: \"{snapshot.Workspace.VirtualDesktopName}\" (Index: {snapshot.Workspace.DesktopIndex})")
                {
                    ForeColor = Color.FromArgb(140, 200, 255)
                });
            }

            // Application Pane & Semantic Hierarchy
            if (snapshot.Focus != null &&
                (snapshot.Focus.PaneLocation != WindowPaneLocation.Unknown ||
                 snapshot.Focus.SemanticPath.Length > 0 ||
                 !string.IsNullOrWhiteSpace(snapshot.Focus.ActiveView)))
            {
                string pathSummary = snapshot.Focus.SemanticPath.Length > 0
                    ? string.Join(" › ", snapshot.Focus.SemanticPath)
                    : snapshot.Focus.PaneLocation.ToString();

                var semanticRootNode = new TreeNode($"🏛️ Semantic Hierarchy ({pathSummary})")
                {
                    ForeColor = Color.FromArgb(255, 215, 100) // Amber / gold
                };

                TreeNode currentSemanticNode = semanticRootNode;

                if (snapshot.Focus.PaneLocation != WindowPaneLocation.Unknown)
                {
                    var paneNode = new TreeNode($"🪟 Window Pane: {snapshot.Focus.PaneLocation}")
                    {
                        ForeColor = Color.FromArgb(100, 200, 255)
                    };
                    currentSemanticNode.Nodes.Add(paneNode);
                    currentSemanticNode = paneNode;
                }

                if (!string.IsNullOrWhiteSpace(snapshot.Focus.ActiveView))
                {
                    var viewNode = new TreeNode($"📑 Active View: {snapshot.Focus.ActiveView}")
                    {
                        ForeColor = Color.FromArgb(180, 220, 255)
                    };
                    currentSemanticNode.Nodes.Add(viewNode);
                    currentSemanticNode = viewNode;
                }

                if (!string.IsNullOrWhiteSpace(snapshot.Focus.SectionName))
                {
                    var sectionNode = new TreeNode($"📂 Section: {snapshot.Focus.SectionName}")
                    {
                        ForeColor = Color.FromArgb(200, 235, 255)
                    };
                    currentSemanticNode.Nodes.Add(sectionNode);
                    currentSemanticNode = sectionNode;
                }

                var targetZoneNode = new TreeNode($"🎯 Target Zone: {snapshot.Focus.SemanticZone} [{snapshot.Focus.ControlType}]")
                {
                    ForeColor = Color.FromArgb(160, 230, 150)
                };
                currentSemanticNode.Nodes.Add(targetZoneNode);

                rootNode.Nodes.Add(semanticRootNode);
            }

            // DOM & Structural Container Hierarchy
            var domRootNode = new TreeNode("📦 DOM & Container Hierarchy")
            {
                ForeColor = Color.FromArgb(120, 220, 255)
            };

            if (snapshot.Focus != null)
            {
                TreeNode currentContainerNode = domRootNode;

                // Build top-down tree from ancestor container chain (reversing child-to-parent extraction order)
                if (snapshot.Focus.ContainerPath.Length > 0)
                {
                    for (int i = snapshot.Focus.ContainerPath.Length - 1; i >= 0; i--)
                    {
                        var containerId = snapshot.Focus.ContainerPath[i];
                        var childNode = new TreeNode($"📁 <container id=\"{containerId}\">")
                        {
                            ForeColor = Color.FromArgb(160, 200, 240)
                        };
                        currentContainerNode.Nodes.Add(childNode);
                        currentContainerNode = childNode;
                    }
                }
                else if (snapshot.Focus.ContainerClasses.Length > 0)
                {
                    for (int i = snapshot.Focus.ContainerClasses.Length - 1; i >= 0; i--)
                    {
                        var cls = snapshot.Focus.ContainerClasses[i];
                        var childNode = new TreeNode($"📁 <container class=\"{cls}\">")
                        {
                            ForeColor = Color.FromArgb(160, 200, 240)
                        };
                        currentContainerNode.Nodes.Add(childNode);
                        currentContainerNode = childNode;
                    }
                }

                // Attach Focused Element as target leaf in container tree
                var focusNode = CreateFocusTreeNode(snapshot.Focus);
                currentContainerNode.Nodes.Add(focusNode);
            }
            rootNode.Nodes.Add(domRootNode);

            // Specialized Domain Subtrees
            if (snapshot.IdeContext != null)
            {
                var ideNode = new TreeNode("💻 IDE & Monaco Context")
                {
                    ForeColor = Color.FromArgb(255, 215, 100)
                };

                if (!string.IsNullOrWhiteSpace(snapshot.IdeContext.WorkspaceRoot))
                {
                    ideNode.Nodes.Add(new TreeNode($"📂 Workspace: {snapshot.IdeContext.WorkspaceRoot}"));
                }
                if (!string.IsNullOrWhiteSpace(snapshot.IdeContext.ActiveFilePath))
                {
                    string diffTag = snapshot.IdeContext.IsDiffEditor ? " [Diff Editor]" : string.Empty;
                    ideNode.Nodes.Add(new TreeNode($"📝 Active File: {snapshot.IdeContext.ActiveFilePath}{diffTag}")
                    {
                        ForeColor = Color.FromArgb(0, 255, 200)
                    });
                }
                if (!string.IsNullOrWhiteSpace(snapshot.IdeContext.GitBranch))
                {
                    ideNode.Nodes.Add(new TreeNode($"🌿 Git Branch: {snapshot.IdeContext.GitBranch}"));
                }

                if (snapshot.IdeContext.Breadcrumbs.Length > 0)
                {
                    var bcNode = new TreeNode($"🧭 Monaco Breadcrumbs ({snapshot.IdeContext.Breadcrumbs.Length})");
                    foreach (var bc in snapshot.IdeContext.Breadcrumbs)
                    {
                        bcNode.Nodes.Add(new TreeNode($"› {bc}") { ForeColor = Color.FromArgb(200, 225, 255) });
                    }
                    ideNode.Nodes.Add(bcNode);
                }

                if (snapshot.IdeContext.OpenEditorTabs.Length > 0)
                {
                    var tabsNode = new TreeNode($"📑 Open Tabs ({snapshot.IdeContext.OpenEditorTabs.Length})");
                    foreach (var tab in snapshot.IdeContext.OpenEditorTabs)
                    {
                        string marker = tab.IsActive ? " [Active ✦]" : string.Empty;
                        string pin = tab.IsPinned ? " [Pinned]" : string.Empty;
                        string dirty = tab.IsDirty ? " •" : string.Empty;
                        var tNode = new TreeNode($"📄 {tab.Title}{dirty}{pin}{marker}");
                        if (tab.IsActive) tNode.ForeColor = Color.FromArgb(0, 255, 200);
                        tabsNode.Nodes.Add(tNode);
                    }
                    ideNode.Nodes.Add(tabsNode);
                }

                rootNode.Nodes.Add(ideNode);
            }
            else if (snapshot.BrowserContext != null)
            {
                var browserNode = new TreeNode("🌐 Browser Context")
                {
                    ForeColor = Color.FromArgb(120, 220, 255)
                };

                if (!string.IsNullOrWhiteSpace(snapshot.BrowserContext.UrlAddress))
                {
                    browserNode.Nodes.Add(new TreeNode($"🔗 URL: {snapshot.BrowserContext.UrlAddress}")
                    {
                        ForeColor = Color.FromArgb(0, 255, 200)
                    });
                }

                if (!string.IsNullOrWhiteSpace(snapshot.BrowserContext.ContainerType))
                {
                    browserNode.Nodes.Add(new TreeNode($"📦 Tab Container: {snapshot.BrowserContext.ContainerType}"));
                }

                if (snapshot.BrowserContext.Tabs.Length > 0)
                {
                    var bTabsNode = new TreeNode($"📑 Tabs ({snapshot.BrowserContext.Tabs.Length})");
                    foreach (var tab in snapshot.BrowserContext.Tabs)
                    {
                        string marker = tab.IsActive ? " [Active ✦]" : string.Empty;
                        var tNode = new TreeNode($"🌐 {tab.Title}{marker}");
                        if (tab.IsActive) tNode.ForeColor = Color.FromArgb(0, 255, 200);
                        bTabsNode.Nodes.Add(tNode);
                    }
                    browserNode.Nodes.Add(bTabsNode);
                }

                rootNode.Nodes.Add(browserNode);
            }
            else if (snapshot.ExplorerContext != null)
            {
                var explorerNode = new TreeNode("📁 Explorer Context")
                {
                    ForeColor = Color.FromArgb(255, 200, 120)
                };

                if (!string.IsNullOrWhiteSpace(snapshot.ExplorerContext.CurrentPath))
                {
                    explorerNode.Nodes.Add(new TreeNode($"📂 Folder: {snapshot.ExplorerContext.CurrentPath}"));
                }
                if (snapshot.ExplorerContext.SelectedItems.Length > 0)
                {
                    var selNode = new TreeNode($"📑 Selected Items ({snapshot.ExplorerContext.SelectedItems.Length})");
                    foreach (var item in snapshot.ExplorerContext.SelectedItems)
                    {
                        selNode.Nodes.Add(new TreeNode($"📄 {item}"));
                    }
                    explorerNode.Nodes.Add(selNode);
                }

                rootNode.Nodes.Add(explorerNode);
            }
            else if (snapshot.TerminalContext != null)
            {
                var termNode = new TreeNode("💻 Terminal Context")
                {
                    ForeColor = Color.FromArgb(180, 230, 160)
                };

                termNode.Nodes.Add(new TreeNode($"🐚 Shell: {snapshot.TerminalContext.ShellTitle ?? "pwsh"}"));
                if (snapshot.TerminalContext.Tabs.Length > 0)
                {
                    var termTabsNode = new TreeNode($"📑 Terminal Tabs ({snapshot.TerminalContext.Tabs.Length})");
                    foreach (var tab in snapshot.TerminalContext.Tabs)
                    {
                        string marker = tab.IsActive ? " [Active ✦]" : string.Empty;
                        termTabsNode.Nodes.Add(new TreeNode($"💻 {tab.Title}{marker}"));
                    }
                    termNode.Nodes.Add(termTabsNode);
                }

                rootNode.Nodes.Add(termNode);
            }

            _structuralTreeView.Nodes.Add(rootNode);
            rootNode.ExpandAll();
        }
        finally
        {
            _structuralTreeView.EndUpdate();
        }
    }

    private static TreeNode CreateFocusTreeNode(FocusedControlInfo focus)
    {
        string nameStr = string.IsNullOrWhiteSpace(focus.ElementName) ? "(unnamed)" : $"\"{focus.ElementName}\"";
        string autoId = string.IsNullOrWhiteSpace(focus.AutomationId) ? string.Empty : $" [ID: {focus.AutomationId}]";
        var focusNode = new TreeNode($"🎯 Focused Element: [{focus.ControlType}] {nameStr}{autoId}")
        {
            ForeColor = Color.FromArgb(255, 190, 60) // Amber
        };

        if (focus.PaneLocation != WindowPaneLocation.Unknown)
        {
            focusNode.Nodes.Add(new TreeNode($"Window Pane: {focus.PaneLocation}")
            {
                ForeColor = Color.FromArgb(100, 200, 255)
            });
        }

        if (!string.IsNullOrWhiteSpace(focus.ActiveView))
        {
            focusNode.Nodes.Add(new TreeNode($"Active View: {focus.ActiveView}")
            {
                ForeColor = Color.FromArgb(180, 220, 255)
            });
        }

        if (!string.IsNullOrWhiteSpace(focus.SectionName))
        {
            focusNode.Nodes.Add(new TreeNode($"Section: {focus.SectionName}")
            {
                ForeColor = Color.FromArgb(200, 235, 255)
            });
        }

        if (focus.SemanticPath.Length > 0)
        {
            focusNode.Nodes.Add(new TreeNode($"Semantic Path: {string.Join(" › ", focus.SemanticPath)}")
            {
                ForeColor = Color.FromArgb(255, 215, 100)
            });
        }

        if (!string.IsNullOrWhiteSpace(focus.ClassName))
        {
            focusNode.Nodes.Add(new TreeNode($"Class: {focus.ClassName}")
            {
                ForeColor = Color.FromArgb(170, 185, 205)
            });
        }

        if (!focus.BoundingBox.IsEmpty)
        {
            focusNode.Nodes.Add(new TreeNode($"BoundingBox: Left={focus.BoundingBox.Left}, Top={focus.BoundingBox.Top}, Width={focus.BoundingBox.Width}, Height={focus.BoundingBox.Height}")
            {
                ForeColor = Color.FromArgb(170, 185, 205)
            });
        }

        if (focus.SemanticZone != DesktopSemanticZone.Unknown)
        {
            focusNode.Nodes.Add(new TreeNode($"Semantic Zone: {focus.SemanticZone}")
            {
                ForeColor = Color.FromArgb(160, 230, 150)
            });
        }

        if (focus.IsOverlay)
        {
            focusNode.Nodes.Add(new TreeNode("Overlay / Modal: True")
            {
                ForeColor = Color.FromArgb(255, 130, 130)
            });
        }

        if (!string.IsNullOrWhiteSpace(focus.ValueSnippet))
        {
            focusNode.Nodes.Add(new TreeNode($"💬 Value: \"{focus.ValueSnippet}\"")
            {
                ForeColor = Color.FromArgb(200, 240, 200)
            });
        }

        return focusNode;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // Draw sleek rounded border and accent top highlight
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var borderPen = new Pen(Color.FromArgb(50, 60, 80), 1.5f);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        using var accentPen = new Pen(Color.FromArgb(0, 210, 255), 2.0f);
        e.Graphics.DrawLine(accentPen, 2, 1, Width - 3, 1);

        // Draw divider when tree view is expanded
        if (_isTreeExpanded)
        {
            using var dividerPen = new Pen(Color.FromArgb(40, 50, 68), 1.0f);
            e.Graphics.DrawLine(dividerPen, 12, 155, Width - 12, 155);
        }
    }

    private void OnHudMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragStartPoint = e.Location;
        }
    }

    private void OnHudMouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var currentScreenPos = PointToScreen(e.Location);
            Location = new Point(currentScreenPos.X - _dragStartPoint.X, currentScreenPos.Y - _dragStartPoint.Y);
        }
    }

    private void OnHudMouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
    }

    private void OnHudDoubleClick(object? sender, EventArgs e)
    {
        var snapshot = _host.GetCurrentSnapshot();
        if (snapshot != null)
        {
            string json = JsonSerializer.Serialize(snapshot, AdceJsonSerializerOptions.Default);
            StaClipboardHelper.SetText(json);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host.SnapshotChanged -= OnSnapshotChanged;
            _hudToolTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
