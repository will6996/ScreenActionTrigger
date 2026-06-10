using System.Windows.Controls;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.UI.Controls;
using ScreenActionTrigger.UI.ViewModels;
using TriggerAction = ScreenActionTrigger.Core.Models.TriggerAction;

namespace ScreenActionTrigger.UI.Views;

public partial class SequencesView : UserControl
{
    private SequencesViewModel? _vm;

    public SequencesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireViewModel();
    }

    private void WireViewModel()
    {
        if (_vm is not null)
        {
            _vm.ColorPickRequested       -= OnColorPickRequested;
            _vm.ClickPointPickRequested  -= OnClickPointPickRequested;
            _vm.PathRecordingRequested   -= OnPathRecordingRequested;
        }

        _vm = DataContext as SequencesViewModel;
        if (_vm is null) return;

        _vm.ColorPickRequested      += OnColorPickRequested;
        _vm.ClickPointPickRequested += OnClickPointPickRequested;
        _vm.PathRecordingRequested  += OnPathRecordingRequested;
    }

    private void OnColorPickRequested(object? sender, RuleCondition condition)
    {
        var overlay = new ColorPickerOverlay();
        overlay.ColorSelected += (_, hex) => _vm?.ApplyPickedColor(condition, hex);
        overlay.ShowDialog();
    }

    private void OnClickPointPickRequested(object? sender, TriggerAction action)
    {
        var overlay = new PointPickerOverlay();
        overlay.PointSelected += (_, pt) => _vm?.ApplyPickedClickPoint(action, pt.X, pt.Y);
        overlay.ShowDialog();
    }

    private void OnPathRecordingRequested(object? sender, TriggerAction action)
    {
        var overlay = new PathRecorderOverlay();
        overlay.PathRecorded += (_, points) => _vm?.ApplyRecordedPath(action, points);
        overlay.ShowDialog();
    }
}
