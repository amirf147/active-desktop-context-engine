// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Models;

namespace ADCE.Spikes.Verification.Models;

/// <summary>
/// Identifiers for the Ground-Truth Claim Verification Matrix scenarios.
/// </summary>
public enum ClaimId
{
    /// <summary>CLM-001: Global Focus Bleed Prevention (Console vs GUI isolation).</summary>
    CLM_001,

    /// <summary>CLM-002: Child HWND Normalization (Sub-panel root resolution).</summary>
    CLM_002,

    /// <summary>CLM-003: IDE Semantic Zone Resolution (Monaco & Terminal climbing).</summary>
    CLM_003,

    /// <summary>CLM-004: Browser Tab Sidebar vs. IDE Explorer (Gecko sidebar isolation).</summary>
    CLM_004,

    /// <summary>CLM-005: Burst Typing Debounce Clamping (WP 3.4 max delay interval).</summary>
    CLM_005,

    /// <summary>CLM-006: Zero-Allocation Deduplication (Twin wavelet suppression).</summary>
    CLM_006
}

/// <summary>
/// Status outcome of a claim verification execution.
/// </summary>
public enum ClaimStatus
{
    Passed,
    Failed,
    Skipped
}

/// <summary>
/// Represents an executable ground-truth claim verification scenario.
/// </summary>
public sealed class ClaimScenario
{
    public required ClaimId Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string TargetAppOrArchetype { get; init; }
    public required Func<IStimulusDriver, CancellationToken, Task<bool>> CheckPreconditions { get; init; }
    public required Func<IStimulusDriver, CancellationToken, Task<ClaimResult>> ExecuteAsync { get; init; }
}

/// <summary>
/// Immutable record detailing the verification outcome and telemetry evidence for a specific claim.
/// </summary>
public sealed record ClaimResult
{
    public required ClaimId Id { get; init; }
    public required string Title { get; init; }
    public required ClaimStatus Status { get; init; }
    public required double ElapsedMs { get; init; }
    public required string TelemetrySummary { get; init; }
    public required IReadOnlyList<string> Assertions { get; init; }
    public string? SkipOrFailureReason { get; init; }
    public DesktopContextSnapshot? CapturedSnapshot { get; init; }
}

/// <summary>
/// Aggregate summary report for an entire claim verification run.
/// </summary>
public sealed record ClaimVerificationSuiteResult
{
    public required string SuiteName { get; init; }
    public required string DriverType { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required double TotalDurationMs { get; init; }
    public required IReadOnlyList<ClaimResult> Results { get; init; }

    public int PassedCount => Results.Count(r => r.Status == ClaimStatus.Passed);
    public int FailedCount => Results.Count(r => r.Status == ClaimStatus.Failed);
    public int SkippedCount => Results.Count(r => r.Status == ClaimStatus.Skipped);
    public bool AllPassed => FailedCount == 0 && PassedCount > 0;
}
