// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using ADCE.Core.Enums;
using ADCE.Extraction.Models;

namespace ADCE.Extraction.Resolvers;

/// <summary>
/// Strategy contract for resolving fine-grained semantic zones and layout panes
/// for a specific application framework archetype.
/// </summary>
public interface IArchetypeZoneResolver
{
    DesktopAppArchetype SupportedArchetype { get; }

    bool TryResolve(
        FocusedControlDescriptor control,
        AncestorChain ancestors,
        out SemanticResolution resolution);
}
