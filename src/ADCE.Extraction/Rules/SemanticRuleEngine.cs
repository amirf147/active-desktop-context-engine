// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using ADCE.Core.Interfaces;
using ADCE.Core.Models;

namespace ADCE.Extraction.Rules;

/// <summary>
/// Thread-safe implementation of ISemanticRuleEngine persisting rules to JSON.
/// </summary>
public sealed class SemanticRuleEngine : ISemanticRuleEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _rulesFilePath;
    private readonly object _lock = new();
    private ImmutableArray<SemanticRule> _rules = ImmutableArray<SemanticRule>.Empty;

    /// <summary>
    /// Initializes a new instance of the SemanticRuleEngine.
    /// </summary>
    /// <param name="rulesFilePath">Optional custom path to semantic_rules.json. Defaults to %LOCALAPPDATA%\ADCE\semantic_rules.json.</param>
    public SemanticRuleEngine(string? rulesFilePath = null)
    {
        _rulesFilePath = rulesFilePath ?? DefaultRulesFilePath;
        LoadRules();
    }

    public static string DefaultRulesFilePath
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ADCE", "semantic_rules.json");
        }
    }

    /// <inheritdoc />
    public DesktopSemanticZone? MatchRule(
        string processName,
        string controlType,
        string elementName,
        string automationId,
        string className,
        ImmutableArray<string> containerPath)
    {
        return FindMatchingRule(processName, controlType, elementName, automationId, className, containerPath)?.TargetZone;
    }

    /// <inheritdoc />
    public SemanticRule? FindMatchingRule(
        string processName,
        string controlType,
        string elementName,
        string automationId,
        string className,
        ImmutableArray<string> containerPath)
    {
        var rules = _rules;
        for (int i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule.Matches(processName, controlType, elementName, automationId, className, containerPath))
            {
                return rule;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void AddOrUpdateRule(SemanticRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        lock (_lock)
        {
            var builder = _rules.Where(r => !string.Equals(r.RuleId, rule.RuleId, StringComparison.OrdinalIgnoreCase)).ToList();
            builder.Add(rule);

            _rules = builder
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.CreatedAtUtc)
                .ToImmutableArray();
        }

        try
        {
            SaveRulesSync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SemanticRuleEngine] Warning: Failed to persist rules to '{_rulesFilePath}': {ex.Message}");
        }
    }

    /// <inheritdoc />
    public bool RemoveRule(string ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId)) return false;

        bool removed = false;
        lock (_lock)
        {
            int before = _rules.Length;
            _rules = _rules.Where(r => !string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase)).ToImmutableArray();
            removed = _rules.Length < before;
        }

        if (removed)
        {
            try
            {
                SaveRulesSync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SemanticRuleEngine] Warning: Failed to persist rules after deletion: {ex.Message}");
            }
        }

        return removed;
    }

    /// <inheritdoc />
    public IReadOnlyList<SemanticRule> GetAllRules()
    {
        return _rules;
    }

    /// <inheritdoc />
    public void LoadRules()
    {
        lock (_lock)
        {
            if (!File.Exists(_rulesFilePath))
            {
                _rules = ImmutableArray<SemanticRule>.Empty;
                return;
            }

            try
            {
                string json = File.ReadAllText(_rulesFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _rules = ImmutableArray<SemanticRule>.Empty;
                    return;
                }

                var loaded = JsonSerializer.Deserialize<List<SemanticRule>>(json, JsonOptions);
                if (loaded != null)
                {
                    _rules = loaded
                        .OrderByDescending(r => r.Priority)
                        .ThenByDescending(r => r.CreatedAtUtc)
                        .ToImmutableArray();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SemanticRuleEngine] Error loading rules from '{_rulesFilePath}': {ex.Message}");
                _rules = ImmutableArray<SemanticRule>.Empty;
            }
        }
    }

    /// <inheritdoc />
    public async Task SaveRulesAsync(CancellationToken cancellationToken = default)
    {
        string dir = Path.GetDirectoryName(_rulesFilePath)!;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        List<SemanticRule> snapshot;
        lock (_lock)
        {
            snapshot = _rules.ToList();
        }

        string tempPath = $"{_rulesFilePath}.tmp.{Guid.NewGuid():N}";
        await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(fs, snapshot, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, _rulesFilePath, overwrite: true);
    }

    private void SaveRulesSync()
    {
        string dir = Path.GetDirectoryName(_rulesFilePath)!;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        List<SemanticRule> snapshot;
        lock (_lock)
        {
            snapshot = _rules.ToList();
        }

        string tempPath = $"{_rulesFilePath}.tmp.{Guid.NewGuid():N}";
        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(fs, snapshot, JsonOptions);
        }

        File.Move(tempPath, _rulesFilePath, overwrite: true);
    }
}
