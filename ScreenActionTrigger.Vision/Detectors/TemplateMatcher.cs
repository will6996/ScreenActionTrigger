using System.Drawing;
using DrawingPoint = System.Drawing.Point;
using DrawingSize  = System.Drawing.Size;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Vision.Detectors;

public sealed class TemplateMatcher
{
    private readonly ILogger<TemplateMatcher> _logger;
    private readonly Dictionary<Guid, Mat> _templateCache = new();
    private readonly Dictionary<Guid, DateTime> _lastExecuted = new();

    public TemplateMatcher(ILogger<TemplateMatcher> logger) => _logger = logger;

    public void LoadTemplate(Template template)
    {
        try
        {
            if (!File.Exists(template.ImagePath)) return;
            var mat = Cv2.ImRead(template.ImagePath, ImreadModes.Color);
            if (!mat.Empty())
            {
                _templateCache.TryGetValue(template.Id, out var old);
                old?.Dispose();
                _templateCache[template.Id] = mat;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load template {Name}", template.Name);
        }
    }

    public void UnloadTemplate(Guid templateId)
    {
        if (_templateCache.TryGetValue(templateId, out var mat))
        {
            mat.Dispose();
            _templateCache.Remove(templateId);
        }
    }

    public void ClearAll()
    {
        foreach (var mat in _templateCache.Values) mat.Dispose();
        _templateCache.Clear();
        _lastExecuted.Clear();
    }

    public DetectionResult Match(
        byte[] frameData,
        MonitoredRegion region,
        RuleCondition condition,
        IReadOnlyList<Template> templates)
    {
        if (condition.TemplateId is null)
            return DetectionResult.NoMatch(region.Id, ConditionType.TemplateMatching);

        var template = templates.FirstOrDefault(t => t.Id == condition.TemplateId);
        if (template is null)
            return DetectionResult.NoMatch(region.Id, ConditionType.TemplateMatching);

        // Cooldown check
        if (_lastExecuted.TryGetValue(template.Id, out var lastTime) &&
            (DateTime.UtcNow - lastTime).TotalMilliseconds < template.CooldownMs)
            return DetectionResult.NoMatch(region.Id, ConditionType.TemplateMatching);

        if (!_templateCache.TryGetValue(template.Id, out var tmplMat))
        {
            LoadTemplate(template);
            if (!_templateCache.TryGetValue(template.Id, out tmplMat))
                return DetectionResult.NoMatch(region.Id, ConditionType.TemplateMatching);
        }

        try
        {
            using var frameMat = Mat.FromImageData(frameData, ImreadModes.Color);
            if (frameMat.Empty() || tmplMat.Empty())
                return DetectionResult.NoMatch(region.Id, ConditionType.TemplateMatching);

            var scales = template.UseAutoScale
                ? GenerateScales(template.MinScale, template.MaxScale, 5)
                : new[] { template.FixedScale };

            double bestConfidence = 0;
            DrawingPoint bestLocation = default;
            DrawingSize  bestSize = default;

            foreach (double scale in scales)
            {
                if (scale <= 0) continue;

                Mat? scaledTmpl = null;
                try
                {
                    scaledTmpl = new Mat();
                    var newSize = new OpenCvSharp.Size(
                        (int)(tmplMat.Width * scale),
                        (int)(tmplMat.Height * scale));

                    if (newSize.Width <= 0 || newSize.Height <= 0 ||
                        newSize.Width > frameMat.Width || newSize.Height > frameMat.Height)
                        continue;

                    Cv2.Resize(tmplMat, scaledTmpl, newSize);

                    using var result = new Mat();
                    var method = GetOpenCvMethod(template.Method);
                    Cv2.MatchTemplate(frameMat, scaledTmpl, result, method);

                    double minVal, maxVal;
                    OpenCvSharp.Point minLoc, maxLoc;
                    Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);

                    double confidence = template.Method == MatchingMethod.SqdiffNormed
                        ? 1.0 - minVal
                        : maxVal;

                    if (confidence > bestConfidence)
                    {
                        bestConfidence = confidence;
                        bestLocation = template.Method == MatchingMethod.SqdiffNormed
                            ? new DrawingPoint(minLoc.X, minLoc.Y)
                            : new DrawingPoint(maxLoc.X, maxLoc.Y);
                        bestSize = new DrawingSize(scaledTmpl.Width, scaledTmpl.Height);
                    }
                }
                finally { scaledTmpl?.Dispose(); }
            }

            bool isMatch = bestConfidence >= template.MinConfidence;

            if (isMatch)
                _lastExecuted[template.Id] = DateTime.UtcNow;

            // Translate match location back to screen coordinates
            DrawingPoint? screenLoc = isMatch
                ? new DrawingPoint(region.X + bestLocation.X, region.Y + bestLocation.Y)
                : null;

            return new DetectionResult
            {
                RegionId = region.Id,
                IsMatch = isMatch,
                Confidence = bestConfidence,
                DetectionType = ConditionType.TemplateMatching,
                TemplateName = template.Name,
                MatchLocation = screenLoc,
                MatchSize = isMatch ? (System.Drawing.Size?)bestSize : null,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Template matching error for template {Name}", template.Name);
            return DetectionResult.NoMatch(region.Id, ConditionType.TemplateMatching);
        }
    }

    private static TemplateMatchModes GetOpenCvMethod(MatchingMethod method) => method switch
    {
        MatchingMethod.CcorrNormed  => TemplateMatchModes.CCorrNormed,
        MatchingMethod.SqdiffNormed => TemplateMatchModes.SqDiffNormed,
        _                           => TemplateMatchModes.CCoeffNormed
    };

    private static double[] GenerateScales(double min, double max, int count)
    {
        if (count <= 1) return new[] { 1.0 };
        var scales = new double[count];
        double step = (max - min) / (count - 1);
        for (int i = 0; i < count; i++)
            scales[i] = min + step * i;
        return scales;
    }

    public void Dispose()
    {
        foreach (var mat in _templateCache.Values) mat.Dispose();
        _templateCache.Clear();
    }
}
