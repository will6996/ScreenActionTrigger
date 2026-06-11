using System.Windows;
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

    private void SequenceContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not ContextMenu menu) return;

            _vm ??= DataContext as SequencesViewModel;
            if (_vm is null) return;

            var sequence = ResolveContextSequence(menu);
            if (sequence is not null)
                _vm.SelectedSequence = sequence;

            menu.DataContext = _vm;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SequenceContextMenu: {ex}");
            if (sender is ContextMenu m)
                m.IsOpen = false;
        }
    }

    private static RuleSequence? ResolveContextSequence(ContextMenu menu)
    {
        if (menu.PlacementTarget is ListViewItem lvi)
            return lvi.DataContext as RuleSequence;

        if (menu.PlacementTarget is ListView list)
            return list.SelectedItem as RuleSequence;

        return null;
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
