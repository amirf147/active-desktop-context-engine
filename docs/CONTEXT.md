<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# ADCE Domain Context & Engineering Hub

> **Target System:** Active Desktop Context Engine (ADCE)
> **Runtime:** .NET 10 (x64) + `FlaUI.UIA3 5.0.0`
> **Architecture:** Decoupled Layered Pipeline (Win32 Event Hooks + FlaUI.UIA3 + SQLite WAL + MCP Server)
> **Active Verification Baseline:** 274 Passing Unit Tests Across 5 Test Suites (`dotnet test`)
> **Canonical Architecture Specifications:** [`docs/architecture/`](./architecture/)

---

## 1. Project Purpose & System Topology

The **Active Desktop Context Engine (ADCE)** is a background Windows daemon and system tray application. It maintains an in-memory semantic snapshot of the active desktop state and exposes it over the **Model Context Protocol (MCP)** to local AI coding assistants and voice frameworks (Caster) with low latency and near-zero idle CPU usage.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                ADCE SYSTEM ARCHITECTURE                                │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  [Windows OS Events] ──▶ SetWinEventHook (Foreground / Focus Transitions)              │
│                                    │                                                   │
│                                    ▼ (STA WinEvent Pump in ADCE.Daemon)                │
│                 [Debounced Desktop Event Pipeline] (50 ms quiet window)                │
│                                    │                                                   │
│                                    ▼ (MTA Worker Thread)                               │
│        [FlaUI.UIA3 Targeted Extractor] (Zero Unbounded DOM Crawling)                   │
│        ├── VS Code / Antigravity: tabs, monaco-editor, chat-input, terminal            │
│        ├── Waterfox / Gecko: tabs, urlbar-input, web document body                     │
│        └── WinUI / Cascadia: Windows Terminal, console controls                        │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [Dynamic Rule Engine & Archetype Classifier]                           │
│                   ├── %LOCALAPPDATA%\ADCE\semantic_rules.json                          │
│                   └── Heuristic Selector Fallback Tree                                 │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [Dual-Tier State Storage Engine]                                       │
│                   ├── Tier 1: In-Memory Atomic Cache (< 1 µs reads)                    │
│                   └── Tier 2: SQLite WAL Time-Series Database                          │
│                                    │                                                   │
│                                    ▼                                                   │
│                 [ADCE.Mcp Server Endpoints (Stdio / SSE / HTTP)]                       │
│                 ├── AI Coding Assistants (Antigravity IDE, Claude, Cline)              │
│                 └── Voice Navigation Engines (Caster Dynamic Grammars)                 │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Documentation Structure & Organization

Documentation is organized into five functional tiers to keep developers and AI assistants grounded in verified contracts:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              ADCE DOCUMENTATION STRUCTURE                              │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 1: PRODUCTION CODE & AUTOMATED TESTS (Ground Truth Baseline)                      │
│ • Production Projects: src/ADCE.Core, ADCE.Extraction, ADCE.Storage, ADCE.Mcp,         │
│   ADCE.Daemon                                                                          │
│ • Automated Test Suites: tests/ (274 passing unit tests across 5 test assemblies)      │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 2: ARCHITECTURE SPECIFICATIONS (Normative Contracts)                              │
│ • Core Engine: CORE_DOMAIN_MODEL.md, EXTRACTION_PIPELINE.md, STORAGE_ARCHITECTURE.md   │
│ • Host & Protocol: DAEMON_AND_CONSUMER_INTEGRATION.md, MCP_SCHEMA_SPEC.md             │
│ • Scope & Strategy: ADCE_PHILOSOPHY_AND_SCOPE_BOUNDARIES.md,                           │
│   PERCEPTION_PURITY_AND_ONTOLOGY_STRATEGY.md                                           │
│ • UI Taxonomy & Rules: DESKTOP_UI_PERCEPTION_AND_VOLATILITY_TAXONOMY.md,              │
│   SEMANTIC_CLASSIFICATION_AND_INTERACTION_GRAPH_SPEC.md,                               │
│   APPLICATION_PANE_AND_HIERARCHY_STRUCTURES_RESEARCH.md,                               │
│   UI_AUTOMATION_STRUCTURES_REFERENCE.md, REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md    │
│ • Security & Hardening: SECURITY_AND_HYGIENE_AUDIT_2026.md,                            │
│   HOSTILE_ARCHITECTURE_REVIEW.md                                                       │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 3: APPLICATION PROFILES, GUIDES & DIAGRAMS (Operational Reference)                │
│ • Application Profiles: docs/app_hierarchies/ (Waterfox, Antigravity IDE, Terminal)    │
│ • Operational Guides: docs/guides/ (Focus detection, Caster voice, UIA refresher)       │
│ • Interactive Visualizers: docs/diagrams/ (Core UML, Waterfox, Antigravity IDE)        │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 4: POSTMORTEMS & VERIFICATION ROADMAPS (Engineering Ledgers)                      │
│ • docs/postmortems/ (11 milestone postmortems, claim verifier deprecation)             │
│ • docs/testing/ (Reviewer observations, systems hardening roadmap)                     │
│ • docs/benchmarks/ (Micro-spike telemetry benchmarks)                                  │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ TIER 5: HISTORICAL ARCHIVE (Non-Normative)                                             │
│ • docs/archive/ (Legacy claim runs, exploratory research, deprecated specifications)   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Master Specification Directory

### 3.1 Core Pipeline & Storage Specifications
| Specification | Component | Primary Contracts Documented |
| :--- | :--- | :--- |
| [`architecture/CORE_DOMAIN_MODEL.md`](architecture/CORE_DOMAIN_MODEL.md) | `ADCE.Core` | `DesktopContextSnapshot`, `FocusedControlInfo`, 19 `DesktopSemanticZone` values, 6 `DesktopAppArchetype` categories, 9 `WindowPaneLocation` quadrants. |
| [`architecture/EXTRACTION_PIPELINE.md`](architecture/EXTRACTION_PIPELINE.md) | `ADCE.Extraction` | Win32 shallow gating (< 0.5 ms), UIPI privilege boundary checks, single-roundtrip `FlaUI.UIA3` batch caching, 50 ms debounce, dynamic rules. |
| [`architecture/STORAGE_ARCHITECTURE.md`](architecture/STORAGE_ARCHITECTURE.md) | `ADCE.Storage` | Sub-microsecond L1 atomic memory cache, channel-decoupled SQLite WAL time-series store (`desktop_snapshots`), automated retention pruning. |
| [`architecture/DAEMON_AND_CONSUMER_INTEGRATION.md`](architecture/DAEMON_AND_CONSUMER_INTEGRATION.md) | `ADCE.Daemon`, `ADCE.Mcp` | STA WinEvent hook pump, single-instance mutex, non-activating floating HUD overlay, JSON-RPC 2.0 endpoints, Caster voice integration. |
| [`architecture/MCP_SCHEMA_SPEC.md`](architecture/MCP_SCHEMA_SPEC.md) | `ADCE.Mcp` | JSON Schema Draft-07 contracts for context endpoints (`get_desktop_context`, `search_desktop_history`, `tag_active_control`, `desktop://current`). |

### 3.2 UI Perception, Hierarchy & Layout Specifications
| Specification | Component | Primary Contracts Documented |
| :--- | :--- | :--- |
| [`architecture/ADCE_PHILOSOPHY_AND_SCOPE_BOUNDARIES.md`](architecture/ADCE_PHILOSOPHY_AND_SCOPE_BOUNDARIES.md) | Architecture Boundary | Scope boundary definition: ADCE as a high-speed passive perception sensor vs. downstream action actors. |
| [`architecture/PERCEPTION_PURITY_AND_ONTOLOGY_STRATEGY.md`](architecture/PERCEPTION_PURITY_AND_ONTOLOGY_STRATEGY.md) | Architecture Strategy | 6 architectural pillars, perception purity vs prescriptive ontology, repository boundaries. |
| [`architecture/DESKTOP_UI_PERCEPTION_AND_VOLATILITY_TAXONOMY.md`](architecture/DESKTOP_UI_PERCEPTION_AND_VOLATILITY_TAXONOMY.md) | System Taxonomy | 4-level volatility spectrum (Level 1 OS ground truth to Level 4 CSS hashes), 7 framework archetypes, out-of-band protocol handoff. |
| [`architecture/SEMANTIC_CLASSIFICATION_AND_INTERACTION_GRAPH_SPEC.md`](architecture/SEMANTIC_CLASSIFICATION_AND_INTERACTION_GRAPH_SPEC.md) | Classification Engine | Closed structural archetype resolution (zero spatial math), fine-grained sub-zones, interactive control roles. |
| [`architecture/APPLICATION_PANE_AND_HIERARCHY_STRUCTURES_RESEARCH.md`](architecture/APPLICATION_PANE_AND_HIERARCHY_STRUCTURES_RESEARCH.md) | Layout Research | Empirical layout boundaries and container hierarchies for Monaco, Gecko, and WinUI workbenches. |
| [`architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md`](architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md) | Target Reference | Master directory of UIA accessibility node hierarchies, control types, class names, and extraction rules. |
| [`architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md`](architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md) | Dynamic Discovery | Adaptive discovery, `semantic_rules.json` persistence, and mitigation of hardcoded selector traps. |

### 3.3 Systems Hardening & Architecture Reviews
| Specification | Focus Area | Primary Scope Documented |
| :--- | :--- | :--- |
| [`architecture/SECURITY_AND_HYGIENE_AUDIT_2026.md`](architecture/SECURITY_AND_HYGIENE_AUDIT_2026.md) | Security & Hygiene | Master security audit covering MCP CORS/auth hardening, privacy sanitizer integration, 64-bit HWND bug, and COM RCW management. |
| [`architecture/HOSTILE_ARCHITECTURE_REVIEW.md`](architecture/HOSTILE_ARCHITECTURE_REVIEW.md) | Adversarial Review | Systems review evaluating COM apartment deadlocks, GC allocation churn, STA message pump isolation, and UIPI boundaries. |
| [`testing/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md`](testing/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md) | Hardening Roadmap | Systems observations and resolution status across Milestones 4 through 6. |

### 3.4 Application Hierarchies & Layout Profiles
| Profile | Target Application | Documented Automation Structure | Status |
| :--- | :--- | :--- | :--- |
| [`app_hierarchies/README.md`](app_hierarchies/README.md) | Profile Catalog Index | Hierarchy topologies, container classifications, and isolated VM testing strategy. | Active Index |
| [`app_hierarchies/01_waterfox.md`](app_hierarchies/01_waterfox.md) | Waterfox (Gecko) | Address bar (`urlbar-input`), tab bar (`tabbrowser-tab`), web document viewport. | Verified Baseline |
| [`app_hierarchies/02_antigravity_ide.md`](app_hierarchies/02_antigravity_ide.md) | Antigravity IDE (Electron) | Activity bar, explorer tree, editor tabs, Monaco text buffer, integrated terminal. | Verified Baseline |
| [`app_hierarchies/03_windows_terminal.md`](app_hierarchies/03_windows_terminal.md) | Windows Terminal (WinUI 3) | Tab strip (`TabView`), terminal console buffer (`TermControl`), new tab launcher. | Active Baseline (3/7 Surfaces) |
| [`app_hierarchies/TEMPLATE.md`](app_hierarchies/TEMPLATE.md) | Profile Specification | Canonical authoring template for new application layout profiles. | Template |

### 3.5 Operational Guides & Integrations
| Guide | Focus Area | Description |
| :--- | :--- | :--- |
| [`guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md`](guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md) | Focus Mechanics | Visual, step-by-step guide to Windows focus pointers, leaf controls, and ancestor climbing. |
| [`guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md`](guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md) | UIA & Systems Basics | Conceptual walkthrough of Windows UI Automation, Win32 programming, and FlaUI batch caching. |
| [`guides/FIRST_REAL_WORLD_USE_CASE_CASTER_DYNAMIC_TERMINAL_GRAMMARS.md`](guides/FIRST_REAL_WORLD_USE_CASE_CASTER_DYNAMIC_TERMINAL_GRAMMARS.md) | Voice Integration | Production integration: Dynamic voice grammar activation in VS Code / Antigravity IDE via SSE streaming. |

### 3.6 Interactive Visualizers
| Visualizer | Format | Description |
| :--- | :--- | :--- |
| [`diagrams/adce_core_architecture_uml.html`](diagrams/adce_core_architecture_uml.html) | Interactive HTML | Full-screen zoomable UML class model, component relationships, and sequence dataflow. |
| [`diagrams/antigravity_ide_hierarchy_diagram.html`](diagrams/antigravity_ide_hierarchy_diagram.html) | Interactive HTML | Interactive layout visualizer for Antigravity IDE workbench containers and automation IDs. |
| [`diagrams/waterfox_hierarchy_diagram.html`](diagrams/waterfox_hierarchy_diagram.html) | Interactive HTML | Interactive layout visualizer for Waterfox / Gecko browser chrome and tab containers. |

### 3.7 Engineering Postmortems & Retrospectives
| Postmortem | Focus Area | Key Architectural Finding |
| :--- | :--- | :--- |
| [`postmortems/README.md`](postmortems/README.md) | Master Index | Catalog covering all 11 milestone retrospectives and technical postmortems. |
| [`postmortems/CLAIM_VERIFIER_DEPRECATION_AND_SELF_CONFIRMATION_LOOP_POSTMORTEM.md`](postmortems/CLAIM_VERIFIER_DEPRECATION_AND_SELF_CONFIRMATION_LOOP_POSTMORTEM.md) | Verification | Analysis of tautological mock loops; enforcement of standard xUnit testing suites. |
| [`postmortems/LESSONS_LEARNED_HARDWARE_ACCELERATED_SCREENSHOTS_AND_UIA.md`](postmortems/LESSONS_LEARNED_HARDWARE_ACCELERATED_SCREENSHOTS_AND_UIA.md) | Graphics & Capture | Resolution of blank capture via `PW_RENDERFULLCONTENT` and UIA immunity to DirectX occlusion. |
| [`postmortems/STA_THREADING_AND_HUD_CASTER_INTEGRATION_POSTMORTEM.md`](postmortems/STA_THREADING_AND_HUD_CASTER_INTEGRATION_POSTMORTEM.md) | Threading & WinForms | STA message pump isolation and non-activating floating HUD window styles (`WS_EX_NOACTIVATE`). |
| [`postmortems/HEURISTIC_NAME_MATCHING_AND_MENU_ITEM_MISCLASSIFICATION.md`](postmortems/HEURISTIC_NAME_MATCHING_AND_MENU_ITEM_MISCLASSIFICATION.md) | Classification | Menu item substring collision analysis and replacement with structural ancestor inspection. |

### 3.8 Archived Historical Research
Superseded test run outputs, exploratory research into unused third-party libraries, and deprecated custom claim verification matrices are preserved in [`docs/archive/`](archive/README.md). They are retained for auditability and are excluded from active system indexing.

---

## 4. Core Engineering Invariants

1. **Win32 Shallow Gating Before UIA:** Always verify HWND validity, process identity, and UIPI privilege boundaries via Win32 APIs (< 0.5 ms) before initializing or querying UI Automation.
2. **Strict Traversal Depth Bounds:** Inspect only the focused leaf element and climb a maximum of 8 ancestor levels. Never invoke child discovery (`FindAllChildren`) on complex containers like web documents or code buffers.
3. **Event Pipeline Debouncing:** Enforce a 50 ms trailing-edge quiet window with a 250 ms maximum delay clamp to absorb rapid typing bursts while bounding latency.
4. **Dedicated Apartment Isolation:** Run `SetWinEventHook` on an STA message pump thread and offload all `FlaUI.UIA3` inspection to background worker threads.
5. **Standard Testing Frameworks:** Verify real behavioral invariants exclusively with standard automated xUnit tests (274 passing tests across 5 test projects). Never build custom verification runners or self-asserting mock drivers.
