using System.Drawing;
using System.Text.Json.Serialization;

namespace ScreenActionTrigger.Core.Models;

public sealed class MonitoredRegion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nova Região";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 200;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string? GroupName { get; set; }

    [JsonIgnore]
    public Rectangle Bounds => new(X, Y, Width, Height);

    public MonitoredRegion Clone() => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{Name} (cópia)",
        X = X, Y = Y, Width = Width, Height = Height,
        IsEnabled = IsEnabled, Priority = Priority, GroupName = GroupName
    };

    public override string ToString() => $"{Name} [{X},{Y} {Width}x{Height}]";
}
