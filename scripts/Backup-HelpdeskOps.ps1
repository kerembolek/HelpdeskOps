<#
.SYNOPSIS
Backs up an IIS install of HelpdeskOps: the SQL Server database and the App_Data folder (attachments and sign-in keys).

.DESCRIPTION
Each run writes <BackupRoot>\<date-time>\ with HelpdeskOps.bak and App_Data.zip, checks the database backup with
RESTORE VERIFYONLY, and deletes runs older than KeepDays. Run it daily from Task Scheduler; see docs/INSTALL_TR.md.
Uses Windows authentication, so run it as an account that may back up the database. The SQL Server service account
must be able to write to BackupRoot, because SQL Server itself writes the .bak file.

.EXAMPLE
.\Backup-HelpdeskOps.ps1 -BackupRoot D:\Yedek\HelpdeskOps
#>
param(
    [Parameter(Mandatory)][string]$BackupRoot,
    [string]$SqlInstance = '.\SQLEXPRESS',
    [string]$Database = 'HelpdeskOps',
    [string]$AppPath = 'C:\inetpub\HelpdeskOps',
    [int]$KeepDays = 14
)
$ErrorActionPreference = 'Stop'

function Invoke-Sql([string]$Query) {
    # -C trusts SQL Server's own certificate, which newer sqlcmd versions otherwise reject on a local instance
    & sqlcmd -S $SqlInstance -E -C -b -Q $Query
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $Query" }
}

$target = Join-Path $BackupRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $target -Force | Out-Null
$bak = Join-Path $target "$Database.bak"

Invoke-Sql "BACKUP DATABASE [$Database] TO DISK = N'$bak' WITH INIT, CHECKSUM"
Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$bak' WITH CHECKSUM"

# Without the data protection keys in App_Data\keys, everyone has to sign in again after a restore
Compress-Archive -Path (Join-Path $AppPath 'App_Data') -DestinationPath (Join-Path $target 'App_Data.zip')

Get-ChildItem -Path $BackupRoot -Directory |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } |
    Remove-Item -Recurse -Force

Write-Host "Backup written to $target"
