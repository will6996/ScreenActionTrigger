using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenActionTrigger.UI.Infrastructure;

namespace ScreenActionTrigger.UI.Controls;

/// <summary>Overlay de tela inteira para escolher um ponto de clique.</summary>
public sealed class PointPickerOverlay : Window
{
    private readonly TextBlock _hint;

    public System.Drawing.Point? SelectedPoint { get; private set; }

    public event EventHandler<System.Drawing.Point>? PointSelected;

    public PointPickerOverlay()
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

        var canvas = new Canvas { Background = Brushes.Transparent };

        _hint = new TextBlock
        {
            Text       = "Clique no ponto desejado. ESC para cancelar.",
            Foreground = Brushes.White,
            FontSize   = 16,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromArgb(200, 20, 20, 35)),
            Padding    = new Thickness(12, 6, 12, 6)
        };
        Canvas.SetLeft(_hint, 20);
        Canvas.SetTop(_hint, 20);
        canvas.Children.Add(_hint);

        Content = canvas;

        MouseLeftButtonDown += OnClick;
        KeyDown             += OnKeyDown;
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        var dip = e.GetPosition(this);
        var physical = ScreenCoordinateHelper.DipPointToPhysical(dip, this);
        SelectedPoint = physical;

        var marker = new Ellipse
        {
            Width  = 16,
            Height = 16,
            Fill   = new SolidColorBrush(Color.FromArgb(180, 0, 170, 255)),
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        Canvas.SetLeft(marker, dip.X - 8);
        Canvas.SetTop(marker, dip.Y - 8);
        if (Content is Canvas c)
            c.Children.Add(marker);

        _hint.Text = $"Ponto: ({physical.X}, {physical.Y})";
        PointSelected?.Invoke(this, physical);

        Dispatcher.BeginInvoke(new Action(Close), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
