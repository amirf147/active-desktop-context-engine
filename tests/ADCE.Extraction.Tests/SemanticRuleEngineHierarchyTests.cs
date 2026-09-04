// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Models;
using ADCE.Extraction.Rules;
using Xunit;

namespace ADCE.Extraction.Tests;

public class SemanticRuleEngineHierarchyTests : IDisposable
{
    private readonly string _testRulesPath;

    public SemanticRuleEngineHierarchyTests()
    {
        _testRulesPath = Path.Combine(Path.GetTempPath(), $"adce_rules_hierarchy_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testRulesPath))
        {
            try { File.Delete(_testRulesPath); } catch { }
        }
    }

    [Fact]
    public void FindMatchingRule_ReturnsRuleWithHierarchyFields()
    {
        var engine = new SemanticRuleEngine(_testRulesPath);
        var rule = new SemanticRule
        {
            RuleId = "rule_custom_timeline",
            TargetZone = DesktopSemanticZone.Timeline,
            TargetPane = WindowPaneLocation.PrimarySidebar,
            TargetView = "Explorer",
            TargetSection = "Timeline",
            ProcessPattern = "code",
            AutomationIdPattern = "timeline.tree",
            Priority = 80
        };
        engine.AddOrUpdateRule(rule);

        var matched = engine.FindMatchingRule(
            processName: "Code.exe",
            controlType: "Tree",
            elementName: "Timeline",
            automationId: "timeline.tree",
            className: "monaco-list",
            containerPath: ImmutableArray<string>.Empty);

        Assert.NotNull(matched);
        Assert.Equal("rule_custom_timeline", matched.RuleId);
        Assert.Equal(DesktopSemanticZone.Timeline, matched.TargetZone);
        Assert.Equal(WindowPaneLocation.PrimarySidebar, matched.TargetPane);
        Assert.Equal("Explorer", matched.TargetView);
        Assert.Equal("Timeline", matched.TargetSection);
    }

    [Fact]
    public void FindMatchingRule_WhenNoRuleMatches_ReturnsNull()
    {
        var engine = new SemanticRuleEngine(_testRulesPath);
        var matched = engine.FindMatchingRule(
            processName: "notepad.exe",
            controlType: "Edit",
            elementName: "Text Editor",
            automationId: "15",
            className: "Edit",
            containerPath: ImmutableArray<string>.Empty);

        Assert.Null(matched);
    }

    [Fact]
    public async Task AddOrUpdateRule_PersistsAndReloadsHierarchyFields()
    {
        var engine = new SemanticRuleEngine(_testRulesPath);
        engine.AddOrUpdateRule(new SemanticRule
        {
            RuleId = "persisted_rule",
            TargetZone = DesktopSemanticZone.ChatConversation,
            TargetPane = WindowPaneLocation.AuxiliarySidebar,
            TargetView = "Chat",
            TargetSection = "PromptHistory",
            ProcessPattern = "antigravity",
            AutomationIdPattern = "chat.container",
            Priority = 75
        });

        await Task.Delay(150);

        var reloadedEngine = new SemanticRuleEngine(_testRulesPath);
        var matched = reloadedEngine.FindMatchingRule(
            processName: "Antigravity IDE",
            controlType: "Pane",
            elementName: "Chat Container",
            automationId: "chat.container",
            className: "monaco-pane",
            containerPath: ImmutableArray<string>.Empty);

        Assert.NotNull(matched);
        Assert.Equal(DesktopSemanticZone.ChatConversation, matched.TargetZone);
        Assert.Equal(WindowPaneLocation.AuxiliarySidebar, matched.TargetPane);
        Assert.Equal("Chat", matched.TargetView);
        Assert.Equal("PromptHistory", matched.TargetSection);
    }
}
