namespace ScreenActionTrigger.Input;

public static class KeyCombinationParser
{
    public static string[] Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
