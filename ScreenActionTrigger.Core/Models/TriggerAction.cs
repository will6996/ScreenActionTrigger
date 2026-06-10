using System.ComponentModel;

namespace ScreenActionTrigger.Core.Models;

public sealed class TriggerAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ActionType Type { get; set; }
    public MouseButton MouseButton { get; set; }
    public string? KeyCode { get; set; }
    public string[] KeyCombination { get; set; } = Array.Empty<string>();
    public string? Command { get; set; }
    public string? SoundPath { get; set; }
    public string? NotificationMessage { get; set; }
    public int DelayBeforeMs { get; set; } = 0;
    public int HoldDurationMs { get; set; } = 0;
    public int ScrollAmount { get; set; } = 3;
    public int? TargetX { get; set; }
    public int? TargetY { get; set; }
    public bool UseDetectionCoordinates { get; set; } = true;
    public int RepeatCount { get; set; } = 1;
    public int RepeatDelayMs { get; set; } = 0;

    // Caminho gravado do mouse
    public List<PathPoint> PathPoints { get; set; } = new();
    public int PathStepDelayMs { get; set; } = 30;
    public bool PathLeftClickAtEnd { get; set; }

    private string ClickTargetSuffix =>
        !UseDetectionCoordinates && TargetX.HasValue && TargetY.HasValue
            ? $" em ({TargetX},{TargetY})"
            : UseDetectionCoordinates ? " (detecção)" : "";

    public string GetDescription() => Type switch
    {
        ActionType.MouseLeftClick    => "Clique Esquerdo" + ClickTargetSuffix,
        ActionType.MouseRightClick   => "Clique Direito" + ClickTargetSuffix,
        ActionType.MouseDoubleClick  => "Clique Duplo" + ClickTargetSuffix,
        ActionType.MousePress        => "Pressionar Mouse",
        ActionType.MouseRelease      => "Soltar Mouse",
        ActionType.MouseScroll       => $"Scroll ({ScrollAmount})",
        ActionType.KeyPress          => $"Tecla: {KeyCode}",
        ActionType.KeyCombination    => $"Combo: {string.Join("+", KeyCombination)}",
        ActionType.KeyHold           => $"Segurar: {KeyCode} ({HoldDurationMs}ms)",
        ActionType.KeyRelease        => $"Soltar: {KeyCode}",
        ActionType.ExecuteCommand    => $"Comando: {Command}",
        ActionType.PlaySound         => $"Som: {SoundPath}",
        ActionType.ShowNotification  => $"Notificação: {NotificationMessage}",
        ActionType.MouseFollowPath   => $"Caminho ({PathPoints.Count} pontos)",
        _ => Type.ToString()
    };
}

public enum ActionType
{
    [Description("Clique Esquerdo")]
    MouseLeftClick,
    [Description("Clique Direito")]
    MouseRightClick,
    [Description("Clique Duplo")]
    MouseDoubleClick,
    [Description("Pressionar Mouse")]
    MousePress,
    [Description("Soltar Mouse")]
    MouseRelease,
    [Description("Scroll do Mouse")]
    MouseScroll,
    [Description("Seguir Caminho Gravado")]
    MouseFollowPath,
    [Description("Pressionar Tecla")]
    KeyPress,
    [Description("Combinação de Teclas")]
    KeyCombination,
    [Description("Segurar Tecla")]
    KeyHold,
    [Description("Soltar Tecla")]
    KeyRelease,
    [Description("Executar Comando")]
    ExecuteCommand,
    [Description("Tocar Som")]
    PlaySound,
    [Description("Exibir Notificação")]
    ShowNotification
}

public enum MouseButton { Left, Right, Middle }
