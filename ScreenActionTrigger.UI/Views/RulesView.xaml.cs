using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.UI.Controls;
using ScreenActionTrigger.UI.ViewModels;
using TriggerAction = ScreenActionTrigger.Core.Models.TriggerAction;

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
                _vm.ColorPickRequested      -= OnColorPickRequested;
                _vm.ExtraColorPickRequested -= OnExtraColorPickRequested;
                _vm.PathRecordingRequested  -= OnPathRecordingRequested;
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

    private void RulesList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is not ListView) return;

            var item = FindParent<ListViewItem>(e.OriginalSource as DependencyObject);
            if (item is null) return;

            item.IsSelected = true;
            item.Focus();
            if (_vm is not null && item.DataContext is VisualRule rule)
                _vm.SelectedRule = rule;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RulesList right-click: {ex}");
        }
    }

    private void RuleContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not ContextMenu menu) return;

            _vm ??= DataContext as RulesViewModel;
            if (_vm is null) return;

            var rule = ResolveContextRule(menu);
            if (rule is not null)
                _vm.SelectedRule = rule;

            menu.DataContext = _vm;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RuleContextMenu: {ex}");
            if (sender is ContextMenu m)
                m.IsOpen = false;
        }
    }

    private static VisualRule? ResolveContextRule(ContextMenu menu)
    {
        if (menu.PlacementTarget is ListViewItem lvi)
            return lvi.DataContext as VisualRule;

        if (menu.PlacementTarget is ListView list)
            return list.SelectedItem as VisualRule;

        return null;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T found) return found;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
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
