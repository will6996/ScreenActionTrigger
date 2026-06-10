using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Vision.Detectors;

public sealed class ColorDetector
{
    private readonly ILogger<ColorDetector> _logger;

    public ColorDetector(ILogger<ColorDetector> logger) => _logger = logger;

    public DetectionResult Detect(byte[] frameData, MonitoredRegion region, RuleCondition condition)
    {
        try
        {
            var target = ColorTranslator.FromHtml(condition.TargetColor);

            using var ms = new MemoryStream(frameData);
            using var bmp = new Bitmap(ms);

            if (bmp.Width == 0 || bmp.Height == 0)
                return DetectionResult.NoMatch(region.Id, ConditionType.ColorDetection);

            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int totalPixels = bmp.Width * bmp.Height;
            int matchCount = 0;

            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                for (int i = 0; i < totalPixels; i++)
                {
                    byte b = ptr[i * 4];
                    byte g = ptr[i * 4 + 1];
                    byte r = ptr[i * 4 + 2];

                    if (Math.Abs(r - target.R) <= condition.ColorTolerance &&
                        Math.Abs(g - target.G) <= condition.ColorTolerance &&
                        Math.Abs(b - target.B) <= condition.ColorTolerance)
                    {
                        matchCount++;
                    }
                }
            }

            bmp.UnlockBits(bmpData);

            double percentage = (double)matchCount / totalPixels;
            bool isMatch = percentage >= condition.MinColorPercentage;

            return new DetectionResult
            {
                RegionId = region.Id,
                IsMatch = isMatch,
                Confidence = percentage,
                DetectionType = ConditionType.ColorDetection,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Color detection error in region {Name}", region.Name);
            return DetectionResult.NoMatch(region.Id, ConditionType.ColorDetection);
        }
    }

    public static Color FindDominantColor(byte[] frameData)
    {
        using var ms = new MemoryStream(frameData);
        using var bmp = new Bitmap(ms);

        var colorCounts = new Dictionary<int, int>();
        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)bmpData.Scan0;
            int total = bmp.Width * bmp.Height;
            for (int i = 0; i < total; i++)
            {
                // Quantize to reduce color space (round to nearest 32)
                int r = (ptr[i * 4 + 2] >> 5) << 5;
                int g = (ptr[i * 4 + 1] >> 5) << 5;
                int b = (ptr[i * 4    ] >> 5) << 5;
                int key = (r << 16) | (g << 8) | b;
                colorCounts.TryGetValue(key, out int cnt);
                colorCounts[key] = cnt + 1;
            }
        }

        bmp.UnlockBits(bmpData);

        if (colorCounts.Count == 0) return Color.Black;
        var dominant = colorCounts.OrderByDescending(kv => kv.Value).First().Key;
        return Color.FromArgb((dominant >> 16) & 0xFF, (dominant >> 8) & 0xFF, dominant & 0xFF);
    }
}
