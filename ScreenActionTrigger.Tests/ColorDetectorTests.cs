using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Vision.Detectors;
using Xunit;

namespace ScreenActionTrigger.Tests;

public sealed class ColorDetectorTests
{
    private readonly ColorDetector _detector;

    public ColorDetectorTests()
    {
        _detector = new ColorDetector(NullLogger<ColorDetector>.Instance);
    }

    private static byte[] CreateSolidColorPng(int width, int height, Color color)
    {
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(color);
        using var ms  = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    [Fact]
    public void Detect_SolidBlueImage_SetsMatchLocationAtCentroid()
    {
        var frame = CreateSolidColorPng(100, 100, Color.Blue);
        var region = new MonitoredRegion { Id = Guid.NewGuid(), X = 200, Y = 300, Width = 100, Height = 100 };
        var condition = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            TargetColor        = "#0000FF",
            ColorTolerance     = 10,
            MinColorPercentage = 0.90
        };

        var result = _detector.Detect(frame, region, condition);

        result.IsMatch.Should().BeTrue();
        result.MatchLocation.Should().NotBeNull();
        result.MatchLocation!.Value.X.Should().BeInRange(240, 260);
        result.MatchLocation!.Value.Y.Should().BeInRange(340, 360);
    }

    [Fact]
    public void Detect_SmallColorPatch_MinMatchingPixels_Triggers()
    {
        // 10x10 blue patch on 100x100 black — only 1% of pixels
        using var bmp = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Black);
        g.FillRectangle(Brushes.Blue, 45, 45, 10, 10);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        var frame = ms.ToArray();

        var region = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var condition = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            TargetColor = "#0000FF",
            ColorTolerance = 10,
            MinColorPercentage = 0.30,
            MinMatchingPixels = 50
        };

        var result = _detector.Detect(frame, region, condition);
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void Detect_SolidBlueImage_AllBluePixels_ShouldMatch()
    {
        var frame = CreateSolidColorPng(100, 100, Color.Blue);
        var region = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var condition = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            TargetColor        = "#0000FF",
            ColorTolerance     = 10,
            MinColorPercentage = 0.90
        };

        var result = _detector.Detect(frame, region, condition);

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void Detect_SolidRedImage_CheckingBlue_ShouldNotMatch()
    {
        var frame = CreateSolidColorPng(100, 100, Color.Red);
        var region = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var condition = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            TargetColor        = "#0000FF",
            ColorTolerance     = 10,
            MinColorPercentage = 0.30
        };

        var result = _detector.Detect(frame, region, condition);

        result.IsMatch.Should().BeFalse();
        result.Confidence.Should().BeLessThan(0.30);
    }

    [Fact]
    public void Detect_HalfBlueImage_BelowThreshold_ShouldNotMatch()
    {
        // 50% blue pixels but threshold 60%
        using var bmp = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.FillRectangle(Brushes.Blue, 0, 0, 50, 100);
            g.FillRectangle(Brushes.Red,  50, 0, 50, 100);
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        var frame = ms.ToArray();

        var region = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var condition = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            TargetColor        = "#0000FF",
            ColorTolerance     = 10,
            MinColorPercentage = 0.60
        };

        var result = _detector.Detect(frame, region, condition);

        result.IsMatch.Should().BeFalse();
        result.Confidence.Should().BeApproximately(0.5, 0.05);
    }

    [Fact]
    public void Detect_ToleranceHandling_SlightlyOffBlue_ShouldMatch()
    {
        var color = Color.FromArgb(0, 0, 245); // slightly off from pure blue (255)
        var frame = CreateSolidColorPng(100, 100, color);
        var region = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var condition = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            TargetColor        = "#0000FF",
            ColorTolerance     = 15,     // within tolerance
            MinColorPercentage = 0.90
        };

        var result = _detector.Detect(frame, region, condition);

        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void Detect_EmptyFrame_ReturnsNoMatch()
    {
        var region    = new MonitoredRegion { Id = Guid.NewGuid() };
        var condition = new RuleCondition { Type = ConditionType.ColorDetection, TargetColor = "#FF0000" };

        var result = _detector.Detect(Array.Empty<byte>(), region, condition);

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void FindDominantColor_SolidBlue_ReturnsBluish()
    {
        var frame = CreateSolidColorPng(80, 80, Color.Blue);

        var dominant = ColorDetector.FindDominantColor(frame);

        dominant.B.Should().BeGreaterThan(200);
        dominant.R.Should().BeLessThan(50);
        dominant.G.Should().BeLessThan(50);
    }
}
