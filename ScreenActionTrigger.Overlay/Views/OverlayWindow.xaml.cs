using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ScreenActionTrigger.Overlay.Infrastructure;
using ScreenActionTrigger.Overlay.ViewModels;

namespace ScreenActionTrigger.Overlay.Views;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int idx, int value);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int idx);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    private const int GWL_EXSTYLE   = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED    = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    public OverlayViewModel ViewModel { get; }

    public OverlayWindow(OverlayViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        OverlayCoordinateHelper.ConfigureFullScreenWindow(this);

        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Make the window click-through, layered, tool, and no-activate
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

        // Keep topmost
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }
}
