// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;
using ADCE.Core.Enums;

namespace ADCE.Core.Models;

/// <summary>
/// Represents a declarative pattern-matching rule for assigning a DesktopSemanticZone to a UI control.
/// </summary>
public sealed record SemanticRule
{
    /// <summary>Unique identifier for this rule.</summary>
    public string RuleId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>The semantic zone assigned when this rule matches.</summary>
    public required DesktopSemanticZone TargetZone { get; init; }

    /// <summary>Optional case-insensitive substring match for process executable name (e.g. "code", "antigravity").</summary>
    public string? ProcessPattern { get; init; }

    /// <summary>Optional exact match for UI Automation control type (e.g. "Edit", "TreeItem", "Document").</summary>
    public string? ControlType { get; init; }

    /// <summary>Optional case-insensitive substring match for AutomationId.</summary>
    public string? AutomationIdPattern { get; init; }

    /// <summary>Optional case-insensitive substring match for Win32 / UIA ClassName.</summary>
    public string? ClassNamePattern { get; init; }

    /// <summary>Optional case-insensitive substring match for ElementName / Name property.</summary>
    public string? ElementNamePattern { get; init; }

    /// <summary>Optional case-insensitive substring match for any container AutomationId in ContainerPath.</summary>
    public string? ContainerPattern { get; init; }

    /// <summary>Evaluation priority. Rules with higher priority are tested first. Defaults to 50.</summary>
    public int Priority { get; init; } = 50;

    /// <summary>Indicates whether this rule was explicitly created by the user via runtime tagging.</summary>
    public bool IsUserOverride { get; init; }

    /// <summary>Timestamp when this rule was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Optional human-readable comment or description.</summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Evaluates whether the provided element properties satisfy this rule.
    /// </summary>
    public bool Matches(
        string processName,
        string controlType,
        string elementName,
        string automationId,
        string className,
        ImmutableArray<string> containerPath)
    {
        if (!string.IsNullOrWhiteSpace(ProcessPattern) &&
            !processName.Contains(ProcessPattern, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ControlType) &&
            !controlType.Equals(ControlType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(AutomationIdPattern) &&
            !automationId.Contains(AutomationIdPattern, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ClassNamePattern) &&
            !className.Contains(ClassNamePattern, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ElementNamePattern) &&
            !elementName.Contains(ElementNamePattern, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ContainerPattern))
        {
            if (containerPath.IsDefaultOrEmpty)
            {
                return false;
            }

            bool matchedContainer = false;
            for (int i = 0; i < containerPath.Length; i++)
            {
                if (containerPath[i].Contains(ContainerPattern, StringComparison.OrdinalIgnoreCase))
                {
                    matchedContainer = true;
                    break;
                }
            }

            if (!matchedContainer)
            {
                return false;
            }
        }

        return true;
    }
}
