# Active Desktop Context Engine (ADCE) Workspace Rules

## Agent Constraints & Workflows

### Runtime & Tooling
- **Language & Framework:** C# 14 / .NET 10 (`<TargetFramework>net10.0-windows</TargetFramework>`).
- **CLI Commands:** Use `dotnet build` and `dotnet run --project <path>` in PowerShell (`pwsh`).
- **UIA Stack:** Exclusively use `FlaUI.UIA3` (v5.0.0+) over native `UIAutomationCore.dll`.

### Documentation Pointers
- Foundational research, telemetry, and architectural post-mortems live in Caster docs at `%LOCALAPPDATA%\caster\docs\accessibility_mcp\`.
- All internal documentation within this repo must use relative links or reference `%LOCALAPPDATA%\caster\docs\accessibility_mcp\`.

### 4-Gate Epistemic Protocol (Mandatory)
Every architectural proposal must strictly adhere to the 4-gate verification protocol:
1. **Gate 1: Physical Observation & Telemetry:** Present raw telemetry metrics without premature architectural conclusions.
2. **Gate 2: Adversarial Red-Team:** Evaluate at least 3 mutually exclusive options with fatal flaws and hidden assumptions exposed.
3. **Gate 3: Empirical Micro-Spike:** Write a minimal (<50-line) live test spike to validate or falsify key physical assumptions before writing extensive code.
4. **Gate 4: Architectural Blueprint & Spec:** Formalize design specifications only after empirical validation passes.

### Licensing & Headers
- All source files (`.cs`), project files, and markdown documentation must include standard SPDX Apache-2.0 headers:
  `// SPDX-License-Identifier: Apache-2.0`
  `// Copyright (c) 2024-2026 Amir Farhadi`
