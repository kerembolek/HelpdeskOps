<#
.SYNOPSIS
Restores a backup made by Backup-HelpdeskOps.ps1, replacing the current database and App_Data folder.

.DESCRIPTION
Stops the IIS application pool, restores the database and App_Data, then starts the pool again. Run it in an
elevated PowerShell. The SQL Server service account must be able to read the backup folder.

.EXAMPLE
.\Restore-HelpdeskOps.ps1 -BackupPath D:\Yedek\HelpdeskOps\20261026-020000
#>
param(
    [Parameter(Mandatory)][string]$BackupPath,
    [string]$SqlInstance = '.\SQLEXPRESS',
    [string]$Database = 'HelpdeskOps',
    [string]$AppPath = 'C:\inetpub\HelpdeskOps',
    [string]$AppPool = 'HelpdeskOps'
)
$ErrorActionPreference = 'Stop'

function Invoke-Sql([string]$Query) {
    & sqlcmd -S $SqlInstance -E -C -b -Q $Query
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $Query" }
}

$bak = Join-Path $BackupPath "$Database.bak"
$zip = Join-Path $BackupPath 'App_Data.zip'
foreach ($file in $bak, $zip) {
    if (-not (Test-Path $file)) { throw "Missing $file" }
}

$answer = Read-Host "This replaces all current HelpdeskOps data with $BackupPath. Type EVET to continue"
if ($answer -cne 'EVET') { Write-Host 'Cancelled.'; exit 1 }

# The app must not write while the data is replaced
Import-Module WebAdministration
if ((Get-WebAppPoolState -Name $AppPool).Value -ne 'Stopped') { Stop-WebAppPool -Name $AppPool }
while ((Get-WebAppPoolState -Name $AppPool).Value -ne 'Stopped') { Start-Sleep -Seconds 1 }

Invoke-Sql "IF DB_ID(N'$Database') IS NOT NULL ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE"
Invoke-Sql "RESTORE DATABASE [$Database] FROM DISK = N'$bak' WITH REPLACE, CHECKSUM"
Invoke-Sql "ALTER DATABASE [$Database] SET MULTI_USER"

# The zip holds the App_Data folder itself
$appData = Join-Path $AppPath 'App_Data'
if (Test-Path $appData) { Get-ChildItem -Path $appData -Force | Remove-Item -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $AppPath -Force

Start-WebAppPool -Name $AppPool
Write-Host "Restored from $BackupPath"
