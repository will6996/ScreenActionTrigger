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

            int totalPixels   = bmp.Width * bmp.Height;
            int matchCount    = 0;
            int contentPixels = 0;
            long sumX = 0, sumY = 0;
            var darkThreshold = condition.DarkPixelThreshold;

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

                            if (condition.ExcludeDarkPixels && IsDarkPixel(r, g, b, darkThreshold))
                                continue;

                            contentPixels++;

                            if (targets.Any(t => ColorMatches(r, g, b, t, condition.ColorTolerance)))
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

            var denominator = condition.ExcludeDarkPixels
                ? Math.Max(contentPixels, 1)
                : totalPixels;

            double rawPercentage      = (double)matchCount / Math.Max(totalPixels, 1);
            double adjustedPercentage = (double)matchCount / denominator;

            bool isMatch = condition.MinMatchingPixels > 0
                ? matchCount >= condition.MinMatchingPixels
                : adjustedPercentage >= condition.MinColorPercentage;

            Point? screenLoc = null;
            if (isMatch && matchCount > 0)
            {
                int localX = (int)(sumX / matchCount);
                int localY = (int)(sumY / matchCount);
                screenLoc = new Point(region.X + localX, region.Y + localY);
            }

            return new DetectionResult
            {
                RegionId        = region.Id,
                IsMatch         = isMatch,
                Confidence      = adjustedPercentage,
                MatchPixelCount = matchCount,
                DetectionType   = ConditionType.ColorDetection,
                MatchLocation   = screenLoc,
                MatchSize       = isMatch ? new Size(1, 1) : null,
                RegionBounds    = region.Bounds,
                Timestamp       = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Color detection error in region {Name}", region.Name);
            return DetectionResult.NoMatch(region.Id, ConditionType.ColorDetection);
        }
    }

    /// <summary>Retorna as cores mais frequentes (não escuras) de um frame, em #RRGGBB.</summary>
    public static IReadOnlyList<string> FindTopColors(byte[] frameData, int count = 3, int darkThreshold = 35)
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
                    byte b = ptr[i * 4];
                    byte g = ptr[i * 4 + 1];
                    byte r = ptr[i * 4 + 2];

                    if (IsDarkPixel(r, g, b, darkThreshold))
                        continue;

                    // Quantiza para agrupar tons parecidos (passo de 8)
                    int rq = (r >> 3) << 3;
                    int gq = (g >> 3) << 3;
                    int bq = (b >> 3) << 3;
                    int key = (rq << 16) | (gq << 8) | bq;
                    colorCounts.TryGetValue(key, out int cnt);
                    colorCounts[key] = cnt + 1;
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        return colorCounts
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => $"#{(kv.Key >> 16) & 0xFF:X2}{(kv.Key >> 8) & 0xFF:X2}{kv.Key & 0xFF:X2}")
            .ToList();
    }

    internal static bool ColorMatches(byte r, byte g, byte b, Color target, int tolerance)
    {
        int dr = Math.Abs(r - target.R);
        int dg = Math.Abs(g - target.G);
        int db = Math.Abs(b - target.B);
        return Math.Max(dr, Math.Max(dg, db)) <= tolerance;
    }

    internal static bool IsDarkPixel(byte r, byte g, byte b, int threshold)
        => r + g + b < threshold;

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
        var top = FindTopColors(frameData, 1);
        if (top.Count == 0) return Color.Black;
        return ColorTranslator.FromHtml(top[0]);
    }
}
