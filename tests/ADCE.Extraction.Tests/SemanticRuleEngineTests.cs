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

public class SemanticRuleEngineTests : IDisposable
{
    private readonly string _testRulesPath;

    public SemanticRuleEngineTests()
    {
        _testRulesPath = Path.Combine(Path.GetTempPath(), $"adce_rules_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testRulesPath))
        {
            try { File.Delete(_testRulesPath); } catch { }
        }
    }

    [Fact]
    public void MatchRule_MatchesByProcessAndAutomationId()
    {
        var engine = new SemanticRuleEngine(_testRulesPath);
        engine.AddOrUpdateRule(new SemanticRule
        {
            RuleId = "r1",
            TargetZone = DesktopSemanticZone.GitCommitBox,
            ProcessPattern = "antigravity",
            AutomationIdPattern = "scm.input",
            Priority = 60
        });

        var matched = engine.MatchRule(
            processName: "Antigravity IDE",
            controlType: "Edit",
            elementName: "Message (Ctrl+Enter to commit)",
            automationId: "workbench.parts.scm.input",
            className: "monaco-editor",
            containerPath: ImmutableArray<string>.Empty);

        Assert.Equal(DesktopSemanticZone.GitCommitBox, matched);
    }

    [Fact]
    public void MatchRule_HigherPriorityTakesPrecedence()
    {
        var engine = new SemanticRuleEngine(_testRulesPath);

        // Low priority general rule
        engine.AddOrUpdateRule(new SemanticRule
        {
            RuleId = "r_general",
            TargetZone = DesktopSemanticZone.EditorBuffer,
            ProcessPattern = "code",
            ControlType = "Edit",
            Priority = 10
        });

        // High priority specific override
        engine.AddOrUpdateRule(new SemanticRule
        {
            RuleId = "r_specific",
            TargetZone = DesktopSemanticZone.GitCommitBox,
            ProcessPattern = "code",
            AutomationIdPattern = "scm.input",
            Priority = 100,
            IsUserOverride = true
        });

        var matched = engine.MatchRule(
            processName: "Code.exe",
            controlType: "Edit",
            elementName: "",
            automationId: "scm.input",
            className: "",
            containerPath: ImmutableArray<string>.Empty);

        Assert.Equal(DesktopSemanticZone.GitCommitBox, matched);
    }

    [Fact]
    public void MatchRule_MatchesContainerPath()
    {
        var engine = new SemanticRuleEngine(_testRulesPath);
        engine.AddOrUpdateRule(new SemanticRule
        {
            RuleId = "r_explorer",
            TargetZone = DesktopSemanticZone.SidebarExplorer,
            ProcessPattern = "antigravity",
            ContainerPattern = "workbench.view.explorer",
            Priority = 50
        });

        var containers = ImmutableArray.Create("split-view", "workbench.view.explorer.tree", "workbench.view.explorer");
        var matched = engine.MatchRule(
            processName: "Antigravity.exe",
            controlType: "TreeItem",
            elementName: "Program.cs",
            automationId: "file-item",
            className: "monaco-list-row",
            containerPath: containers);

        Assert.Equal(DesktopSemanticZone.SidebarExplorer, matched);
    }

    [Fact]
    public void Persistence_LoadsSavedRulesAcrossInstances()
    {
        var engine1 = new SemanticRuleEngine(_testRulesPath);
        engine1.AddOrUpdateRule(new SemanticRule
        {
            RuleId = "custom_rule_1",
            TargetZone = DesktopSemanticZone.Terminal,
            ProcessPattern = "wezterm",
            Priority = 80,
            IsUserOverride = true
        });

        Assert.True(File.Exists(_testRulesPath));

        var engine2 = new SemanticRuleEngine(_testRulesPath);
        var rules = engine2.GetAllRules();

        Assert.Single(rules);
        Assert.Equal("custom_rule_1", rules[0].RuleId);
        Assert.Equal(DesktopSemanticZone.Terminal, rules[0].TargetZone);
        Assert.Equal("wezterm", rules[0].ProcessPattern);
        Assert.True(rules[0].IsUserOverride);
    }
}
