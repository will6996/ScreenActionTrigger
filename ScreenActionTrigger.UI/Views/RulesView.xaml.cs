using System.Windows.Controls;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.UI.Controls;
using ScreenActionTrigger.UI.ViewModels;

namespace ScreenActionTrigger.UI.Views;

public partial class RulesView : UserControl
{
    private RulesViewModel? _vm;

    public RulesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.ColorPickRequested       -= OnColorPickRequested;
                _vm.ExtraColorPickRequested  -= OnExtraColorPickRequested;
                _vm.PathRecordingRequested   -= OnPathRecordingRequested;
            }

            _vm = DataContext as RulesViewModel;

            if (_vm is not null)
            {
                _vm.ColorPickRequested      += OnColorPickRequested;
                _vm.ExtraColorPickRequested += OnExtraColorPickRequested;
                _vm.PathRecordingRequested  += OnPathRecordingRequested;
            }
        };
    }

    private void OnColorPickRequested(object? sender, RuleCondition condition)
    {
        var overlay = new ColorPickerOverlay();
        overlay.ColorSelected += (_, hex) => _vm?.ApplyPickedColor(condition, hex);
        overlay.ShowDialog();
    }

    private void OnExtraColorPickRequested(object? sender, EventArgs e)
    {
        var overlay = new ColorPickerOverlay();
        overlay.ColorSelected += (_, hex) => _vm?.ApplyPickedExtraColor(hex);
        overlay.ShowDialog();
    }

    private void OnPathRecordingRequested(object? sender, TriggerAction action)
    {
        var overlay = new PathRecorderOverlay();
        overlay.PathRecorded += (_, points) => _vm?.ApplyRecordedPath(action, points);
        overlay.ShowDialog();
    }
}
