using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class UpdateViewModel : ObservableObject
{
    private readonly IUpdateService        _updater;
    private readonly ILogger<UpdateViewModel> _logger;
    private UpdateInfo? _pendingUpdate;

    [ObservableProperty] private bool   _updateAvailable;
    [ObservableProperty] private bool   _isChecking;
    [ObservableProperty] private bool   _isDownloading;
    [ObservableProperty] private string _updateMessage    = string.Empty;
    [ObservableProperty] private string _releaseNotes     = string.Empty;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string _downloadStatus   = string.Empty;
    [ObservableProperty] private string _currentVersion   = string.Empty;
    [ObservableProperty] private string _latestVersion    = string.Empty;
    [ObservableProperty] private bool   _isMandatory;

    public UpdateViewModel(IUpdateService updater, ILogger<UpdateViewModel> logger)
    {
        _updater         = updater;
        _logger          = logger;
        CurrentVersion   = updater.CurrentVersion.ToString(3);
    }

    // ── Verificação automática (chamada no startup) ──────────────────────────
    public async Task CheckOnStartupAsync()
    {
        await Task.Delay(5_000); // aguarda 5s após o app abrir
        await CheckForUpdatesAsync();
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        IsChecking    = true;
        UpdateMessage = "Verificando atualizações...";

        try
        {
            var info = await _updater.CheckAsync();

            if (info is null)
            {
                UpdateMessage = "Não foi possível verificar atualizações.";
                return;
            }

            LatestVersion = info.LatestVersion.ToString(3);

            if (!info.IsUpdateAvailable)
            {
                UpdateMessage    = $"Você está na versão mais recente ({CurrentVersion}).";
                UpdateAvailable  = false;
                return;
            }

            _pendingUpdate  = info;
            UpdateAvailable = true;
            IsMandatory     = info.IsMandatory;
            ReleaseNotes    = info.ReleaseNotes;
            UpdateMessage   = info.IsMandatory
                ? $"Atualização obrigatória disponível: v{info.LatestVersion} ({info.FileSizeFormatted})"
                : $"Nova versão disponível: v{info.LatestVersion} ({info.FileSizeFormatted})";

            _logger.LogInformation("Atualização disponível: v{V}", info.LatestVersion);
        }
        catch (Exception ex)
        {
            UpdateMessage = "Erro ao verificar atualizações.";
            _logger.LogError(ex, "Erro ao verificar atualizações");
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    public async Task DownloadAndApplyAsync()
    {
        if (_pendingUpdate is null) return;

        IsDownloading    = true;
        DownloadProgress = 0;
        DownloadStatus   = "Iniciando download...";

        try
        {
            var progress = new Progress<(long dl, long total, double pct)>(p =>
            {
                DownloadProgress = p.pct;
                var dlMb    = p.dl    / 1_048_576.0;
                var totalMb = p.total / 1_048_576.0;
                DownloadStatus = $"Baixando... {dlMb:F1} MB / {totalMb:F1} MB ({p.pct:F0}%)";
            });

            var exePath = await _updater.DownloadAsync(_pendingUpdate, progress);

            DownloadStatus   = "Download concluído. Aplicando atualização...";
            DownloadProgress = 100;

            var result = MessageBox.Show(
                $"v{_pendingUpdate.LatestVersion} baixada com sucesso.\n\n" +
                "O aplicativo será fechado, atualizado e reiniciado automaticamente.\n\n" +
                "Deseja continuar?",
                "Screen Action Trigger — Atualização",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                _updater.ApplyAndRestart(exePath);
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "Download cancelado.";
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Erro no download: {ex.Message}";
            _logger.LogError(ex, "Erro ao baixar atualização");
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        if (IsMandatory) return; // atualização obrigatória não pode ser ignorada
        UpdateAvailable = false;
        UpdateMessage   = $"Atualização v{LatestVersion} adiada.";
    }
}
