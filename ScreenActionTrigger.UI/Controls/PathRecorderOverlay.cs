using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Controls;

/// <summary>Grava um caminho de mouse clicando pontos na tela.</summary>
public sealed class PathRecorderOverlay : Window
{
    private readonly Canvas _canvas;
    private readonly TextBlock _hint;
    private readonly Polyline _pathLine;
    private readonly List<PathPoint> _points = new();
    private PathPoint? _lastPoint;

    public IReadOnlyList<PathPoint> RecordedPoints => _points;

    public event EventHandler<IReadOnlyList<PathPoint>>? PathRecorded;

    public PathRecorderOverlay()
    {
        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        Background         = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
        Topmost            = true;
        Left = Top         = 0;
        Width              = SystemParameters.PrimaryScreenWidth;
        Height             = SystemParameters.PrimaryScreenHeight;
        Cursor             = Cursors.Cross;
        ShowInTaskbar      = false;

        _canvas = new Canvas { Background = Brushes.Transparent };

        _pathLine = new Polyline
        {
            Stroke          = new SolidColorBrush(Color.FromRgb(0, 170, 255)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        _canvas.Children.Add(_pathLine);

        _hint = new TextBlock
        {
            Text       = "Clique esquerdo: adicionar ponto  •  Clique direito ou ENTER: concluir  •  ESC: cancelar",
            Foreground = Brushes.White,
            FontSize   = 15,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            Padding    = new Thickness(12, 6, 12, 6)
        };
        Canvas.SetLeft(_hint, 20);
        Canvas.SetTop(_hint, 20);
        _canvas.Children.Add(_hint);

        Content = _canvas;

        MouseLeftButtonDown  += OnLeftClick;
        MouseRightButtonDown += OnFinish;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { DialogResult = false; Close(); }
            if (e.Key == Key.Enter)  OnFinish(this, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Right));
        };
    }

    private void OnLeftClick(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(_canvas);
        var x   = (int)(Left + pos.X);
        var y   = (int)(Top + pos.Y);

        int delay = 50;
        if (_lastPoint is not null)
            delay = Math.Clamp(
                (int)Math.Sqrt(Math.Pow(x - _lastPoint.X, 2) + Math.Pow(y - _lastPoint.Y, 2)) / 4,
                30, 300);

        var pt = new PathPoint { X = x, Y = y, DelayMs = _points.Count == 0 ? 0 : delay };
        _points.Add(pt);
        _lastPoint = pt;

        _pathLine.Points.Add(pos);

        var dot = new Ellipse
        {
            Width  = 10,
            Height = 10,
            Fill   = new SolidColorBrush(Color.FromRgb(0, 255, 136)),
            Stroke = Brushes.White,
            StrokeThickness = 1
        };
        Canvas.SetLeft(dot, pos.X - 5);
        Canvas.SetTop(dot, pos.Y - 5);
        _canvas.Children.Add(dot);

        _hint.Text = $"{_points.Count} ponto(s)  —  clique direito ou ENTER para concluir";
    }

    private void OnFinish(object? sender, MouseButtonEventArgs e)
    {
        if (_points.Count == 0) return;
        PathRecorded?.Invoke(this, _points);
        DialogResult = true;
        Close();
    }
}
