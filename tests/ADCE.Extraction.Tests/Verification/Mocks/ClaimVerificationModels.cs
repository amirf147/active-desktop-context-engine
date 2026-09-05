// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Models;

namespace ADCE.Extraction.Tests.Verification.Mocks;

/// <summary>
/// Identifiers for the Ground-Truth Claim Verification Matrix scenarios.
/// </summary>
public enum ClaimId
{
    CLM_001,
    CLM_002,
    CLM_003,
    CLM_004,
    CLM_005,
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
