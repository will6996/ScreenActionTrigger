namespace ScreenActionTrigger.Core.Models;

public sealed class ExecutionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Novo Perfil";
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<MonitoredRegion> Regions { get; set; } = new();
    public List<Template> Templates { get; set; } = new();
    public List<VisualRule> Rules { get; set; } = new();
    public List<RuleSequence> Sequences { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}

public sealed class AppSettings
{
    public bool OverlayEnabled { get; set; } = true;
    public int CaptureIntervalMs { get; set; } = 100;
    public int MaxParallelRegions { get; set; } = 10;
    public bool LogDetections { get; set; } = true;
    public bool LogActions { get; set; } = true;
    public bool GrayscaleProcessing { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public string HotkeyStartStop { get; set; } = "F9";
    public string HotkeyPause { get; set; } = "F10";
    public int MaxMonitoringEntries { get; set; } = 500;
}
