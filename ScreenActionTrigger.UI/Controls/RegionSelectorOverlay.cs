using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenActionTrigger.UI.Infrastructure;

namespace ScreenActionTrigger.UI.Controls;

/// <summary>
/// Full-screen transparent overlay that lets the user drag-select a region.
/// </summary>
public sealed class RegionSelectorOverlay : Window
{
    private Point _startPoint;
    private Rectangle? _selectionRect;
    private Canvas? _canvas;
    private bool _isDragging;

    public System.Drawing.Rectangle SelectedRegion { get; private set; }

    public event EventHandler<System.Drawing.Rectangle>? RegionSelected;

    public RegionSelectorOverlay()
    {
        WindowStyle       = WindowStyle.None;
        AllowsTransparency = true;
        Background        = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
        Topmost           = true;
        Left = Top        = 0;
        Width             = SystemParameters.PrimaryScreenWidth;
        Height            = SystemParameters.PrimaryScreenHeight;
        Cursor            = Cursors.Cross;
        ShowInTaskbar     = false;

        _canvas = new Canvas { Background = Brushes.Transparent };
        Content = _canvas;

        var hint = new TextBlock
        {
            Text       = "Clique e arraste para selecionar a região. ESC para cancelar.",
            Foreground = Brushes.White,
            FontSize   = 16,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            Padding    = new Thickness(12, 6, 12, 6)
        };
        Canvas.SetLeft(hint, 20);
        Canvas.SetTop(hint, 20);
        _canvas.Children.Add(hint);

        MouseLeftButtonDown += OnMouseDown;
        MouseMove           += OnMouseMove;
        MouseLeftButtonUp   += OnMouseUp;
        KeyDown             += OnKeyDown;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(_canvas);
        _isDragging = true;

        _selectionRect = new Rectangle
        {
            Stroke          = new SolidColorBrush(Color.FromRgb(0, 170, 255)),
            StrokeThickness = 2,
            Fill            = new SolidColorBrush(Color.FromArgb(40, 0, 170, 255)),
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        _canvas!.Children.Add(_selectionRect);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _selectionRect is null) return;
        var cur = e.GetPosition(_canvas);

        double x = Math.Min(_startPoint.X, cur.X);
        double y = Math.Min(_startPoint.Y, cur.Y);
        double w = Math.Abs(cur.X - _startPoint.X);
        double h = Math.Abs(cur.Y - _startPoint.Y);

        Canvas.SetLeft(_selectionRect, x);
        Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width  = w;
        _selectionRect.Height = h;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        if (_selectionRect is null) { Close(); return; }

        double x = Canvas.GetLeft(_selectionRect);
        double y = Canvas.GetTop(_selectionRect);
        double w = _selectionRect.Width;
        double h = _selectionRect.Height;

        if (w >= 5 && h >= 5)
        {
            SelectedRegion = ScreenCoordinateHelper.DipRectToPhysical(
                new Rect(x, y, w, h), this);
            RegionSelected?.Invoke(this, SelectedRegion);
            DialogResult = true;
        }
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; Close(); }
    }
}
