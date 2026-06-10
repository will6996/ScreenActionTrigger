using System.Drawing;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface IOverlayService
{
    bool IsVisible { get; }
    bool IsEnabled { get; set; }

    void Show();
    void Hide();
    void Toggle();

    void UpdateRegions(IEnumerable<MonitoredRegion> regions);
    void ShowConfigurationPreview(
        IEnumerable<MonitoredRegion> regions,
        IEnumerable<ClickTargetMarker> clickTargets);
    void ShowDetection(DetectionResult result, MonitoredRegion region);
    void ShowRuleTriggered(VisualRule rule, DetectionResult result);
    void ClearDetections();
}
