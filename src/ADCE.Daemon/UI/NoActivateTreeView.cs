// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ADCE.Daemon.UI;

/// <summary>
/// A non-activating, double-buffered WinForms TreeView designed for floating HUD overlays.
/// Intercepts Win32 WM_MOUSEACTIVATE to return MA_NOACTIVATE so user interaction (clicks, scrolls, expands)
/// never steals keyboard or window focus from active target applications.
/// </summary>
public sealed class NoActivateTreeView : TreeView
{
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public NoActivateTreeView()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.FromArgb(16, 20, 28);
        ForeColor = Color.FromArgb(220, 230, 245);
        LineColor = Color.FromArgb(60, 75, 100);
        BorderStyle = BorderStyle.None;
        Font = new Font("Consolas", 8.5f, FontStyle.Regular);
        ShowLines = true;
        ShowPlusMinus = true;
        ShowRootLines = true;
        FullRowSelect = false;
        HideSelection = false;
        HotTracking = true;

        NodeMouseDoubleClick += OnNodeMouseDoubleClick;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (nint)MA_NOACTIVATE;
            return;
        }

        base.WndProc(ref m);
    }

    private void OnNodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node != null && !string.IsNullOrWhiteSpace(e.Node.Text))
        {
            StaClipboardHelper.SetText(e.Node.Text);
        }
    }
}
