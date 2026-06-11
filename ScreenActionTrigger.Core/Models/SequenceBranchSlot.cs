namespace ScreenActionTrigger.Core.Models;

/// <summary>Ramo if/else após concluir um passo — direciona para outro passo da sequência.</summary>
public sealed class SequenceBranchSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "Se";
    /// <summary>Quando true, é o ramo padrão (senão) — não exige condição.</summary>
    public bool IsElse { get; set; }
    public Guid? TargetStepId { get; set; }
    public RuleCondition Condition { get; set; } = new();

    public SequenceBranchSlot Clone() => new()
    {
        Id = Guid.NewGuid(),
        Label = Label,
        IsElse = IsElse,
        TargetStepId = TargetStepId,
        Condition = Condition
    };
}

public enum SequenceAdvanceMode
{
    [System.ComponentModel.Description("Próximo passo")]
    Next,
    [System.ComponentModel.Description("Ramificação (if/else)")]
    Branch,
    [System.ComponentModel.Description("Reiniciar sequência")]
    Restart
}

public enum SlotCountMode
{
    [System.ComponentModel.Description("Pelo menos")]
    AtLeast,
    [System.ComponentModel.Description("Exatamente")]
    Exactly,
    [System.ComponentModel.Description("No máximo")]
    AtMost
}
