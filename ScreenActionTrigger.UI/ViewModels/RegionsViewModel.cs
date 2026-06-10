using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class RegionsViewModel : ObservableObject
{
    public ObservableCollection<MonitoredRegion> Regions { get; } = new();

    [ObservableProperty] private MonitoredRegion? _selectedRegion;
    [ObservableProperty] private string           _filterText = string.Empty;
    [ObservableProperty] private string?          _filterGroup;

    public IEnumerable<string> Groups => Regions
        .Where(r => r.GroupName is not null)
        .Select(r => r.GroupName!)
        .Distinct()
        .OrderBy(g => g);

    public void SetProfile(ExecutionProfile profile)
    {
        Regions.Clear();
        foreach (var r in profile.Regions) Regions.Add(r);
    }

    [RelayCommand]
    private void AddRegion()
    {
        var region = new MonitoredRegion
        {
            Name   = $"Região {Regions.Count + 1}",
            X      = 100, Y = 100,
            Width  = 200, Height = 200
        };
        Regions.Add(region);
        SelectedRegion = region;
    }

    [RelayCommand]
    private void DuplicateRegion(MonitoredRegion? region)
    {
        if (region is null) return;
        var clone = region.Clone();
        clone.X += 20; clone.Y += 20;
        Regions.Add(clone);
        SelectedRegion = clone;
    }

    [RelayCommand]
    private void RemoveRegion(MonitoredRegion? region)
    {
        if (region is null) return;
        Regions.Remove(region);
        if (SelectedRegion == region) SelectedRegion = null;
    }

    [RelayCommand]
    private void MoveRegionUp(MonitoredRegion? region)
    {
        if (region is null) return;
        var idx = Regions.IndexOf(region);
        if (idx > 0) Regions.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveRegionDown(MonitoredRegion? region)
    {
        if (region is null) return;
        var idx = Regions.IndexOf(region);
        if (idx >= 0 && idx < Regions.Count - 1) Regions.Move(idx, idx + 1);
    }

    [RelayCommand]
    private void ToggleRegion(MonitoredRegion? region)
    {
        if (region is null) return;
        region.IsEnabled = !region.IsEnabled;
        OnPropertyChanged(nameof(Regions));
    }

    [RelayCommand]
    private void SetGroup(string? groupName)
    {
        if (SelectedRegion is null) return;
        SelectedRegion.GroupName = groupName;
    }

    [RelayCommand]
    private void SelectFromScreen()
    {
        // Signal to UI layer to enter region-selection mode
        RegionSelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RegionSelectionRequested;

    public void ApplySelectedRegion(int x, int y, int w, int h)
    {
        if (SelectedRegion is null) AddRegion();
        if (SelectedRegion is null) return;
        SelectedRegion.X = x; SelectedRegion.Y = y;
        SelectedRegion.Width = w; SelectedRegion.Height = h;
        OnPropertyChanged(nameof(SelectedRegion));
    }
}
