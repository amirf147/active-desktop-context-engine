# Active Desktop Context Engine (ADCE) Workspace Rules

## Agent Constraints & Workflows

### Runtime & Tooling
- **Language & Framework:** C# 14 / .NET 10 (`<TargetFramework>net10.0-windows</TargetFramework>`).
- **CLI Commands:** Use `dotnet build` and `dotnet run --project <path>` in PowerShell (`pwsh`).
- **UIA Stack:** Exclusively use `FlaUI.UIA3` (v5.0.0+) over native `UIAutomationCore.dll`.

### Documentation & Progressive Disclosure Hub
- **Primary Domain Context Hub:** [`docs/CONTEXT.md`](../docs/CONTEXT.md)
- **Educational Guide & Architecture Refresher:** [`docs/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md`](../docs/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md)
- **UI Automation SSOT:** [`docs/UI_AUTOMATION_STRUCTURES_REFERENCE.md`](../docs/UI_AUTOMATION_STRUCTURES_REFERENCE.md)
- **Requirements & Archetype Spec:** [`docs/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md`](../docs/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md)
- **MCP Schema & Tool Endpoints:** [`docs/MCP_SCHEMA_SPEC.md`](../docs/MCP_SCHEMA_SPEC.md)
- **External Research & Ecosystem Audit:** [`docs/external_research/README.md`](../docs/external_research/README.md)
- **Foundational Research Lineage:** Upstream accessibility research (documents 001–018) lives in [caster-user-directory-and-notes](https://github.com/amirf147/caster-user-directory-and-notes/tree/master/docs/accessibility_mcp).
- All internal documentation within this repository must use clean relative links.

### 4-Gate Epistemic Protocol (Mandatory)
Every architectural proposal must strictly adhere to the 4-gate verification protocol:
1. **Gate 1: Physical Observation & Telemetry:** Present raw telemetry metrics without premature architectural conclusions.
2. **Gate 2: Adversarial Red-Team:** Evaluate at least 3 mutually exclusive options with fatal flaws and hidden assumptions exposed.
3. **Gate 3: Empirical Micro-Spike:** Write a minimal (<50-line) live test spike to validate or falsify key physical assumptions before writing extensive code.
4. **Gate 4: Architectural Blueprint & Spec:** Formalize design specifications only after empirical validation passes.

### Version Control & Commits
- **NEVER execute `git commit` or `git push` commands autonomously.**
- When changes are complete, stage files (`git add`) if appropriate, and format a copy-paste ready conventional commit message using the `/commit` workflow for user review and manual execution.

### Licensing & Headers
- All source files (`.cs`), project files, and markdown documentation must include standard SPDX Apache-2.0 headers:
  `// SPDX-License-Identifier: Apache-2.0`
  `// Copyright (c) 2024-2026 Amir Farhadi`
