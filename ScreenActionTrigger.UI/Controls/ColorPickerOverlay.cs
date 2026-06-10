using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenActionTrigger.UI.Controls;

/// <summary>Overlay de tela inteira para capturar a cor de um pixel com o mouse.</summary>
public sealed class ColorPickerOverlay : Window
{
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")]  private static extern uint GetPixel(IntPtr hdc, int x, int y);

    private readonly TextBlock _preview;
    private readonly Border _swatch;

    public string? SelectedColorHex { get; private set; }

    public event EventHandler<string>? ColorSelected;

    public ColorPickerOverlay()
    {
        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        Background         = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        Topmost            = true;
        Left = Top         = 0;
        Width              = SystemParameters.PrimaryScreenWidth;
        Height             = SystemParameters.PrimaryScreenHeight;
        Cursor             = Cursors.Cross;
        ShowInTaskbar      = false;

        var root = new Grid { Background = Brushes.Transparent };

        var panel = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            Margin              = new Thickness(20),
            VerticalAlignment   = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background          = new SolidColorBrush(Color.FromArgb(200, 20, 20, 35))
        };

        _swatch = new Border
        {
            Width  = 28,
            Height = 28,
            Margin = new Thickness(0, 0, 10, 0),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4)
        };

        _preview = new TextBlock
        {
            Foreground  = Brushes.White,
            FontFamily  = new FontFamily("Consolas"),
            FontSize    = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Text        = "Clique para capturar cor  •  ESC cancela"
        };

        panel.Children.Add(_swatch);
        panel.Children.Add(_preview);
        root.Children.Add(panel);
        Content = root;

        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnMouseDown;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = PointToScreen(e.GetPosition(this));
        var hex = GetScreenColorHex((int)pos.X, (int)pos.Y);
        _preview.Text = $"{hex}  —  clique para confirmar  •  ESC cancela";
        try
        {
            _swatch.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hex)!);
        }
        catch { }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = PointToScreen(e.GetPosition(this));
        SelectedColorHex = GetScreenColorHex((int)pos.X, (int)pos.Y);
        ColorSelected?.Invoke(this, SelectedColorHex);
        DialogResult = true;
        Close();
    }

    public static string GetScreenColorHex(int x, int y)
    {
        var hdc   = GetDC(IntPtr.Zero);
        var pixel = GetPixel(hdc, x, y);
        ReleaseDC(IntPtr.Zero, hdc);

        var r = (int)(pixel & 0xFF);
        var g = (int)((pixel >> 8) & 0xFF);
        var b = (int)((pixel >> 16) & 0xFF);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
