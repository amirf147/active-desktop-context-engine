<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# ADCE Dual-Tier Storage Architecture Specification

> **Document Status:** Active / Normative Storage Architecture Reference
> **Epistemic Authority:** Tier 1 (Normative Production Specification)
> **Implementation Target:** `src/ADCE.Storage/` (.NET 10 / C# 14 / SQLite)
> **Test Baseline:** 14/14 Passing Unit Tests in `tests/ADCE.Storage.Tests/`

---

## 1. Storage Architecture Overview

`ADCE.Storage` provides a dual-tier storage engine engineered for zero-latency live queries and durable historical auditing:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        ADCE DUAL-TIER STORAGE                          │
├────────────────────────────────────────────────────────────────────────┤
│ Ingestion:                                                             │
│   New Snapshot ──▶ [ Thread-Safe Channel Writer ]                      │
│                           │                                            │
│            ┌──────────────┴──────────────┐                             │
│            ▼                             ▼                             │
│   [ L1: Memory Cache ]          [ Background Queue ]                   │
│   - Atomic reference swap       - Bounded Channel                      │
│   - Sub-microsecond read        - DropOldest backpressure              │
│   - Zero allocation             - Non-blocking to pipeline             │
│            │                             │                             │
│            ▼                             ▼                             │
│   Live MCP Queries              [ L2: SQLite WAL Store ]               │
│   (get_current_snapshot)        - Single-writer connection             │
│                                 - WAL journal mode                     │
│                                 - desktop_snapshots table              │
│                                 - Automatic retention pruning          │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Storage Tiers & Guarantees

### 2.1 Tier 1: Live Atomic Cache (`InMemoryDesktopStateCache`)
* **Read Latency:** Sub-microsecond (< 1 µs).
* **Concurrency:** Lock-free atomic reference exchange via `Interlocked.Exchange`.
* **Serving Surface:** Handles high-frequency live queries from `ADCE.Mcp` (`get_current_snapshot`) and the tray HUD (`FloatingHudForm`).

### 2.2 Tier 2: SQLite WAL Time-Series Store (`SqliteDesktopStateStore`)
* **Persistence File:** Default location is `%LOCALAPPDATA%\ADCE\adce_state.db`.
* **Write Decoupling:** Snapshots are written to a bounded `System.Threading.Channels.Channel<DesktopContextSnapshot>` (capacity: 1,000 items). If the disk writer falls behind during burst events, the channel drops oldest records, preventing memory growth or UI thread stalls.
* **Single-Writer Loop:** A dedicated background worker executes batched transaction inserts using SQLite Write-Ahead Logging (`PRAGMA journal_mode=WAL;`).
* **Table Schema (`desktop_snapshots`):**
  - `id` (INTEGER PRIMARY KEY AUTOINCREMENT)
  - `timestamp_utc` (TEXT ISO 8601)
  - `hwnd` (INTEGER)
  - `process_id` (INTEGER)
  - `process_name` (TEXT)
  - `window_title` (TEXT)
  - `semantic_zone` (INTEGER)
  - `pane_location` (INTEGER)
  - `archetype` (INTEGER)
  - `snapshot_json` (TEXT)

---

## 3. Maintenance & Retention Policy

To bound disk utilization on long-running developer machines:
1. **Periodic Pruning:** Every 1,000 committed snapshots or 1 hour of runtime, the background writer executes a bounded cleanup query deleting snapshots older than the configured retention period (default: 7 days).
2. **WAL Checkpointing:** Passive checkpointing runs periodically to prevent unbounded `.db-wal` growth.
