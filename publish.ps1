# publish.ps1 — Genera un .exe único sin consola para distribución
# Run from repo root: .\publish.ps1

$ErrorActionPreference = "Stop"
$project = "$PSScriptRoot\src\QuizHelper\QuizHelper.csproj"
$output  = "$PSScriptRoot\publish"

Write-Host ""
Write-Host "QUIZ HELPER — PUBLISH" -ForegroundColor White
Write-Host "=====================" -ForegroundColor White
Write-Host ""
Write-Host "Target : win-x64, self-contained, single file" -ForegroundColor Cyan
Write-Host "Output : $output" -ForegroundColor Cyan
Write-Host ""

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $output

Write-Host ""
Write-Host "Publicado en: $output\QuizHelper.exe" -ForegroundColor Green
Write-Host ""
