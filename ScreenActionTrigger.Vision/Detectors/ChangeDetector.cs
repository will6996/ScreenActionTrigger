using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Vision.Detectors;

public sealed class ChangeDetector
{
    private readonly ILogger<ChangeDetector> _logger;
    private readonly Dictionary<Guid, byte[]> _previousFrames = new();

    public ChangeDetector(ILogger<ChangeDetector> logger) => _logger = logger;

    public DetectionResult Detect(byte[] currentFrameData, MonitoredRegion region, RuleCondition condition)
    {
        try
        {
            if (!_previousFrames.TryGetValue(region.Id, out var previousFrameData))
            {
                _previousFrames[region.Id] = currentFrameData;
                return DetectionResult.NoMatch(region.Id, ConditionType.ChangeDetection);
            }

            double changeRatio = ComputeChangeRatio(currentFrameData, previousFrameData, condition.ChangeSensitivity);
            _previousFrames[region.Id] = currentFrameData;

            bool isMatch = changeRatio >= condition.MinChangePercentage;

            return new DetectionResult
            {
                RegionId      = region.Id,
                IsMatch       = isMatch,
                Confidence    = changeRatio,
                DetectionType = ConditionType.ChangeDetection,
                RegionBounds  = region.Bounds,
                MatchLocation = isMatch
                    ? new Point(region.X + region.Width / 2, region.Y + region.Height / 2)
                    : null,
                MatchSize     = isMatch ? new Size(1, 1) : null,
                Timestamp     = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change detection error in region {Name}", region.Name);
            return DetectionResult.NoMatch(region.Id, ConditionType.ChangeDetection);
        }
    }

    private static unsafe double ComputeChangeRatio(byte[] current, byte[] previous, double sensitivity)
    {
        try
        {
            using var msA = new MemoryStream(current);
            using var msB = new MemoryStream(previous);
            using var bmpA = new Bitmap(msA);
            using var bmpB = new Bitmap(msB);

            int w = Math.Min(bmpA.Width, bmpB.Width);
            int h = Math.Min(bmpA.Height, bmpB.Height);
            if (w == 0 || h == 0) return 0;

            var dataA = bmpA.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var dataB = bmpB.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int total = w * h;
            int changed = 0;
            int threshold = (int)(sensitivity * 255 * 3); // sensitivity applied to sum of RGB diffs

            byte* ptrA = (byte*)dataA.Scan0;
            byte* ptrB = (byte*)dataB.Scan0;

            for (int i = 0; i < total; i++)
            {
                int diffR = Math.Abs(ptrA[i * 4 + 2] - ptrB[i * 4 + 2]);
                int diffG = Math.Abs(ptrA[i * 4 + 1] - ptrB[i * 4 + 1]);
                int diffB = Math.Abs(ptrA[i * 4    ] - ptrB[i * 4    ]);
                if (diffR + diffG + diffB > threshold) changed++;
            }

            bmpA.UnlockBits(dataA);
            bmpB.UnlockBits(dataB);

            return (double)changed / total;
        }
        catch { return 0; }
    }

    public void ClearCache(Guid regionId) => _previousFrames.Remove(regionId);
    public void ClearAll() => _previousFrames.Clear();
}
