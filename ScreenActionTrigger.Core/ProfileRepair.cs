using System.Collections.ObjectModel;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core;

public static class ProfileRepair
{
    /// <summary>Reassocia regras cuja RegionId ficou inválida após recriar regiões.</summary>
    public static void RepairRuleRegionLinks(IEnumerable<VisualRule> rules, IList<MonitoredRegion> regions)
    {
        if (regions.Count == 0) return;

        var byId = regions.ToDictionary(r => r.Id);
        foreach (var rule in rules)
        {
            if (byId.ContainsKey(rule.RegionId))
                continue;

            var match = regions.FirstOrDefault(r =>
                            rule.Name.Contains(r.Name, StringComparison.OrdinalIgnoreCase))
                        ?? regions.FirstOrDefault();

            if (match is not null)
                rule.RegionId = match.Id;
        }
    }

    public static void EnsureRuleCollections(VisualRule rule)
    {
        if (rule.Condition.TargetColors is not ObservableCollection<string>)
        {
            rule.Condition.TargetColors = new ObservableCollection<string>(
                rule.Condition.TargetColors ?? []);
        }

        if (rule.Actions is not ObservableCollection<TriggerAction>)
        {
            rule.Actions = new ObservableCollection<TriggerAction>(
                rule.Actions ?? []);
        }

        RepairColorDetectionDefaults(rule.Condition);
    }

    public static void EnsureSequenceCollections(RuleSequence sequence)
    {
        foreach (var step in sequence.Steps)
        {
            if (step.Condition.TargetColors is not ObservableCollection<string>)
            {
                step.Condition.TargetColors = new ObservableCollection<string>(
                    step.Condition.TargetColors ?? []);
            }

            if (step.Actions is not ObservableCollection<TriggerAction>)
            {
                step.Actions = new ObservableCollection<TriggerAction>(
                    step.Actions ?? []);
            }

            RepairColorDetectionDefaults(step.Condition);
        }
    }

    public static void RepairSequenceRegionLinks(IEnumerable<RuleSequence> sequences, IList<MonitoredRegion> regions)
    {
        if (regions.Count == 0) return;

        var byId = regions.ToDictionary(r => r.Id);
        foreach (var step in sequences.SelectMany(s => s.Steps))
        {
            if (byId.ContainsKey(step.RegionId))
                continue;

            var match = regions.FirstOrDefault();
            if (match is not null)
                step.RegionId = match.Id;
        }
    }

    /// <summary>Ajusta regras antigas que exigiam 30% e não detectavam ícones pequenos.</summary>
    public static void RepairColorDetectionDefaults(RuleCondition condition)
    {
        if (condition.Type != ConditionType.ColorDetection)
            return;

        if (condition.MinMatchingPixels <= 0 && condition.MinColorPercentage >= 0.15)
        {
            condition.MinMatchingPixels  = 8;
            condition.MinColorPercentage = 0.03;
        }

        if (condition.ColorTolerance < 20)
            condition.ColorTolerance = 28;

        if (condition.DarkPixelThreshold <= 0)
            condition.DarkPixelThreshold = 35;
    }
}
