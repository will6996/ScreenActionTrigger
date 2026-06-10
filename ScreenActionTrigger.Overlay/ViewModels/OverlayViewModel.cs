using System.Drawing;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Overlay.ViewModels;

public sealed partial class OverlayViewModel : ObservableObject
{
    [ObservableProperty] private bool _isVisible = true;

    public ObservableCollection<RegionOverlayItem> Regions { get; } = new();
    public ObservableCollection<ClickTargetOverlayItem> ClickTargets { get; } = new();
    public ObservableCollection<DetectionOverlayItem> Detections { get; } = new();

    public void UpdateRegions(IEnumerable<MonitoredRegion> regions)
    {
        Regions.Clear();
        ClickTargets.Clear();
        foreach (var r in regions.Where(r => r.IsEnabled))
            Regions.Add(new RegionOverlayItem(r));
    }

    public void UpdateRegionsPreview(IEnumerable<MonitoredRegion> regions)
    {
        Regions.Clear();
        foreach (var r in regions)
            Regions.Add(new RegionOverlayItem(r));
    }

    public void UpdateClickTargets(IEnumerable<ClickTargetMarker> targets)
    {
        ClickTargets.Clear();
        foreach (var t in targets)
            ClickTargets.Add(new ClickTargetOverlayItem(t));
    }

    public void AddDetection(DetectionResult result, MonitoredRegion region, VisualRule? rule = null)
    {
        var item = new DetectionOverlayItem
        {
            RegionId   = result.RegionId,
            RegionName = region.Name,
            X          = result.MatchLocation?.X ?? region.X,
            Y          = result.MatchLocation?.Y ?? region.Y,
            Width      = result.MatchSize?.Width  ?? region.Width,
            Height     = result.MatchSize?.Height ?? region.Height,
            Confidence = result.Confidence,
            Label      = rule is not null
                ? result.MatchPixelCount > 0
                    ? $"{rule.Name} ({result.Confidence:P0} / {result.MatchPixelCount}px)"
                    : $"{rule.Name} ({result.Confidence:P0})"
                : result.TemplateName ?? region.Name,
            CreatedAt  = DateTime.UtcNow
        };

        // Remove old detections for the same region
        var old = Detections.Where(d => d.RegionId == result.RegionId).ToList();
        foreach (var o in old) Detections.Remove(o);

        Detections.Add(item);
        PruneOldDetections();
    }

    public void ClearDetections() => Detections.Clear();

    private void PruneOldDetections()
    {
        var threshold = DateTime.UtcNow.AddSeconds(-3);
        var stale = Detections.Where(d => d.CreatedAt < threshold).ToList();
        foreach (var d in stale) Detections.Remove(d);
    }
}

public sealed class RegionOverlayItem
{
    public Guid   Id       { get; }
    public string Name     { get; }
    public double Left     { get; }
    public double Top      { get; }
    public double Width    { get; }
    public double Height   { get; }
    public int    Priority { get; }

    public RegionOverlayItem(MonitoredRegion r)
    {
        Id = r.Id; Name = r.Name;
        Left = r.X; Top = r.Y; Width = r.Width; Height = r.Height;
        Priority = r.Priority;
    }
}

public sealed class ClickTargetOverlayItem
{
    public double Left   { get; }
    public double Top    { get; }
    public string Label  { get; }

    public ClickTargetOverlayItem(ClickTargetMarker marker)
    {
        Left  = marker.X - 12;
        Top   = marker.Y - 12;
        Label = marker.Label;
    }
}

public sealed class DetectionOverlayItem
{
    public Guid     RegionId   { get; init; }
    public string   RegionName { get; init; } = string.Empty;
    public string   Label      { get; init; } = string.Empty;
    public double   X          { get; init; }
    public double   Y          { get; init; }
    public double   Width      { get; init; }
    public double   Height     { get; init; }
    public double   Confidence { get; init; }
    public DateTime CreatedAt  { get; init; }
}
