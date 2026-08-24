// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using ADCE.Core.Enums;

namespace ADCE.Core.Interfaces;

/// <summary>
/// Classifies top-level Windows applications into universal UI archetypes based on Win32 metadata.
/// </summary>
public interface IArchetypeClassifier
{
    /// <summary>
    /// Classifies an application into a universal archetype given its Win32 class and process name.
    /// </summary>
    /// <param name="className">Win32 window class name.</param>
    /// <param name="processName">Executable process name.</param>
    /// <param name="title">Window title bar string.</param>
    /// <returns>Resolved UI archetype.</returns>
    DesktopAppArchetype Classify(string className, string processName, string title);
}
