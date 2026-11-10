param(
    [Parameter(Mandatory = $true)]
    [string]$PublishSettings,

    [string]$PosHubMode = $(if ($env:POS_HUB_MODE) { $env:POS_HUB_MODE } else { "Standard" }),
    [string]$ShopName = $(if ($env:SHOP_NAME) { $env:SHOP_NAME } else { "Main Shop" })
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PublishSettings)) {
    throw "Published settings not found: $PublishSettings"
}

$connection = [string]$env:CONNECTION_STRING
if ([string]::IsNullOrWhiteSpace($connection)) {
    throw "CONNECTION_STRING environment variable is required."
}
if ($connection -match "YOUR_SQL_SERVER|YOUR_USER|YOUR_PASSWORD") {
    throw "CONNECTION_STRING still contains placeholder values."
}

$central = [string]$env:CONNECTION_STRING_CENTRAL
if ([string]::IsNullOrWhiteSpace($central)) { $central = $connection }

$offline = [string]$env:CONNECTION_STRING_OFFLINE
if ([string]::IsNullOrWhiteSpace($offline)) { $offline = "Data Source=App_Data/pos-offline.db" }

$allowedHosts = [string]$env:ALLOWED_HOSTS
if ([string]::IsNullOrWhiteSpace($allowedHosts)) { $allowedHosts = "*" }

$pub = Get-Content -LiteralPath $PublishSettings -Raw | ConvertFrom-Json
if ($null -eq $pub.ConnectionStrings) {
    $pub | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue ([pscustomobject]@{})
}

$pub.ConnectionStrings | Add-Member -NotePropertyName POS_MT -NotePropertyValue $connection -Force
$pub.ConnectionStrings | Add-Member -NotePropertyName POS_MT_Central -NotePropertyValue $central -Force
$pub.ConnectionStrings | Add-Member -NotePropertyName POS_MT_Offline -NotePropertyValue $offline -Force
if ($null -ne $pub.ConnectionStrings.PSObject.Properties["POS_MT_Local"]) {
    $pub.ConnectionStrings.PSObject.Properties.Remove("POS_MT_Local")
}

if ($null -eq $pub.PosHub) {
    $pub | Add-Member -NotePropertyName PosHub -NotePropertyValue ([pscustomobject]@{})
}
$pub.PosHub | Add-Member -NotePropertyName Mode -NotePropertyValue $PosHubMode -Force
$pub.PosHub | Add-Member -NotePropertyName ShopName -NotePropertyValue $ShopName -Force
$pub.PosHub | Add-Member -NotePropertyName OfflineSqlitePath -NotePropertyValue "App_Data/pos-offline.db" -Force
$pub.PosHub | Add-Member -NotePropertyName SyncEnabled -NotePropertyValue $true -Force
$pub | Add-Member -NotePropertyName AllowedHosts -NotePropertyValue $allowedHosts -Force

$pub | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $PublishSettings -Encoding utf8
Write-Host "Applied production settings to $PublishSettings"
