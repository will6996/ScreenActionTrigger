using System.Collections.ObjectModel;

namespace ScreenActionTrigger.Core.Models;

/// <summary>Automação com passos executados em ordem: detectar → agir → próximo passo.</summary>
public sealed class RuleSequence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nova Sequência";
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    /// <summary>Reinicia do primeiro passo ao concluir todos.</summary>
    public bool Loop { get; set; } = true;
    /// <summary>Pausa mínima entre passos (ms).</summary>
    public int DelayBetweenStepsMs { get; set; } = 300;
    public List<SequenceStep> Steps { get; set; } = new();

    public IEnumerable<SequenceStep> OrderedSteps =>
        Steps.OrderBy(s => s.Order);

    public RuleSequence Clone() => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{Name} (cópia)",
        Description = Description,
        IsEnabled = IsEnabled,
        Loop = Loop,
        DelayBetweenStepsMs = DelayBetweenStepsMs,
        Steps = Steps.Select(s => s.Clone()).ToList()
    };
}

public sealed class SequenceStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Passo";
    public int Order { get; set; }
    /// <summary>Região onde a condição é avaliada (ex.: canto da tela).</summary>
    public Guid RegionId { get; set; }
    public RuleCondition Condition { get; set; } = new();
    public ObservableCollection<TriggerAction> Actions { get; set; } = new();
    /// <summary>Espera após executar as ações antes de avançar (ms).</summary>
    public int DelayAfterMs { get; set; } = 200;
    /// <summary>Como avançar após concluir este passo.</summary>
    public SequenceAdvanceMode AdvanceMode { get; set; } = SequenceAdvanceMode.Next;
    /// <summary>Ramos if/else — usados quando AdvanceMode = Branch.</summary>
    public List<SequenceBranchSlot> BranchSlots { get; set; } = new();

    public SequenceStep Clone() => new()
    {
        Id = Guid.NewGuid(),
        Name = Name,
        Order = Order,
        RegionId = RegionId,
        Condition = Condition,
        Actions = new ObservableCollection<TriggerAction>(Actions),
        DelayAfterMs = DelayAfterMs,
        AdvanceMode = AdvanceMode,
        BranchSlots = BranchSlots.Select(b => b.Clone()).ToList()
    };
}
