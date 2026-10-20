param(
    [switch]$DesktopShortcut
)

$ErrorActionPreference = 'Stop'
$shortcuts = @((Join-Path ([Environment]::GetFolderPath('Programs')) 'SOFT2027.lnk'))
if ($DesktopShortcut) { $shortcuts += Join-Path ([Environment]::GetFolderPath('Desktop')) 'SOFT2027.lnk' }
foreach ($shortcut in $shortcuts) {
    if (Test-Path -LiteralPath $shortcut -PathType Leaf) {
        Remove-Item -LiteralPath $shortcut -Force
        Write-Host "Removed shortcut: $shortcut"
    }
}
Write-Host 'Application files, business data, and backups were not removed.'
