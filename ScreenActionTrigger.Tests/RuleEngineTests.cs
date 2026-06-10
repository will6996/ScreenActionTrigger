using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScreenActionTrigger.Core.Engines;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using Xunit;

namespace ScreenActionTrigger.Tests;

public sealed class RuleEngineTests
{
    private readonly Mock<IVisionEngine> _visionMock = new();
    private readonly RuleEngine _engine;

    public RuleEngineTests()
    {
        _engine = new RuleEngine(_visionMock.Object, NullLogger<RuleEngine>.Instance);
    }

    private static MonitoredRegion MakeRegion() => new()
    {
        Id = Guid.NewGuid(), Name = "Test", X = 0, Y = 0, Width = 100, Height = 100
    };

    private static VisualRule MakeRule(Guid regionId, bool enabled = true) => new()
    {
        Id       = Guid.NewGuid(),
        Name     = "TestRule",
        RegionId = regionId,
        IsEnabled = enabled,
        CooldownMs = 0,
        Condition = new RuleCondition { Type = ConditionType.ColorDetection },
        Actions   = new ObservableCollection<TriggerAction> { new() { Type = ActionType.MouseLeftClick } }
    };

    [Fact]
    public void LoadRules_ShouldPopulateRuleList()
    {
        var region = MakeRegion();
        var rules  = new[] { MakeRule(region.Id), MakeRule(region.Id) };

        _engine.LoadRules(rules);

        _engine.GetRules().Should().HaveCount(2);
    }

    [Fact]
    public void AddRule_ShouldAppendRule()
    {
        var region = MakeRegion();
        var rule   = MakeRule(region.Id);

        _engine.AddRule(rule);

        _engine.GetRules().Should().ContainSingle(r => r.Id == rule.Id);
    }

    [Fact]
    public void RemoveRule_ShouldDeleteRule()
    {
        var region = MakeRegion();
        var rule   = MakeRule(region.Id);
        _engine.AddRule(rule);

        _engine.RemoveRule(rule.Id);

        _engine.GetRules().Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessRegionAsync_WhenDetectionMatches_FiresRuleTriggered()
    {
        var region = MakeRegion();
        var rule   = MakeRule(region.Id);
        _engine.LoadRules(new[] { rule });

        var matchResult = new DetectionResult
        {
            RegionId = region.Id, IsMatch = true, Confidence = 0.99,
            DetectionType = ConditionType.ColorDetection
        };

        _visionMock.Setup(v => v.EvaluateAsync(
                It.IsAny<byte[]>(),
                It.IsAny<MonitoredRegion>(),
                It.IsAny<RuleCondition>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchResult);

        RuleTriggeredEventArgs? fired = null;
        _engine.RuleTriggered += (_, args) => fired = args;

        await _engine.ProcessRegionAsync(region, new byte[1]);

        fired.Should().NotBeNull();
        fired!.Rule.Id.Should().Be(rule.Id);
        fired.Detection.Confidence.Should().Be(0.99);
    }

    [Fact]
    public async Task ProcessRegionAsync_WhenNoMatch_DoesNotFireRuleTriggered()
    {
        var region = MakeRegion();
        var rule   = MakeRule(region.Id);
        _engine.LoadRules(new[] { rule });

        _visionMock.Setup(v => v.EvaluateAsync(
                It.IsAny<byte[]>(), It.IsAny<MonitoredRegion>(),
                It.IsAny<RuleCondition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DetectionResult.NoMatch(region.Id, ConditionType.ColorDetection));

        bool triggered = false;
        _engine.RuleTriggered += (_, _) => triggered = true;

        await _engine.ProcessRegionAsync(region, new byte[1]);

        triggered.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRegionAsync_DisabledRule_IsSkipped()
    {
        var region = MakeRegion();
        var rule   = MakeRule(region.Id, enabled: false);
        _engine.LoadRules(new[] { rule });

        bool triggered = false;
        _engine.RuleTriggered += (_, _) => triggered = true;

        await _engine.ProcessRegionAsync(region, new byte[1]);

        triggered.Should().BeFalse();
        _visionMock.Verify(v => v.EvaluateAsync(
            It.IsAny<byte[]>(), It.IsAny<MonitoredRegion>(),
            It.IsAny<RuleCondition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRegionAsync_CooldownActive_IsSkipped()
    {
        var region = MakeRegion();
        var rule   = MakeRule(region.Id);
        rule.CooldownMs = 10000;
        rule.LastExecuted = DateTime.UtcNow;
        _engine.LoadRules(new[] { rule });

        int triggerCount = 0;
        _engine.RuleTriggered += (_, _) => triggerCount++;

        await _engine.ProcessRegionAsync(region, new byte[1]);

        triggerCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessRegionAsync_MaxExecutionsReached_IsSkipped()
    {
        var region = MakeRegion();
        var rule   = MakeRule(region.Id);
        rule.MaxExecutions = 2;
        rule.ExecutionCount = 2;
        _engine.LoadRules(new[] { rule });

        bool triggered = false;
        _engine.RuleTriggered += (_, _) => triggered = true;

        await _engine.ProcessRegionAsync(region, new byte[1]);

        triggered.Should().BeFalse();
    }

    [Fact]
    public void ResetRule_ShouldClearExecutionCount()
    {
        var region = MakeRegion();
        var rule   = MakeRule(region.Id);
        rule.ExecutionCount = 5;
        rule.LastExecuted   = DateTime.UtcNow;
        _engine.AddRule(rule);

        _engine.ResetRule(rule.Id);

        rule.ExecutionCount.Should().Be(0);
        rule.LastExecuted.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void ResetAll_ShouldClearAllRules()
    {
        var region = MakeRegion();
        var r1     = MakeRule(region.Id);
        var r2     = MakeRule(region.Id);
        r1.ExecutionCount = 3;
        r2.ExecutionCount = 7;
        _engine.LoadRules(new[] { r1, r2 });

        _engine.ResetAll();

        r1.ExecutionCount.Should().Be(0);
        r2.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task CompositeCondition_AND_BothMatch_ShouldTrigger()
    {
        var region = MakeRegion();
        var rule   = new VisualRule
        {
            RegionId  = region.Id,
            IsEnabled = true,
            Condition = new RuleCondition
            {
                Type     = ConditionType.Composite,
                Operator = LogicalOperator.And,
                SubConditions = new List<RuleCondition>
                {
                    new() { Type = ConditionType.ColorDetection },
                    new() { Type = ConditionType.ChangeDetection }
                }
            }
        };
        _engine.AddRule(rule);

        _visionMock.Setup(v => v.EvaluateAsync(
                It.IsAny<byte[]>(), It.IsAny<MonitoredRegion>(),
                It.IsAny<RuleCondition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] _, MonitoredRegion r, RuleCondition c, CancellationToken _) =>
                new DetectionResult { RegionId = r.Id, IsMatch = true, Confidence = 0.95, DetectionType = c.Type });

        bool fired = false;
        _engine.RuleTriggered += (_, _) => fired = true;

        await _engine.ProcessRegionAsync(region, new byte[1]);

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task CompositeCondition_AND_OneNoMatch_ShouldNotTrigger()
    {
        var region = MakeRegion();
        int callCount = 0;
        var rule = new VisualRule
        {
            RegionId = region.Id, IsEnabled = true,
            Condition = new RuleCondition
            {
                Type = ConditionType.Composite, Operator = LogicalOperator.And,
                SubConditions = new List<RuleCondition>
                {
                    new() { Type = ConditionType.ColorDetection },
                    new() { Type = ConditionType.ChangeDetection }
                }
            }
        };
        _engine.AddRule(rule);

        _visionMock.Setup(v => v.EvaluateAsync(
                It.IsAny<byte[]>(), It.IsAny<MonitoredRegion>(),
                It.IsAny<RuleCondition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] _, MonitoredRegion r, RuleCondition c, CancellationToken _) =>
            {
                callCount++;
                bool match = callCount == 1; // first sub-condition matches, second doesn't
                return new DetectionResult { RegionId = r.Id, IsMatch = match, DetectionType = c.Type };
            });

        bool fired = false;
        _engine.RuleTriggered += (_, _) => fired = true;

        await _engine.ProcessRegionAsync(region, new byte[1]);

        fired.Should().BeFalse();
    }

    [Fact]
    public async Task NegatedCondition_WhenMatchTrue_ShouldNotTrigger()
    {
        var region = MakeRegion();
        var rule   = new VisualRule
        {
            RegionId = region.Id, IsEnabled = true,
            Condition = new RuleCondition
            {
                Type = ConditionType.ColorDetection,
                IsNegated = true
            }
        };
        _engine.AddRule(rule);

        _visionMock.Setup(v => v.EvaluateAsync(
                It.IsAny<byte[]>(), It.IsAny<MonitoredRegion>(),
                It.IsAny<RuleCondition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] _, MonitoredRegion r, RuleCondition c, CancellationToken _) =>
                new DetectionResult { RegionId = r.Id, IsMatch = true, Confidence = 0.9, DetectionType = c.Type });

        bool fired = false;
        _engine.RuleTriggered += (_, _) => fired = true;

        await _engine.ProcessRegionAsync(region, new byte[1]);

        fired.Should().BeFalse();
    }
}
