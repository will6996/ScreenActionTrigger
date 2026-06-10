using System.Collections.ObjectModel;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Services;

public sealed class MonitoringService : IMonitoringService, IDisposable
{
    private readonly IScreenCaptureService _capture;
    private readonly IRuleEngine _ruleEngine;
    private readonly IActionDispatcher _dispatcher;
    private readonly IVisionEngine _vision;
    private readonly IOverlayService _overlay;
    private readonly ILogger<MonitoringService> _logger;

    private readonly ObservableCollection<MonitoringEntry> _entries = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private ExecutionProfile? _profile;
    private bool _paused;

    public event EventHandler<MonitoringEntry>? EntryAdded;
    public bool IsRunning => _monitorTask is { IsCompleted: false };
    public IReadOnlyList<MonitoringEntry> Entries => _entries;

    public MonitoringService(
        IScreenCaptureService capture,
        IRuleEngine ruleEngine,
        IActionDispatcher dispatcher,
        IVisionEngine vision,
        IOverlayService overlay,
        ILogger<MonitoringService> logger)
    {
        _capture    = capture;
        _ruleEngine = ruleEngine;
        _dispatcher = dispatcher;
        _vision     = vision;
        _overlay    = overlay;
        _logger     = logger;

        _ruleEngine.RuleTriggered      += OnRuleTriggered;
        _ruleEngine.DetectionCompleted += OnDetectionCompleted;
        _dispatcher.ActionExecuted     += OnActionExecuted;
    }

    public async Task StartAsync(ExecutionProfile profile, CancellationToken ct = default)
    {
        if (IsRunning) await StopAsync();

        _profile = profile;
        _paused  = false;
        _cts     = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await _capture.InitializeAsync(_cts.Token);

        _ruleEngine.LoadRules(profile.Rules);
        _vision.SetTemplates(profile.Templates);
        _overlay.UpdateRegions(profile.Regions);

        if (profile.Settings.OverlayEnabled)
            _overlay.Show();

        _monitorTask = RunMonitorLoopAsync(profile, _cts.Token);
        _logger.LogInformation("Monitoring started with {Count} regions", profile.Regions.Count);
    }

    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_monitorTask is not null)
            {
                try { await _monitorTask; }
                catch (OperationCanceledException) { }
            }
            _cts.Dispose();
            _cts = null;
        }

        _dispatcher.CancelAll();
        _overlay.Hide();
        _ruleEngine.ResetAll();
        _vision.ClearAllCaches();
        _logger.LogInformation("Monitoring stopped");
    }

    public Task PauseAsync()  { _paused = true;  return Task.CompletedTask; }
    public Task ResumeAsync() { _paused = false; return Task.CompletedTask; }

    private async Task RunMonitorLoopAsync(ExecutionProfile profile, CancellationToken ct)
    {
        var interval   = profile.Settings.CaptureIntervalMs;
        var maxParallel = Math.Min(profile.Settings.MaxParallelRegions, profile.Regions.Count);
        var semaphore  = new SemaphoreSlim(maxParallel, maxParallel);
        var enabledRegions = profile.Regions.Where(r => r.IsEnabled).ToList();

        while (!ct.IsCancellationRequested)
        {
            var loopStart = DateTime.UtcNow;

            if (!_paused)
            {
                var tasks = enabledRegions
                    .OrderByDescending(r => r.Priority)
                    .Select(region => ProcessRegionAsync(region, semaphore, profile, ct))
                    .ToArray();

                await Task.WhenAll(tasks);
            }

            var elapsed = (DateTime.UtcNow - loopStart).TotalMilliseconds;
            var delay   = Math.Max(0, interval - (int)elapsed);

            if (delay > 0)
                await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    private async Task ProcessRegionAsync(
        MonitoredRegion region,
        SemaphoreSlim semaphore,
        ExecutionProfile profile,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            var frameData = await _capture.CaptureRegionAsync(region, ct);
            if (frameData is null) return;

            var needsColor = profile.Rules.Any(r =>
                r.RegionId == region.Id && r.IsEnabled && r.Condition.UsesColorDetection());

            if (profile.Settings.GrayscaleProcessing && !needsColor)
                frameData = await ConvertToGrayscaleAsync(frameData);

            await _ruleEngine.ProcessRegionAsync(region, frameData, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing region '{Name}'", region.Name);
        }
        finally { semaphore.Release(); }
    }

    private void OnRuleTriggered(object? sender, Core.Interfaces.RuleTriggeredEventArgs e)
    {
        if (e.Rule.Actions.Count == 0) return;

        _ = _dispatcher.EnqueueBatchAsync(e.Rule.Actions, e.Rule.Priority, e.Detection);

        _overlay.ShowRuleTriggered(e.Rule, e.Detection);

        AddEntry(new MonitoringEntry
        {
            RegionName    = e.Detection.RegionName ?? string.Empty,
            RuleName      = e.Rule.Name,
            ActionName    = e.Rule.Actions.FirstOrDefault()?.GetDescription(),
            Confidence    = e.Detection.Confidence,
            DetectionType = e.Detection.DetectionType.ToString(),
            WasExecuted   = true
        });
    }

    private void OnDetectionCompleted(object? sender, Core.Interfaces.DetectionEventArgs e)
    {
        if (e.Detection.IsMatch)
        {
            _overlay.ShowDetection(e.Detection, e.Region);
            return;
        }

        if (_profile?.Settings.LogDetections == true
            && e.Detection.Confidence > 0
            && e.EvaluatedRule is not null)
        {
            AddEntry(new MonitoringEntry
            {
                RegionName    = e.Region.Name,
                RuleName      = e.EvaluatedRule.Name,
                Confidence    = e.Detection.Confidence,
                DetectionType = e.Detection.DetectionType.ToString(),
                WasExecuted   = false,
                ActionName    = "Sem match (ajuste % ou pixels mínimos)"
            });
        }
    }

    private void OnActionExecuted(object? sender, Core.Interfaces.ActionExecutedEventArgs e)
    {
        if (!e.Success)
        {
            AddEntry(new MonitoringEntry
            {
                RegionName    = "System",
                ActionName    = e.Action.GetDescription(),
                WasExecuted   = false,
                ErrorMessage  = e.Error?.Message
            });
        }
    }

    public void AddEntry(MonitoringEntry entry)
    {
        // Dispatch to UI thread if needed
        if (System.Windows.Application.Current?.Dispatcher is { } d)
        {
            d.BeginInvoke(() =>
            {
                if (_entries.Count >= (_profile?.Settings.MaxMonitoringEntries ?? 500))
                    _entries.RemoveAt(0);
                _entries.Add(entry);
                EntryAdded?.Invoke(this, entry);
            });
        }
    }

    public void ClearEntries()
    {
        if (System.Windows.Application.Current?.Dispatcher is { } d)
            d.BeginInvoke(_entries.Clear);
        else
            _entries.Clear();
    }

    private static Task<byte[]> ConvertToGrayscaleAsync(byte[] pngData)
    {
        return Task.Run(() =>
        {
            using var ms = new System.IO.MemoryStream(pngData);
            using var bmp = new System.Drawing.Bitmap(ms);
            using var gray = new System.Drawing.Bitmap(bmp.Width, bmp.Height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using var g = System.Drawing.Graphics.FromImage(gray);
            var colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
            {
                new[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                new[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                new[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                new[] { 0f, 0f, 0f, 1f, 0 },
                new[] { 0f, 0f, 0f, 0f, 1f }
            });
            var ia = new System.Drawing.Imaging.ImageAttributes();
            ia.SetColorMatrix(colorMatrix);
            g.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                0, 0, bmp.Width, bmp.Height, System.Drawing.GraphicsUnit.Pixel, ia);

            using var outMs = new System.IO.MemoryStream();
            gray.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
            return outMs.ToArray();
        });
    }

    public void Dispose()
    {
        _ruleEngine.RuleTriggered      -= OnRuleTriggered;
        _ruleEngine.DetectionCompleted -= OnDetectionCompleted;
        _dispatcher.ActionExecuted     -= OnActionExecuted;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
