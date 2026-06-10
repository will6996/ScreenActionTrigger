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

    public string GetDescription() => Type switch
    {
        ActionType.MouseLeftClick    => "Clique Esquerdo",
        ActionType.MouseRightClick   => "Clique Direito",
        ActionType.MouseDoubleClick  => "Clique Duplo",
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
        _ => Type.ToString()
    };
}

public enum ActionType
{
    MouseLeftClick,
    MouseRightClick,
    MouseDoubleClick,
    MousePress,
    MouseRelease,
    MouseScroll,
    KeyPress,
    KeyCombination,
    KeyHold,
    KeyRelease,
    ExecuteCommand,
    PlaySound,
    ShowNotification
}

public enum MouseButton { Left, Right, Middle }
