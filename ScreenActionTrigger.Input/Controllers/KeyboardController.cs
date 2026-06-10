using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Input.Win32;
using static ScreenActionTrigger.Input.Win32.NativeMethods;

namespace ScreenActionTrigger.Input.Controllers;

public sealed class KeyboardController
{
    private readonly ILogger<KeyboardController> _logger;
    private static readonly Dictionary<string, ushort> _keyMap = BuildKeyMap();

    public KeyboardController(ILogger<KeyboardController> logger) => _logger = logger;

    public async Task PressKeyAsync(string keyName, int delayMs = 0)
    {
        if (delayMs > 0) await Task.Delay(delayMs);
        if (!_keyMap.TryGetValue(keyName.ToUpperInvariant(), out var vk))
        {
            _logger.LogWarning("Unknown key: {Key}", keyName);
            return;
        }
        KeyDown(vk);
        await Task.Delay(30);
        KeyUp(vk);
    }

    public async Task HoldKeyAsync(string keyName, int holdMs)
    {
        if (!_keyMap.TryGetValue(keyName.ToUpperInvariant(), out var vk)) return;
        KeyDown(vk);
        await Task.Delay(Math.Max(holdMs, 10));
        KeyUp(vk);
    }

    public async Task ReleaseKeyAsync(string keyName)
    {
        if (!_keyMap.TryGetValue(keyName.ToUpperInvariant(), out var vk)) return;
        KeyUp(vk);
        await Task.CompletedTask;
    }

    public async Task SendCombinationAsync(string[] keys, int delayMs = 0)
    {
        if (delayMs > 0) await Task.Delay(delayMs);

        var vkeys = keys
            .Select(k => _keyMap.TryGetValue(k.ToUpperInvariant(), out var v) ? v : (ushort)0)
            .Where(v => v != 0)
            .ToArray();

        try
        {
            foreach (var vk in vkeys) KeyDown(vk);
            await Task.Delay(50);
            foreach (var vk in vkeys.Reverse()) KeyUp(vk);
        }
        finally
        {
            ReleaseAllModifiers();
        }
    }

    public void ReleaseAllModifiers()
    {
        KeyUp(VK_SHIFT);
        KeyUp(VK_CONTROL);
        KeyUp(VK_MENU);
        KeyUp(VK_LWIN);
    }

    private static void KeyDown(ushort vk)
    {
        var inputs = new INPUT[] { MakeKey(vk, 0) };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void KeyUp(ushort vk)
    {
        var inputs = new INPUT[] { MakeKey(vk, KEYEVENTF_KEYUP) };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKey(ushort vk, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags } }
    };

    private static Dictionary<string, ushort> BuildKeyMap()
    {
        var map = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

        // Letters
        for (char c = 'A'; c <= 'Z'; c++)
            map[c.ToString()] = (ushort)c;

        // Numbers
        for (char c = '0'; c <= '9'; c++)
            map[c.ToString()] = (ushort)c;

        // Function keys F1-F24
        for (int i = 1; i <= 24; i++)
            map[$"F{i}"] = (ushort)(0x6F + i);

        // Special keys
        map["ENTER"]     = 0x0D; map["RETURN"]   = 0x0D;
        map["ESCAPE"]    = 0x1B; map["ESC"]       = 0x1B;
        map["SPACE"]     = 0x20; map["TAB"]       = 0x09;
        map["BACKSPACE"] = 0x08; map["DELETE"]    = 0x2E;
        map["INSERT"]    = 0x2D; map["HOME"]      = 0x24;
        map["END"]       = 0x23; map["PAGEUP"]    = 0x21;
        map["PAGEDOWN"]  = 0x22; map["LEFT"]      = 0x25;
        map["RIGHT"]     = 0x27; map["UP"]        = 0x26;
        map["DOWN"]      = 0x28; map["PRINTSCREEN"] = 0x2C;
        map["SCROLL"]    = 0x91; map["PAUSE"]     = 0x13;
        map["NUMLOCK"]   = 0x90; map["CAPSLOCK"]  = 0x14;

        // Modifiers
        map["SHIFT"]     = 0x10; map["CTRL"]      = 0x11;
        map["CONTROL"]   = 0x11; map["ALT"]       = 0x12;
        map["WIN"]       = 0x5B;

        // Numpad
        map["NUMPAD0"] = 0x60; map["NUMPAD1"] = 0x61;
        map["NUMPAD2"] = 0x62; map["NUMPAD3"] = 0x63;
        map["NUMPAD4"] = 0x64; map["NUMPAD5"] = 0x65;
        map["NUMPAD6"] = 0x66; map["NUMPAD7"] = 0x67;
        map["NUMPAD8"] = 0x68; map["NUMPAD9"] = 0x69;
        map["MULTIPLY"] = 0x6A; map["ADD"]    = 0x6B;
        map["SUBTRACT"] = 0x6D; map["DECIMAL"] = 0x6E;
        map["DIVIDE"]   = 0x6F;

        return map;
    }
}
