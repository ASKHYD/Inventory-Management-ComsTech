# SOFT2027 Windows Desktop Application Publish Script
# Target: Windows x64 Self-Contained Single-File Executable
$ErrorActionPreference = "Stop"

# Ensure local dotnet SDK is accessible in this environment
$dotnetPath = "$env:LocalAppData\Microsoft\dotnet"
if (Test-Path $dotnetPath) {
    $env:PATH = "$dotnetPath;" + $env:PATH
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SOFT2027 Production Packaging - Windows x64 Single File " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$projectPath = "src/SOFT2027.UI/SOFT2027.UI.csproj"
$outputDir = "publish"

Write-Host "`nPublishing $projectPath to $outputDir..." -ForegroundColor Yellow

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Packaging failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

$exePath = Join-Path $outputDir "SOFT2027.UI.exe"
if (Test-Path $exePath) {
    $sizeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
    Write-Host "`n[SUCCESS] Single-file executable generated successfully!" -ForegroundColor Green
    Write-Host "Executable Path: $exePath" -ForegroundColor Green
    Write-Host "Executable Size: $sizeMb MB" -ForegroundColor Green
} else {
    Write-Error "Expected executable was not found at $exePath."
    exit 1
}

$schemaPath = Join-Path $outputDir "docs\DATABASE_SCHEMA.sql"
if (Test-Path $schemaPath) {
    Write-Host "Schema Bundled:  $schemaPath" -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
