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

$pub | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $PublishSettings -Encoding utf8
Write-Host "Connection string applied to $PublishSettings"
