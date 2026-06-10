using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.UI.Controls;
using ScreenActionTrigger.UI.ViewModels;

namespace ScreenActionTrigger.UI.Views;

public partial class RegionsView : UserControl
{
    private RegionsViewModel? _vm;

    public RegionsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
                _vm.RegionSelectionRequested -= OnRegionSelectionRequested;

            _vm = DataContext as RegionsViewModel;

            if (_vm is not null)
                _vm.RegionSelectionRequested += OnRegionSelectionRequested;
        };
    }

    private void RegionsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is not ListView) return;

            var item = FindParent<ListViewItem>(e.OriginalSource as DependencyObject);
            if (item is null) return;

            item.IsSelected = true;
            item.Focus();
            if (_vm is not null && item.DataContext is MonitoredRegion region)
                _vm.SelectedRegion = region;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RegionsList right-click: {ex}");
        }
    }

    private void RegionContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not ContextMenu menu) return;

            _vm ??= DataContext as RegionsViewModel;
            if (_vm is null) return;

            if (menu.PlacementTarget is ListViewItem lvi &&
                lvi.DataContext is MonitoredRegion region)
                _vm.SelectedRegion = region;

            menu.DataContext = _vm;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RegionContextMenu: {ex}");
            if (sender is ContextMenu m)
                m.IsOpen = false;
        }
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

    private void RegionEditor_LostFocus(object sender, RoutedEventArgs e)
        => _vm?.RequestOverlayPreview();

    private void OnRegionSelectionRequested(object? sender, EventArgs e)
    {
        var overlay = new RegionSelectorOverlay();
        overlay.RegionSelected += (_, rect) =>
            _vm?.ApplySelectedRegion(rect.X, rect.Y, rect.Width, rect.Height);
        overlay.ShowDialog();
    }
}
