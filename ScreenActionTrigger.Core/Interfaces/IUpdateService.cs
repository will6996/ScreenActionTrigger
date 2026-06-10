using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface IUpdateService
{
    Version CurrentVersion { get; }

    /// <summary>Verifica se há atualização disponível no servidor remoto.</summary>
    Task<UpdateInfo?> CheckAsync(CancellationToken ct = default);

    /// <summary>Baixa o novo executável e retorna o caminho local.</summary>
    Task<string> DownloadAsync(
        UpdateInfo info,
        IProgress<(long downloaded, long total, double percent)>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Aplica a atualização: cria script PowerShell que espera o processo
    /// atual fechar, substitui o .exe e reinicia.
    /// </summary>
    void ApplyAndRestart(string downloadedExePath);
}
