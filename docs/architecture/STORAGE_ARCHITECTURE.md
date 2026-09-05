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
│   - Atomic reference swap       - Bounded Channel (512 capacity)       │
│   - Sub-microsecond read        - DropOldest backpressure              │
│   - Zero allocation             - Non-blocking to pipeline             │
│            │                             │                             │
│            ▼                             ▼                             │
│   Live MCP & HUD Queries        [ L2: SQLite WAL Store ]               │
│   (get_desktop_context,         - Single-writer connection             │
│    desktop://current)           - WAL journal mode                     │
│                                 - desktop_snapshots table              │
│                                 - Cadenced maintenance pruning         │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Storage Tiers & Guarantees

### 2.1 Tier 1: Live Atomic Cache (`InMemoryDesktopStateCache`)
* **Read Latency:** Sub-microsecond (< 1 µs).
* **Concurrency:** Lock-free atomic reference exchange via `Interlocked.Exchange`.
* **Serving Surface:** Handles high-frequency live queries from `ADCE.Mcp` (`get_desktop_context`, `desktop://current`) and the tray HUD (`FloatingHudForm`).

### 2.2 Tier 2: SQLite WAL Time-Series Store (`SqliteDesktopStateStore`)
* **Persistence File:** Default location is `%LOCALAPPDATA%\ADCE\context_history.db` per `StorageOptions.DefaultDatabasePath`. In-memory mode is supported via `":memory:"`.
* **Write Decoupling:** Snapshots are enqueued into a bounded `System.Threading.Channels.Channel<DesktopContextSnapshot>` (capacity: 512 items, configurable via `StorageOptions.WriteQueueCapacity`). If the disk writer falls behind during burst events, the channel drops oldest records, preventing memory growth or UI thread stalls.
* **Single-Writer Loop:** A dedicated background worker executes batched transaction inserts using SQLite Write-Ahead Logging (`PRAGMA journal_mode=WAL;`, `PRAGMA synchronous=NORMAL;`).
* **Table Schema (`desktop_snapshots`):**
  - `id` (INTEGER PRIMARY KEY AUTOINCREMENT)
  - `timestamp_utc` (TEXT NOT NULL, ISO-8601 UTC)
  - `timestamp_unix_ms` (INTEGER NOT NULL, Unix epoch milliseconds)
  - `hwnd` (INTEGER NOT NULL, Win32 window handle)
  - `window_title` (TEXT NOT NULL)
  - `process_name` (TEXT NOT NULL)
  - `class_name` (TEXT NOT NULL)
  - `archetype` (INTEGER NOT NULL, `DesktopAppArchetype` integer value)
  - `focus_control_type` (TEXT, UIA ControlType)
  - `focus_element_name` (TEXT, Accessible element name)
  - `focus_semantic_zone` (INTEGER NOT NULL, `DesktopSemanticZone` integer value)
  - `pane_location` (TEXT DEFAULT 'unknown', snake_case string representation)
  - `active_view` (TEXT DEFAULT '', e.g. "Explorer", "SourceControl")
  - `section_name` (TEXT DEFAULT '', e.g. "Timeline", "Outline")
  - `semantic_path` (TEXT DEFAULT '', e.g. "PrimarySidebar/Explorer/Timeline")
  - `active_file_or_tab` (TEXT, Resolved active file, tab, or shell title)
  - `container_path` (TEXT DEFAULT '', Ancestor automation IDs)
  - `container_classes` (TEXT DEFAULT '', Ancestor class names)
  - `snapshot_json` (TEXT NOT NULL, Complete serialized `DesktopContextSnapshot`)

---

## 3. Maintenance & Retention Policy

To bound disk utilization and maintain indexing efficiency on long-running developer machines:
1. **Periodic Pruning:** Every 500 committed snapshots (`MaintenanceCommitCadence`) or 5 minutes of runtime (`MaintenanceInterval`), the background writer executes bounded maintenance pruning records older than the retention window (default: 24 hours per `StorageOptions.RetentionWindow`).
2. **Capacity Bounds:** The store enforces a maximum retention count (default: 10,000 snapshots via `MaxRetentionCount`), pruning oldest records if row counts exceed this threshold.
3. **WAL Checkpointing:** Passive checkpointing runs periodically during maintenance passes to bound `.db-wal` file size.
