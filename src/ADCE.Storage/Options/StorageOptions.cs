// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.IO;

namespace ADCE.Storage.Options;

/// <summary>
/// Configuration options for ADCE in-memory live cache and SQLite WAL time-series store.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// File path to SQLite database. Set to ":memory:" or a temp path for in-memory / testing.
    /// Default: LocalAppData/ADCE/context_history.db
    /// </summary>
    public string DatabasePath { get; set; } = DefaultDatabasePath;

    /// <summary>
    /// Retention time window for historical snapshots.
    /// Default: 24 hours.
    /// </summary>
    public TimeSpan RetentionWindow { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum number of snapshots to retain before pruning oldest records.
    /// Default: 10,000.
    /// </summary>
    public int MaxRetentionCount { get; set; } = 10_000;

    /// <summary>
    /// Background write channel queue capacity.
    /// Default: 512.
    /// </summary>
    public int WriteQueueCapacity { get; set; } = 512;

    /// <summary>
    /// Interval for periodic WAL checkpointing and retention pruning.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Number of committed snapshots between maintenance pruning passes.
    /// Default: 500.
    /// </summary>
    public int MaintenanceCommitCadence { get; set; } = 500;

    public static string DefaultDatabasePath
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ADCE", "context_history.db");
        }
    }
}
