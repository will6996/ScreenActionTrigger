using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Vision.Detectors;

namespace ScreenActionTrigger.Vision.Services;

public sealed class VisionEngine : IVisionEngine
{
    private readonly ColorDetector _colorDetector;
    private readonly ChangeDetector _changeDetector;
    private readonly TemplateMatcher _templateMatcher;
    private readonly ILogger<VisionEngine> _logger;
    private List<Template> _templates = new();

    public VisionEngine(
        ColorDetector colorDetector,
        ChangeDetector changeDetector,
        TemplateMatcher templateMatcher,
        ILogger<VisionEngine> logger)
    {
        _colorDetector = colorDetector;
        _changeDetector = changeDetector;
        _templateMatcher = templateMatcher;
        _logger = logger;
    }

    public void SetTemplates(IEnumerable<Template> templates)
    {
        _templates = templates.ToList();
        foreach (var t in _templates)
            _templateMatcher.LoadTemplate(t);
    }

    public Task<DetectionResult> EvaluateAsync(
        byte[] frameData, MonitoredRegion region, RuleCondition condition, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var result = condition.Type switch
        {
            ConditionType.ColorDetection   => _colorDetector.Detect(frameData, region, condition),
            ConditionType.ChangeDetection  => _changeDetector.Detect(frameData, region, condition),
            ConditionType.TemplateMatching => _templateMatcher.Match(frameData, region, condition, _templates),
            _ => DetectionResult.NoMatch(region.Id, condition.Type)
        };

        return Task.FromResult(result);
    }

    public void ClearFrameCache(Guid regionId) => _changeDetector.ClearCache(regionId);

    public void ClearAllCaches()
    {
        _changeDetector.ClearAll();
        _templateMatcher.ClearAll();
    }
}
