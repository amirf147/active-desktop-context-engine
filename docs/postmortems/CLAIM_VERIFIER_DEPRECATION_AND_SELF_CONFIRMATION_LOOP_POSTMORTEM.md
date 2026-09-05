<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Postmortems ](./README.md) › **Claim Verifier Deprecation & Self-Confirmation Loop Postmortem**

---

# ADCE Claim Verifier Deprecation & Self-Confirmation Loop Postmortem

> **Document Status:** Active Retrospective Postmortem / Architectural Deprecation Notice
> **Epistemic Authority:** Tier 5 (Historical Subsystem Retrospective & Anti-Pattern Ledger — Non-Normative)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).
> **Target Subsystems:** `src/ADCE.Spikes/Verification/`, `docs/testing/EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md`
> **Verdict:** Subsystem Deprecated (Declared Legacy Meta-Tooling Anti-Pattern)
> **Replacement Baseline:** Standard automated xUnit test suites (`tests/ADCE.*.Tests`, 263 tests via `dotnet test`)

---

## 1. Executive Summary

During Milestone 4.5, a custom "Claim Verification Matrix" (`CLM-001` through `CLM-006`), an execution runner (`ClaimVerificationRunner`), and an automated markdown evidence generator (`EvidenceLedger`) were introduced into `ADCE.Spikes`. The stated ambition was an epistemic truth-engine: verifying that claims made in project documentation and architectural specifications were physically grounded in Windows OS reality rather than aspirational fiction.

In practice, this subsystem degraded into a **tautological self-confirmation loop** and **redundant meta-tooling**:
1. **The Self-Confirmation Loop:** To make headless CI/CD pass without a real Windows GUI, `MockStimulusDriver` was engineered to instantiate synthetic `DesktopContextSnapshot` objects in memory and assert that its own instantiated objects matched the properties it had just assigned. It exercised zero Win32 gating, zero UIA tree traversal, and zero heuristic classification logic.
2. **Fragile Live Execution:** When executed against the live desktop (`LiveWin32StimulusDriver`), the harness depended on accidental host desktop state (e.g., Waterfox with Tree Style Tab actively open and focused). In standard development environments, tests were silently skipped.
3. **Cognitive Drag & Meta-Tooling Sprawl:** The system created an entire secondary ceremony (bespoke runner, custom CLI flags, markdown report generation) parallel to standard .NET unit testing, obscuring what the product actually is: a fast background desktop context daemon (`ADCE.Daemon`) and MCP server (`ADCE.Mcp`) for AI agents and voice interfaces (Caster).

**Decision:** The bespoke Claim Verifier subsystem in `ADCE.Spikes/Verification` is formally deprecated as a failed experiment in redundant meta-tooling. Real behavioral invariants remain guarded by standard xUnit tests in `tests/ADCE.Extraction.Tests/Verification/ClaimVerificationTests.cs`.

---

## 2. Anatomy of the Self-Confirmation Loop Anti-Pattern

The fatal flaw of the Milestone 4.5 claim harness was the epistemic disconnect between what the test claimed to verify versus what the code actually executed.

### Code Case Study: `CLM-001` (Global Focus Bleed Prevention)

The documented claim for `CLM-001` states:
> *"Switching from GUI app to Win32 console (`pwsh.exe`) bounds focus to target PID with zero cross-process leaf bleed."*

In `MockStimulusDriver.cs`, the test was implemented as follows:

```csharp
// Source: src/ADCE.Spikes/Verification/Drivers/MockStimulusDriver.cs
public Task<ClaimResult> VerifyClm001GlobalFocusBleedAsync(CancellationToken ct)
{
    // 1. Manually construct a mock snapshot in memory:
    int pwshPid = 4812;
    nint pwshHwnd = (nint)0x001A00F4;
    var pwshFocus = new FocusedControlInfo {
        ControlType = "Window",
        ElementName = "Administrator: PowerShell",
        ClassName = "ConsoleWindowClass",
        SemanticZone = DesktopSemanticZone.Unknown
    };
    var snapshot = new DesktopContextSnapshot {
        Window = new WindowEnvelope { Hwnd = pwshHwnd, ProcessName = "pwsh", ProcessId = pwshPid, ... },
        Focus = pwshFocus
    };

    // 2. Assert against the object just created above:
    bool validHwnd = snapshot.Window.Hwnd == pwshHwnd; // Tautologically true
    bool pidBound = snapshot.Focus.SemanticZone != DesktopSemanticZone.EditorBuffer; // Tautologically true

    return Task.FromResult(new ClaimResult {
        Status = (validHwnd && pidBound) ? ClaimStatus.Passed : ClaimStatus.Failed,
        ...
    });
}
```

### Epistemic Critique:
* `UiaExtractionEngine` was never invoked.
* `Win32Gating` was never tested.
* The actual Windows OS interaction (foreground window switching, input attachment, HWND extraction) was completely bypassed.
* **The test was checking a mirror.** It passed 100% of the time in `0.00 ms`, producing a false green checkmark in `LATEST_CLAIM_VERIFICATION.md` that offered zero proof that focus bleed was actually prevented in Windows.

*(Exception: `CLM-005` burst debounce clamping and `CLM-006` deduplication did exercise `DebouncedDesktopEventPipeline` with real thread timings; however, these are standard pipeline unit tests, not unique "claims").*

---

## 3. Why Bespoke Meta-Tooling Fails

When engineering complex systems, developers and AI pair programmers frequently fall into the **Meta-Tooling Trap**: creating specialized frameworks to verify other frameworks rather than building the product.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        THE META-TOOLING SPIRAL                         │
├────────────────────────────────────────────────────────────────────────┤
│  Actual Product:                                                       │
│    ADCE.Daemon (Tray App) ──▶ ADCE.Extraction ──▶ ADCE.Mcp (AI / Voice)│
│                                                                        │
│  The Meta-Tooling Accretion:                                           │
│    ├── 4-Gate Protocol Documentation                                   │
│    ├── Claim Verification Matrix (CLM-001..CLM-006)                    │
│    ├── Bespoke ClaimVerificationRunner & Scenario Models               │
│    ├── Custom CLI flags (--verify-mocks, --verify-all, --verify)       │
│    ├── EvidenceLedger generating Markdown audit reports                │
│    └── Proposals for CLM-007 through CLM-014 meta-audits               │
└────────────────────────────────────────────────────────────────────────┘
```

### Key Retrospective Lessons:
1. **Industry-Standard Unit Testing Trumps Custom Runners:** Standard test frameworks (`xUnit`, `NUnit`) already solve test discovery, assertion reporting, parallelism, and CI integration. Writing a custom test runner inside an application project (`ADCE.Spikes`) introduces maintenance overhead with zero functional advantage.
2. **Ceremony Displaces Real Use Cases:** Time spent maintaining custom claim runners and formatting evidence ledgers was time not spent integrating ADCE with its actual consumers (e.g., Caster voice grammars or Claude Desktop MCP connections).
3. **If a Mock Tests Its Own Assignment, Delete It:** A mock that constructs its own verification target without passing through the production system-under-test is worse than no test—it creates an illusion of verification that masks underlying regressions.

---

## 4. Remediation & Deprecation Actions

To restore clarity and eliminate cognitive drag:

1. **Subsystem Deprecation:** `src/ADCE.Spikes/Verification/` is marked `[Obsolete]` and retained solely as a legacy milestone artifact.
2. **CLI Simplification:** The primary `dotnet run --project src/ADCE.Spikes -- --help` menu categorizes claim verification commands under `[LEGACY / RESEARCH SCAFFOLD]`.
3. **Canonical Verification Baseline:** All behavioral assertions for focus bleed, HWND normalization, and debounce clamping remain verified exclusively via the standard xUnit test suite:
   ```pwsh
   dotnet test tests/ADCE.Extraction.Tests/ADCE.Extraction.Tests.csproj
   ```
4. **Shift to Real-World Value:** Engineering focus transitions immediately away from claim verification ceremonies and directly to production consumer integrations:
   - **Caster Dynamic Voice Grammars:** [`docs/guides/FIRST_REAL_WORLD_USE_CASE_CASTER_DYNAMIC_TERMINAL_GRAMMARS.md`](../guides/FIRST_REAL_WORLD_USE_CASE_CASTER_DYNAMIC_TERMINAL_GRAMMARS.md).
   - **MCP Client Integrations:** Wiring live AI agents to `ADCE.Daemon` on `http://localhost:8424/messages` and SSE.
