<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# Active Desktop Context Engine (ADCE) Workspace Rules

## Agent Constraints & Engineering Invariants

### Runtime & Language Stack
- **Language & Framework:** C# 14 / .NET 10 (`<TargetFramework>net10.0-windows</TargetFramework>`).
- **Automation Library:** Exclusively use `FlaUI.UIA3` (v5.0.0+) over native `UIAutomationCore.dll`.
- **CLI Commands:** Use `dotnet build` and `dotnet run --project <path>` in PowerShell (`pwsh`).
- **Daemon Process Lock Awareness:** When `ADCE.Daemon` is running in the background, executing a bare `dotnet test` will fail on MSBuild file locks. Target specific test projects (e.g. `dotnet test tests/ADCE.Extraction.Tests/ADCE.Extraction.Tests.csproj`), use `dotnet test --no-build`, or execute the change-aware test runner:
  ```pwsh
  python scripts/test_runner.py
  ```

### Performance & UI Automation Invariants
- **Zero Unbounded DOM Crawling:** Never crawl browser DOMs (`MozillaWindowClass`, `Chrome_WidgetWin_1`) or Monaco IDE trees with unpruned recursive walks. Always scope queries to container elements and use `CacheRequest` with `TreeScope.Children` and `AutomationElementMode.None`.
- **Apartment & Hook Decoupling:** OS WinEvent hooks run on an STA UI message pump and must push tokens to a `System.Threading.Channels.Channel<T>` and exit immediately (< 0.5 ms). All UIA element extraction is executed on MTA worker threads.
- **Trailing-Edge Debouncing:** Wait 50–75 ms after the latest focus or window event before querying UIA to ensure the target application's visual tree has stabilized.
- **Self-Monitoring Filtering:** Always ignore `GetConsoleWindow()`, own process PID, and terminal processes (`WindowsTerminal.exe`, `conhost.exe`).

### Documentation & Relative Link Standards
- **Documentation Hub:** [`docs/CONTEXT.md`](../docs/CONTEXT.md)
- **Core Architecture Specs:**
  - [`docs/architecture/CORE_DOMAIN_MODEL.md`](../docs/architecture/CORE_DOMAIN_MODEL.md)
  - [`docs/architecture/EXTRACTION_PIPELINE.md`](../docs/architecture/EXTRACTION_PIPELINE.md)
  - [`docs/architecture/STORAGE_ARCHITECTURE.md`](../docs/architecture/STORAGE_ARCHITECTURE.md)
  - [`docs/architecture/DAEMON_AND_CONSUMER_INTEGRATION.md`](../docs/architecture/DAEMON_AND_CONSUMER_INTEGRATION.md)
  - [`docs/architecture/PERCEPTION_PURITY_AND_ONTOLOGY_STRATEGY.md`](../docs/architecture/PERCEPTION_PURITY_AND_ONTOLOGY_STRATEGY.md)
- **Archive Exclusion:** Never index, search, or cite files in `docs/archive/` for active architecture or coding tasks.
- **Relative Links:** All documentation links must use clean relative markdown paths.

### Testing & Verification Invariants
- **Standard xUnit Suites Only:** Verification is performed exclusively via standard automated xUnit tests in `tests/`.
- **No Bespoke Meta-Runners:** Never construct synthetic claim verification runners, in-memory mock stimulus drivers, or markdown evidence generators.
- **Change-Aware Verification:** Prefer running `python scripts/test_runner.py` before presenting changes, as it verifies 7 independent domains (documentation links, secret/path hygiene, and 5 project test assemblies) with Merkle hash caching.

### Version Control & Safety Hygiene
- **NEVER execute `git commit` or `git push` autonomously.**
- Stage verified files (`git add`) and format a copy-paste ready conventional commit message following the `/commit` workflow for user review and manual execution.
- **Path Hygiene:** Never introduce hardcoded absolute user paths (e.g. personal profiles or user directories). Verified by `scripts/check_repo_safety.py`.
- **SPDX Headers:** All `.cs`, `.csproj`, `.py`, and `.md` files must include standard SPDX Apache-2.0 headers.
