using System.Drawing;

namespace ScreenActionTrigger.Core.Models;

public sealed record DetectionResult
{
    public Guid RegionId { get; set; }
    public bool IsMatch { get; set; }
    public double Confidence { get; set; }
    public Point? MatchLocation { get; set; }
    public Size? MatchSize { get; set; }
    /// <summary>Região monitorada em coordenadas de tela (fallback do clique).</summary>
    public Rectangle? RegionBounds { get; set; }
    public ConditionType DetectionType { get; set; }
    public string? TemplateName { get; set; }
    public string? RegionName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static DetectionResult NoMatch(Guid regionId, ConditionType type) =>
        new() { RegionId = regionId, IsMatch = false, DetectionType = type };
}
