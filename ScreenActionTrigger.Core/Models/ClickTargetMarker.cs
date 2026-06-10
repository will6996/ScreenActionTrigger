namespace ScreenActionTrigger.Core.Models;

/// <summary>Marcador visual de um ponto de clique configurado pelo usuário.</summary>
public sealed class ClickTargetMarker
{
    public int X { get; init; }
    public int Y { get; init; }
    public string Label { get; init; } = string.Empty;
}
