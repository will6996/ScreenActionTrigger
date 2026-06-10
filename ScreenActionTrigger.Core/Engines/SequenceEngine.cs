using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Engines;

public sealed class SequenceEngine : ISequenceEngine
{
    private readonly IVisionEngine _vision;
    private readonly ILogger<SequenceEngine> _logger;
    private readonly List<RuleSequence> _sequences = new();
    private readonly Dictionary<Guid, SequenceState> _states = new();

    public event EventHandler<SequenceStepTriggeredEventArgs>? StepTriggered;

    public SequenceEngine(IVisionEngine vision, ILogger<SequenceEngine> logger)
    {
        _vision = vision;
        _logger = logger;
    }

    public void LoadSequences(IEnumerable<RuleSequence> sequences)
    {
        _sequences.Clear();
        _states.Clear();
        _sequences.AddRange(sequences.Where(s => s.IsEnabled && s.Steps.Count > 0));
        foreach (var seq in _sequences)
            _states[seq.Id] = new SequenceState();
        _logger.LogInformation("Loaded {Count} sequences", _sequences.Count);
    }

    public void ResetAll()
    {
        foreach (var state in _states.Values)
            state.Reset();
    }

    public void ResetSequence(Guid sequenceId)
    {
        if (_states.TryGetValue(sequenceId, out var state))
            state.Reset();
    }

    public int? GetCurrentStepIndex(Guid sequenceId) =>
        _states.TryGetValue(sequenceId, out var state) ? state.CurrentStepIndex : null;

    public async Task ProcessRegionAsync(
        MonitoredRegion region,
        byte[] frameData,
        CancellationToken ct = default)
    {
        foreach (var sequence in _sequences)
        {
            if (ct.IsCancellationRequested) break;
            if (!_states.TryGetValue(sequence.Id, out var state)) continue;
            if (state.IsCompleted || state.AwaitingCompletion) continue;

            var steps = sequence.OrderedSteps.ToList();
            if (steps.Count == 0) continue;

            if (state.LastStepCompletedAt is not null)
            {
                var elapsed = (DateTime.UtcNow - state.LastStepCompletedAt.Value).TotalMilliseconds;
                if (elapsed < sequence.DelayBetweenStepsMs)
                    continue;
            }

            if (state.CurrentStepIndex >= steps.Count)
            {
                if (sequence.Loop)
                    state.CurrentStepIndex = 0;
                else
                {
                    state.IsCompleted = true;
                    continue;
                }
            }

            var step = steps[state.CurrentStepIndex];
            if (step.RegionId != region.Id) continue;

            try
            {
                var result = await EvaluateConditionAsync(frameData, region, step.Condition, ct);
                result.RegionName   = region.Name;
                result.RegionBounds = region.Bounds;

                if (!result.IsMatch) continue;

                state.AwaitingCompletion = true;
                _logger.LogDebug("Sequence '{Name}' step {Index} matched in '{Region}'",
                    sequence.Name, state.CurrentStepIndex + 1, region.Name);

                StepTriggered?.Invoke(this, new SequenceStepTriggeredEventArgs
                {
                    Sequence   = sequence,
                    Step       = step,
                    Detection  = result,
                    StepIndex  = state.CurrentStepIndex
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing sequence '{Name}' step {Index}",
                    sequence.Name, state.CurrentStepIndex);
            }
        }
    }

    public void CompleteStep(Guid sequenceId)
    {
        if (!_states.TryGetValue(sequenceId, out var state)) return;

        state.AwaitingCompletion = false;
        state.CurrentStepIndex++;
        state.LastStepCompletedAt = DateTime.UtcNow;

        var sequence = _sequences.FirstOrDefault(s => s.Id == sequenceId);
        if (sequence is null) return;

        var stepCount = sequence.OrderedSteps.Count();
        if (state.CurrentStepIndex >= stepCount && !sequence.Loop)
            state.IsCompleted = true;

        _logger.LogDebug("Sequence '{Name}' advanced to step {Index}",
            sequence.Name, state.CurrentStepIndex + 1);
    }

    public IReadOnlyList<RuleSequence> GetSequences() => _sequences.AsReadOnly();

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

        foreach (var sub in composite.SubConditions)
        {
            var r = await EvaluateConditionAsync(frameData, region, sub, ct);
            if (r.IsMatch)
            {
                var match = new DetectionResult
                {
                    RegionId = region.Id,
                    IsMatch = true,
                    Confidence = r.Confidence,
                    DetectionType = ConditionType.Composite
                };
                return composite.IsNegated ? match with { IsMatch = false } : match;
            }
        }

        return composite.IsNegated
            ? new DetectionResult { RegionId = region.Id, IsMatch = true, Confidence = 1.0, DetectionType = ConditionType.Composite }
            : DetectionResult.NoMatch(region.Id, ConditionType.Composite);
    }

    private sealed class SequenceState
    {
        public int CurrentStepIndex { get; set; }
        public bool AwaitingCompletion { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? LastStepCompletedAt { get; set; }

        public void Reset()
        {
            CurrentStepIndex = 0;
            AwaitingCompletion = false;
            IsCompleted = false;
            LastStepCompletedAt = null;
        }
    }
}
