using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Vision.Detectors;
using Xunit;

namespace ScreenActionTrigger.Tests;

public sealed class TemplateMatcherTests
{
    private readonly TemplateMatcher _matcher;

    public TemplateMatcherTests()
    {
        _matcher = new TemplateMatcher(NullLogger<TemplateMatcher>.Instance);
    }

    [Fact]
    public void Match_WhenTemplateIdIsNull_ReturnsNoMatch()
    {
        var region    = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var condition = new RuleCondition { Type = ConditionType.TemplateMatching, TemplateId = null };
        var frame     = CreateBlankPng(100, 100);

        var result = _matcher.Match(frame, region, condition, new List<Template>());

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void Match_WhenTemplateNotInList_ReturnsNoMatch()
    {
        var region    = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var condition = new RuleCondition { Type = ConditionType.TemplateMatching, TemplateId = Guid.NewGuid() };
        var frame     = CreateBlankPng(100, 100);

        var result = _matcher.Match(frame, region, condition, new List<Template>());

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void Match_WhenTemplateImageMissing_ReturnsNoMatch()
    {
        var templateId = Guid.NewGuid();
        var template   = new Template
        {
            Id          = templateId,
            ImagePath   = "/nonexistent/path.png",
            MinConfidence = 0.95
        };
        var region    = new MonitoredRegion { Id = Guid.NewGuid(), Width = 100, Height = 100 };
        var condition = new RuleCondition
        {
            Type       = ConditionType.TemplateMatching,
            TemplateId = templateId
        };
        var frame = CreateBlankPng(100, 100);

        var result = _matcher.Match(frame, region, condition, new[] { template });

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void LoadTemplate_NonExistentFile_DoesNotThrow()
    {
        var template = new Template { ImagePath = "/nonexistent.png" };

        var action = () => _matcher.LoadTemplate(template);

        action.Should().NotThrow();
    }

    [Fact]
    public void ClearAll_DoesNotThrow()
    {
        var action = () => _matcher.ClearAll();
        action.Should().NotThrow();
    }

    [Fact]
    public void Match_ResultHasCorrectRegionId()
    {
        var regionId  = Guid.NewGuid();
        var region    = new MonitoredRegion { Id = regionId, Width = 100, Height = 100 };
        var condition = new RuleCondition { Type = ConditionType.TemplateMatching, TemplateId = null };
        var frame     = CreateBlankPng(100, 100);

        var result = _matcher.Match(frame, region, condition, new List<Template>());

        result.RegionId.Should().Be(regionId);
        result.DetectionType.Should().Be(ConditionType.TemplateMatching);
    }

    [Fact]
    public void Match_WhenFrameDataIsEmpty_ReturnsNoMatch()
    {
        var templateId = Guid.NewGuid();
        var template   = new Template { Id = templateId, ImagePath = "dummy.png", MinConfidence = 0.95 };
        var region     = new MonitoredRegion { Id = Guid.NewGuid(), Width = 50, Height = 50 };
        var condition  = new RuleCondition { Type = ConditionType.TemplateMatching, TemplateId = templateId };

        var result = _matcher.Match(Array.Empty<byte>(), region, condition, new[] { template });

        result.IsMatch.Should().BeFalse();
    }

    private static byte[] CreateBlankPng(int width, int height)
    {
        using var bmp = new System.Drawing.Bitmap(width, height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var ms  = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
}
