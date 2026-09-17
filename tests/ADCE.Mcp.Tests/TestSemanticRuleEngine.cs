// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;

namespace ADCE.Mcp.Tests;

internal sealed class TestSemanticRuleEngine : ISemanticRuleEngine
{
    private readonly List<SemanticRule> _rules = [];

    public void AddOrUpdateRule(SemanticRule rule)
    {
        _rules.RemoveAll(r => r.RuleId == rule.RuleId);
        _rules.Add(rule);
    }

    public bool RemoveRule(string ruleId)
    {
        return _rules.RemoveAll(r => r.RuleId == ruleId) > 0;
    }

    public IReadOnlyList<SemanticRule> GetAllRules() => _rules.ToList();

    public DesktopSemanticZone? MatchRule(
        string processName,
        string controlType,
        string elementName,
        string automationId,
        string className,
        ImmutableArray<string> containerPath) => null;

    public SemanticRule? FindMatchingRule(
        string processName,
        string controlType,
        string elementName,
        string automationId,
        string className,
        ImmutableArray<string> containerPath) => null;

    public Task SaveRulesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void LoadRules() { }
}
