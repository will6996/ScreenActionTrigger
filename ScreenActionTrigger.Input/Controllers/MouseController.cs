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
        await SendClickAsync(x, y, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP);
    }

    public async Task ClickRightAsync(int x, int y, int delayMs = 0)
    {
        if (delayMs > 0) await Task.Delay(delayMs);
        await SendClickAsync(x, y, MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP);
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
        SendMouseFlag(ButtonDownFlag(btn));
    }

    public async Task ReleaseAsync(int x, int y, Core.Models.MouseButton btn)
    {
        MoveTo(x, y);
        await Task.Delay(10);
        SendMouseFlag(ButtonUpFlag(btn));
    }

    public void ReleaseAllButtons()
    {
        SendMouseFlag(MOUSEEVENTF_LEFTUP);
        SendMouseFlag(MOUSEEVENTF_RIGHTUP);
        SendMouseFlag(MOUSEEVENTF_MIDDLEUP);
    }

    public async Task FollowPathAsync(
        IReadOnlyList<Core.Models.PathPoint> points,
        int stepDelayMs = 30,
        bool clickAtEnd = false,
        CancellationToken ct = default)
    {
        if (points.Count == 0) return;

        try
        {
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
        finally
        {
            ReleaseAllButtons();
        }
    }

    public async Task ScrollAsync(int x, int y, int amount)
    {
        MoveTo(x, y);
        await Task.Delay(20);
        SendMouseInput(MOUSEEVENTF_WHEEL, x, y, (uint)(amount * WHEEL_DELTA));
    }

    private async Task SendClickAsync(int x, int y, uint downFlag, uint upFlag)
    {
        try
        {
            MoveTo(x, y);
            await Task.Delay(15);
            SendMouseFlag(downFlag);
            await Task.Delay(15);
            SendMouseFlag(upFlag);
        }
        finally
        {
            ReleaseAllButtons();
        }
    }

    private static void MoveTo(int x, int y)
    {
        SetCursorPos(x, y);
        SendMouseInput(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, x, y);
    }

    private static void SendMouseFlag(uint flags)
    {
        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } }
            }
        };
        SendInputChecked(inputs);
    }

    private static void SendMouseInput(uint flags, int x, int y, uint data = 0)
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
                    dwFlags = flags
                }}
            }
        };
        SendInputChecked(inputs);
    }

    private static void SendInputChecked(INPUT[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            throw new InvalidOperationException($"SendInput falhou (enviados {sent}/{inputs.Length}, Win32={Marshal.GetLastWin32Error()})");
    }

    private static uint ButtonDownFlag(Core.Models.MouseButton btn) => btn switch
    {
        Core.Models.MouseButton.Right  => MOUSEEVENTF_RIGHTDOWN,
        Core.Models.MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
        _                              => MOUSEEVENTF_LEFTDOWN
    };

    private static uint ButtonUpFlag(Core.Models.MouseButton btn) => btn switch
    {
        Core.Models.MouseButton.Right  => MOUSEEVENTF_RIGHTUP,
        Core.Models.MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
        _                              => MOUSEEVENTF_LEFTUP
    };
}
