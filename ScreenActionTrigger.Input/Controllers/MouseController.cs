using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Input.Win32;
using static ScreenActionTrigger.Input.Win32.NativeMethods;

namespace ScreenActionTrigger.Input.Controllers;

public sealed class MouseController
{
    private readonly ILogger<MouseController> _logger;

    public MouseController(ILogger<MouseController> logger) => _logger = logger;

    public async Task ClickLeftAsync(int x, int y, int delayMs = 0)
    {
        if (delayMs > 0) await Task.Delay(delayMs);
        MoveTo(x, y);
        await Task.Delay(30);
        SendMouseButton(MOUSEEVENTF_LEFTDOWN);
        await Task.Delay(30);
        SendMouseButton(MOUSEEVENTF_LEFTUP);
    }

    public async Task ClickRightAsync(int x, int y, int delayMs = 0)
    {
        if (delayMs > 0) await Task.Delay(delayMs);
        MoveTo(x, y);
        await Task.Delay(30);
        SendMouseButton(MOUSEEVENTF_RIGHTDOWN);
        await Task.Delay(30);
        SendMouseButton(MOUSEEVENTF_RIGHTUP);
    }

    public async Task DoubleClickAsync(int x, int y, int delayMs = 0)
    {
        await ClickLeftAsync(x, y, delayMs);
        await Task.Delay(50);
        await ClickLeftAsync(x, y);
    }

    public async Task PressAsync(int x, int y, Core.Models.MouseButton btn)
    {
        MoveTo(x, y);
        await Task.Delay(20);
        uint flag = btn switch
        {
            Core.Models.MouseButton.Right  => MOUSEEVENTF_RIGHTDOWN,
            Core.Models.MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
            _                              => MOUSEEVENTF_LEFTDOWN
        };
        SendMouseEvent(flag | MOUSEEVENTF_ABSOLUTE, x, y);
    }

    public async Task ReleaseAsync(int x, int y, Core.Models.MouseButton btn)
    {
        uint flag = btn switch
        {
            Core.Models.MouseButton.Right  => MOUSEEVENTF_RIGHTUP,
            Core.Models.MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
            _                              => MOUSEEVENTF_LEFTUP
        };
        SendMouseEvent(flag | MOUSEEVENTF_ABSOLUTE, x, y);
        await Task.CompletedTask;
    }

    public async Task FollowPathAsync(
        IReadOnlyList<Core.Models.PathPoint> points,
        int stepDelayMs = 30,
        bool clickAtEnd = false,
        CancellationToken ct = default)
    {
        if (points.Count == 0) return;

        foreach (var pt in points)
        {
            ct.ThrowIfCancellationRequested();
            if (pt.DelayMs > 0)
                await Task.Delay(pt.DelayMs, ct);

            MoveTo(pt.X, pt.Y);

            if (stepDelayMs > 0)
                await Task.Delay(stepDelayMs, ct);
        }

        if (clickAtEnd)
        {
            var last = points[^1];
            await ClickLeftAsync(last.X, last.Y);
        }
    }

    public async Task ScrollAsync(int x, int y, int amount)
    {
        MoveTo(x, y);
        await Task.Delay(20);
        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                u = new InputUnion { mi = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_WHEEL,
                    mouseData = (uint)(amount * WHEEL_DELTA),
                    dx = ToAbsoluteX(x), dy = ToAbsoluteY(y)
                }}
            }
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void MoveTo(int x, int y)
    {
        SetCursorPos(x, y);
    }

    private static void SendMouseButton(uint flags)
    {
        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } }
            }
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseEvent(uint flags, int x, int y, uint data = 0)
    {
        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                u = new InputUnion { mi = new MOUSEINPUT
                {
                    dx = ToAbsoluteX(x),
                    dy = ToAbsoluteY(y),
                    mouseData = data,
                    dwFlags = flags | MOUSEEVENTF_ABSOLUTE
                }}
            }
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }
}
