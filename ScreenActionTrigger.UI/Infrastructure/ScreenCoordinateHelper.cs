using System.Windows;
using System.Windows.Media;

namespace ScreenActionTrigger.UI.Infrastructure;

/// <summary>Converte coordenadas WPF (DIPs) para pixels físicos da tela (GDI/Win32).</summary>
public static class ScreenCoordinateHelper
{
    public static System.Drawing.Rectangle DipRectToPhysical(Rect dipRect, Visual? visual = null)
    {
        var scale = GetScale(visual);
        return new System.Drawing.Rectangle(
            (int)Math.Round(dipRect.X * scale.X),
            (int)Math.Round(dipRect.Y * scale.Y),
            (int)Math.Round(dipRect.Width * scale.X),
            (int)Math.Round(dipRect.Height * scale.Y));
    }

    public static System.Drawing.Point DipPointToPhysical(Point dipPoint, Visual? visual = null)
    {
        var scale = GetScale(visual);
        return new System.Drawing.Point(
            (int)Math.Round(dipPoint.X * scale.X),
            (int)Math.Round(dipPoint.Y * scale.Y));
    }

    private static Vector GetScale(Visual? visual)
    {
        if (visual is not null)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget is not null)
            {
                var m = source.CompositionTarget.TransformToDevice;
                return new Vector(m.M11, m.M22);
            }
        }

        using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        return new Vector(g.DpiX / 96.0, g.DpiY / 96.0);
    }
}
