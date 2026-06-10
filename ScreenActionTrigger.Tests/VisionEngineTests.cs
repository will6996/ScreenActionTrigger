using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Vision.Detectors;
using ScreenActionTrigger.Vision.Services;
using Xunit;

namespace ScreenActionTrigger.Tests;

public sealed class VisionEngineTests
{
    private readonly VisionEngine _engine;

    public VisionEngineTests()
    {
        _engine = new VisionEngine(
            new ColorDetector(NullLogger<ColorDetector>.Instance),
            new ChangeDetector(NullLogger<ChangeDetector>.Instance),
            new TemplateMatcher(NullLogger<TemplateMatcher>.Instance),
            NullLogger<VisionEngine>.Instance);
    }

    private static byte[] SolidPng(int w, int h, Color c)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(c);
        using var ms  = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    [Fact]
    public async Task Evaluate_ColorDetection_SolidBlue_Matches()
    {
        var frame   = SolidPng(80, 80, Color.Blue);
        var region  = new MonitoredRegion { Id = Guid.NewGuid(), Width = 80, Height = 80 };
        var cond    = new RuleCondition
        {
            Type = ConditionType.ColorDetection,
            TargetColor        = "#0000FF",
            ColorTolerance     = 10,
            MinColorPercentage = 0.90
        };

        var result = await _engine.EvaluateAsync(frame, region, cond);

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.9);
        result.DetectionType.Should().Be(ConditionType.ColorDetection);
    }

    [Fact]
    public async Task Evaluate_ChangeDetection_FirstFrame_NoMatch()
    {
        var regionId = Guid.NewGuid();
        var frame    = SolidPng(60, 60, Color.Gray);
        var region   = new MonitoredRegion { Id = regionId, Width = 60, Height = 60 };
        var cond     = new RuleCondition
        {
            Type = ConditionType.ChangeDetection,
            MinChangePercentage = 0.10, ChangeSensitivity = 0.30
        };

        var result = await _engine.EvaluateAsync(frame, region, cond);

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_ChangeDetection_TotalChange_Matches()
    {
        var regionId = Guid.NewGuid();
        var frameA   = SolidPng(60, 60, Color.Black);
        var frameB   = SolidPng(60, 60, Color.White);
        var region   = new MonitoredRegion { Id = regionId, Width = 60, Height = 60 };
        var cond     = new RuleCondition
        {
            Type = ConditionType.ChangeDetection,
            MinChangePercentage = 0.50, ChangeSensitivity = 0.10
        };

        await _engine.EvaluateAsync(frameA, region, cond); // seed
        var result = await _engine.EvaluateAsync(frameB, region, cond);

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task Evaluate_TemplateMatching_MissingTemplate_NoMatch()
    {
        var frame  = SolidPng(100, 100, Color.LightBlue);
        var region = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var cond   = new RuleCondition
        {
            Type       = ConditionType.TemplateMatching,
            TemplateId = Guid.NewGuid()
        };
        _engine.SetTemplates(new List<Template>()); // empty library

        var result = await _engine.EvaluateAsync(frame, region, cond);

        result.IsMatch.Should().BeFalse();
        result.DetectionType.Should().Be(ConditionType.TemplateMatching);
    }

    [Fact]
    public async Task Evaluate_UnknownConditionType_ReturnsNoMatch()
    {
        var frame  = SolidPng(50, 50, Color.Orange);
        var region = new MonitoredRegion { Id = Guid.NewGuid(), Width = 50, Height = 50 };
        var cond   = new RuleCondition { Type = (ConditionType)999 };

        var result = await _engine.EvaluateAsync(frame, region, cond);

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void ClearFrameCache_DoesNotThrow()
    {
        var action = () => _engine.ClearFrameCache(Guid.NewGuid());
        action.Should().NotThrow();
    }

    [Fact]
    public void ClearAllCaches_DoesNotThrow()
    {
        var action = () => _engine.ClearAllCaches();
        action.Should().NotThrow();
    }

    [Fact]
    public async Task Evaluate_CancellationToken_ThrowsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var frame  = SolidPng(50, 50, Color.Red);
        var region = new MonitoredRegion { Id = Guid.NewGuid(), Width = 50, Height = 50 };
        var cond   = new RuleCondition { Type = ConditionType.ColorDetection };

        var act = () => _engine.EvaluateAsync(frame, region, cond, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void SetTemplates_Empty_DoesNotThrow()
    {
        var action = () => _engine.SetTemplates(new List<Template>());
        action.Should().NotThrow();
    }
}
