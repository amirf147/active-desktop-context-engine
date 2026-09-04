// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;

namespace ADCE.Core.Interfaces;

/// <summary>
/// Defines the contract for evaluating, adding, and persisting declarative semantic classification rules.
/// </summary>
public interface ISemanticRuleEngine
{
    /// <summary>
    /// Matches the specified control attributes against active rules in priority order.
    /// </summary>
    DesktopSemanticZone? MatchRule(
        string processName,
        string controlType,
        string elementName,
        string automationId,
        string className,
        ImmutableArray<string> containerPath);

    /// <summary>
    /// Adds or replaces a rule in the active rule collection.
    /// </summary>
    void AddOrUpdateRule(SemanticRule rule);

    /// <summary>
    /// Removes a rule by its unique identifier.
    /// </summary>
    bool RemoveRule(string ruleId);

    /// <summary>
    /// Returns all registered rules ordered by priority descending.
    /// </summary>
    IReadOnlyList<SemanticRule> GetAllRules();

    /// <summary>
    /// Persists all current rules to the storage backend.
    /// </summary>
    Task SaveRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads or reloads rules from the configuration file.
    /// </summary>
    void LoadRules();
}
