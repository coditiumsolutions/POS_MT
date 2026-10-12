# Run as Administrator on the shop hub PC.
# Opens HTTP 5227 for LAN browsers. Does NOT open SQL 1433 to the network.

param(
    [int]$Port = 5227
)

$ruleName = "POS_MT LAN $Port"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Firewall rule already exists: $ruleName"
} else {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
    Write-Host "Created firewall rule: $ruleName"
}

Write-Host "Start the hub with:"
Write-Host "  dotnet run --launch-profile LocalHub --urls http://0.0.0.0:$Port"
Write-Host "Other PCs: http://<this-pc-ip>:$Port"
Write-Host "Do not expose SQL Server (1433) to the internet."
