using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface ISequenceEngine
{
    event EventHandler<SequenceStepTriggeredEventArgs>? StepTriggered;

    void LoadSequences(IEnumerable<RuleSequence> sequences);
    void ResetAll();
    void ResetSequence(Guid sequenceId);

    Task ProcessRegionAsync(
        MonitoredRegion region,
        byte[] frameData,
        CancellationToken ct = default);

    /// <summary>Chamado após as ações do passo atual terminarem.</summary>
    void CompleteStep(Guid sequenceId);

    IReadOnlyList<RuleSequence> GetSequences();
    int? GetCurrentStepIndex(Guid sequenceId);

    /// <summary>Contexto de runtime para avaliar inventário e ramificações multi-região.</summary>
    void SetRuntimeContext(
        IReadOnlyList<MonitoredRegion> regions,
        Func<MonitoredRegion, CancellationToken, Task<byte[]?>> captureRegion);
}

public sealed class SequenceStepTriggeredEventArgs : EventArgs
{
    public required RuleSequence Sequence { get; init; }
    public required SequenceStep Step { get; init; }
    public required DetectionResult Detection { get; init; }
    public int StepIndex { get; init; }
    public DateTime TriggeredAt { get; init; } = DateTime.UtcNow;
}
