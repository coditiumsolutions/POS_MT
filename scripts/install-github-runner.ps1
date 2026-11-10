<#
.SYNOPSIS
  One-time setup on the TezHost Windows server (run inside Remote Desktop as Administrator).

.DESCRIPTION
  Installs a GitHub Actions self-hosted runner labeled tezhost-windows.
  Create a registration token first:
    GitHub repo → Settings → Actions → Runners → New self-hosted runner
  Or:
    gh api -X POST repos/OWNER/REPO/actions/runners/registration-token --jq .token
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$RepoUrl, # e.g. https://github.com/coditiumsolutions/POS_MT

    [Parameter(Mandatory = $true)]
    [string]$RegistrationToken,

    [string]$RunnerDir = "C:\actions-runner",
    [string]$RunnerName = $env:COMPUTERNAME,
    [string]$Labels = "self-hosted,windows,tezhost-windows"
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script as Administrator in Remote Desktop."
}

New-Item -ItemType Directory -Path $RunnerDir -Force | Out-Null
Set-Location $RunnerDir

$release = Invoke-RestMethod -Uri "https://api.github.com/repos/actions/runner/releases/latest"
$asset = $release.assets | Where-Object { $_.name -match "actions-runner-win-x64-.*\.zip$" } | Select-Object -First 1
if (-not $asset) {
    throw "Could not find Windows x64 runner package."
}

$zip = Join-Path $RunnerDir $asset.name
Write-Host "Downloading $($asset.name)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip
Expand-Archive -Path $zip -DestinationPath $RunnerDir -Force

& .\config.cmd --unattended `
    --url $RepoUrl `
    --token $RegistrationToken `
    --name $RunnerName `
    --labels $Labels `
    --work "_work" `
    --replace

& .\svc.cmd install
& .\svc.cmd start

Write-Host ""
Write-Host "Runner installed and started."
Write-Host "Labels: $Labels"
Write-Host "Next: set GitHub variable IIS_SITE_PATH (e.g. C:\inetpub\wwwroot\pos) then run workflow Deploy TezHost (RDP / Self-hosted)."
