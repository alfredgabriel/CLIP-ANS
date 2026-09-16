# installer/build.ps1 — Compila la versión Release y genera el instalador CLIP-ANS-Setup.exe
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path "$PSScriptRoot\.."
$project  = "$repoRoot\src\CLIP-ANS\CLIP-ANS.csproj"
$publish  = "$repoRoot\publish"
$iss      = "$PSScriptRoot\CLIP-ANS.iss"

Write-Host "1. Publicando aplicación en $publish..." -ForegroundColor Cyan
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $publish

Write-Host "2. Compilando instalador con Inno Setup..." -ForegroundColor Cyan
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" $iss

Write-Host "3. Limpiando carpeta temporal de publicación..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $publish -ErrorAction SilentlyContinue

Write-Host "`nInstalador generado correctamente en: $PSScriptRoot\CLIP-ANS-Setup.exe" -ForegroundColor Green
