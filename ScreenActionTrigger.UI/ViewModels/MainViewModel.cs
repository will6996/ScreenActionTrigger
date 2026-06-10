using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

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

    public UpdateViewModel     UpdateVM     { get; }
    public RegionsViewModel    RegionsVM    { get; }
    public RulesViewModel      RulesVM      { get; }
    public TemplatesViewModel  TemplatesVM  { get; }
    public MonitoringViewModel MonitoringVM { get; }
    public SettingsViewModel   SettingsVM   { get; }

    public MainViewModel(
        IMonitoringService monitoring,
        IProfileManager profileManager,
        IOverlayService overlay,
        RegionsViewModel regionsVm,
        RulesViewModel rulesVm,
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
        TemplatesVM  = templatesVm;
        MonitoringVM = monitoringVm;
        SettingsVM   = settingsVm;

        _monitoring.EntryAdded += (_, e) => MonitoringVM.AddEntry(e);
        SyncChildVMs();

        // Verificação de atualização em background (não bloqueia o startup)
        _ = Task.Run(async () => await UpdateVM.CheckOnStartupAsync());
    }

    private void SyncChildVMs()
    {
        RegionsVM.SetProfile(Profile);
        RulesVM.SetProfile(Profile);
        TemplatesVM.SetProfile(Profile);
        SettingsVM.SetProfile(Profile);
    }

    partial void OnProfileChanged(ExecutionProfile value) => SyncChildVMs();

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        try
        {
            Profile.Regions   = new List<MonitoredRegion>(RegionsVM.Regions);
            Profile.Rules     = new List<VisualRule>(RulesVM.Rules);
            Profile.Templates = new List<Template>(TemplatesVM.Templates);
            Profile.Settings  = SettingsVM.Settings;

            await _monitoring.StartAsync(Profile);
            IsMonitoring = true;
            IsPaused     = false;
            StatusText   = $"Monitorando {Profile.Regions.Count(r => r.IsEnabled)} regiões…";
            Title        = $"Screen Action Trigger — {Profile.Name} ▶";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start monitoring");
            StatusText = $"Erro: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopMonitoringAsync()
    {
        await _monitoring.StopAsync();
        IsMonitoring = false;
        IsPaused     = false;
        StatusText   = "Monitoramento parado";
        Title        = $"Screen Action Trigger — {Profile.Name}";
    }

    [RelayCommand]
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
        _overlay.Toggle();
        StatusText = _overlay.IsVisible ? "Overlay visível" : "Overlay oculto";
    }

    private void PushChangesToProfile()
    {
        Profile.Regions   = new List<MonitoredRegion>(RegionsVM.Regions);
        Profile.Rules     = new List<VisualRule>(RulesVM.Rules);
        Profile.Templates = new List<Template>(TemplatesVM.Templates);
        Profile.Settings  = SettingsVM.Settings;
        Profile.UpdatedAt = DateTime.UtcNow;
    }
}
