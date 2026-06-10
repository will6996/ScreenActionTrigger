using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenActionTrigger.Overlay.Infrastructure;

/// <summary>Converte pixels físicos (GDI) para DIPs do overlay WPF.</summary>
internal static class OverlayCoordinateHelper
{
    private const int SmXVirtualScreen  = 76;
    private const int SmYVirtualScreen  = 77;

    private static readonly double ScaleX;
    private static readonly double ScaleY;
    private static readonly int VirtualScreenLeftPhysical;
    private static readonly int VirtualScreenTopPhysical;

    static OverlayCoordinateHelper()
    {
        using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        ScaleX = g.DpiX / 96.0;
        ScaleY = g.DpiY / 96.0;
        VirtualScreenLeftPhysical = GetSystemMetrics(SmXVirtualScreen);
        VirtualScreenTopPhysical  = GetSystemMetrics(SmYVirtualScreen);
    }

    public static double ToDipX(int physicalX)
        => (physicalX - VirtualScreenLeftPhysical) / ScaleX;

    public static double ToDipY(int physicalY)
        => (physicalY - VirtualScreenTopPhysical) / ScaleY;

    public static double ToDipWidth(int physicalWidth)  => physicalWidth / ScaleX;
    public static double ToDipHeight(int physicalHeight) => physicalHeight / ScaleY;

    public static void ConfigureFullScreenWindow(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left   = SystemParameters.VirtualScreenLeft;
        window.Top    = SystemParameters.VirtualScreenTop;
        window.Width  = SystemParameters.VirtualScreenWidth;
        window.Height = SystemParameters.VirtualScreenHeight;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
