using System.Collections.ObjectModel;

namespace ScreenActionTrigger.Core.Models;

public sealed class VisualRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nova Regra";
    public Guid RegionId { get; set; }
    public RuleCondition Condition { get; set; } = new();
    public ObservableCollection<TriggerAction> Actions { get; set; } = new();
    public int Priority { get; set; } = 0;
    public int CooldownMs { get; set; } = 500;
    public bool IsEnabled { get; set; } = true;
    public int MaxExecutions { get; set; } = -1;  // -1 = unlimited
    public int ExecutionCount { get; set; } = 0;
    public DateTime LastExecuted { get; set; } = DateTime.MinValue;

    public bool IsOnCooldown =>
        LastExecuted != DateTime.MinValue &&
        (DateTime.UtcNow - LastExecuted).TotalMilliseconds < CooldownMs;

    public bool HasReachedMaxExecutions =>
        MaxExecutions > 0 && ExecutionCount >= MaxExecutions;

    public bool CanExecute => IsEnabled && !IsOnCooldown && !HasReachedMaxExecutions;

    public void RecordExecution()
    {
        LastExecuted = DateTime.UtcNow;
        ExecutionCount++;
    }

    public void Reset()
    {
        ExecutionCount = 0;
        LastExecuted = DateTime.MinValue;
    }

    public VisualRule Clone() => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{Name} (cópia)",
        RegionId = RegionId,
        Condition = Condition,
        Actions = new ObservableCollection<TriggerAction>(Actions),
        Priority = Priority,
        CooldownMs = CooldownMs,
        IsEnabled = IsEnabled,
        MaxExecutions = MaxExecutions
    };
}
