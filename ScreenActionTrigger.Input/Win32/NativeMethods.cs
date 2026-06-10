using System.Runtime.InteropServices;

namespace ScreenActionTrigger.Input.Win32;

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    internal static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    internal static extern short GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll")]
    internal static extern bool Beep(uint dwFreq, uint dwDuration);

    internal const int SM_CXSCREEN        = 0;
    internal const int SM_CYSCREEN        = 1;
    internal const int SM_XVIRTUALSCREEN  = 76;
    internal const int SM_YVIRTUALSCREEN  = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    // Input types
    internal const uint INPUT_MOUSE    = 0;
    internal const uint INPUT_KEYBOARD = 1;

    // Mouse flags
    internal const uint MOUSEEVENTF_MOVE        = 0x0001;
    internal const uint MOUSEEVENTF_LEFTDOWN    = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP      = 0x0004;
    internal const uint MOUSEEVENTF_RIGHTDOWN   = 0x0008;
    internal const uint MOUSEEVENTF_RIGHTUP     = 0x0010;
    internal const uint MOUSEEVENTF_MIDDLEDOWN  = 0x0020;
    internal const uint MOUSEEVENTF_MIDDLEUP    = 0x0040;
    internal const uint MOUSEEVENTF_WHEEL       = 0x0800;
    internal const uint MOUSEEVENTF_ABSOLUTE    = 0x8000;
    internal const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

    // Keyboard flags
    internal const uint KEYEVENTF_KEYUP       = 0x0002;
    internal const uint KEYEVENTF_SCANCODE    = 0x0008;
    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    // Virtual key codes
    internal const ushort VK_SHIFT   = 0x10;
    internal const ushort VK_CONTROL = 0x11;
    internal const ushort VK_MENU    = 0x12; // Alt
    internal const ushort VK_LWIN    = 0x5B;

    internal const int WHEEL_DELTA = 120;

    internal static int VirtualScreenLeft   => GetSystemMetrics(SM_XVIRTUALSCREEN);
    internal static int VirtualScreenTop    => GetSystemMetrics(SM_YVIRTUALSCREEN);
    internal static int VirtualScreenWidth  => GetSystemMetrics(SM_CXVIRTUALSCREEN);
    internal static int VirtualScreenHeight => GetSystemMetrics(SM_CYVIRTUALSCREEN);

    internal static int ToAbsoluteX(int x)
    {
        var w = Math.Max(VirtualScreenWidth - 1, 1);
        return (int)((double)(x - VirtualScreenLeft) / w * 65535 + 0.5);
    }

    internal static int ToAbsoluteY(int y)
    {
        var h = Math.Max(VirtualScreenHeight - 1, 1);
        return (int)((double)(y - VirtualScreenTop) / h * 65535 + 0.5);
    }
}
