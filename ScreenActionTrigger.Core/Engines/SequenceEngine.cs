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
    private IReadOnlyList<MonitoredRegion> _regions = Array.Empty<MonitoredRegion>();
    private Func<MonitoredRegion, CancellationToken, Task<byte[]?>>? _captureRegion;

    public event EventHandler<SequenceStepTriggeredEventArgs>? StepTriggered;

    public SequenceEngine(IVisionEngine vision, ILogger<SequenceEngine> logger)
    {
        _vision = vision;
        _logger = logger;
    }

    public void SetRuntimeContext(
        IReadOnlyList<MonitoredRegion> regions,
        Func<MonitoredRegion, CancellationToken, Task<byte[]?>> captureRegion)
    {
        _regions = regions;
        _captureRegion = captureRegion;
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
            if (!StepWatchesRegion(step, region.Id)) continue;

            try
            {
                var result = await EvaluateStepConditionAsync(step, region, frameData, ct);
                result.RegionName   = region.Name;
                result.RegionBounds = region.Bounds;

                if (!result.IsMatch) continue;

                state.AwaitingCompletion = true;
                state.LastTriggeredStep  = step;
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
        state.LastStepCompletedAt = DateTime.UtcNow;

        var sequence = _sequences.FirstOrDefault(s => s.Id == sequenceId);
        if (sequence is null) return;

        var completedStep = state.LastTriggeredStep;
        state.LastTriggeredStep = null;

        var steps = sequence.OrderedSteps.ToList();
        AdvanceAfterStep(state, sequence, steps, completedStep);

        if (state.CurrentStepIndex >= steps.Count && !sequence.Loop)
            state.IsCompleted = true;

        _logger.LogDebug("Sequence '{Name}' advanced to step {Index}",
            sequence.Name, state.CurrentStepIndex + 1);
    }

    public IReadOnlyList<RuleSequence> GetSequences() => _sequences.AsReadOnly();

    private static bool StepWatchesRegion(SequenceStep step, Guid regionId)
    {
        if (step.Condition.Type == ConditionType.InventorySlotCount)
        {
            var slots = step.Condition.InventorySlotRegionIds;
            return slots.Count == 0 || slots.Contains(regionId);
        }

        return step.RegionId == regionId;
    }

    private void AdvanceAfterStep(
        SequenceState state,
        RuleSequence sequence,
        List<SequenceStep> steps,
        SequenceStep? completedStep)
    {
        if (completedStep is null)
        {
            state.CurrentStepIndex++;
            return;
        }

        switch (completedStep.AdvanceMode)
        {
            case SequenceAdvanceMode.Restart:
                state.CurrentStepIndex = 0;
                return;

            case SequenceAdvanceMode.Branch:
                var targetId = ResolveBranchTarget(completedStep, steps);
                if (targetId is not null)
                {
                    var idx = steps.FindIndex(s => s.Id == targetId.Value);
                    if (idx >= 0)
                    {
                        state.CurrentStepIndex = idx;
                        return;
                    }
                }
                state.CurrentStepIndex++;
                return;

            default:
                state.CurrentStepIndex++;
                return;
        }
    }

    private Guid? ResolveBranchTarget(SequenceStep step, List<SequenceStep> steps)
    {
        SequenceBranchSlot? elseSlot = null;

        foreach (var slot in step.BranchSlots)
        {
            if (slot.IsElse)
            {
                elseSlot = slot;
                continue;
            }

            if (slot.TargetStepId is null) continue;

            if (EvaluateBranchSlotSync(step, slot))
                return slot.TargetStepId;
        }

        return elseSlot?.TargetStepId;
    }

    private bool EvaluateBranchSlotSync(SequenceStep parentStep, SequenceBranchSlot slot)
    {
        try
        {
            return EvaluateBranchSlotAsync(parentStep, slot, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Branch slot '{Label}' evaluation failed", slot.Label);
            return false;
        }
    }

    private async Task<bool> EvaluateBranchSlotAsync(
        SequenceStep parentStep, SequenceBranchSlot slot, CancellationToken ct)
    {
        if (slot.IsElse) return false;

        var condition = slot.Condition;
        if (condition.Type == ConditionType.InventorySlotCount)
        {
            var result = await EvaluateInventorySlotCountAsync(condition, ct);
            return result.IsMatch;
        }

        var region = _regions.FirstOrDefault(r => r.Id == parentStep.RegionId);
        if (region is null || _captureRegion is null) return false;

        var frame = await _captureRegion(region, ct);
        if (frame is null) return false;

        var detection = await EvaluateConditionAsync(frame, region, condition, ct);
        return detection.IsMatch;
    }

    private async Task<DetectionResult> EvaluateStepConditionAsync(
        SequenceStep step,
        MonitoredRegion region,
        byte[] frameData,
        CancellationToken ct)
    {
        if (step.Condition.Type == ConditionType.InventorySlotCount)
            return await EvaluateInventorySlotCountAsync(step.Condition, ct);

        return await EvaluateConditionAsync(frameData, region, step.Condition, ct);
    }

    private async Task<DetectionResult> EvaluateInventorySlotCountAsync(
        RuleCondition condition,
        CancellationToken ct)
    {
        if (_captureRegion is null || condition.InventorySlotRegionIds.Count == 0)
            return DetectionResult.NoMatch(Guid.Empty, ConditionType.InventorySlotCount);

        var colorCondition = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            TargetColor = condition.TargetColor,
            TargetColors = condition.TargetColors,
            ColorTolerance = condition.ColorTolerance,
            MinColorPercentage = condition.MinColorPercentage,
            MinMatchingPixels = condition.MinMatchingPixels,
            ExcludeDarkPixels = condition.ExcludeDarkPixels,
            DarkPixelThreshold = condition.DarkPixelThreshold
        };

        var occupied = 0;
        Guid firstRegionId = condition.InventorySlotRegionIds[0];

        foreach (var slotId in condition.InventorySlotRegionIds)
        {
            var slotRegion = _regions.FirstOrDefault(r => r.Id == slotId);
            if (slotRegion is null) continue;

            var frame = await _captureRegion(slotRegion, ct);
            if (frame is null) continue;

            var result = await _vision.EvaluateAsync(frame, slotRegion, colorCondition, ct);
            if (result.IsMatch)
                occupied++;
        }

        var required = condition.RequiredSlotCount;
        var isMatch = condition.SlotCountMode switch
        {
            SlotCountMode.Exactly  => occupied == required,
            SlotCountMode.AtMost   => occupied <= required,
            _                      => occupied >= required
        };

        return new DetectionResult
        {
            RegionId = firstRegionId,
            IsMatch = isMatch,
            Confidence = condition.InventorySlotRegionIds.Count > 0
                ? (double)occupied / condition.InventorySlotRegionIds.Count
                : 0,
            MatchPixelCount = occupied,
            DetectionType = ConditionType.InventorySlotCount
        };
    }

    private async Task<DetectionResult> EvaluateConditionAsync(
        byte[] frameData, MonitoredRegion region, RuleCondition condition, CancellationToken ct)
    {
        if (condition.Type == ConditionType.Composite)
            return await EvaluateCompositeAsync(frameData, region, condition, ct);

        if (condition.Type == ConditionType.InventorySlotCount)
            return await EvaluateInventorySlotCountAsync(condition, ct);

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
        public SequenceStep? LastTriggeredStep { get; set; }

        public void Reset()
        {
            CurrentStepIndex = 0;
            AwaitingCompletion = false;
            IsCompleted = false;
            LastStepCompletedAt = null;
            LastTriggeredStep = null;
        }
    }
}
