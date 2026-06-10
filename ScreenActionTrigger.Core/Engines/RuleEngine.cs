using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Engines;

public sealed class RuleEngine : IRuleEngine
{
    private readonly IVisionEngine _vision;
    private readonly ILogger<RuleEngine> _logger;
    private readonly List<VisualRule> _rules = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event EventHandler<RuleTriggeredEventArgs>? RuleTriggered;
    public event EventHandler<DetectionEventArgs>? DetectionCompleted;

    public RuleEngine(IVisionEngine vision, ILogger<RuleEngine> logger)
    {
        _vision = vision;
        _logger = logger;
    }

    public void LoadRules(IEnumerable<VisualRule> rules)
    {
        _rules.Clear();
        _rules.AddRange(rules);
        _logger.LogInformation("Loaded {Count} rules", _rules.Count);
    }

    public void AddRule(VisualRule rule)
    {
        _rules.RemoveAll(r => r.Id == rule.Id);
        _rules.Add(rule);
    }

    public void RemoveRule(Guid ruleId) => _rules.RemoveAll(r => r.Id == ruleId);

    public void UpdateRule(VisualRule rule)
    {
        var idx = _rules.FindIndex(r => r.Id == rule.Id);
        if (idx >= 0) _rules[idx] = rule;
    }

    public async Task ProcessRegionAsync(MonitoredRegion region, byte[] frameData, CancellationToken ct = default)
    {
        if (!region.IsEnabled) return;

        var regionRules = _rules
            .Where(r => r.RegionId == region.Id && r.IsEnabled)
            .OrderByDescending(r => r.Priority)
            .ToList();

        foreach (var rule in regionRules)
        {
            if (ct.IsCancellationRequested) break;
            if (!rule.CanExecute) continue;

            try
            {
                var result = await EvaluateConditionAsync(frameData, region, rule.Condition, ct);
                result.RegionName = region.Name;

                DetectionCompleted?.Invoke(this, new DetectionEventArgs
                {
                    Detection = result,
                    Region = region,
                    MatchedRule = result.IsMatch ? rule : null
                });

                if (result.IsMatch)
                {
                    rule.RecordExecution();
                    _logger.LogDebug("Rule '{RuleName}' triggered in region '{RegionName}'",
                        rule.Name, region.Name);

                    RuleTriggered?.Invoke(this, new RuleTriggeredEventArgs
                    {
                        Rule = rule,
                        Detection = result
                    });
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing rule '{RuleName}'", rule.Name);
            }
        }
    }

    private async Task<DetectionResult> EvaluateConditionAsync(
        byte[] frameData, MonitoredRegion region, RuleCondition condition, CancellationToken ct)
    {
        if (condition.Type == ConditionType.Composite)
            return await EvaluateCompositeAsync(frameData, region, condition, ct);

        var result = await _vision.EvaluateAsync(frameData, region, condition, ct);

        return condition.IsNegated
            ? result with { IsMatch = !result.IsMatch }
            : result;
    }

    private async Task<DetectionResult> EvaluateCompositeAsync(
        byte[] frameData, MonitoredRegion region, RuleCondition composite, CancellationToken ct)
    {
        if (composite.SubConditions.Count == 0)
            return DetectionResult.NoMatch(region.Id, ConditionType.Composite);

        if (composite.Operator == LogicalOperator.And)
        {
            double minConfidence = 1.0;
            foreach (var sub in composite.SubConditions)
            {
                var r = await EvaluateConditionAsync(frameData, region, sub, ct);
                if (!r.IsMatch)
                    return composite.IsNegated
                        ? r with { IsMatch = true }
                        : DetectionResult.NoMatch(region.Id, ConditionType.Composite);
                minConfidence = Math.Min(minConfidence, r.Confidence);
            }
            var match = new DetectionResult
            {
                RegionId = region.Id,
                IsMatch = true,
                Confidence = minConfidence,
                DetectionType = ConditionType.Composite
            };
            return composite.IsNegated ? match with { IsMatch = false } : match;
        }
        else // OR
        {
            double maxConfidence = 0;
            foreach (var sub in composite.SubConditions)
            {
                var r = await EvaluateConditionAsync(frameData, region, sub, ct);
                if (r.IsMatch)
                {
                    maxConfidence = Math.Max(maxConfidence, r.Confidence);
                    var match = new DetectionResult
                    {
                        RegionId = region.Id,
                        IsMatch = true,
                        Confidence = maxConfidence,
                        DetectionType = ConditionType.Composite
                    };
                    return composite.IsNegated ? match with { IsMatch = false } : match;
                }
            }
            return composite.IsNegated
                ? new DetectionResult { RegionId = region.Id, IsMatch = true, Confidence = 1.0, DetectionType = ConditionType.Composite }
                : DetectionResult.NoMatch(region.Id, ConditionType.Composite);
        }
    }

    public void ResetRule(Guid ruleId)
    {
        _rules.FirstOrDefault(r => r.Id == ruleId)?.Reset();
    }

    public void ResetAll()
    {
        foreach (var rule in _rules) rule.Reset();
    }

    public IReadOnlyList<VisualRule> GetRules() => _rules.AsReadOnly();
}
