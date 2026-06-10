# ============================================================
# build.ps1  — Screen Action Trigger Build & Publish Script
# ============================================================
# Uso:
#   .\build.ps1              → compila em Release
#   .\build.ps1 -publish fd  → publica como single-file framework-dependent (~18 MB)
#   .\build.ps1 -publish sc  → publica como single-file self-contained     (~165 MB)
# ============================================================

param(
    [ValidateSet("fd","sc","none")]
    [string]$publish = "none"
)

$ErrorActionPreference = "Stop"
$UI = "ScreenActionTrigger.UI\ScreenActionTrigger.UI.csproj"

Write-Host "`n══ Screen Action Trigger ══" -ForegroundColor Cyan

# ── Build ──────────────────────────────────────────────────
Write-Host "`n[1/3] Compilando..." -ForegroundColor Yellow
dotnet build $UI -c Release --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "Build falhou"; exit 1 }
Write-Host "      ✓ Build OK" -ForegroundColor Green

# ── Tests ─────────────────────────────────────────────────
Write-Host "`n[2/3] Rodando testes..." -ForegroundColor Yellow
dotnet test ScreenActionTrigger.Tests\ScreenActionTrigger.Tests.csproj `
    -c Release --nologo --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) { Write-Error "Testes falharam"; exit 1 }
Write-Host "      ✓ Testes OK" -ForegroundColor Green

# ── Publish ────────────────────────────────────────────────
if ($publish -eq "none") {
    Write-Host "`n[3/3] Publicação ignorada (passe -publish fd ou -publish sc)" `
        -ForegroundColor DarkGray
    exit 0
}

Write-Host "`n[3/3] Publicando ($publish)..." -ForegroundColor Yellow

if ($publish -eq "fd") {
    $profile = "SingleFile_FrameworkDependent"
    $outDir  = "publish\framework-dependent"
    $note    = "Requer .NET 8 Desktop Runtime na máquina alvo"
} else {
    $profile = "SingleFile_SelfContained"
    $outDir  = "publish\self-contained"
    $note    = "Sem dependências externas (inclui runtime .NET 8)"
}

dotnet publish $UI -c Release -p:PublishProfile=$profile --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "Publicação falhou"; exit 1 }

$exe  = Join-Path $outDir "ScreenActionTrigger.UI.exe"
$size = if (Test-Path $exe) {
    "{0:N1} MB" -f ((Get-Item $exe).Length / 1MB)
} else { "?" }

Write-Host ""
Write-Host "  ✓ Publicado em : $outDir" -ForegroundColor Green
Write-Host "  ✓ Tamanho exe  : $size"   -ForegroundColor Green
Write-Host "  ✓ Nota         : $note"   -ForegroundColor DarkCyan
Write-Host ""
