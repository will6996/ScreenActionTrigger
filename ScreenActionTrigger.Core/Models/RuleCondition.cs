using System.ComponentModel;

namespace ScreenActionTrigger.Core.Models;

public sealed class RuleCondition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ConditionType Type { get; set; }

    // Color detection
    public string TargetColor { get; set; } = "#0000FF";
    /// <summary>Cores adicionais alvo (qualquer uma satisfaz a condição).</summary>
    public List<string> TargetColors { get; set; } = new();
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

    public IEnumerable<string> GetAllTargetColors()
    {
        var colors = new List<string>();
        if (!string.IsNullOrWhiteSpace(TargetColor))
            colors.Add(TargetColor);
        colors.AddRange(TargetColors.Where(c => !string.IsNullOrWhiteSpace(c)));
        return colors.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public string GetDescription() => Type switch
    {
        ConditionType.ColorDetection   => $"Cor {string.Join("/", GetAllTargetColors())} > {MinColorPercentage:P0}",
        ConditionType.ChangeDetection  => $"Mudança > {MinChangePercentage:P0}",
        ConditionType.TemplateMatching => $"Template {TemplateId}",
        ConditionType.Composite        => $"{Operator} ({SubConditions.Count} condições)",
        _ => Type.ToString()
    };
}

public enum ConditionType
{
    [Description("Detecção por Cor")]
    ColorDetection,
    [Description("Detecção por Mudança")]
    ChangeDetection,
    [Description("Template (imagem)")]
    TemplateMatching,
    [Description("Condição Composta")]
    Composite
}

public enum LogicalOperator { None, And, Or }
