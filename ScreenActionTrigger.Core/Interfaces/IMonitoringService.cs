using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface IMonitoringService
{
    event EventHandler<MonitoringEntry>? EntryAdded;

    bool IsRunning { get; }
    IReadOnlyList<MonitoringEntry> Entries { get; }

    Task StartAsync(ExecutionProfile profile, CancellationToken ct = default);
    Task StopAsync();
    Task PauseAsync();
    Task ResumeAsync();

    void AddEntry(MonitoringEntry entry);
    void ClearEntries();
}
