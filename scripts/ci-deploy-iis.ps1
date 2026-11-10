<#
.SYNOPSIS
  Deploy published POS_MT output into a local IIS site folder (run on the TezHost Windows server).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$SitePath,

    [string]$AppPoolName = "",
    [string]$SiteName = "",
    [string]$OfflineFile = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PublishDir)) {
    throw "Publish directory not found: $PublishDir"
}

if (-not (Test-Path -LiteralPath $SitePath)) {
    New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
}

$PublishDir = (Resolve-Path -LiteralPath $PublishDir).Path
$SitePath = (Resolve-Path -LiteralPath $SitePath).Path

if ([string]::IsNullOrWhiteSpace($OfflineFile)) {
    $repoOffline = Join-Path $PSScriptRoot "..\deploy\app_offline.htm"
    if (Test-Path -LiteralPath $repoOffline) {
        $OfflineFile = (Resolve-Path -LiteralPath $repoOffline).Path
    }
}

function Import-WebAdministrationSafe {
    if (Get-Module -ListAvailable -Name WebAdministration) {
        Import-Module WebAdministration -ErrorAction Stop
        return $true
    }
    return $false
}

$webAdmin = Import-WebAdministrationSafe

Write-Host "==> Taking site offline"
if ($webAdmin -and -not [string]::IsNullOrWhiteSpace($AppPoolName)) {
    try {
        if (Test-Path "IIS:\AppPools\$AppPoolName") {
            if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne "Stopped") {
                Stop-WebAppPool -Name $AppPoolName
            }
        }
    }
    catch {
        Write-Warning "Could not stop app pool '$AppPoolName': $($_.Exception.Message)"
    }
}

$offlineTarget = Join-Path $SitePath "app_offline.htm"
if (-not [string]::IsNullOrWhiteSpace($OfflineFile) -and (Test-Path -LiteralPath $OfflineFile)) {
    Copy-Item -LiteralPath $OfflineFile -Destination $offlineTarget -Force
}
else {
    @"
<!DOCTYPE html><html><head><meta charset='utf-8'><title>Updating</title></head>
<body><h1>Updating Coditium POS</h1><p>Please try again shortly.</p></body></html>
"@ | Set-Content -LiteralPath $offlineTarget -Encoding UTF8
}

Start-Sleep -Seconds 3

Write-Host "==> Syncing files to $SitePath"
$robolog = Join-Path $env:TEMP ("pos-mt-deploy-" + [Guid]::NewGuid().ToString("N") + ".log")
& robocopy $PublishDir $SitePath /MIR /E /R:2 /W:2 /NFL /NDL /NP `
    /XD App_Data `
    /XF app_offline.htm web.config.bak `
    /LOG:$robolog

# robocopy exit codes 0-7 are success
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE (log: $robolog)"
}

Write-Host "==> Bringing site online"
Remove-Item -LiteralPath $offlineTarget -Force -ErrorAction SilentlyContinue

if ($webAdmin -and -not [string]::IsNullOrWhiteSpace($AppPoolName)) {
    try {
        if (Test-Path "IIS:\AppPools\$AppPoolName") {
            Start-WebAppPool -Name $AppPoolName
        }
    }
    catch {
        Write-Warning "Could not start app pool '$AppPoolName': $($_.Exception.Message)"
    }
}

if ($webAdmin -and -not [string]::IsNullOrWhiteSpace($SiteName)) {
    try {
        if (Test-Path "IIS:\Sites\$SiteName") {
            Start-Website -Name $SiteName -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Warning "Could not start website '$SiteName': $($_.Exception.Message)"
    }
}

Write-Host "==> IIS deploy complete"
exit 0
