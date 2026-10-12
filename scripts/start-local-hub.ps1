# Starts the shop Local Hub (SQLite offline + sync when online).
# Shop PCs should open THIS URL - not https://pos.coditium.com - so sales work when internet is down.
param(
    [string]$PublishDir = "",
    [int]$Port = 5227,
    [switch]$SkipBrowser
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $candidate = Join-Path $PSScriptRoot '..\build'
    $PublishDir = (Resolve-Path -LiteralPath $candidate).Path
}

$exe = Join-Path $PublishDir 'POS_MT.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "POS_MT.exe not found in $PublishDir. Publish first: dotnet publish -c Release -o .\build"
}

$appData = Join-Path $PublishDir 'App_Data'
New-Item -ItemType Directory -Force -Path $appData | Out-Null
$sqlite = Join-Path $appData 'pos-offline.db'

Set-Location -LiteralPath $PublishDir

# Ensure SQLite catalog exists (needs internet / central SQL the first time).
if (-not (Test-Path -LiteralPath $sqlite)) {
    Write-Host "SQLite offline DB missing - running: POS_MT.exe offline pull"
    & $exe offline pull
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "offline pull failed. Hub will still start; Offline/Auto may be limited until pull succeeds while online."
    }
}

# Already listening?
$listening = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if (-not $listening) {
    Write-Host "Starting Local Hub on http://0.0.0.0:$Port ..."
    $env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    Start-Process -FilePath $exe -WorkingDirectory $PublishDir -WindowStyle Minimized
    Start-Sleep -Seconds 3
}
else {
    Write-Host "Local Hub already listening on port $Port."
}

$url = "http://localhost:$Port"
Write-Host ""
Write-Host "Use this URL for POS (works offline): $url"
Write-Host "Do NOT use https://pos.coditium.com when the internet is down - that site needs DNS/internet."
Write-Host ""

if (-not $SkipBrowser) {
    Start-Process $url
}
