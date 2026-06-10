using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface IVisionEngine
{
    Task<DetectionResult> EvaluateAsync(
        byte[] frameData,
        MonitoredRegion region,
        RuleCondition condition,
        CancellationToken ct = default);

    void SetTemplates(IEnumerable<Template> templates);
    void ClearFrameCache(Guid regionId);
    void ClearAllCaches();
}
