// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Threading;
using System.Threading.Channels;
using ADCE.Core.Events;

namespace ADCE.Core.Interfaces;

/// <summary>
/// Defines the event hook contract for capturing Windows OS foreground, focus, and desktop switch events.
/// </summary>
public interface IEventHookProvider : IDisposable
{
    /// <summary>
    /// Channel reader exposing the non-blocking stream of 16-byte unmanaged desktop event tokens.
    /// </summary>
    ChannelReader<DesktopEventToken> EventReader { get; }

    /// <summary>
    /// Installs OS WinEvent hooks and starts dispatching events.
    /// </summary>
    void Start();

    /// <summary>
    /// Unhooks OS WinEvent hooks and ceases dispatching events.
    /// </summary>
    void Stop();

    /// <summary>
    /// Indicates whether the event hooks are currently active.
    /// </summary>
    bool IsRunning { get; }
}
