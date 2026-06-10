namespace ScreenActionTrigger.Core.Models;

public sealed class ColorPreset
{
    public string Name { get; init; } = string.Empty;
    public string Hex { get; init; } = "#000000";
}

public static class ColorPresets
{
    public static IReadOnlyList<ColorPreset> All { get; } =
    [
        new() { Name = "Vermelho",      Hex = "#FF0000" },
        new() { Name = "Verde",         Hex = "#00FF00" },
        new() { Name = "Verde RPG",     Hex = "#00AA44" },
        new() { Name = "Verde borda",   Hex = "#44CC66" },
        new() { Name = "Azul",          Hex = "#0000FF" },
        new() { Name = "Amarelo",       Hex = "#FFFF00" },
        new() { Name = "Laranja",       Hex = "#FF8800" },
        new() { Name = "Roxo",          Hex = "#AA00FF" },
        new() { Name = "Ciano",         Hex = "#00FFFF" },
        new() { Name = "Rosa",          Hex = "#FF00AA" },
        new() { Name = "Branco",        Hex = "#FFFFFF" },
        new() { Name = "Preto",         Hex = "#000000" },
        new() { Name = "Cinza",         Hex = "#808080" },
        new() { Name = "Cinza borda",   Hex = "#AAAAAA" },
        new() { Name = "Cinza escuro",  Hex = "#666666" },
        new() { Name = "Verde-limão",   Hex = "#88FF00" },
        new() { Name = "Dourado",       Hex = "#FFD700" },
        new() { Name = "Marrom",        Hex = "#8B4513" },
        new() { Name = "Vida (verde)",  Hex = "#00CC44" },
        new() { Name = "Alerta",        Hex = "#FF4444" },
    ];
}
