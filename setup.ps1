# setup.ps1 — CLIP-ANS setup script
# Downloads JetBrains Mono font and prepares the project for build
# Run from the repo root: .\setup.ps1

$ErrorActionPreference = "Stop"
$fontsDir = "$PSScriptRoot\src\QuizHelper\Resources\Fonts"

Write-Host ""
Write-Host "CLIP-ANS — SETUP" -ForegroundColor White
Write-Host "================" -ForegroundColor White
Write-Host ""

# ── 1. Download JetBrains Mono ────────────────────────────────────────────────
$regularUrl = "https://github.com/JetBrains/JetBrainsMono/raw/master/fonts/ttf/JetBrainsMono-Regular.ttf"
$boldUrl    = "https://github.com/JetBrains/JetBrainsMono/raw/master/fonts/ttf/JetBrainsMono-Bold.ttf"

$regularDst = "$fontsDir\JetBrainsMono-Regular.ttf"
$boldDst    = "$fontsDir\JetBrainsMono-Bold.ttf"

if (-not (Test-Path $regularDst)) {
    Write-Host "Descargando JetBrains Mono Regular..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $regularUrl -OutFile $regularDst -UseBasicParsing
    Write-Host "  OK: $regularDst" -ForegroundColor Green
} else {
    Write-Host "  JetBrains Mono Regular ya existe, omitiendo." -ForegroundColor DarkGray
}

if (-not (Test-Path $boldDst)) {
    Write-Host "Descargando JetBrains Mono Bold..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $boldUrl -OutFile $boldDst -UseBasicParsing
    Write-Host "  OK: $boldDst" -ForegroundColor Green
} else {
    Write-Host "  JetBrains Mono Bold ya existe, omitiendo." -ForegroundColor DarkGray
}

# ── 2. Restore NuGet packages ─────────────────────────────────────────────────
Write-Host ""
Write-Host "Restaurando paquetes NuGet..." -ForegroundColor Cyan
dotnet restore "$PSScriptRoot\src\QuizHelper\QuizHelper.csproj"
Write-Host "  OK" -ForegroundColor Green

# ── 3. Build ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Compilando..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot\src\QuizHelper\QuizHelper.csproj" -c Debug
Write-Host "  OK" -ForegroundColor Green

Write-Host ""
Write-Host "Setup completado. Para ejecutar:" -ForegroundColor White
Write-Host "  dotnet run --project src\QuizHelper\QuizHelper.csproj" -ForegroundColor Yellow
Write-Host ""
