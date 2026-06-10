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
    }
}
