// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using ADCE.Core.Models;

namespace ADCE.Daemon.Hosting;

/// <summary>
/// Operational state of the ADCE Daemon.
/// </summary>
public enum DaemonState
{
    Starting,
    Running,
    Paused,
    Stopped,
    Faulted
}

/// <summary>
/// Immutable snapshot of the live operational health, telemetry metrics, and context state of the ADCE Daemon.
/// </summary>
public sealed record DaemonStatus
{
    public required DaemonState State { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required TimeSpan Uptime { get; init; }
    public required long TotalEventsReceived { get; init; }
    public required long TotalSnapshotsExtracted { get; init; }
    public required long TotalMcpRequestsServed { get; init; }
    public DesktopContextSnapshot? CurrentSnapshot { get; init; }
    public int SsePort { get; init; }
    public bool IsSseActive { get; init; }
    public bool IsStdioActive { get; init; }
    public required string DatabasePath { get; init; }
    public string? LastError { get; init; }
}
