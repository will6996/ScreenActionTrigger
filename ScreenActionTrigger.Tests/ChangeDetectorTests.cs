using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Vision.Detectors;
using Xunit;

namespace ScreenActionTrigger.Tests;

public sealed class ChangeDetectorTests
{
    private readonly ChangeDetector _detector;

    public ChangeDetectorTests()
    {
        _detector = new ChangeDetector(NullLogger<ChangeDetector>.Instance);
    }

    private static byte[] CreateSolidPng(int w, int h, Color c)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(c);
        using var ms  = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    [Fact]
    public void Detect_FirstFrame_ReturnsNoMatch()
    {
        var region    = new MonitoredRegion { Id = Guid.NewGuid(), Width = 50, Height = 50 };
        var condition = new RuleCondition { MinChangePercentage = 0.1, ChangeSensitivity = 0.3 };
        var frame     = CreateSolidPng(50, 50, Color.Blue);

        var result = _detector.Detect(frame, region, condition);

        result.IsMatch.Should().BeFalse("first frame has no previous to compare");
    }

    [Fact]
    public void Detect_IdenticalFrames_ShouldNotMatch()
    {
        var region    = new MonitoredRegion { Id = Guid.NewGuid(), Width = 50, Height = 50 };
        var condition = new RuleCondition { MinChangePercentage = 0.05, ChangeSensitivity = 0.3 };
        var frame     = CreateSolidPng(50, 50, Color.Green);

        _detector.Detect(frame, region, condition);              // seed
        var result = _detector.Detect(frame, region, condition); // compare same

        result.IsMatch.Should().BeFalse();
        result.Confidence.Should().BeApproximately(0, 0.01);
    }

    [Fact]
    public void Detect_CompletelyDifferentFrames_ShouldMatch()
    {
        var region    = new MonitoredRegion { Id = Guid.NewGuid(), Width = 60, Height = 60 };
        var condition = new RuleCondition { MinChangePercentage = 0.5, ChangeSensitivity = 0.1 };
        var frameA    = CreateSolidPng(60, 60, Color.Black);
        var frameB    = CreateSolidPng(60, 60, Color.White);

        _detector.Detect(frameA, region, condition);
        var result = _detector.Detect(frameB, region, condition);

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void ClearCache_ThenNextCallIsSeedFrame()
    {
        var region    = new MonitoredRegion { Id = Guid.NewGuid(), Width = 40, Height = 40 };
        var condition = new RuleCondition { MinChangePercentage = 0.1, ChangeSensitivity = 0.3 };
        var frameA    = CreateSolidPng(40, 40, Color.Red);
        var frameB    = CreateSolidPng(40, 40, Color.Blue);

        _detector.Detect(frameA, region, condition); // seed
        _detector.ClearCache(region.Id);

        // After clear, next call acts as first frame
        var result = _detector.Detect(frameB, region, condition);

        result.IsMatch.Should().BeFalse("cache was cleared — frameB is new seed");
    }

    [Fact]
    public void ClearAll_AllCachesReset()
    {
        var r1 = new MonitoredRegion { Id = Guid.NewGuid(), Width = 30, Height = 30 };
        var r2 = new MonitoredRegion { Id = Guid.NewGuid(), Width = 30, Height = 30 };
        var cond  = new RuleCondition { MinChangePercentage = 0.1, ChangeSensitivity = 0.3 };
        var frame = CreateSolidPng(30, 30, Color.Cyan);

        _detector.Detect(frame, r1, cond);
        _detector.Detect(frame, r2, cond);
        _detector.ClearAll();

        var res1 = _detector.Detect(frame, r1, cond);
        var res2 = _detector.Detect(frame, r2, cond);

        res1.IsMatch.Should().BeFalse("r1 cache cleared");
        res2.IsMatch.Should().BeFalse("r2 cache cleared");
    }

    [Fact]
    public void Detect_HighSensitivity_DetectsSmallChanges()
    {
        var region    = new MonitoredRegion { Id = Guid.NewGuid(), Width = 50, Height = 50 };
        var condition = new RuleCondition { MinChangePercentage = 0.3, ChangeSensitivity = 0.01 };
        var frameA    = CreateSolidPng(50, 50, Color.FromArgb(100, 100, 100));
        var frameB    = CreateSolidPng(50, 50, Color.FromArgb(105, 105, 105)); // tiny difference

        _detector.Detect(frameA, region, condition);
        var result = _detector.Detect(frameB, region, condition);

        // With very low sensitivity threshold, even tiny RGB differences register
        result.Confidence.Should().BeGreaterThan(0);
    }
}
