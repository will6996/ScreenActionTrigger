using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Overlay.ViewModels;
using ScreenActionTrigger.Overlay.Views;

namespace ScreenActionTrigger.Overlay.Services;

public sealed class OverlayService : IOverlayService
{
    private readonly ILogger<OverlayService> _logger;
    private OverlayWindow? _window;
    private OverlayViewModel? _viewModel;

    public bool IsVisible => _window?.IsVisible == true;
    public bool IsEnabled { get; set; } = true;

    public OverlayService(ILogger<OverlayService> logger) => _logger = logger;

    public void Show()
    {
        if (!IsEnabled) return;
        RunOnUiThread(() =>
        {
            if (_window is null)
            {
                _viewModel = new OverlayViewModel();
                _window = new OverlayWindow(_viewModel);
            }
            _window.Show();
        });
    }

    public void Hide() => RunOnUiThread(() => _window?.Hide());

    public void Toggle()
    {
        if (IsVisible) Hide(); else Show();
    }

    public void UpdateRegions(IEnumerable<MonitoredRegion> regions) =>
        RunOnUiThread(() => _viewModel?.UpdateRegions(regions));

    public void ShowDetection(DetectionResult result, MonitoredRegion region)
    {
        if (!IsEnabled || !IsVisible) return;
        RunOnUiThread(() => _viewModel?.AddDetection(result, region));
    }

    public void ShowRuleTriggered(VisualRule rule, DetectionResult result)
    {
        if (!IsEnabled || !IsVisible) return;
        // Get the region indirectly through result
        RunOnUiThread(() =>
        {
            if (_viewModel is null) return;
            var fakeRegion = new MonitoredRegion
            {
                Id = result.RegionId,
                Name = result.RegionName ?? "Region",
                X = result.MatchLocation?.X ?? 0,
                Y = result.MatchLocation?.Y ?? 0,
                Width = result.MatchSize?.Width ?? 0,
                Height = result.MatchSize?.Height ?? 0
            };
            _viewModel.AddDetection(result, fakeRegion, rule);
        });
    }

    public void ClearDetections() => RunOnUiThread(() => _viewModel?.ClearDetections());

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher is { } d)
            d.BeginInvoke(action, DispatcherPriority.Normal);
        else
            action();
    }
}
