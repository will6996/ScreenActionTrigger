using System.Windows.Controls;
using ScreenActionTrigger.UI.ViewModels;
using ScreenActionTrigger.UI.Controls;

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

    private void OnRegionSelectionRequested(object? sender, EventArgs e)
    {
        var overlay = new RegionSelectorOverlay();
        overlay.RegionSelected += (_, rect) =>
            _vm?.ApplySelectedRegion(rect.X, rect.Y, rect.Width, rect.Height);
        overlay.ShowDialog();
    }
}
