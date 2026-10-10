# SOFT2027 Windows Desktop Application Publish Script
# Target: Windows x64 Self-Contained Single-File Executable
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src/SOFT2027.UI/SOFT2027.UI.csproj'
$outputDir = Join-Path $repoRoot 'publish/win-x64-rc.2'
$localDotnetCandidates = @(
    (Join-Path $repoRoot '.dotnet/dotnet.exe'),
    (Join-Path (Split-Path $repoRoot -Parent) '.dotnet/dotnet.exe')
)
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$localDotnet = $localDotnetCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($localDotnet) {
    $dotnet = $localDotnet
} elseif ($dotnetCommand) {
    $dotnet = $dotnetCommand.Source
} else {
    throw 'The .NET 8 SDK was not found. Install it or place dotnet.exe under .dotnet at the repository root.'
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SOFT2027 Production Packaging - Windows x64 Single File " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Write-Host "`nPublishing $projectPath to $outputDir..." -ForegroundColor Yellow

$null = New-Item -ItemType Directory -Path $outputDir -Force
& $dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=1.0.0-rc.2 `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Packaging failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

$exePath = Join-Path $outputDir 'SOFT2027.UI.exe'
if (Test-Path $exePath) {
    $sizeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
    Write-Host "`n[SUCCESS] Single-file executable generated successfully!" -ForegroundColor Green
    Write-Host "Executable Path: $exePath" -ForegroundColor Green
    Write-Host "Executable Size: $sizeMb MB" -ForegroundColor Green
} else {
    Write-Error "Expected executable was not found at $exePath."
    exit 1
}

$schemaPath = Join-Path $outputDir 'docs\DATABASE_SCHEMA.sql'
if (Test-Path $schemaPath) {
    Write-Host "Schema Bundled:  $schemaPath" -ForegroundColor Green
}

$packageReadme = Join-Path $outputDir 'README-INSTALL.txt'
@'
SOFT2027 — Ali Traders FMCG Wholesale & Distribution
Portable Windows x64 package, version 1.0.0-rc.2 (release candidate; not approved for production)

Install: Extract this folder to a stable location (for example, %LOCALAPPDATA%\Programs\SOFT2027) and run SOFT2027.UI.exe.
The app stores business data separately at %LOCALAPPDATA%\SOFT2027\soft2027.db.
For Start Menu and optional desktop shortcuts, run Create-Shortcuts.ps1 from this folder.
To update, close SOFT2027 and replace only the application folder with the new package. Do not remove the separate data folder.
To uninstall this portable package, remove its extracted application folder. This does not delete business data or backups.
The published app is self-contained and does not require a separately installed .NET runtime.
'@ | Set-Content -LiteralPath $packageReadme -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/Create-Shortcuts.ps1') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/Remove-Shortcuts.ps1') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/USER_GUIDE.md') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Destination (Join-Path $outputDir 'RELEASE-NOTES.md') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/INSTALLATION_AND_HANDOVER.md') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/FINAL_TEST_REPORT.md') -Destination $outputDir -Force

$zipPath = Join-Path $repoRoot 'publish/SOFT2027-1.0.0-rc.2-win-x64-portable.zip'
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $outputDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Portable package: $zipPath" -ForegroundColor Green

Write-Host "`n==========================================================" -ForegroundColor Cyan
