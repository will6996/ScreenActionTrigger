namespace ScreenActionTrigger.Core.Models;

public sealed class ActionQueueItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TriggerAction Action { get; set; } = null!;
    public DetectionResult? Context { get; set; }
    public int Priority { get; set; } = 0;
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public bool IsCancelled { get; set; } = false;
}
