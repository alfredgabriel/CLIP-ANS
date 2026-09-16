# build-installer.ps1 — Compila CLIP-ANS y genera el instalador Setup.exe profesional
# Uso: .\build-installer.ps1

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CLIP-ANS — GENERADOR DE INSTALADOR    " -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ── 1. Configurar rutas de .NET y Inno Setup ───────────────────────────────────
$dotnetDir = "$env:LOCALAPPDATA\Microsoft\dotnet"
if (Test-Path $dotnetDir) {
    $env:PATH = $dotnetDir + ";" + $env:PATH
}

$innoDir = "$env:LOCALAPPDATA\Programs\Inno Setup 6"
if (Test-Path $innoDir) {
    $env:PATH = $innoDir + ";" + $env:PATH
}

# ── 2. Verificar dotnet ────────────────────────────────────────────────────────
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    throw "No se encontro dotnet. Por favor verifica la instalacion de .NET 8 SDK."
}
Write-Host "[1/3] Usando .NET: $($dotnetCmd.Source)" -ForegroundColor Green

# ── 3. Publicar aplicacion en ./publish ─────────────────────────────────────────
$project = "$PSScriptRoot\src\QuizHelper\QuizHelper.csproj"
$publishDir = "$PSScriptRoot\publish"

Write-Host "[2/3] Compilando y publicando CLIP-ANS (win-x64, single-file)..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Fallo la compilacion de la aplicacion."
}
Write-Host "      Compilacion exitosa en: $publishDir" -ForegroundColor Green

# ── 4. Compilar instalador con Inno Setup (ISCC) ────────────────────────────────
Write-Host "[3/3] Buscando Inno Setup (ISCC.exe)..." -ForegroundColor Cyan
$isccCmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $isccCmd) {
    $fallbackIscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    if (Test-Path $fallbackIscc) {
        $isccPath = $fallbackIscc
    } else {
        throw "No se encontro ISCC.exe. Por favor verifica la instalacion de Inno Setup."
    }
} else {
    $isccPath = $isccCmd.Source
}

Write-Host "      Usando Inno Setup: $isccPath" -ForegroundColor Green
$issFile = "$PSScriptRoot\installer\CLIP-ANS.iss"
& $isccPath $issFile

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "  INSTALADOR GENERADO CON EXITO:                                 " -ForegroundColor Green
    Write-Host "  $PSScriptRoot\installer_output\CLIP-ANS-Setup-1.0.0.exe        " -ForegroundColor White
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host ""
} else {
    throw "Error al empaquetar con Inno Setup."
}
