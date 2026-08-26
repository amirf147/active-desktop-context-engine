// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Models;

namespace ADCE.Core.Interfaces;

/// <summary>
/// Defines the workspace contract for virtual desktop and monitor topology resolution.
/// </summary>
public interface IWorkspaceManager
{
    /// <summary>
    /// Gets the current active virtual desktop workspace envelope.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active workspace envelope.</returns>
    ValueTask<WorkspaceEnvelope> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the virtual desktop envelope for a specific window handle.
    /// </summary>
    /// <param name="hwnd">Target window handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Workspace envelope containing the window.</returns>
    ValueTask<WorkspaceEnvelope> GetWindowWorkspaceAsync(nint hwnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates all currently configured virtual desktops.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of virtual desktops.</returns>
    ValueTask<IReadOnlyList<WorkspaceEnvelope>> GetAllWorkspacesAsync(CancellationToken cancellationToken = default);
}
