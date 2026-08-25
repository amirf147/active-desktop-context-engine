// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Daemon.Configuration;
using ADCE.Daemon.Hosting;

namespace ADCE.Daemon.UI;

/// <summary>
/// Windows System Tray host application context.
/// Coordinates the NotifyIcon lifecycle, dynamic context menu updates, clipboard export, and graceful shutdown.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly DaemonHost _host;
    private readonly DaemonOptions _options;
    private readonly SynchronizationContext? _syncContext;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    private ToolStripMenuItem _headerMenuItem = null!;
    private ToolStripMenuItem _activeContextMenuItem = null!;
    private ToolStripMenuItem _copyJsonMenuItem = null!;
    private ToolStripMenuItem _pauseResumeMenuItem = null!;
    private ToolStripMenuItem _mcpEndpointsMenuItem = null!;
    private ToolStripMenuItem _storageStatsMenuItem = null!;

    private Icon? _currentIcon;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of <see cref="TrayApplicationContext"/> on the STA UI thread.
    /// </summary>
    public TrayApplicationContext(DaemonHost host, DaemonOptions options)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _syncContext = SynchronizationContext.Current;

        _contextMenu = new ContextMenuStrip
        {
            ShowImageMargin = false
        };

        BuildContextMenu();

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = "ADCE: Active Desktop Context Engine"
        };

        UpdateIcon(DaemonState.Running);

        // Wire event handlers
        _host.SnapshotChanged += OnSnapshotChanged;
        _host.StateChanged += OnStateChanged;
        _notifyIcon.DoubleClick += (s, e) => CopyCurrentContextToClipboard();

        // Initial menu and tooltip state
        var initialSnapshot = _host.GetCurrentSnapshot();
        if (initialSnapshot != null)
        {
            UpdateContextDisplay(initialSnapshot);
        }
    }

    private void BuildContextMenu()
    {
        _headerMenuItem = new ToolStripMenuItem("ADCE - Active Desktop Context Engine")
        {
            Enabled = false,
            Font = new Font(Control.DefaultFont, FontStyle.Bold)
        };

        _activeContextMenuItem = new ToolStripMenuItem("Active: Initializing...")
        {
            Enabled = false
        };

        _copyJsonMenuItem = new ToolStripMenuItem("📋 Copy Active Context (JSON)", null, (s, e) => CopyCurrentContextToClipboard());

        _pauseResumeMenuItem = new ToolStripMenuItem("⏸ Pause Monitoring", null, (s, e) => TogglePauseResume());

        _mcpEndpointsMenuItem = new ToolStripMenuItem("🔌 MCP Endpoints");
        PopulateMcpSubmenu();

        _storageStatsMenuItem = new ToolStripMenuItem("💾 Storage & Stats");
        PopulateStorageSubmenu();

        var refreshMenuItem = new ToolStripMenuItem("🔄 Refresh Context", null, (s, e) => _host.Resume());

        var exitMenuItem = new ToolStripMenuItem("❌ Exit ADCE", null, async (s, e) => await ExitApplicationAsync());

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            _headerMenuItem,
            _activeContextMenuItem,
            _copyJsonMenuItem,
            new ToolStripSeparator(),
            _pauseResumeMenuItem,
            _mcpEndpointsMenuItem,
            _storageStatsMenuItem,
            new ToolStripSeparator(),
            refreshMenuItem,
            exitMenuItem
        });
    }

    private void PopulateMcpSubmenu()
    {
        _mcpEndpointsMenuItem.DropDownItems.Clear();

        if (_options.EnableSse)
        {
            string sseUrl = $"http://localhost:{_options.Port}/sse";
            var sseItem = new ToolStripMenuItem($"SSE: {sseUrl}", null, (s, e) =>
            {
                Clipboard.SetText(sseUrl);
                _notifyIcon.ShowBalloonTip(1500, "ADCE MCP", "SSE URL copied to clipboard", ToolTipIcon.Info);
            });
            _mcpEndpointsMenuItem.DropDownItems.Add(sseItem);
        }

        if (_options.IsStdio)
        {
            _mcpEndpointsMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stdio: Active (Pipes)") { Enabled = false });
        }

        if (!_options.EnableSse && !_options.IsStdio)
        {
            _mcpEndpointsMenuItem.DropDownItems.Add(new ToolStripMenuItem("No active MCP transports") { Enabled = false });
        }
    }

    private void PopulateStorageSubmenu()
    {
        _storageStatsMenuItem.DropDownItems.Clear();

        var status = _host.GetStatus();
        _storageStatsMenuItem.DropDownItems.Add(new ToolStripMenuItem($"Snapshots Captured: {status.TotalSnapshotsExtracted}") { Enabled = false });
        _storageStatsMenuItem.DropDownItems.Add(new ToolStripMenuItem($"Events Ingested: {status.TotalEventsReceived}") { Enabled = false });
        _storageStatsMenuItem.DropDownItems.Add(new ToolStripMenuItem($"DB: {Path.GetFileName(status.DatabasePath)}") { Enabled = false });

        var openFolderItem = new ToolStripMenuItem("📂 Open Database Folder", null, (s, e) =>
        {
            var dir = Path.GetDirectoryName(status.DatabasePath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
        });
        _storageStatsMenuItem.DropDownItems.Add(openFolderItem);
    }

    private void OnSnapshotChanged(DesktopContextSnapshot snapshot)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => UpdateContextDisplay(snapshot), null);
        }
        else
        {
            UpdateContextDisplay(snapshot);
        }
    }

    private void OnStateChanged(DaemonState state)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ =>
            {
                UpdateIcon(state);
                UpdateStateMenu(state);
            }, null);
        }
        else
        {
            UpdateIcon(state);
            UpdateStateMenu(state);
        }
    }

    private void UpdateContextDisplay(DesktopContextSnapshot snapshot)
    {
        string title = snapshot.Window?.Title ?? "No active window";
        if (title.Length > 35) title = title[..32] + "...";

        string zone = snapshot.Focus?.SemanticZone.ToString() ?? "Unknown";

        _activeContextMenuItem.Text = $"Active: {title} [{zone}]";

        string tooltip = $"ADCE: {title} [{zone}]";
        if (tooltip.Length >= 64) tooltip = tooltip[..60] + "...";
        _notifyIcon.Text = tooltip;

        PopulateStorageSubmenu();
    }

    private void UpdateStateMenu(DaemonState state)
    {
        if (state == DaemonState.Paused)
        {
            _pauseResumeMenuItem.Text = "▶ Resume Monitoring";
            _headerMenuItem.Text = "ADCE - [PAUSED]";
        }
        else
        {
            _pauseResumeMenuItem.Text = "⏸ Pause Monitoring";
            _headerMenuItem.Text = "ADCE - Active Desktop Context Engine";
        }
    }

    private void UpdateIcon(DaemonState state)
    {
        var oldIcon = _currentIcon;
        _currentIcon = TrayIconFactory.CreateStateIcon(state, 32);
        _notifyIcon.Icon = _currentIcon;

        // Dispose old managed icon wrapper
        oldIcon?.Dispose();
    }

    private void TogglePauseResume()
    {
        if (_host.IsPaused)
        {
            _host.Resume();
        }
        else
        {
            _host.Pause();
        }
    }

    private void CopyCurrentContextToClipboard()
    {
        var snapshot = _host.GetCurrentSnapshot();
        if (snapshot != null)
        {
            string json = JsonSerializer.Serialize(snapshot, AdceJsonSerializerOptions.Default);
            Clipboard.SetText(json);
            _notifyIcon.ShowBalloonTip(1500, "ADCE Context", "Active snapshot JSON copied to clipboard", ToolTipIcon.Info);
        }
        else
        {
            _notifyIcon.ShowBalloonTip(1500, "ADCE Context", "No active snapshot available", ToolTipIcon.Warning);
        }
    }

    private async Task ExitApplicationAsync()
    {
        _notifyIcon.Visible = false;
        await _host.StopAsync();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (disposing)
            {
                _host.SnapshotChanged -= OnSnapshotChanged;
                _host.StateChanged -= OnStateChanged;

                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _contextMenu.Dispose();
                _currentIcon?.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
