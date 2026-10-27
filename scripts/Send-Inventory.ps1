<#
.SYNOPSIS
    Sends this computer's hardware details to HelpdeskOps.

.DESCRIPTION
    Reads the serial number, manufacturer, model, processor, RAM, system disk size, Windows version
    and IPv4 address, then posts them to /api/inventory. HelpdeskOps matches the computer by serial
    number: a known device is updated, an unknown one is added as a new device.

    Run it on every computer at sign-in and once a day, for example with a scheduled task deployed
    by Group Policy (Computer Configuration > Preferences > Control Panel Settings > Scheduled Tasks):
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File \\server\share\Send-Inventory.ps1
            -ServerUrl https://helpdesk.example.com -ApiKey <key>

    Antivirus products may flag scripts that collect system details and send them to a server.
    Add the script's folder to your antivirus exclusions (see docs/INSTALL_TR.md).

    Works with Windows PowerShell 5.1 and PowerShell 7. Needs no administrator rights.

.PARAMETER ServerUrl
    Address of the HelpdeskOps site, e.g. https://helpdesk.example.com

.PARAMETER ApiKey
    The "Inventory:ApiKey" setting of the site (INVENTORY_API_KEY in a Docker install).

.PARAMETER DryRun
    Prints the report instead of sending it.

.EXAMPLE
    .\Send-Inventory.ps1 -ServerUrl http://localhost:5161 -ApiKey dev-inventory-key
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerUrl,

    [string]$ApiKey,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# Chassis types that mean a portable computer (laptop, notebook, tablet, convertible...)
$portableChassis = 8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32

$system = Get-CimInstance Win32_ComputerSystem
$bios = Get-CimInstance Win32_BIOS
$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$($env:SystemDrive)'"
$chassis = (Get-CimInstance Win32_SystemEnclosure).ChassisTypes

$board = Get-CimInstance Win32_BaseBoard
$uuid = (Get-CimInstance Win32_ComputerSystemProduct).UUID

$isPortable = @($chassis | Where-Object { $portableChassis -contains $_ }).Count -gt 0

# Custom-built PCs often carry firmware placeholders instead of a real serial number or model.
# HelpdeskOps rejects these, so fall back to the motherboard serial and then to the system UUID.
$placeholders = 'system serial number', 'to be filled by o.e.m.', 'default string', 'not specified',
    'not applicable', 'none', 'n/a', 'na', '0', '00000000', '0123456789', '123456789', 'serial number',
    'chassis serial number', 'system product name', 'system manufacturer',
    '03000200-0400-0500-0006-000700080009', 'ffffffff-ffff-ffff-ffff-ffffffffffff', '00000000-0000-0000-0000-000000000000'

function Get-RealValue([string[]]$Candidates) {
    foreach ($value in $Candidates) {
        if ($value -and $value.Trim() -and ($placeholders -notcontains $value.Trim().ToLowerInvariant())) {
            return $value.Trim()
        }
    }
    return $null
}

$serial = Get-RealValue @($bios.SerialNumber, $board.SerialNumber)
if (-not $serial -and (Get-RealValue @($uuid))) { $serial = "UUID-$uuid" }
$model = Get-RealValue @($system.Model, $board.Product)

# The address on the adapter that has a default gateway is the one the helpdesk can reach
$ip = Get-NetIPConfiguration |
    Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
    ForEach-Object { $_.IPv4Address.IPAddress } |
    Select-Object -First 1

# "Windows 11 Pro" plus the feature update, e.g. 24H2
$displayVersion = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction SilentlyContinue).DisplayVersion
$osName = ($os.Caption -replace '^Microsoft\s+', '').Trim()
if ($displayVersion) { $osName = "$osName $displayVersion" }

$report = [ordered]@{
    hostname     = $env:COMPUTERNAME
    serialNumber = $serial
    ipAddress    = $ip
    manufacturer = Get-RealValue @($system.Manufacturer, $board.Manufacturer)
    model        = $model
    deviceType   = $(if ($isPortable) { 'Laptop' } else { 'Desktop' })
    processor    = ($cpu.Name -replace '\s+', ' ').Trim()
    ramGb        = [int][Math]::Round($system.TotalPhysicalMemory / 1GB)
    diskGb       = [int][Math]::Round($disk.Size / 1GB)
    osVersion    = $osName
}

$json = $report | ConvertTo-Json

if ($DryRun) {
    $json
    return
}

if (-not $ApiKey) { throw 'ApiKey is required unless -DryRun is used.' }
if (-not $serial) { throw 'No real serial number, motherboard serial or system UUID was found.' }
if (-not $ip) { throw 'No IPv4 address with a default gateway was found.' }

# Windows PowerShell 5.1 does not offer TLS 1.2 by default
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$result = Invoke-RestMethod -Method Post -Uri ($ServerUrl.TrimEnd('/') + '/api/inventory') `
    -Headers @{ 'X-Inventory-Key' = $ApiKey } `
    -ContentType 'application/json; charset=utf-8' `
    -Body ([Text.Encoding]::UTF8.GetBytes($json))

if ($result.created) {
    Write-Output "Added to HelpdeskOps as device $($result.deviceId)."
} else {
    Write-Output "Updated device $($result.deviceId) in HelpdeskOps."
}
