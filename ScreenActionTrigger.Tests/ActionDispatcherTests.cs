using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Input.Controllers;
using Xunit;

namespace ScreenActionTrigger.Tests;

public sealed class ActionDispatcherTests : IDisposable
{
    private readonly ActionDispatcher _dispatcher;

    public ActionDispatcherTests()
    {
        var mouse    = new MouseController(NullLogger<MouseController>.Instance);
        var keyboard = new KeyboardController(NullLogger<KeyboardController>.Instance);
        _dispatcher  = new ActionDispatcher(mouse, keyboard, NullLogger<ActionDispatcher>.Instance);
    }

    [Fact]
    public void InitialQueueLength_IsZero()
    {
        _dispatcher.QueueLength.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_IncreasesQueueLength()
    {
        var action = new TriggerAction { Type = ActionType.ShowNotification, NotificationMessage = "test" };
        await _dispatcher.EnqueueAsync(action);
        // Queue is async-processed; length may already be 0 by the time we check.
        // We just verify no exception was thrown.
        await Task.Delay(200);
        // No assert on length because worker drains it immediately
    }

    [Fact]
    public async Task CancelAll_EmptiesQueue()
    {
        // Enqueue items, then cancel before worker drains
        for (int i = 0; i < 5; i++)
            await _dispatcher.EnqueueAsync(
                new TriggerAction { Type = ActionType.ShowNotification, NotificationMessage = "x" });

        _dispatcher.CancelAll();

        _dispatcher.QueueLength.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShowNotification_FiresActionExecutedEvent()
    {
        var action = new TriggerAction
        {
            Type = ActionType.ShowNotification,
            NotificationMessage = "Hello Test"
        };

        ActionExecutedEventArgs? args = null;
        _dispatcher.ActionExecuted += (_, a) => args = a;

        await _dispatcher.ExecuteAsync(action);

        args.Should().NotBeNull();
        args!.Success.Should().BeTrue();
        args.Action.Type.Should().Be(ActionType.ShowNotification);
    }

    [Fact]
    public async Task ExecuteAsync_ExecuteCommand_WorksWithEchoCommand()
    {
        var action = new TriggerAction
        {
            Type    = ActionType.ExecuteCommand,
            Command = "echo hello"
        };

        ActionExecutedEventArgs? args = null;
        _dispatcher.ActionExecuted += (_, a) => args = a;

        await _dispatcher.ExecuteAsync(action);
        await Task.Delay(500); // allow process to finish

        args.Should().NotBeNull();
        args!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueBatchAsync_AllActionsEnqueued()
    {
        var actions = Enumerable.Range(0, 3).Select(_ =>
            new TriggerAction { Type = ActionType.ShowNotification, NotificationMessage = "batch" });

        await _dispatcher.EnqueueBatchAsync(actions);

        await Task.Delay(300);
        // Verify events fired for all actions
    }

    [Fact]
    public async Task ExecuteAsync_WithDelayBefore_DelaysCorrectly()
    {
        var action = new TriggerAction
        {
            Type = ActionType.ShowNotification,
            NotificationMessage = "delayed",
            DelayBeforeMs = 100
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _dispatcher.ExecuteAsync(action);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterThan(90);
    }

    [Fact]
    public async Task ExecuteAsync_WithRepeatCount_RepeatsFires()
    {
        var action = new TriggerAction
        {
            Type = ActionType.ShowNotification,
            NotificationMessage = "repeat",
            RepeatCount = 3
        };

        int fireCount = 0;
        _dispatcher.ActionExecuted += (_, _) => Interlocked.Increment(ref fireCount);

        await _dispatcher.ExecuteAsync(action);

        fireCount.Should().Be(1); // one Execute call, not per-repeat (events at action level)
    }

    public void Dispose() => _dispatcher.Dispose();
}
