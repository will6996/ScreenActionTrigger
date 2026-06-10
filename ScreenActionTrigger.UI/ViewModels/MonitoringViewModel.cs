using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class MonitoringViewModel : ObservableObject
{
    public ObservableCollection<MonitoringEntry> Entries { get; } = new();

    [ObservableProperty] private MonitoringEntry? _selectedEntry;
    [ObservableProperty] private int    _totalDetections;
    [ObservableProperty] private int    _totalExecutions;
    [ObservableProperty] private int    _totalErrors;
    [ObservableProperty] private int    _queueLength;
    [ObservableProperty] private double _avgConfidence;
    [ObservableProperty] private bool   _autoScroll = true;
    [ObservableProperty] private string _filterRegion = string.Empty;
    [ObservableProperty] private bool   _showOnlyExecuted;

    private const int MaxEntries = 1000;

    public void AddEntry(MonitoringEntry entry)
    {
        if (Entries.Count >= MaxEntries)
            Entries.RemoveAt(0);

        Entries.Add(entry);
        UpdateStats(entry);
    }

    private void UpdateStats(MonitoringEntry e)
    {
        TotalDetections++;
        if (e.WasExecuted) TotalExecutions++;
        if (e.ErrorMessage is not null) TotalErrors++;
        AvgConfidence = Entries.Count > 0
            ? Entries.Average(x => x.Confidence)
            : 0;
    }

    [RelayCommand]
    private void ClearEntries()
    {
        Entries.Clear();
        TotalDetections = TotalExecutions = TotalErrors = 0;
        AvgConfidence = 0;
    }

    [RelayCommand]
    private async Task ExportLogAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "CSV (*.csv)|*.csv|Texto (*.txt)|*.txt",
            FileName = $"SAT_Log_{DateTime.Now:yyyyMMdd_HHmmss}",
            DefaultExt = ".csv"
        };
        if (dlg.ShowDialog() != true) return;

        var lines = new List<string>
        {
            "Timestamp,Região,Regra,Ação,Confiança,Tipo,Executado"
        };
        lines.AddRange(Entries.Select(e =>
            $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
            $"{Esc(e.RegionName)},{Esc(e.RuleName)},{Esc(e.ActionName)}," +
            $"{e.Confidence:F4},{e.DetectionType},{e.WasExecuted}"));

        await File.WriteAllLinesAsync(dlg.FileName, lines);
    }

    private static string Esc(string? s) => s is null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

    public IEnumerable<MonitoringEntry> FilteredEntries => Entries.Where(e =>
        (!ShowOnlyExecuted || e.WasExecuted) &&
        (string.IsNullOrEmpty(FilterRegion) ||
         e.RegionName.Contains(FilterRegion, StringComparison.OrdinalIgnoreCase)));

    partial void OnFilterRegionChanged(string value)    => OnPropertyChanged(nameof(FilteredEntries));
    partial void OnShowOnlyExecutedChanged(bool value)  => OnPropertyChanged(nameof(FilteredEntries));
}
