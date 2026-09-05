// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ADCE.Core.Events;

namespace ADCE.Extraction.Tests.Verification.Mocks;

/// <summary>
/// Abstraction for driving deterministic UI actions and event streams during claim verification.
/// </summary>
public interface IStimulusDriver
{
    /// <summary>
    /// Human-readable identifier for the driver.
    /// </summary>
    string DriverName { get; }

    /// <summary>
    /// Indicates whether the driver operates on live OS windows or simulated in-memory state.
    /// </summary>
    bool IsLive { get; }

    /// <summary>
    /// Attempts to locate a running window by class name, process name, or title substring.
    /// </summary>
    Task<nint> FindWindowAsync(string processOrClassName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates and brings the specified window handle to the foreground.
    /// </summary>
    Task<bool> ActivateWindowAsync(nint hwnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets keyboard focus onto a specific named or identified UI control within a window.
    /// </summary>
    Task<bool> SetFocusControlAsync(nint hwnd, string autoIdOrName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Injects a high-frequency stream of desktop event tokens into a channel for debounce/burst testing.
    /// </summary>
    Task InjectEventBurstAsync(
        ChannelWriter<DesktopEventToken> writer,
        nint hwnd,
        int eventCount,
        TimeSpan spacing,
        CancellationToken cancellationToken = default);
}
