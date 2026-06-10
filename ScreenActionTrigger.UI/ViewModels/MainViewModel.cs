using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.UI.Infrastructure;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IMonitoringService _monitoring;
    private readonly IProfileManager    _profileManager;
    private readonly IOverlayService    _overlay;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty] private string _title = "Screen Action Trigger";
    [ObservableProperty] private string _statusText = "Pronto";
    [ObservableProperty] private bool   _isMonitoring;
    [ObservableProperty] private bool   _isPaused;
    [ObservableProperty] private string? _currentProfilePath;
    [ObservableProperty] private ExecutionProfile _profile = new() { Name = "Padrão" };
    [ObservableProperty] private bool _isBusy;

    public UpdateViewModel     UpdateVM     { get; }
    public RegionsViewModel    RegionsVM    { get; }
    public RulesViewModel      RulesVM      { get; }
    public SequencesViewModel  SequencesVM  { get; }
    public TemplatesViewModel  TemplatesVM  { get; }
    public MonitoringViewModel MonitoringVM { get; }
    public SettingsViewModel   SettingsVM   { get; }

    public MainViewModel(
        IMonitoringService monitoring,
        IProfileManager profileManager,
        IOverlayService overlay,
        RegionsViewModel regionsVm,
        RulesViewModel rulesVm,
        SequencesViewModel sequencesVm,
        TemplatesViewModel templatesVm,
        MonitoringViewModel monitoringVm,
        SettingsViewModel settingsVm,
        UpdateViewModel updateVm,
        ILogger<MainViewModel> logger)
    {
        _monitoring     = monitoring;
        _profileManager = profileManager;
        _overlay        = overlay;
        _logger         = logger;

        UpdateVM     = updateVm;
        RegionsVM    = regionsVm;
        RulesVM      = rulesVm;
        SequencesVM  = sequencesVm;
        TemplatesVM  = templatesVm;
        MonitoringVM = monitoringVm;
        SettingsVM   = settingsVm;

        _monitoring.EntryAdded += (_, e) => MonitoringVM.AddEntry(e);
        RegionsVM.OverlayPreviewRequested   += (_, _) => RefreshOverlayPreview();
        RulesVM.OverlayPreviewRequested     += (_, _) => RefreshOverlayPreview();
        SequencesVM.OverlayPreviewRequested += (_, _) => RefreshOverlayPreview();
        SettingsVM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SettingsViewModel.OverlayEnabled)) return;
            _overlay.IsEnabled = SettingsVM.OverlayEnabled;
            if (!SettingsVM.OverlayEnabled)
                _overlay.Hide();
        };
        SyncChildVMs();

        // Verificação de atualização em background (não bloqueia o startup)
        _ = Task.Run(async () => await UpdateVM.CheckOnStartupAsync());
    }

    private void RefreshOverlayPreview()
    {
        if (IsMonitoring) return;

        _overlay.IsEnabled = SettingsVM.OverlayEnabled;
        _overlay.ShowConfigurationPreview(
            RegionsVM.Regions,
            CollectClickTargets(),
            RegionsVM.SelectedRegion?.Id);
        StatusText = "Overlay: região selecionada visível na tela";
    }

    private IEnumerable<ClickTargetMarker> CollectClickTargets()
    {
        foreach (var rule in RulesVM.Rules)
        {
            foreach (var action in rule.Actions)
            {
                if (action.UseDetectionCoordinates
                    || !action.TargetX.HasValue
                    || !action.TargetY.HasValue)
                    continue;

                yield return new ClickTargetMarker
                {
                    X = action.TargetX.Value,
                    Y = action.TargetY.Value,
                    Label = $"{rule.Name}: {action.GetDescription()}"
                };
            }
        }

        foreach (var sequence in SequencesVM.Sequences)
        {
            foreach (var step in sequence.Steps)
            {
                foreach (var action in step.Actions)
                {
                    if (action.UseDetectionCoordinates
                        || !action.TargetX.HasValue
                        || !action.TargetY.HasValue)
                        continue;

                    yield return new ClickTargetMarker
                    {
                        X = action.TargetX.Value,
                        Y = action.TargetY.Value,
                        Label = $"{sequence.Name} → {step.Name}: {action.GetDescription()}"
                    };
                }
            }
        }
    }

    private void SyncChildVMs()
    {
        RegionsVM.SetProfile(Profile);
        RulesVM.SetProfile(Profile);
        SequencesVM.SetProfile(Profile);
        TemplatesVM.SetProfile(Profile);
        SettingsVM.SetProfile(Profile);
        _overlay.IsEnabled = Profile.Settings.OverlayEnabled;
    }

    partial void OnProfileChanged(ExecutionProfile value) => SyncChildVMs();

    [RelayCommand(CanExecute = nameof(CanRunMonitoringCommand))]
    private async Task StartMonitoringAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Profile.Regions   = new List<MonitoredRegion>(RegionsVM.Regions);
            Profile.Rules      = new List<VisualRule>(RulesVM.Rules);
            Profile.Sequences  = new List<RuleSequence>(SequencesVM.Sequences);
            Profile.Templates  = new List<Template>(TemplatesVM.Templates);
            Profile.Settings   = SettingsVM.Settings;

            foreach (var rule in Profile.Rules)
                ProfileRepair.EnsureRuleCollections(rule);
            foreach (var seq in Profile.Sequences)
                ProfileRepair.EnsureSequenceCollections(seq);
            ProfileRepair.RepairRuleRegionLinks(Profile.Rules, Profile.Regions);
            ProfileRepair.RepairSequenceRegionLinks(Profile.Sequences, Profile.Regions);

            var activeRules = Profile.Rules.Count(r => r.IsEnabled);
            var activeSeqs  = Profile.Sequences.Count(s => s.IsEnabled && s.Steps.Count > 0);
            if (activeRules == 0 && activeSeqs == 0)
            {
                StatusText = "Nenhuma regra ou sequência ativa";
                return;
            }

            await _monitoring.StartAsync(Profile).ConfigureAwait(true);
            IsMonitoring = true;
            IsPaused     = false;
            StatusText   = $"Monitorando {Profile.Regions.Count(r => r.IsEnabled)} regiões, {activeRules} regras, {activeSeqs} sequências…";
            OnPropertyChanged(nameof(Profile));
            Title        = $"Screen Action Trigger — {Profile.Name} ▶";
            _ = SaveAutoSaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start monitoring");
            StatusText = $"Erro: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopMonitoring))]
    private async Task StopMonitoringAsync()
    {
        if (!IsMonitoring) return;

        try
        {
            await _monitoring.StopAsync().ConfigureAwait(true);
            IsMonitoring = false;
            IsPaused     = false;
            StatusText   = "Monitoramento parado";
            Title        = $"Screen Action Trigger — {Profile.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop monitoring");
            StatusText = $"Erro ao parar: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunMonitoringCommand))]
    private async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring)
            await StopMonitoringAsync();
        else
            await StartMonitoringAsync();
    }

    /// <summary>Atalho global — sempre permite parar, mesmo com IsBusy.</summary>
    [RelayCommand]
    private async Task HotkeyToggleMonitoringAsync()
    {
        if (IsMonitoring)
            await StopMonitoringAsync();
        else if (!IsBusy)
            await StartMonitoringAsync();
    }

    private bool CanRunMonitoringCommand() => !IsBusy;
    private bool CanStopMonitoring() => IsMonitoring;

    partial void OnIsBusyChanged(bool value)
    {
        StartMonitoringCommand.NotifyCanExecuteChanged();
        StopMonitoringCommand.NotifyCanExecuteChanged();
        ToggleMonitoringCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        StopMonitoringCommand.NotifyCanExecuteChanged();
        TogglePauseCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanTogglePause))]
    private async Task TogglePauseAsync()
    {
        if (IsPaused)
        {
            await _monitoring.ResumeAsync();
            IsPaused   = false;
            StatusText = "Monitorando…";
        }
        else
        {
            await _monitoring.PauseAsync();
            IsPaused   = true;
            StatusText = "Pausado";
        }
    }

    private bool CanTogglePause() => IsMonitoring;

    [RelayCommand]
    private async Task NewProfileAsync()
    {
        await StopMonitoringAsync();
        Profile = _profileManager.CreateNew("Novo Perfil");
        CurrentProfilePath = null;
        Title = $"Screen Action Trigger — {Profile.Name}";
    }

    [RelayCommand]
    private async Task OpenProfileAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Perfis SAT (*.satprofile)|*.satprofile|JSON (*.json)|*.json|Todos (*.*)|*.*",
            Title  = "Abrir Perfil"
        };
        if (dlg.ShowDialog() != true) return;

        await StopMonitoringAsync();
        var loaded = await _profileManager.LoadAsync(dlg.FileName);
        if (loaded is not null)
        {
            Profile            = loaded;
            CurrentProfilePath = dlg.FileName;
            Title = $"Screen Action Trigger — {Profile.Name}";
            StatusText = "Perfil carregado";
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (CurrentProfilePath is null)
        {
            await SaveProfileAsAsync();
            return;
        }
        PushChangesToProfile();
        await _profileManager.SaveAsync(Profile, CurrentProfilePath);
        StatusText = "Perfil salvo";
    }

    [RelayCommand]
    private async Task SaveProfileAsAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter           = "Perfis SAT (*.satprofile)|*.satprofile|JSON (*.json)|*.json",
            Title            = "Salvar Perfil",
            FileName         = Profile.Name,
            DefaultExt       = ".satprofile"
        };
        if (dlg.ShowDialog() != true) return;

        PushChangesToProfile();
        await _profileManager.SaveAsync(Profile, dlg.FileName);
        CurrentProfilePath = dlg.FileName;
        StatusText = "Perfil salvo";
    }

    [RelayCommand]
    private void ToggleOverlay()
    {
        if (_overlay.IsVisible)
        {
            _overlay.Hide();
            StatusText = "Overlay oculto";
        }
        else if (IsMonitoring)
        {
            _overlay.Show();
            StatusText = "Overlay visível";
        }
        else
        {
            RefreshOverlayPreview();
        }
    }

    public void PushChangesToProfile()
    {
        Profile.Regions   = new List<MonitoredRegion>(RegionsVM.Regions);
        Profile.Rules      = new List<VisualRule>(RulesVM.Rules);
        Profile.Sequences  = new List<RuleSequence>(SequencesVM.Sequences);
        Profile.Templates  = new List<Template>(TemplatesVM.Templates);
        Profile.Settings  = SettingsVM.Settings;
        Profile.UpdatedAt = DateTime.UtcNow;
    }

    public async Task LoadAutoSaveAsync()
    {
        try
        {
            var path = AppPaths.AutoSaveProfilePath;
            if (!File.Exists(path))
                return;

            var loaded = await _profileManager.LoadAsync(path);
            if (loaded is null)
                return;

            Profile            = loaded;
            CurrentProfilePath = path;
            Title              = $"Screen Action Trigger — {Profile.Name}";
            StatusText         = "Sessão anterior restaurada";
            _logger.LogInformation("Auto-save profile loaded from {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load auto-save profile");
        }
    }

    public async Task SaveAutoSaveAsync()
    {
        try
        {
            PushChangesToProfile();
            var path = AppPaths.AutoSaveProfilePath;
            await _profileManager.SaveAsync(Profile, path).ConfigureAwait(false);
            _logger.LogInformation("Auto-save profile saved to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save auto-save profile");
        }
    }

    public async Task ShutdownAsync()
    {
        if (IsMonitoring)
            await _monitoring.StopAsync().ConfigureAwait(true);

        await SaveAutoSaveAsync().ConfigureAwait(false);
    }
}
