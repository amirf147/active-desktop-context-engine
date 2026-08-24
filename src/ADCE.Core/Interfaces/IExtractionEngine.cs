// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Models;

namespace ADCE.Core.Interfaces;

/// <summary>
/// Defines the engine contract for extracting semantic desktop context snapshots.
/// </summary>
public interface IExtractionEngine
{
    /// <summary>
    /// Extracts a comprehensive semantic context snapshot for the specified window handle.
    /// </summary>
    /// <param name="hwnd">Target window handle (HWND).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted context snapshot.</returns>
    ValueTask<DesktopContextSnapshot> ExtractSnapshotAsync(nint hwnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a comprehensive semantic context snapshot for the current foreground window.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted context snapshot.</returns>
    ValueTask<DesktopContextSnapshot> ExtractForegroundSnapshotAsync(CancellationToken cancellationToken = default);
}
