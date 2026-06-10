namespace ScreenActionTrigger.Core.Models;

public sealed class MonitoringEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string RegionName { get; set; } = string.Empty;
    public string? RuleName { get; set; }
    public string? ActionName { get; set; }
    public double Confidence { get; set; }
    public string DetectionType { get; set; } = string.Empty;
    public bool WasExecuted { get; set; }
    public string? ErrorMessage { get; set; }
}
