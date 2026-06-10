using System.Runtime.InteropServices;
using ScreenActionTrigger.Input.Win32;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Input.Controllers;

public sealed class ActionDispatcher : IActionDispatcher, IDisposable
{
    private readonly MouseController _mouse;
    private readonly KeyboardController _keyboard;
    private readonly ILogger<ActionDispatcher> _logger;
    private readonly PriorityQueue<ActionQueueItem, int> _queue = new();
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly CancellationTokenSource _workerCts = new();
    private readonly Task _workerTask;

    public event EventHandler<ActionExecutedEventArgs>? ActionExecuted;
    public int QueueLength { get; private set; }

    public ActionDispatcher(
        MouseController mouse,
        KeyboardController keyboard,
        ILogger<ActionDispatcher> logger)
    {
        _mouse = mouse;
        _keyboard = keyboard;
        _logger = logger;
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public async Task ExecuteAsync(TriggerAction action, DetectionResult? context = null, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool success = false;
        Exception? error = null;

        try
        {
            if (action.DelayBeforeMs > 0) await Task.Delay(action.DelayBeforeMs, ct);

            var (x, y) = ResolveClickPoint(action, context);
            if (IsMouseAction(action.Type) && !HasValidClickPoint(action, context, x, y))
            {
                error = new InvalidOperationException(
                    $"Coordenadas inválidas para clique ({x},{y}) — verifique região e detecção");
                _logger.LogWarning("Ação {Type} ignorada — {Message}", action.Type, error.Message);
            }
            else
            {
                _logger.LogDebug("Action {Type} at ({X},{Y})", action.Type, x, y);

                for (int i = 0; i < Math.Max(action.RepeatCount, 1); i++)
                {
                    await DispatchAsync(action, x, y, ct);
                    if (i < action.RepeatCount - 1 && action.RepeatDelayMs > 0)
                        await Task.Delay(action.RepeatDelayMs, ct);
                }

                success = true;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            error = ex;
            _logger.LogError(ex, "Action execution failed: {Type}", action.Type);
        }

        sw.Stop();
        ActionExecuted?.Invoke(this, new ActionExecutedEventArgs
        {
            Action = action,
            Success = success,
            Error = error,
            Duration = sw.Elapsed
        });
    }

    private async Task DispatchAsync(TriggerAction action, int x, int y, CancellationToken ct)
    {
        switch (action.Type)
        {
            case ActionType.MouseLeftClick:   await _mouse.ClickLeftAsync(x, y); break;
            case ActionType.MouseRightClick:  await _mouse.ClickRightAsync(x, y); break;
            case ActionType.MouseDoubleClick: await _mouse.DoubleClickAsync(x, y); break;
            case ActionType.MousePress:       await _mouse.PressAsync(x, y, action.MouseButton); break;
            case ActionType.MouseRelease:     await _mouse.ReleaseAsync(x, y, action.MouseButton); break;
            case ActionType.MouseScroll:      await _mouse.ScrollAsync(x, y, action.ScrollAmount); break;
            case ActionType.MouseFollowPath:
                await _mouse.FollowPathAsync(
                    action.PathPoints, action.PathStepDelayMs, action.PathLeftClickAtEnd, ct);
                break;
            case ActionType.KeyPress:
                if (action.KeyCode is not null) await _keyboard.PressKeyAsync(action.KeyCode); break;
            case ActionType.KeyCombination:
            {
                var combo = action.KeyCombination?.Length > 0
                    ? action.KeyCombination
                    : KeyCombinationParser.Parse(action.KeyCode);
                if (combo.Length > 0)
                    await _keyboard.SendCombinationAsync(combo);
                break;
            }
            case ActionType.KeyHold:
                if (action.KeyCode is not null)
                    await _keyboard.HoldKeyAsync(action.KeyCode, action.HoldDurationMs); break;
            case ActionType.KeyRelease:
                if (action.KeyCode is not null)
                    await _keyboard.ReleaseKeyAsync(action.KeyCode); break;
            case ActionType.ExecuteCommand:
                if (action.Command is not null)
                    await ExecuteCommandAsync(action.Command, ct); break;
            case ActionType.PlaySound:
                if (action.SoundPath is not null) PlaySound(action.SoundPath); break;
            case ActionType.ShowNotification:
                ShowNotification(action.NotificationMessage ?? "Screen Action Trigger"); break;
        }
    }

    public async Task EnqueueAsync(TriggerAction action, int priority = 0, DetectionResult? context = null)
    {
        var item = new ActionQueueItem { Action = action, Priority = priority, Context = context };
        await _queueLock.WaitAsync();
        try { _queue.Enqueue(item, -priority); QueueLength++; }
        finally { _queueLock.Release(); }
        _queueSignal.Release();
    }

    public async Task EnqueueBatchAsync(IEnumerable<TriggerAction> actions, int priority = 0, DetectionResult? context = null)
    {
        foreach (var a in actions) await EnqueueAsync(a, priority, context);
    }

    public void CancelAll()
    {
        _queueLock.Wait();
        try
        {
            while (_queue.Count > 0) _queue.Dequeue();
            QueueLength = 0;
        }
        finally { _queueLock.Release(); }

        ReleaseAllInputs();
    }

    public void ReleaseAllInputs()
    {
        _mouse.ReleaseAllButtons();
        _keyboard.ReleaseAllModifiers();
    }

    private async Task ProcessQueueAsync()
    {
        while (!_workerCts.Token.IsCancellationRequested)
        {
            try
            {
                await _queueSignal.WaitAsync(_workerCts.Token);

                ActionQueueItem? item = null;
                await _queueLock.WaitAsync(_workerCts.Token);
                try
                {
                    if (_queue.TryDequeue(out item, out _)) QueueLength--;
                }
                finally { _queueLock.Release(); }

                if (item is { IsCancelled: false })
                    await ExecuteAsync(item.Action, item.Context, _workerCts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Queue worker error"); }
        }
    }

    private static async Task ExecuteCommandAsync(string command, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c {command}")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is not null)
            await proc.WaitForExitAsync(ct);
    }

    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool mciPlaySound(string pszSound, IntPtr hmod, uint fdwSound);
    private const uint SND_FILENAME = 0x00020000;
    private const uint SND_ASYNC    = 0x00000001;

    private static void PlaySound(string soundPath)
    {
        if (File.Exists(soundPath))
            mciPlaySound(soundPath, IntPtr.Zero, SND_FILENAME | SND_ASYNC);
    }

    private static void ShowNotification(string message)
    {
        // Delegate to UI layer via task queue; basic Beep as fallback
        // Beep de notificação
        try { Console.Beep(800, 200); } catch { }
    }

    private static (int x, int y) ResolveClickPoint(TriggerAction action, DetectionResult? context)
    {
        if (action.UseDetectionCoordinates && context is not null)
        {
            if (context.MatchLocation is { } loc)
            {
                var sz = context.MatchSize ?? System.Drawing.Size.Empty;
                return (loc.X + sz.Width / 2, loc.Y + sz.Height / 2);
            }

            if (context.RegionBounds is { } bounds)
                return (bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }

        if (action.TargetX.HasValue && action.TargetY.HasValue)
            return (action.TargetX.Value, action.TargetY.Value);

        return (0, 0);
    }

    private static bool IsMouseAction(ActionType type) => type is
        ActionType.MouseLeftClick or ActionType.MouseRightClick or ActionType.MouseDoubleClick
        or ActionType.MousePress or ActionType.MouseRelease or ActionType.MouseScroll
        or ActionType.MouseFollowPath;

    private static bool HasValidClickPoint(TriggerAction action, DetectionResult? context, int x, int y)
    {
        if (action.TargetX.HasValue && action.TargetY.HasValue)
            return true;

        if (!action.UseDetectionCoordinates || context is null)
            return false;

        return context.MatchLocation is not null || context.RegionBounds is not null;
    }

    public void Dispose()
    {
        CancelAll();
        _workerCts.Cancel();
        _workerCts.Dispose();
        _queueLock.Dispose();
        _queueSignal.Dispose();
    }
}
