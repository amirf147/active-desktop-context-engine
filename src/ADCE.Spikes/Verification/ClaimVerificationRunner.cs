// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Spikes.Verification.Drivers;
using ADCE.Spikes.Verification.Models;

namespace ADCE.Spikes.Verification;

/// <summary>
/// Orchestrates execution of the Claim Verification Matrix (CLM-001 through CLM-006)
/// against either live Windows targets or synthetic mock drivers.
/// </summary>
public sealed class ClaimVerificationRunner
{
    private readonly IReadOnlyList<ClaimScenario> _scenarios;

    public ClaimVerificationRunner()
    {
        _scenarios = BuildScenarios();
    }

    public IReadOnlyList<ClaimScenario> Scenarios => _scenarios;

    public async Task<ClaimVerificationSuiteResult> RunSuiteAsync(
        IStimulusDriver driver,
        string suiteName = "Ground-Truth Claim Verification Suite",
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var totalSw = Stopwatch.StartNew();
        var results = new List<ClaimResult>();

        foreach (var scenario in _scenarios)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var result = await ExecuteScenarioAsync(scenario, driver, cancellationToken);
            results.Add(result);
        }

        totalSw.Stop();
        var endTime = DateTimeOffset.UtcNow;

        return new ClaimVerificationSuiteResult
        {
            SuiteName = suiteName,
            DriverType = driver.DriverName,
            StartTime = startTime,
            EndTime = endTime,
            TotalDurationMs = totalSw.Elapsed.TotalMilliseconds,
            Results = results
        };
    }

    public async Task<ClaimResult> RunSingleClaimAsync(
        ClaimId claimId,
        IStimulusDriver driver,
        CancellationToken cancellationToken = default)
    {
        var scenario = _scenarios.FirstOrDefault(s => s.Id == claimId)
            ?? throw new ArgumentException($"Unknown claim ID: {claimId}", nameof(claimId));

        return await ExecuteScenarioAsync(scenario, driver, cancellationToken);
    }

    private static async Task<ClaimResult> ExecuteScenarioAsync(
        ClaimScenario scenario,
        IStimulusDriver driver,
        CancellationToken cancellationToken)
    {
        try
        {
            bool preconditionsMet = await scenario.CheckPreconditions(driver, cancellationToken);
            if (!preconditionsMet)
            {
                return new ClaimResult
                {
                    Id = scenario.Id,
                    Title = scenario.Title,
                    Status = ClaimStatus.Skipped,
                    ElapsedMs = 0.0,
                    TelemetrySummary = "Prerequisites not met (target application or window not found).",
                    Assertions = ["Precondition check failed: required desktop window not discovered."],
                    SkipOrFailureReason = "Target application is not currently open.",
                    CapturedSnapshot = null
                };
            }

            return await scenario.ExecuteAsync(driver, cancellationToken);
        }
        catch (Exception ex)
        {
            return new ClaimResult
            {
                Id = scenario.Id,
                Title = scenario.Title,
                Status = ClaimStatus.Failed,
                ElapsedMs = 0.0,
                TelemetrySummary = $"Execution crashed: {ex.GetType().Name}: {ex.Message}",
                Assertions = [$"Unhandled Exception: {ex.Message}"],
                SkipOrFailureReason = ex.ToString(),
                CapturedSnapshot = null
            };
        }
    }

    private static IReadOnlyList<ClaimScenario> BuildScenarios()
    {
        return
        [
            new ClaimScenario
            {
                Id = ClaimId.CLM_001,
                Title = "Global Focus Bleed Prevention",
                Description = "Switching from GUI app to Win32 console (pwsh/conhost) bounds focus to target PID with zero cross-process leaf bleed.",
                TargetAppOrArchetype = "ClassicWin32 / ConsoleWindowClass (pwsh.exe, conhost.exe)",
                CheckPreconditions = (driver, ct) => Task.FromResult(true),
                ExecuteAsync = (driver, ct) =>
                {
                    if (driver is MockStimulusDriver mock) return mock.VerifyClm001GlobalFocusBleedAsync(ct);
                    if (driver is LiveWin32StimulusDriver live) return live.VerifyClm001GlobalFocusBleedAsync(ct);
                    return new MockStimulusDriver().VerifyClm001GlobalFocusBleedAsync(ct);
                }
            },
            new ClaimScenario
            {
                Id = ClaimId.CLM_002,
                Title = "Child HWND Normalization",
                Description = "Clicking nested Electron sub-panels (chat, terminal, git input) binds top-level root window identity and avoids noise dropping.",
                TargetAppOrArchetype = "ChromiumElectron / Chrome_WidgetWin_1 (VS Code / Antigravity)",
                CheckPreconditions = (driver, ct) => Task.FromResult(true),
                ExecuteAsync = (driver, ct) =>
                {
                    if (driver is MockStimulusDriver mock) return mock.VerifyClm002ChildHwndNormalizationAsync(ct);
                    if (driver is LiveWin32StimulusDriver live) return live.VerifyClm002ChildHwndNormalizationAsync(ct);
                    return new MockStimulusDriver().VerifyClm002ChildHwndNormalizationAsync(ct);
                }
            },
            new ClaimScenario
            {
                Id = ClaimId.CLM_003,
                Title = "IDE Semantic Zone Resolution",
                Description = "Ancestor climbing resolves Monaco code editor, integrated terminal, git commit box, and chat assistant.",
                TargetAppOrArchetype = "ChromiumElectron / Monaco Editor, xterm",
                CheckPreconditions = (driver, ct) => Task.FromResult(true),
                ExecuteAsync = (driver, ct) =>
                {
                    if (driver is MockStimulusDriver mock) return mock.VerifyClm003IdeSemanticZoneResolutionAsync(ct);
                    if (driver is LiveWin32StimulusDriver live) return live.VerifyClm003IdeSemanticZoneResolutionAsync(ct);
                    return new MockStimulusDriver().VerifyClm003IdeSemanticZoneResolutionAsync(ct);
                }
            },
            new ClaimScenario
            {
                Id = ClaimId.CLM_004,
                Title = "Browser Tab Sidebar vs. IDE Explorer",
                Description = "Browser vertical tabs (Tree Style Tab in Gecko) resolve to TabBar or DocumentContent, never misclassified as SidebarExplorer.",
                TargetAppOrArchetype = "Gecko / MozillaWindowClass (Waterfox, Firefox)",
                CheckPreconditions = (driver, ct) => Task.FromResult(true),
                ExecuteAsync = (driver, ct) =>
                {
                    if (driver is MockStimulusDriver mock) return mock.VerifyClm004BrowserSidebarVsIdeExplorerAsync(ct);
                    if (driver is LiveWin32StimulusDriver live) return live.VerifyClm004BrowserSidebarVsIdeExplorerAsync(ct);
                    return new MockStimulusDriver().VerifyClm004BrowserSidebarVsIdeExplorerAsync(ct);
                }
            },
            new ClaimScenario
            {
                Id = ClaimId.CLM_005,
                Title = "Burst Typing Debounce Clamping (WP 3.4)",
                Description = "Continuous typing bursts trigger periodic snapshot commits at <= 250ms intervals rather than starving indefinitely.",
                TargetAppOrArchetype = "Universal Event Pipeline (DebouncedDesktopEventPipeline)",
                CheckPreconditions = (driver, ct) => Task.FromResult(true),
                ExecuteAsync = (driver, ct) => new MockStimulusDriver().VerifyClm005BurstDebounceClampingAsync(ct)
            },
            new ClaimScenario
            {
                Id = ClaimId.CLM_006,
                Title = "Zero-Allocation Deduplication",
                Description = "Identical consecutive focus states emit zero SQLite writes or duplicate snapshot commits via HasSameSemanticState().",
                TargetAppOrArchetype = "Universal Event Pipeline & State Cache",
                CheckPreconditions = (driver, ct) => Task.FromResult(true),
                ExecuteAsync = (driver, ct) => new MockStimulusDriver().VerifyClm006ZeroAllocationDeduplicationAsync(ct)
            }
        ];
    }
}
