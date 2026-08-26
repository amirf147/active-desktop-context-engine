---
description: Structured 4-Gate Epistemic Protocol for milestone verification and adversarial red-teaming.
---
<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# 4-Gate Epistemic Milestone Verification Workflow

Use this workflow before and during the implementation of any architectural milestone or major subsystem in the Active Desktop Context Engine (ADCE).

---

## The 4-Gate Protocol Lifecycle

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        4-GATE EPISTEMIC VERIFICATION PIPELINE                          │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ GATE 1: Physical Observation & Telemetry Baseline                                     │
│ • Inspect the real-world Win32 API, OS behavior, or data baseline.                     │
│ • Present raw telemetry metrics without premature architectural conclusions.           │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ GATE 2: Adversarial Red-Team (3 Multi-Persona Reviewers)                               │
│ • Systems & Windows Internals Architect (COM, STA/MTA, UIPI, P/Invokes).               │
│ • Zero-Allocation Performance Profiler (GC churn, boxing, structs, latency).           │
│ • Security & Privacy Auditor (token leaks, credential redaction, buffer security).     │
│ • Evaluate at least 3 mutually exclusive options with fatal flaws exposed.             │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ GATE 3: Empirical Micro-Spike (< 50 lines in ADCE.Spikes)                              │
│ • Write a minimal, isolated test in `src/ADCE.Spikes` to validate physical assumptions.│
│ • Run live in the console, verify output with the user, and confirm latency/CPU metrics│
├────────────────────────────────────────────────────────────────────────────────────────┤
│ GATE 4: Architectural Blueprint & Production Implementation                            │
│ • Formalize specifications and implement production library code.                      │
│ • Run unit test suite (dotnet test) + check_repo_safety.py.                            │
│ • Generate Mermaid.js architectural blueprints and document lessons learned.           │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## Step 1: Gate 1 — Physical Observation & Telemetry Baseline
1. Identify the specific OS API, Win32 event, or data contract for this milestone.
2. Record baseline metrics (event firing frequency, typical payload sizes, process elevation states).
3. Do **NOT** propose solutions or write production library code during Gate 1.

---

## Step 2: Gate 2 — Adversarial Red-Team Review

Conduct a hostile review using the 3 specialized reviewer personas:

### Persona 1: Systems & Windows Internals Architect
* Where does this design mix STA and MTA COM apartments?
* What happens if the target process hangs, pauses for GC, or runs elevated (UIPI barrier)?
* Does the event queue block the UI thread if the consumer stalls?

### Persona 2: Zero-Allocation Performance Profiler
* Identify hidden heap allocations in high-frequency loops (boxing, LINQ, string parsing).
* Are value-type structs (`ImmutableArray<T>`, `DesktopEventToken`) used where appropriate?
* Does custom `Equals()` avoid `IEnumerator<T>` allocations?

### Persona 3: Security & Privacy Auditor
* Does this component scrape sensitive buffers (`.env`, `.pem`, passwords)?
* Are URL query parameters and OAuth tokens stripped before emitting MCP payloads?
* What happens if an unauthenticated client connects to the endpoint?

*Mandate at least 3 mutually exclusive implementation options and select the strongest architecture.*

---

## Step 3: Gate 3 — Empirical Micro-Spike (< 50 Lines)
1. Add a minimal test method or CLI option to `src/ADCE.Spikes/Program.cs`.
2. Keep the spike under 50 lines—no large frameworks or extra layers.
3. Run the spike live:
   ```powershell
   dotnet run --project src/ADCE.Spikes -- <spike-flag>
   ```
4. Verify physical assumptions (e.g. 0% idle CPU, $< 15\text{ ms}$ latency, proper desktop station binding).

---

## Step 4: Gate 4 — Production Implementation & Documentation
1. Scaffold or implement the modular project in `src/`.
2. Add comprehensive automated unit tests in `tests/`.
3. Verify test pass rate:
   ```powershell
   dotnet test
   python scripts/check_repo_safety.py
   ```
4. Create/update architectural blueprints with Mermaid diagrams in `docs/`.
5. Format commit message with `/commit` for user review.
