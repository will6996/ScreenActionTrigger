using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface IRuleEngine
{
    event EventHandler<RuleTriggeredEventArgs>? RuleTriggered;
    event EventHandler<DetectionEventArgs>? DetectionCompleted;

    void LoadRules(IEnumerable<VisualRule> rules);
    void AddRule(VisualRule rule);
    void RemoveRule(Guid ruleId);
    void UpdateRule(VisualRule rule);

    Task ProcessRegionAsync(
        MonitoredRegion region,
        byte[] frameData,
        CancellationToken ct = default);

    void ResetRule(Guid ruleId);
    void ResetAll();
    IReadOnlyList<VisualRule> GetRules();
}

public sealed class RuleTriggeredEventArgs : EventArgs
{
    public required VisualRule Rule { get; init; }
    public required DetectionResult Detection { get; init; }
    public DateTime TriggeredAt { get; init; } = DateTime.Now;
}

public sealed class DetectionEventArgs : EventArgs
{
    public required DetectionResult Detection { get; init; }
    public required MonitoredRegion Region { get; init; }
    public VisualRule? EvaluatedRule { get; init; }
    public VisualRule? MatchedRule { get; init; }
}
