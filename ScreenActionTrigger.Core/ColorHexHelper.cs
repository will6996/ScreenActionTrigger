namespace ScreenActionTrigger.Core;

public static class ColorHexHelper
{
    public static string Normalize(string input)
    {
        var hex = input.Trim().TrimStart('#');
        return "#" + hex.ToUpperInvariant();
    }

    public static bool IsValid(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var hex = input.Trim().TrimStart('#');
        return hex.Length is 6 or 8
            && hex.All(c => "0123456789ABCDEFabcdef".Contains(c));
    }

    public static bool TryNormalize(string input, out string normalized)
    {
        normalized = string.Empty;
        if (!IsValid(input))
            return false;

        normalized = Normalize(input);
        return true;
    }
}
