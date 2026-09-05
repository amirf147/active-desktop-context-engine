# Active Desktop Context Engine (ADCE) Workspace Rules

## Agent Constraints & Workflows

### Runtime & Tooling
- **Language & Framework:** C# 14 / .NET 10 (`<TargetFramework>net10.0-windows</TargetFramework>`).
- **CLI Commands:** Use `dotnet build` and `dotnet run --project <path>` in PowerShell (`pwsh`).
- **UIA Stack:** Exclusively use `FlaUI.UIA3` (v5.0.0+) over native `UIAutomationCore.dll`.
- **Daemon Process Lock Awareness:** When `ADCE.Daemon` is running in the background, executing a bare `dotnet test` will fail on MSBuild file locks. Target specific test projects (e.g. `dotnet test tests/ADCE.Extraction.Tests/ADCE.Extraction.Tests.csproj`) or use `dotnet test --no-build`.

### Documentation & Progressive Disclosure Hub
- **Primary Domain Context Hub:** [`docs/CONTEXT.md`](../docs/CONTEXT.md)
- **Core Domain Model Spec:** [`docs/architecture/CORE_DOMAIN_MODEL.md`](../docs/architecture/CORE_DOMAIN_MODEL.md)
- **Extraction Pipeline Spec:** [`docs/architecture/EXTRACTION_PIPELINE.md`](../docs/architecture/EXTRACTION_PIPELINE.md)
- **Storage Architecture Spec:** [`docs/architecture/STORAGE_ARCHITECTURE.md`](../docs/architecture/STORAGE_ARCHITECTURE.md)
- **Daemon & Consumer Spec:** [`docs/architecture/DAEMON_AND_CONSUMER_INTEGRATION.md`](../docs/architecture/DAEMON_AND_CONSUMER_INTEGRATION.md)
- **Postmortems Ledger:** [`docs/postmortems/README.md`](../docs/postmortems/README.md)
- **Archive Exclusion:** Never index, search, or cite files in `docs/archive/` for active architecture or coding tasks. These are non-normative historical records.
- All internal documentation within this repository must use clean relative links.

### Testing & Verification Invariants
- **Standard xUnit Suites Only:** Real behavioral invariants are verified exclusively via standard automated xUnit tests in `tests/`.
- **No Bespoke Meta-Runners:** Never construct custom claim verification runners, in-memory mock stimulus drivers, or markdown evidence generators. If a test asserts against its own manually instantiated mock without exercising production code, delete it.

### 4-Gate Epistemic Protocol (Mandatory)
Every milestone or major subsystem must strictly adhere to the 4-gate verification workflow (`/gate`):
1. **Gate 1: Physical Observation & Telemetry:** Raw metrics and OS behavior baseline.
2. **Gate 2: Adversarial Red-Team:** 3-persona review (Internals, Performance, Security) evaluating 3 options.
3. **Gate 3: Empirical Micro-Spike:** Minimal (<50-line) live test in `ADCE.Spikes` before full library coding.
4. **Gate 4: Architectural Blueprint & Implementation:** Production code, unit tests, and Mermaid specs.

### Version Control & Commits
- **NEVER execute `git commit` or `git push` commands autonomously.**
- When changes are complete, stage files (`git add`) if appropriate, and format a copy-paste ready conventional commit message using the `/commit` workflow for user review and manual execution.

### Licensing & Headers
- All source files (`.cs`), project files, and markdown documentation must include standard SPDX Apache-2.0 headers:
  `// SPDX-License-Identifier: Apache-2.0`
  `// Copyright (c) 2026 Amir Farhadi`
