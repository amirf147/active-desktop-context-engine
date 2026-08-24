// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

namespace ADCE.Core.Enums;

/// <summary>
/// Classifies operating system event notifications emitted by the desktop event hooks.
/// </summary>
public enum DesktopEventType
{
    /// <summary>Unspecified event.</summary>
    None = 0,

    /// <summary>The active foreground top-level window has changed (EVENT_SYSTEM_FOREGROUND).</summary>
    ForegroundChanged = 1,

    /// <summary>Keyboard or UI input focus moved to a different control (EVENT_OBJECT_FOCUS).</summary>
    FocusChanged = 2,

    /// <summary>The active virtual desktop workspace was switched.</summary>
    VirtualDesktopSwitched = 3,

    /// <summary>The internal UI tree structure of the active window changed (EVENT_OBJECT_CREATE/DESTROY).</summary>
    StructureChanged = 4,

    /// <summary>Periodic heartbeat token for liveness verification.</summary>
    Heartbeat = 5
}
