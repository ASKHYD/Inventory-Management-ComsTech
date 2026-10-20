param(
    [switch]$DesktopShortcut
)

$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'SOFT2027.UI.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "SOFT2027.UI.exe was not found beside this script: $PSScriptRoot"
}

$shell = New-Object -ComObject WScript.Shell
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'SOFT2027.lnk'
$shortcut = $shell.CreateShortcut($startMenu)
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = $PSScriptRoot
$shortcut.Description = 'SOFT2027 — Ali Traders FMCG Wholesale & Distribution'
$shortcut.Save()
Write-Host "Created Start Menu shortcut: $startMenu"

if ($DesktopShortcut) {
    $desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'SOFT2027.lnk'
    $shortcut = $shell.CreateShortcut($desktop)
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = $PSScriptRoot
    $shortcut.Description = 'SOFT2027 — Ali Traders FMCG Wholesale & Distribution'
    $shortcut.Save()
    Write-Host "Created desktop shortcut: $desktop"
}
