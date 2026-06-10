namespace ScreenActionTrigger.Core.Models;

public sealed class RuleCondition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ConditionType Type { get; set; }

    // Color detection
    public string TargetColor { get; set; } = "#0000FF";
    public int ColorTolerance { get; set; } = 15;
    public double MinColorPercentage { get; set; } = 0.30;
    public bool UseDominantColor { get; set; } = false;

    // Change detection
    public double MinChangePercentage { get; set; } = 0.20;
    public double ChangeSensitivity { get; set; } = 0.50;

    // Template matching
    public Guid? TemplateId { get; set; }

    // Composite (AND / OR / NOT)
    public LogicalOperator Operator { get; set; } = LogicalOperator.None;
    public List<RuleCondition> SubConditions { get; set; } = new();
    public bool IsNegated { get; set; } = false;

    public string GetDescription() => Type switch
    {
        ConditionType.ColorDetection   => $"Cor {TargetColor} > {MinColorPercentage:P0}",
        ConditionType.ChangeDetection  => $"Mudança > {MinChangePercentage:P0}",
        ConditionType.TemplateMatching => $"Template {TemplateId}",
        ConditionType.Composite        => $"{Operator} ({SubConditions.Count} condições)",
        _ => Type.ToString()
    };
}

public enum ConditionType
{
    ColorDetection,
    ChangeDetection,
    TemplateMatching,
    Composite
}

public enum LogicalOperator { None, And, Or }
