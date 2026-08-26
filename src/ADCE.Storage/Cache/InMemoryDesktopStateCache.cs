// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Threading;
using ADCE.Core.Models;

namespace ADCE.Storage.Cache;

/// <summary>
/// High-performance, lock-free L1 in-memory cache for live desktop context snapshots.
/// Delivers sub-microsecond (< 0.001 ms) reads with zero locking overhead
/// via atomic pointer assignment over immutable records.
/// </summary>
public sealed class InMemoryDesktopStateCache
{
    private DesktopContextSnapshot? _currentSnapshot;

    /// <summary>
    /// Gets the current live snapshot in O(1) time (< 0.001 ms) with zero locks.
    /// </summary>
    public DesktopContextSnapshot? GetCurrentSnapshot()
    {
        return Volatile.Read(ref _currentSnapshot);
    }

    /// <summary>
    /// Atomically updates the live snapshot pointer with zero locks.
    /// </summary>
    public void UpdateCurrentSnapshot(DesktopContextSnapshot snapshot)
    {
        Volatile.Write(ref _currentSnapshot, snapshot);
    }

    /// <summary>
    /// Clears the live snapshot cache.
    /// </summary>
    public void Clear()
    {
        Volatile.Write(ref _currentSnapshot, null);
    }
}
