// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Windows.Forms;
using ADCE.Core.Models;
using ADCE.Core.Serialization;
using ADCE.Daemon.Hosting;

namespace ADCE.Daemon.UI;

/// <summary>
/// Lightweight, non-activating floating HUD overlay for real-time visual telemetry.
/// Utilizes WS_EX_NOACTIVATE, WS_EX_TOPMOST, and ShowWithoutActivation to observe desktop
/// transitions in real-time without ever stealing keyboard focus from active target applications.
/// </summary>
public sealed class FloatingHudForm : Form
{
    private readonly DaemonHost _host;
    private Label _titleLabel = null!;
    private Label _processLabel = null!;
    private Label _zoneLabel = null!;
    private Label _detailLabel = null!;
    private Label _latencyLabel = null!;

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

    public FloatingHudForm(DaemonHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

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
        Size = new Size(380, 115);

        // Position in top-right of primary screen
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(workingArea.Right - Width - 25, workingArea.Top + 25);

        BackColor = Color.FromArgb(24, 28, 36);
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
            Location = new Point(310, 12),
            AutoSize = true
        };

        _titleLabel = new Label
        {
            Text = "Window: (Waiting for active window)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(220, 228, 240),
            Location = new Point(12, 34),
            Size = new Size(355, 18),
            AutoEllipsis = true
        };

        _zoneLabel = new Label
        {
            Text = "Zone: [Unknown] | Control: None",
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 190, 60), // Amber
            Location = new Point(12, 57),
            Size = new Size(355, 18),
            AutoEllipsis = true
        };

        _detailLabel = new Label
        {
            Text = "Tabs / Breadcrumbs: (None)",
            Font = new Font("Consolas", 8.0f, FontStyle.Regular),
            ForeColor = Color.FromArgb(170, 185, 205),
            Location = new Point(12, 80),
            Size = new Size(355, 18),
            AutoEllipsis = true
        };

        Controls.Add(_processLabel);
        Controls.Add(_latencyLabel);
        Controls.Add(_titleLabel);
        Controls.Add(_zoneLabel);
        Controls.Add(_detailLabel);

        // Mouse drag and double click handlers without focus stealing
        MouseDown += OnHudMouseDown;
        MouseMove += OnHudMouseMove;
        MouseUp += OnHudMouseUp;
        DoubleClick += OnHudDoubleClick;

        foreach (Control ctrl in Controls)
        {
            ctrl.MouseDown += OnHudMouseDown;
            ctrl.MouseMove += OnHudMouseMove;
            ctrl.MouseUp += OnHudMouseUp;
            ctrl.DoubleClick += OnHudDoubleClick;
        }
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
        string archetype = snapshot.Window?.Archetype.ToString() ?? "Unknown";
        _processLabel.Text = $"{proc} [{archetype}]";

        _latencyLabel.Text = $"{snapshot.ExtractionDurationMs:F1} ms";

        string title = snapshot.Window?.Title ?? "No Window Title";
        _titleLabel.Text = $"Window: {title}";

        string zone = snapshot.Focus?.SemanticZone.ToString() ?? "Unknown";
        string elemName = snapshot.Focus?.ElementName ?? "None";
        string ctrlType = snapshot.Focus?.ControlType ?? "None";
        _zoneLabel.Text = $"Zone: [{zone}] | {elemName} ({ctrlType})";

        if (snapshot.IdeContext != null)
        {
            string activeFile = string.IsNullOrWhiteSpace(snapshot.IdeContext.ActiveFilePath) ? "None" : System.IO.Path.GetFileName(snapshot.IdeContext.ActiveFilePath);
            _detailLabel.Text = $"IDE: {snapshot.IdeContext.OpenEditorTabs.Length} tabs | Active: {activeFile}";
        }
        else if (snapshot.BrowserContext != null)
        {
            string activeTab = string.IsNullOrWhiteSpace(snapshot.BrowserContext.ActiveTab) ? "None" : snapshot.BrowserContext.ActiveTab;
            _detailLabel.Text = $"Browser: {snapshot.BrowserContext.Tabs.Length} tabs | Active: {activeTab}";
        }
        else
        {
            _detailLabel.Text = $"Desktop: {snapshot.Workspace?.VirtualDesktopName ?? "Default"} (HWND 0x{snapshot.Window?.Hwnd:X8})";
        }
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
        }
        base.Dispose(disposing);
    }
}
