# Registers Local Hub auto-start at Windows logon (Startup folder + desktop shortcut).
# No admin required.
#   powershell -ExecutionPolicy Bypass -File .\scripts\install-local-hub-autostart.ps1
param(
    [string]$PublishDir = "",
    [int]$Port = 5227,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $candidate = Join-Path $PSScriptRoot '..\build'
    $PublishDir = (Resolve-Path -LiteralPath $candidate).Path
}

$startScript = Join-Path $PSScriptRoot 'start-local-hub.ps1'
if (-not (Test-Path -LiteralPath $startScript)) {
    throw "Missing $startScript"
}
if (-not (Test-Path -LiteralPath (Join-Path $PublishDir 'POS_MT.exe'))) {
    throw "Publish the app first: dotnet publish -c Release -o .\build"
}

$startup = [Environment]::GetFolderPath('Startup')
$desktop = [Environment]::GetFolderPath('Desktop')
$startupLnk = Join-Path $startup 'Coditium POS Local Hub.lnk'
$desktopLnk = Join-Path $desktop 'Coditium POS (Local Hub).lnk'

if ($Uninstall) {
    Remove-Item -LiteralPath $startupLnk -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $desktopLnk -Force -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName 'POS_MT_LocalHub' -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Removed Local Hub autostart shortcuts."
    exit 0
}

function New-HubShortcut([string]$Path, [switch]$SkipBrowser) {
    $args = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Minimized -File `"$startScript`" -PublishDir `"$PublishDir`" -Port $Port"
    if ($SkipBrowser) { $args += ' -SkipBrowser' }
    $wsh = New-Object -ComObject WScript.Shell
    $lnk = $wsh.CreateShortcut($Path)
    $lnk.TargetPath = 'powershell.exe'
    $lnk.Arguments = $args
    $lnk.WorkingDirectory = $PublishDir
    $lnk.IconLocation = (Join-Path $PublishDir 'POS_MT.exe')
    $lnk.Description = 'Shop POS - works offline via local SQLite (not pos.coditium.com)'
    $lnk.Save()
}

New-HubShortcut -Path $startupLnk -SkipBrowser
New-HubShortcut -Path $desktopLnk

Write-Host "Autostart installed (Windows Startup folder): $startupLnk"
Write-Host "Desktop shortcut: $desktopLnk"
Write-Host "Always open http://localhost:$Port - cloud https://pos.coditium.com cannot work without internet."
