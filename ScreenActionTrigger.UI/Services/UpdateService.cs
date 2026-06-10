using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Services;

public sealed class UpdateService : IUpdateService, IDisposable
{
    private readonly HttpClient             _http;
    private readonly ILogger<UpdateService> _logger;

    /// <summary>
    /// URL do version.json hospedado (GitHub Pages, raw.githubusercontent.com, etc.).
    /// Formato: { "version":"1.1.0", "downloadUrl":"https://...", "releaseNotes":"...",
    ///            "mandatory":false, "fileSize":165000000, "releasedAt":"2025-01-01T00:00:00Z" }
    /// </summary>
    public static string VersionManifestUrl { get; set; } =
        "https://raw.githubusercontent.com/will6996/ScreenActionTrigger/main/version.json";

    public static string GitHubOwner { get; set; } = "will6996";
    public static string GitHubRepo   { get; set; } = "ScreenActionTrigger";

    public Version CurrentVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Add("User-Agent", "ScreenActionTrigger-Updater/1.0");
    }

    // ─── Verificar: version.json + GitHub Releases API (usa a MAIS NOVA) ─────
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        var fromManifest = await TryCheckManifestAsync(ct);
        var fromGitHub   = await CheckViaGitHubAsync(GitHubOwner, GitHubRepo, ct);

        if (fromManifest is null) return fromGitHub;
        if (fromGitHub   is null) return fromManifest;

        // raw.githubusercontent.com pode ficar em cache (ex.: 1.1.4) — GitHub API é mais confiável
        if (fromGitHub.LatestVersion > fromManifest.LatestVersion)
        {
            _logger.LogWarning(
                "version.json desatualizado ({Manifest} < {GitHub}) — usando GitHub Releases",
                fromManifest.LatestVersion, fromGitHub.LatestVersion);

            return MergeUpdateInfo(fromManifest, fromGitHub);
        }

        return fromManifest;
    }

    private static UpdateInfo MergeUpdateInfo(UpdateInfo manifest, UpdateInfo github)
    {
        return new UpdateInfo
        {
            CurrentVersion = github.CurrentVersion,
            LatestVersion  = github.LatestVersion,
            DownloadUrl    = string.IsNullOrWhiteSpace(github.DownloadUrl)
                ? manifest.DownloadUrl : github.DownloadUrl,
            ReleaseNotes   = string.IsNullOrWhiteSpace(github.ReleaseNotes)
                ? manifest.ReleaseNotes : github.ReleaseNotes,
            IsMandatory    = manifest.IsMandatory || github.IsMandatory,
            FileSizeBytes  = github.FileSizeBytes > 0 ? github.FileSizeBytes : manifest.FileSizeBytes,
            ReleasedAt     = github.ReleasedAt > manifest.ReleasedAt ? github.ReleasedAt : manifest.ReleasedAt
        };
    }

    private async Task<UpdateInfo?> TryCheckManifestAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{VersionManifestUrl.TrimEnd('?')}?_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            _logger.LogInformation("Verificando atualizações em {Url}", url);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true, NoStore = true, MustRevalidate = true
            };
            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);

            var manifest = JsonSerializer.Deserialize<RemoteVersionManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
                return null;

            var latest = Version.Parse(manifest.Version);
            var info   = new UpdateInfo
            {
                CurrentVersion = CurrentVersion,
                LatestVersion  = latest,
                DownloadUrl    = manifest.DownloadUrl,
                ReleaseNotes   = manifest.ReleaseNotes,
                IsMandatory    = manifest.Mandatory,
                FileSizeBytes  = manifest.FileSize,
                ReleasedAt     = DateTime.TryParse(manifest.ReleasedAt, out var dt) ? dt : DateTime.UtcNow
            };

            _logger.LogInformation("Atual: {Cur}  Remota: {Lat}  Nova versão: {Has}",
                CurrentVersion, latest, info.IsUpdateAvailable);

            return info;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler version.json");
            return null;
        }
    }

    // ─── Alternativa: verificar via GitHub Releases API ───────────────────────
    public async Task<UpdateInfo?> CheckViaGitHubAsync(
        string owner, string repo, CancellationToken ct = default)
    {
        try
        {
            var url  = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;

            var tag      = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "1.0.0";
            var latest   = Version.Parse(tag);
            var body     = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var pub      = root.TryGetProperty("published_at", out var p) ? p.GetString() ?? "" : "";

            string dlUrl   = string.Empty;
            long   dlSize  = 0;

            if (root.TryGetProperty("assets", out var assets))
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        dlUrl  = asset.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString() ?? "" : "";
                        dlSize = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                        break;
                    }
                }

            return new UpdateInfo
            {
                CurrentVersion = CurrentVersion,
                LatestVersion  = latest,
                DownloadUrl    = dlUrl,
                ReleaseNotes   = body,
                FileSizeBytes  = dlSize,
                ReleasedAt     = string.IsNullOrEmpty(pub) ? DateTime.UtcNow : DateTime.Parse(pub)
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao verificar GitHub Releases");
            return null;
        }
    }

    // ─── Download do novo executável ──────────────────────────────────────────
    public async Task<string> DownloadAsync(
        UpdateInfo info,
        IProgress<(long downloaded, long total, double percent)>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(info.DownloadUrl))
            throw new InvalidOperationException("URL de download não definida no manifesto.");

        var dest = Path.Combine(Path.GetTempPath(),
            $"SAT_Update_v{info.LatestVersion}.exe");

        _logger.LogInformation("Baixando v{V} de {Url}", info.LatestVersion, info.DownloadUrl);

        using var response = await _http.GetAsync(
            info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? info.FileSizeBytes;

        await using var src  = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(dest);

        var  buf        = new byte[81_920];
        long downloaded = 0;
        int  read;

        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await file.WriteAsync(buf.AsMemory(0, read), ct);
            downloaded += read;
            progress?.Report((downloaded, total,
                total > 0 ? (double)downloaded / total * 100.0 : 0));
        }

        _logger.LogInformation("Download completo: {Size}", info.FileSizeFormatted);
        return dest;
    }

    // ─── Aplicar atualização + reiniciar via PowerShell ───────────────────────
    public void ApplyAndRestart(string downloadedExePath)
    {
        var currentExe = Environment.ProcessPath
            ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Caminho do executável não encontrado.");

        var pid        = Environment.ProcessId;
        var scriptPath = Path.Combine(Path.GetTempPath(), "SAT_Apply_Update.ps1");

        // Escapa barras para uso no script PowerShell
        var srcEsc  = downloadedExePath.Replace("'", "''");
        var dstEsc  = currentExe.Replace("'", "''");
        var scrEsc  = scriptPath.Replace("'", "''");

        var logPath = Path.Combine(Path.GetTempPath(), "SAT_Updater.log");
        var logEsc  = logPath.Replace("'", "''");

        // $appPid — NUNCA usar $pid: conflita com $PID (read-only) do PowerShell
        var script = new System.Text.StringBuilder();
        script.AppendLine("# Screen Action Trigger — Auto-Update Script");
        script.AppendLine($"# Gerado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        script.AppendLine($"$appPid = {pid}");
        script.AppendLine($"$src   = '{srcEsc}'");
        script.AppendLine($"$dst   = '{dstEsc}'");
        script.AppendLine($"$scr   = '{scrEsc}'");
        script.AppendLine($"$log   = '{logEsc}'");
        script.AppendLine("");
        script.AppendLine("function Write-Log([string]$Message) {");
        script.AppendLine("    $line = \"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message\"");
        script.AppendLine("    Add-Content -Path $log -Value $line -Encoding UTF8");
        script.AppendLine("}");
        script.AppendLine("");
        script.AppendLine("Write-Log 'SAT Updater: iniciado'");
        script.AppendLine("Write-Log \"Aguardando processo $appPid fechar...\"");
        script.AppendLine("$limit = 30; $elapsed = 0");
        script.AppendLine("while ((Get-Process -Id $appPid -EA SilentlyContinue) -and $elapsed -lt $limit) {");
        script.AppendLine("    Start-Sleep -Milliseconds 500; $elapsed += 0.5");
        script.AppendLine("}");
        script.AppendLine("if (Get-Process -Id $appPid -EA SilentlyContinue) {");
        script.AppendLine("    Write-Log 'Processo ainda ativo — forçando encerramento'");
        script.AppendLine("    Stop-Process -Id $appPid -Force -EA SilentlyContinue");
        script.AppendLine("    Start-Sleep -Seconds 2");
        script.AppendLine("}");
        script.AppendLine("Write-Log 'Aplicando atualização...'");
        script.AppendLine("try {");
        script.AppendLine("    if (-not (Test-Path -LiteralPath $src)) { throw \"Arquivo baixado não encontrado: $src\" }");
        script.AppendLine("    Copy-Item -LiteralPath $src -Destination $dst -Force -EA Stop");
        script.AppendLine("    Write-Log 'Cópia concluída com sucesso'");
        script.AppendLine("} catch {");
        script.AppendLine("    Write-Log \"ERRO na cópia: $_\"");
        script.AppendLine("    exit 1");
        script.AppendLine("}");
        script.AppendLine("Remove-Item -LiteralPath $src -Force -EA SilentlyContinue");
        script.AppendLine("Remove-Item -LiteralPath $scr -Force -EA SilentlyContinue");
        script.AppendLine("Write-Log 'Reiniciando aplicativo...'");
        script.AppendLine("$workDir = Split-Path -LiteralPath $dst -Parent");
        script.AppendLine("Start-Process -FilePath $dst -WorkingDirectory $workDir");
        script.AppendLine("Write-Log 'Reinício solicitado'");

        File.WriteAllText(scriptPath, script.ToString(), System.Text.Encoding.UTF8);

        _logger.LogInformation("Lançando updater: {Script} (log: {Log})", scriptPath, logPath);

        Process.Start(new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-ExecutionPolicy Bypass -Sta -File \"{scriptPath}\"",
            CreateNoWindow  = true,
            UseShellExecute = false,
            WindowStyle     = ProcessWindowStyle.Hidden
        });

        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose() => _http.Dispose();
}
