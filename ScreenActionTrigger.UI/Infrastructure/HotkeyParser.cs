namespace ScreenActionTrigger.UI.Infrastructure;

/// <summary>Converte strings como "F9" ou "CTRL+SHIFT+A" para RegisterHotKey.</summary>
public static class HotkeyParser
{
    private const uint ModAlt     = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift   = 0x0004;
    private const uint ModWin     = 0x0008;

    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers  = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!TryParseModifier(parts[i], out var mod))
                return false;
            modifiers |= mod;
        }

        return TryParseVirtualKey(parts[^1], out virtualKey);
    }

    private static bool TryParseModifier(string token, out uint modifier)
    {
        modifier = token.ToUpperInvariant() switch
        {
            "ALT" or "MENU"           => ModAlt,
            "CTRL" or "CONTROL"       => ModControl,
            "SHIFT"                   => ModShift,
            "WIN" or "WINDOWS" or "LWIN" => ModWin,
            _ => 0
        };
        return modifier != 0;
    }

    private static bool TryParseVirtualKey(string token, out uint vk)
    {
        vk = 0;
        var key = token.ToUpperInvariant();

        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z')
        {
            vk = key[0];
            return true;
        }

        if (key.Length == 1 && key[0] is >= '0' and <= '9')
        {
            vk = key[0];
            return true;
        }

        if (key.StartsWith('F') && int.TryParse(key[1..], out var fn) && fn is >= 1 and <= 24)
        {
            vk = (uint)(0x6F + fn);
            return true;
        }

        vk = key switch
        {
            "ENTER" or "RETURN" => 0x0D,
            "ESCAPE" or "ESC"   => 0x1B,
            "SPACE"             => 0x20,
            "TAB"               => 0x09,
            "BACKSPACE"         => 0x08,
            "DELETE"            => 0x2E,
            "INSERT"            => 0x2D,
            "HOME"              => 0x24,
            "END"               => 0x23,
            "PAGEUP"            => 0x21,
            "PAGEDOWN"          => 0x22,
            "LEFT"              => 0x25,
            "RIGHT"             => 0x27,
            "UP"                => 0x26,
            "DOWN"              => 0x28,
            "PRINTSCREEN"       => 0x2C,
            "PAUSE"             => 0x13,
            "NUMLOCK"           => 0x90,
            "CAPSLOCK"          => 0x14,
            _                   => 0
        };
        return vk != 0;
    }
}
