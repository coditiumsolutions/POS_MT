param(
    [Parameter(Mandatory = $true)]
    [string]$DevSettings,

    [Parameter(Mandatory = $true)]
    [string]$PublishSettings
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $DevSettings)) {
    throw "Development settings not found: $DevSettings"
}

if (-not (Test-Path -LiteralPath $PublishSettings)) {
    throw "Published appsettings.json not found: $PublishSettings"
}

$dev = Get-Content -LiteralPath $DevSettings -Raw | ConvertFrom-Json
$pub = Get-Content -LiteralPath $PublishSettings -Raw | ConvertFrom-Json

$connectionString = [string]$dev.ConnectionStrings.POS_MT
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'POS_MT connection string missing from appsettings.Development.json'
}

if ($connectionString -match 'YOUR_SQL_SERVER|YOUR_USER|YOUR_PASSWORD') {
    throw 'appsettings.Development.json still contains placeholder connection string values.'
}

if ($null -eq $pub.ConnectionStrings) {
    $pub | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue ([pscustomobject]@{})
}

$pub.ConnectionStrings | Add-Member -NotePropertyName POS_MT -NotePropertyValue $connectionString -Force

$centralCs = [string]$dev.ConnectionStrings.POS_MT_Central
if (-not [string]::IsNullOrWhiteSpace($centralCs) -and $centralCs -notmatch 'YOUR_') {
    $pub.ConnectionStrings | Add-Member -NotePropertyName POS_MT_Central -NotePropertyValue $centralCs -Force
}
elseif ([string]::IsNullOrWhiteSpace([string]$pub.ConnectionStrings.POS_MT_Central)) {
    $pub.ConnectionStrings | Add-Member -NotePropertyName POS_MT_Central -NotePropertyValue $connectionString -Force
}

$offlineCs = [string]$dev.ConnectionStrings.POS_MT_Offline
if ([string]::IsNullOrWhiteSpace($offlineCs)) {
    $offlineCs = 'Data Source=App_Data/pos-offline.db'
}
$pub.ConnectionStrings | Add-Member -NotePropertyName POS_MT_Offline -NotePropertyValue $offlineCs -Force

# Remove legacy SQL Express local key if present
if ($null -ne $pub.ConnectionStrings.PSObject.Properties['POS_MT_Local']) {
    $pub.ConnectionStrings.PSObject.Properties.Remove('POS_MT_Local')
}

if ($null -ne $dev.PosHub) {
    $pub | Add-Member -NotePropertyName PosHub -NotePropertyValue $dev.PosHub -Force
}

if ($null -eq $pub.PosHub) {
    $pub | Add-Member -NotePropertyName PosHub -NotePropertyValue ([pscustomobject]@{})
}
# Shop publish always runs as LocalHub (SQLite for POS; central SQL for sync only).
$pub.PosHub | Add-Member -NotePropertyName Mode -NotePropertyValue 'LocalHub' -Force
$pub.PosHub | Add-Member -NotePropertyName OfflineSqlitePath -NotePropertyValue 'App_Data/pos-offline.db' -Force
$pub.PosHub | Add-Member -NotePropertyName SyncEnabled -NotePropertyValue $true -Force

$urls = [string]$dev.Urls
if ([string]::IsNullOrWhiteSpace($urls)) {
    $urls = 'http://localhost:5227'
}
$pub | Add-Member -NotePropertyName Urls -NotePropertyValue $urls -Force

$pub | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $PublishSettings -Encoding utf8
Write-Host "Connection strings applied to $PublishSettings"
