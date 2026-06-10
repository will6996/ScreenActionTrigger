namespace ScreenActionTrigger.Core.Models;

public sealed class Template
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Novo Template";
    public string Category { get; set; } = "Personalizados";
    public string ImagePath { get; set; } = string.Empty;
    public double MinConfidence { get; set; } = 0.95;
    public int Priority { get; set; } = 0;
    public int CooldownMs { get; set; } = 500;
    public MatchingMethod Method { get; set; } = MatchingMethod.CcoeffNormed;
    public bool UseAutoScale { get; set; } = false;
    public double FixedScale { get; set; } = 1.0;
    public double MinScale { get; set; } = 0.8;
    public double MaxScale { get; set; } = 1.2;
    public bool AllowRotation { get; set; } = false;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Template Clone() => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{Name} (cópia)",
        Category = Category,
        ImagePath = ImagePath,
        MinConfidence = MinConfidence,
        Priority = Priority,
        CooldownMs = CooldownMs,
        Method = Method,
        UseAutoScale = UseAutoScale,
        FixedScale = FixedScale
    };
}

public enum MatchingMethod
{
    CcoeffNormed,
    CcorrNormed,
    SqdiffNormed
}
