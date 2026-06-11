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
    private static readonly string[] PreferredAssetNames =
    [
        "ScreenActionTrigger.exe",
        "ScreenActionTrigger.UI.exe"
    ];

    private readonly HttpClient             _http;
    private readonly ILogger<UpdateService> _logger;

    public static string VersionManifestUrl { get; set; } =
        "https://raw.githubusercontent.com/will6996/ScreenActionTrigger/main/version.json";

    public static string GitHubOwner { get; set; } = "will6996";
    public static string GitHubRepo   { get; set; } = "ScreenActionTrigger";

    public Version CurrentVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
        _http   = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _http.DefaultRequestHeaders.Add("User-Agent", "ScreenActionTrigger-Updater/1.0");
    }

    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        var fromGitHub   = await CheckViaGitHubAsync(GitHubOwner, GitHubRepo, ct);
        var fromManifest = await TryCheckManifestAsync(ct);

        if (fromGitHub is null && fromManifest is null) return null;
        if (fromGitHub is null) return fromManifest;
        if (fromManifest is null) return fromGitHub;

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

            var latest = ParseVersion(manifest.Version);
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
            var latest   = ParseVersion(tag);
            var body     = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var pub      = root.TryGetProperty("published_at", out var p) ? p.GetString() ?? "" : "";

            var (dlUrl, dlSize) = ExtractPreferredAsset(root);

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

    private static (string Url, long Size) ExtractPreferredAsset(JsonElement releaseRoot)
    {
        if (!releaseRoot.TryGetProperty("assets", out var assets))
            return (string.Empty, 0);

        var candidates = new List<(string Name, string Url, long Size)>();
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

            var url  = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            var size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
            if (!string.IsNullOrWhiteSpace(url))
                candidates.Add((name, url, size));
        }

        foreach (var preferred in PreferredAssetNames)
        {
            var match = candidates.FirstOrDefault(c =>
                c.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase));
            if (match != default) return (match.Url, match.Size);
        }

        var largest = candidates.OrderByDescending(c => c.Size).FirstOrDefault();
        return largest != default ? (largest.Url, largest.Size) : (string.Empty, 0);
    }

    public async Task<string> DownloadAsync(
        UpdateInfo info,
        IProgress<(long downloaded, long total, double percent)>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(info.DownloadUrl))
            throw new InvalidOperationException("URL de download não definida no manifesto.");

        var dest = Path.Combine(Path.GetTempPath(),
            $"ScreenActionTrigger_v{info.LatestVersion}.exe");

        if (File.Exists(dest))
            File.Delete(dest);

        _logger.LogInformation("Baixando v{V} de {Url}", info.LatestVersion, info.DownloadUrl);

        using var request = new HttpRequestMessage(HttpMethod.Get, info.DownloadUrl);
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        using var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);

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

        await file.FlushAsync(ct);

        var fileInfo = new FileInfo(dest);
        if (fileInfo.Length < 1_048_576)
            throw new InvalidOperationException(
                $"Download incompleto ou inválido ({fileInfo.Length} bytes). Tente novamente.");

        if (info.FileSizeBytes > 0 && fileInfo.Length < info.FileSizeBytes * 0.95)
            throw new InvalidOperationException(
                $"Download incompleto: {fileInfo.Length} de {info.FileSizeBytes} bytes.");

        _logger.LogInformation("Download completo: {Size}", info.FileSizeFormatted);
        return dest;
    }

    public void ApplyAndRestart(string downloadedExePath)
    {
        if (!File.Exists(downloadedExePath))
            throw new FileNotFoundException("Arquivo baixado não encontrado.", downloadedExePath);

        var currentExe = ResolveCurrentExecutablePath();
        var pid        = Environment.ProcessId;
        var scriptPath = Path.Combine(Path.GetTempPath(), "SAT_Apply_Update.ps1");
        var logPath    = Path.Combine(Path.GetTempPath(), "SAT_Updater.log");

        var script = BuildUpdateScript(
            downloadedExePath, currentExe, pid, scriptPath, logPath);

        File.WriteAllText(scriptPath, script, System.Text.Encoding.UTF8);

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

    public static string UpdaterLogPath =>
        Path.Combine(Path.GetTempPath(), "SAT_Updater.log");

    private static string ResolveCurrentExecutablePath()
    {
        var path = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Caminho do executável não encontrado.");

        return Path.GetFullPath(path);
    }

    private static string BuildUpdateScript(
        string downloadedExePath, string currentExe, int pid,
        string scriptPath, string logPath)
    {
        static string Esc(string s) => s.Replace("'", "''");

        var srcEsc = Esc(downloadedExePath);
        var dstEsc = Esc(currentExe);
        var scrEsc = Esc(scriptPath);
        var logEsc = Esc(logPath);
        var bakEsc = Esc(currentExe + ".old");

        var script = new System.Text.StringBuilder();
        script.AppendLine("# Screen Action Trigger — Auto-Update Script");
        script.AppendLine($"# Gerado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.AppendLine($"$appPid = {pid}");
        script.AppendLine($"$src    = '{srcEsc}'");
        script.AppendLine($"$dst    = '{dstEsc}'");
        script.AppendLine($"$bak    = '{bakEsc}'");
        script.AppendLine($"$scr    = '{scrEsc}'");
        script.AppendLine($"$log    = '{logEsc}'");
        script.AppendLine("");
        script.AppendLine("function Write-Log([string]$Message) {");
        script.AppendLine("    $line = \"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message\"");
        script.AppendLine("    Add-Content -Path $log -Value $line -Encoding UTF8");
        script.AppendLine("}");
        script.AppendLine("");
        script.AppendLine("Write-Log 'SAT Updater: iniciado'");
        script.AppendLine("Write-Log \"Origem: $src\"");
        script.AppendLine("Write-Log \"Destino: $dst\"");
        script.AppendLine("");
        script.AppendLine("Write-Log \"Aguardando processo $appPid fechar...\"");
        script.AppendLine("$limit = 45; $elapsed = 0");
        script.AppendLine("while ((Get-Process -Id $appPid -EA SilentlyContinue) -and $elapsed -lt $limit) {");
        script.AppendLine("    Start-Sleep -Milliseconds 500; $elapsed += 0.5");
        script.AppendLine("}");
        script.AppendLine("if (Get-Process -Id $appPid -EA SilentlyContinue) {");
        script.AppendLine("    Write-Log 'Processo ainda ativo — forçando encerramento'");
        script.AppendLine("    Stop-Process -Id $appPid -Force -EA SilentlyContinue");
        script.AppendLine("    Start-Sleep -Seconds 2");
        script.AppendLine("}");
        script.AppendLine("");
        script.AppendLine("if (-not (Test-Path -LiteralPath $src)) {");
        script.AppendLine("    Write-Log \"ERRO: arquivo baixado não encontrado: $src\"");
        script.AppendLine("    exit 1");
        script.AppendLine("}");
        script.AppendLine("");
        script.AppendLine("try { Unblock-File -LiteralPath $src -EA SilentlyContinue } catch { }");
        script.AppendLine("");
        script.AppendLine("Write-Log 'Aplicando atualização...'");
        script.AppendLine("$applied = $false");
        script.AppendLine("for ($i = 1; $i -le 8; $i++) {");
        script.AppendLine("    try {");
        script.AppendLine("        if (Test-Path -LiteralPath $dst) {");
        script.AppendLine("            if (Test-Path -LiteralPath $bak) { Remove-Item -LiteralPath $bak -Force -EA SilentlyContinue }");
        script.AppendLine("            attrib -R -A -S -H $dst 2>$null");
        script.AppendLine("            Move-Item -LiteralPath $dst -Destination $bak -Force -EA Stop");
        script.AppendLine("        }");
        script.AppendLine("        Move-Item -LiteralPath $src -Destination $dst -Force -EA Stop");
        script.AppendLine("        $applied = $true");
        script.AppendLine("        Write-Log 'Substituição concluída com sucesso'");
        script.AppendLine("        break");
        script.AppendLine("    } catch {");
        script.AppendLine("        Write-Log \"Tentativa $i falhou: $_\"");
        script.AppendLine("        Start-Sleep -Seconds 1");
        script.AppendLine("    }");
        script.AppendLine("}");
        script.AppendLine("");
        script.AppendLine("if (-not $applied) {");
        script.AppendLine("    Write-Log 'ERRO: não foi possível substituir o executável após várias tentativas'");
        script.AppendLine("    exit 1");
        script.AppendLine("}");
        script.AppendLine("");
        script.AppendLine("Remove-Item -LiteralPath $bak -Force -EA SilentlyContinue");
        script.AppendLine("Remove-Item -LiteralPath $scr -Force -EA SilentlyContinue");
        script.AppendLine("");
        script.AppendLine("Write-Log 'Reiniciando aplicativo...'");
        script.AppendLine("$workDir = Split-Path -LiteralPath $dst -Parent");
        script.AppendLine("Start-Process -FilePath $dst -WorkingDirectory $workDir");
        script.AppendLine("Write-Log 'Reinício solicitado'");

        return script.ToString();
    }

    private static Version ParseVersion(string raw)
    {
        var cleaned = raw.Trim().TrimStart('v');
        if (Version.TryParse(cleaned, out var version))
            return version;

        var parts = cleaned.Split('.');
        if (parts.Length >= 3
            && int.TryParse(parts[0], out var major)
            && int.TryParse(parts[1], out var minor)
            && int.TryParse(parts[2], out var build))
            return new Version(major, minor, build);

        return new Version(1, 0, 0);
    }

    public void Dispose() => _http.Dispose();
}
