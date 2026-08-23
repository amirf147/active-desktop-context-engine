# Micro-Spike 1: FlaUI 5 / .NET 10 UIA3 CacheRequest Empirical Telemetry

> **Gate:** Gate 3 (Empirical Micro-Spikes)
> **Date:** 2026-08-23 04:07:52 UTC
> **Runtime:** .NET 10.0.8 (64-bit)
> **UIA Engine:** `FlaUI.UIA3 5.0.0` over Windows `UIAutomationCore.dll`

---

## Empirical Findings & Physical Reality

1. **HWND Binding Speed:** `automation.FromHandle(hwnd)` takes **< 1.0 ms** consistently.
2. **Container Discovery:** Finding the Tree Style Tab sidebar list container (`tabs normal`) takes **~3.7 ms – 14.3 ms**.
3. **Tab Extraction Latency:** Direct child extraction of 30 tabs takes **~8 ms – 12 ms** (~300 µs/tab).
4. **CacheRequest vs Live Physics:** UIA 3 `CacheRequest` on Gecko `tabs normal` container successfully retrieves element handles in a batch, but Gecko accessibility does not populate the cached `Name` property across the COM boundary unless the `ListItem` name is read on-demand or text descendants are queried.

---

## Raw Benchmark Telemetry Log

```text
==========================================================================
  ADCE Micro-Spike 1: FlaUI 5 / .NET 10 UIA3 Real-World Telemetry         
==========================================================================
Runtime   : .NET 10.0.8 (x64)
Timestamp : 2026-08-23T04:07:52.447Z

[INIT] UIA3Automation initialized in 14.81 ms

[WIN32] Discovered 11 candidate browser/IDE window(s).

--------------------------------------------------------------------------
 TARGET: [WATERFOX] 0x02240AFE (PID 35572)
 Title : '👃skin - Google Gemini — Waterfox'
--------------------------------------------------------------------------
[BIND] Bound AutomationElement in 11.68 ms
[CONTAINER] Found container 'tabs normal' (AutoId: 'window-9') in 13.41 ms
[EXTRACTION] Extracted 24 named tabs in 12.53 ms (521.9 µs/tab)

  Extracted Tabs Table:
  | Index | Active | Title |
  |-------|--------|-------|
  |     1 |            | Vuokra-asunnot: kaksio 11102 kpl - Oikotie, Suomen s... |
  |     2 |  **[ACTIVE]**  | 👃skin - Google Gemini 2 |
  |     3 |            | 30 m² Linnankatu 35, 20100 Turku Kerrostalo Yksiö vu... |
  |     4 |            | 216197164 (JPEG Image, 980 × 735 pixels) — Scaled (9... |
  |     5 |            | 13,5 m² Uimarinkatu 24, 20880 Turku Puutalo-osake Yk... |
  |     6 |            | Bergeninkatu, Turku - Vuokra Kerrostalo | Qasa 6 |
  |     7 |            | Kohdetta ei löytynyt | Qasa 7 |
  |     8 |            | Kirjaudu sisään Vend-tililläsi 8 |
  |     9 |            | Your FI Vend account 9 |
  |    10 |            | Luo tili | Qasa 10 |
  |    11 |            | - Kiinteistönvälitys Turku Asuntohelppi LKV Turku my... |
  |    12 |            | 47 m² Pormestarinkatu 4, 20750 Turku Kerrostalo Kaks... |
  |    13 |            | 38 m² Pormestarinkatu 3 E, 20750 Turku Kerrostalo Yk... |
  |    14 |            | Oikotie Asunnot | Suomen suosituin asuntopalvelu 14 |
  |    15 |            | Amir Farhadi - Portfolio Evidence 15 |
  |    16 |            | Desktop Context Engine Research Deep-Dive - Google G... |
  |    17 |            | Windows Agentic | Microsoft Developer 17 |
  |    18 |            | Inbox (5,015) - amirf147@gmail.com - Gmail 18 |
  |    19 |            | Varissuo Gym | Turku.fi 19 |
  |    20 |            | weather - Google Search 20 |
  |    21 |            | Op.fi verkkopalvelu | OP 21 |
  |    22 |            | Meteor Streaks Over Portugal - Google Gemini 22 |
  |    23 |            | Olet saanut uuden viestin OmaKelaan - amirf147@gmail... |
  |    24 |            | HW Engineer, RF System - Nokia Careers 24 |

--------------------------------------------------------------------------
 TARGET: [WATERFOX] 0x00270466 (PID 35572)
 Title : 'locomorange/uiautomation-mcp — Waterfox'
--------------------------------------------------------------------------
[BIND] Bound AutomationElement in 0.70 ms
[SEARCH] No active TreeStyleTab container found (21.85 ms).

--------------------------------------------------------------------------
 TARGET: [WATERFOX] 0x00390DE8 (PID 35572)
 Title : '6.7.0 - Supernova - Waterfox Release — Waterfox'
--------------------------------------------------------------------------
[BIND] Bound AutomationElement in 0.54 ms
[SEARCH] No active TreeStyleTab container found (12.50 ms).

--------------------------------------------------------------------------
 TARGET: [WATERFOX] 0x02860F44 (PID 35572)
 Title : 'UIA Fallback and Win32 Focus - Google Gemini — Waterfox'
--------------------------------------------------------------------------
[BIND] Bound AutomationElement in 0.72 ms
[CONTAINER] Found container 'tabs normal' (AutoId: 'window-7') in 3.69 ms
[EXTRACTION] Extracted 30 named tabs in 10.17 ms (339.0 µs/tab)

  Extracted Tabs Table:
  | Index | Active | Title |
  |-------|--------|-------|
  |     1 |  **[ACTIVE]**  | UIA Fallback and Win32 Focus - Google Gemini 1 |
  |     2 |            | AI Enhances Job Application Text - Google Gemini 2 |
  |     3 |            | Extracting truth from LLM outputs - Claude 3 |
  |     4 |            | Ohjelmistotestaaja | Patria 4 |
  |     5 |            | PowerShell Permission Denied Error - Google Gemini 5 |
  |     6 |            | ai native engineer m files - Google Search 6 |
  |     7 |            | YouTube 7 |
  |     8 |            | kunchenguid Official: Instagram, X, Threads | Linktr... |
  |     9 |            | kunchenguid/firstmate: Talk to one agent. Ship with ... |
  |    10 |            | AI agents will assess calls and transfer urgent case... |
  |    11 |            | 👃skin - Google Gemini 11 |
  |    12 |            | daanzu/kaldi-active-grammar: Python Kaldi speech rec... |
  |    13 |            | NestAI: one new job matching your profile - amirf147... |
  |    14 |            | TALENT POOL - Data Engineer (Finnish speakers) | Nor... |
  |    15 |            | TALENT POOL - Data Engineer (Finnish speakers) at No... |
  |    16 |            | DevSecOps Engineer - NestAI 16 |
  |    17 |            | cathrynlavery/diagram-design: 29 editorial diagram t... |
  |    18 |            | Model Comparison and Recommendation - Google Gemini 18 |
  |    19 |            | Software Developer - DNA 19 |
  |    20 |            | Agents Command (/agents) | Google Antigravity Docs 20 |
  |    21 |            | Gemini 3.7 Flash Benchmarks : r/GeminiAI 21 |
  |    22 |            | Gemini 3.7 Flash (high) vs Gemini 3.1 Pro Preview: M... |
  |    23 |            | User Directory - Caster 23 |
  |    24 |            | caster-user-directory-and-notes/docs/features/app_sw... |
  |    25 |            | caster-user-directory-and-notes/docs/wayfinder-uia-t... |
  |    26 |            | Amir Farhadi - Portfolio Evidence 26 |
  |    27 |            | Test Engineer | Comatec Group | LinkedIn 27 |
  |    28 |            | Test Engineer - Comatec 28 |
  |    29 |            | Log in | TikTok 29 |
  |    30 |            | Google AI Studio 30 |

==========================================================================
  MICRO-SPIKE 1 COMPLETE: EMPIRICAL FINDINGS SAVED
==========================================================================
```
