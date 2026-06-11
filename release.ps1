# ============================================================
# release.ps1 — Publica ScreenActionTrigger.exe no GitHub
# ============================================================
# Uso:
#   .\release.ps1                  → usa versão do .csproj
#   .\release.ps1 -Notes "texto"   → notas personalizadas
# ============================================================

param(
    [string]$Notes = ""
)

$ErrorActionPreference = "Stop"
$UI = "ScreenActionTrigger.UI\ScreenActionTrigger.UI.csproj"

Write-Host "`n══ Screen Action Trigger — Release ══" -ForegroundColor Cyan

# Lê versão do csproj
[xml]$csproj = Get-Content $UI
$version = ($csproj.Project.PropertyGroup.Version | Select-Object -First 1).'#text'
if (-not $version) { throw "Versão não encontrada no csproj" }
$tag = "v$version"

Write-Host "Versão: $version ($tag)" -ForegroundColor Yellow

# Build + testes + publish
Write-Host "`n[1/4] Compilando e testando..." -ForegroundColor Yellow
dotnet test ScreenActionTrigger.Tests\ScreenActionTrigger.Tests.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Testes falharam" }

Write-Host "`n[2/4] Publicando single-file self-contained..." -ForegroundColor Yellow
dotnet publish $UI -c Release -p:PublishProfile=SingleFile_SelfContained --nologo
if ($LASTEXITCODE -ne 0) { throw "Publicação falhou" }

$exe = "publish\self-contained\ScreenActionTrigger.exe"
if (-not (Test-Path $exe)) {
    $legacy = "publish\self-contained\ScreenActionTrigger.UI.exe"
    if (Test-Path $legacy) { $exe = $legacy }
    else { throw "Executável não encontrado em publish\self-contained\" }
}

$size = (Get-Item $exe).Length
Write-Host "      ✓ $exe ({0:N1} MB)" -f ($size / 1MB) -ForegroundColor Green

# Atualiza version.json
Write-Host "`n[3/4] Atualizando version.json..." -ForegroundColor Yellow
if (-not $Notes) {
    $Notes = "- Sequências com ramificação if/else`n- Contagem de slots no inventário`n- Painel de sequência recolhível`n- Atualização automática corrigida"
}

$manifest = [ordered]@{
    version      = $version
    downloadUrl  = "https://github.com/will6996/ScreenActionTrigger/releases/download/$tag/ScreenActionTrigger.exe"
    releaseNotes = $Notes
    mandatory    = $false
    fileSize     = $size
    releasedAt   = (Get-Date).ToUniversalTime().ToString("o")
}
$manifest | ConvertTo-Json | Set-Content version.json -Encoding UTF8
Write-Host "      ✓ version.json atualizado" -ForegroundColor Green

# Git tag + release
Write-Host "`n[4/4] Criando release no GitHub..." -ForegroundColor Yellow

git add version.json
git diff --staged --quiet
if ($LASTEXITCODE -ne 0) {
    git commit -m "chore: release $tag"
}

git tag -a $tag -m "Release $tag" 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "      Tag $tag já existe — atualizando release..." -ForegroundColor DarkYellow
}

git push origin main
git push origin $tag --force

gh release create $tag $exe version.json `
    --title "Screen Action Trigger $tag" `
    --notes $Notes `
    --clobber

Write-Host ""
Write-Host "  ✓ Release publicada: $tag" -ForegroundColor Green
Write-Host "  ✓ Download: publish\self-contained\ScreenActionTrigger.exe" -ForegroundColor Green
Write-Host ""
