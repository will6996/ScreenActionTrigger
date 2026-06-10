using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core;
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
            var targets = condition.GetAllTargetColors()
                .Select(TryParseColor)
                .Where(c => c.HasValue)
                .Select(c => c!.Value)
                .ToList();

            if (targets.Count == 0)
                return DetectionResult.NoMatch(region.Id, ConditionType.ColorDetection);

            using var ms  = new MemoryStream(frameData);
            using var raw = new Bitmap(ms);
            using var bmp = Ensure32bpp(raw);

            if (bmp.Width == 0 || bmp.Height == 0)
                return DetectionResult.NoMatch(region.Id, ConditionType.ColorDetection);

            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int totalPixels = bmp.Width * bmp.Height;
            int matchCount  = 0;
            long sumX = 0, sumY = 0;

            try
            {
                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0;
                    int stride = bmpData.Stride;
                    int width  = bmp.Width;
                    int height = bmp.Height;

                    for (int y = 0; y < height; y++)
                    {
                        byte* row = ptr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int i = x * 4;
                            byte b = row[i];
                            byte g = row[i + 1];
                            byte r = row[i + 2];

                            if (targets.Any(t =>
                                    Math.Abs(r - t.R) <= condition.ColorTolerance &&
                                    Math.Abs(g - t.G) <= condition.ColorTolerance &&
                                    Math.Abs(b - t.B) <= condition.ColorTolerance))
                            {
                                matchCount++;
                                sumX += x;
                                sumY += y;
                            }
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            double percentage = (double)matchCount / totalPixels;
            bool isMatch = condition.MinMatchingPixels > 0
                ? matchCount >= condition.MinMatchingPixels
                : percentage >= condition.MinColorPercentage;

            Point? screenLoc = null;
            if (isMatch && matchCount > 0)
            {
                int localX = (int)(sumX / matchCount);
                int localY = (int)(sumY / matchCount);
                screenLoc = new Point(region.X + localX, region.Y + localY);
            }

            return new DetectionResult
            {
                RegionId      = region.Id,
                IsMatch       = isMatch,
                Confidence    = percentage,
                DetectionType = ConditionType.ColorDetection,
                MatchLocation = screenLoc,
                MatchSize     = isMatch ? new Size(1, 1) : null,
                RegionBounds  = region.Bounds,
                Timestamp     = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Color detection error in region {Name}", region.Name);
            return DetectionResult.NoMatch(region.Id, ConditionType.ColorDetection);
        }
    }

    private static Color? TryParseColor(string hex)
    {
        if (!ColorHexHelper.TryNormalize(hex, out var normalized))
            return null;

        try { return ColorTranslator.FromHtml(normalized); }
        catch { return null; }
    }

    private static Bitmap Ensure32bpp(Bitmap source)
    {
        if (source.PixelFormat == PixelFormat.Format32bppArgb)
            return (Bitmap)source.Clone();

        var converted = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(converted);
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return converted;
    }

    public static Color FindDominantColor(byte[] frameData)
    {
        using var ms  = new MemoryStream(frameData);
        using var raw = new Bitmap(ms);
        using var bmp = Ensure32bpp(raw);

        var colorCounts = new Dictionary<int, int>();
        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                int total = bmp.Width * bmp.Height;
                for (int i = 0; i < total; i++)
                {
                    int r = (ptr[i * 4 + 2] >> 5) << 5;
                    int g = (ptr[i * 4 + 1] >> 5) << 5;
                    int b = (ptr[i * 4    ] >> 5) << 5;
                    int key = (r << 16) | (g << 8) | b;
                    colorCounts.TryGetValue(key, out int cnt);
                    colorCounts[key] = cnt + 1;
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        if (colorCounts.Count == 0) return Color.Black;
        var dominant = colorCounts.OrderByDescending(kv => kv.Value).First().Key;
        return Color.FromArgb((dominant >> 16) & 0xFF, (dominant >> 8) & 0xFF, dominant & 0xFF);
    }

}
