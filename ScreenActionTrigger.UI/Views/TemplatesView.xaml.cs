using System.Windows.Controls;
using ScreenActionTrigger.UI.ViewModels;
using ScreenActionTrigger.UI.Controls;
using ScreenActionTrigger.Core.Interfaces;

namespace ScreenActionTrigger.UI.Views;

public partial class TemplatesView : UserControl
{
    private TemplatesViewModel? _vm;
    private readonly IScreenCaptureService? _capture;

    public TemplatesView() => InitializeComponent();

    public TemplatesView(IScreenCaptureService capture) : this()
    {
        _capture = capture;
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
                _vm.TemplateCaptureRequested -= OnCaptureRequested;
            _vm = DataContext as TemplatesViewModel;
            if (_vm is not null)
                _vm.TemplateCaptureRequested += OnCaptureRequested;
        };
    }

    private async void OnCaptureRequested(object? sender, EventArgs e)
    {
        if (_vm is null || _capture is null) return;

        var selector = new RegionSelectorOverlay();
        bool? result = selector.ShowDialog();
        if (result != true) { _vm.IsCapturing = false; return; }

        var rect = selector.SelectedRegion;
        var imageData = await _capture.CaptureAreaAsync(rect.X, rect.Y, rect.Width, rect.Height);
        if (imageData is not null)
            await _vm.ApplyCapturedImageAsync(imageData, $"Template_{DateTime.Now:HHmmss}");
    }
}
