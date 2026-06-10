using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface IActionDispatcher
{
    event EventHandler<ActionExecutedEventArgs>? ActionExecuted;

    Task ExecuteAsync(TriggerAction action, DetectionResult? context = null, CancellationToken ct = default);
    Task EnqueueAsync(TriggerAction action, int priority = 0, DetectionResult? context = null);
    Task EnqueueBatchAsync(IEnumerable<TriggerAction> actions, int priority = 0, DetectionResult? context = null);
    void CancelAll();
    void ReleaseAllInputs();
    int QueueLength { get; }
}

public sealed class ActionExecutedEventArgs : EventArgs
{
    public required TriggerAction Action { get; init; }
    public bool Success { get; init; }
    public Exception? Error { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime ExecutedAt { get; init; } = DateTime.Now;
}
