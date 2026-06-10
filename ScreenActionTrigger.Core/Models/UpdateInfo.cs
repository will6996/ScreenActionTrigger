namespace ScreenActionTrigger.Core.Models;

public sealed class UpdateInfo
{
    public Version  CurrentVersion    { get; init; } = new(1, 0, 0);
    public Version  LatestVersion     { get; init; } = new(1, 0, 0);
    public bool     IsUpdateAvailable => LatestVersion > CurrentVersion;
    public bool     IsMandatory       { get; init; }
    public string   DownloadUrl       { get; init; } = string.Empty;
    public string   ReleaseNotes      { get; init; } = string.Empty;
    public long     FileSizeBytes     { get; init; }
    public DateTime ReleasedAt        { get; init; }

    public string FileSizeFormatted => FileSizeBytes switch
    {
        >= 1_048_576 => $"{FileSizeBytes / 1_048_576.0:F1} MB",
        >= 1_024     => $"{FileSizeBytes / 1_024.0:F1} KB",
        _            => $"{FileSizeBytes} B"
    };
}

/// <summary>
/// Estrutura do arquivo version.json hospedado no servidor.
/// Exemplo: https://raw.githubusercontent.com/SEU_USUARIO/SAT/main/version.json
/// </summary>
public sealed class RemoteVersionManifest
{
    public string Version      { get; set; } = "1.0.0";
    public string DownloadUrl  { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public bool   Mandatory    { get; set; } = false;
    public long   FileSize     { get; set; } = 0;
    public string ReleasedAt   { get; set; } = DateTime.UtcNow.ToString("O");
}
