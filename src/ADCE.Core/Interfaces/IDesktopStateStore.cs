// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Models;

namespace ADCE.Core.Interfaces;

/// <summary>
/// Defines the storage contract for maintaining live in-memory context state and temporal history.
/// </summary>
public interface IDesktopStateStore
{
    /// <summary>
    /// Gets the current live in-memory snapshot with sub-millisecond latency.
    /// </summary>
    DesktopContextSnapshot? GetCurrentSnapshot();

    /// <summary>
    /// Updates the live in-memory snapshot and queues it for asynchronous persistence.
    /// </summary>
    /// <param name="snapshot">New desktop context snapshot.</param>
    void UpdateCurrentSnapshot(DesktopContextSnapshot snapshot);

    /// <summary>
    /// Queries historical snapshots within a relative time window (e.g. past 15 minutes).
    /// </summary>
    /// <param name="since">Start time boundary.</param>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of historical snapshots matching the time window.</returns>
    IAsyncEnumerable<DesktopContextSnapshot> GetHistoryAsync(
        DateTimeOffset since,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches past desktop history for matching keywords in titles, tabs, or file paths.
    /// </summary>
    /// <param name="query">Search term.</param>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching historical snapshots.</returns>
    IAsyncEnumerable<DesktopContextSnapshot> SearchHistoryAsync(
        string query,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
