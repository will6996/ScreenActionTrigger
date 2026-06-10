using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool   _overlayEnabled      = true;
    [ObservableProperty] private int    _captureIntervalMs   = 100;
    [ObservableProperty] private int    _maxParallelRegions  = 10;
    [ObservableProperty] private bool   _logDetections       = true;
    [ObservableProperty] private bool   _logActions          = true;
    [ObservableProperty] private bool   _grayscaleProcessing = true;
    [ObservableProperty] private bool   _startMinimized;
    [ObservableProperty] private bool   _minimizeToTray      = true;
    [ObservableProperty] private string _hotkeyStartStop     = "F9";
    [ObservableProperty] private string _hotkeyPause         = "F10";
    [ObservableProperty] private int    _maxMonitoringEntries = 500;

    public AppSettings Settings => new()
    {
        OverlayEnabled       = OverlayEnabled,
        CaptureIntervalMs    = CaptureIntervalMs,
        MaxParallelRegions   = MaxParallelRegions,
        LogDetections        = LogDetections,
        LogActions           = LogActions,
        GrayscaleProcessing  = GrayscaleProcessing,
        StartMinimized       = StartMinimized,
        MinimizeToTray       = MinimizeToTray,
        HotkeyStartStop      = HotkeyStartStop,
        HotkeyPause          = HotkeyPause,
        MaxMonitoringEntries = MaxMonitoringEntries
    };

    public void SetProfile(ExecutionProfile profile)
    {
        var s = profile.Settings;
        OverlayEnabled       = s.OverlayEnabled;
        CaptureIntervalMs    = s.CaptureIntervalMs;
        MaxParallelRegions   = s.MaxParallelRegions;
        LogDetections        = s.LogDetections;
        LogActions           = s.LogActions;
        GrayscaleProcessing  = s.GrayscaleProcessing;
        StartMinimized       = s.StartMinimized;
        MinimizeToTray       = s.MinimizeToTray;
        HotkeyStartStop      = s.HotkeyStartStop;
        HotkeyPause          = s.HotkeyPause;
        MaxMonitoringEntries = s.MaxMonitoringEntries;
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        OverlayEnabled       = true;
        CaptureIntervalMs    = 100;
        MaxParallelRegions   = 10;
        LogDetections        = true;
        LogActions           = true;
        GrayscaleProcessing  = true;
        StartMinimized       = false;
        MinimizeToTray       = true;
        HotkeyStartStop      = "F9";
        HotkeyPause          = "F10";
        MaxMonitoringEntries = 500;
    }
}
