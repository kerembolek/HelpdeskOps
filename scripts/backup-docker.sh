#!/usr/bin/env bash
# Backs up a Docker Compose install of HelpdeskOps: the database and App_Data (attachments and sign-in keys).
# Each run writes backups/<date-time>/ next to docker-compose.yml and deletes runs older than KEEP_DAYS.
# Usage: scripts/backup-docker.sh [KEEP_DAYS]   (default 14). Run it daily from cron, see docs/INSTALL_TR.md.
set -euo pipefail

cd "$(dirname "$0")/.."
keep_days="${1:-14}"
target="backups/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$target"

# Full backup inside the database container, checked with RESTORE VERIFYONLY, then copied out.
# MSSQL_SA_PASSWORD is read inside the container, so the password never appears in this machine's process list.
docker compose exec -T db bash -c '
  set -e
  mkdir -p /var/opt/mssql/backups
  sql() { /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -Q "$1"; }
  sql "BACKUP DATABASE [HelpdeskOps] TO DISK = N'\''/var/opt/mssql/backups/HelpdeskOps.bak'\'' WITH INIT, CHECKSUM"
  sql "RESTORE VERIFYONLY FROM DISK = N'\''/var/opt/mssql/backups/HelpdeskOps.bak'\'' WITH CHECKSUM"
'
docker compose cp db:/var/opt/mssql/backups/HelpdeskOps.bak "$target/HelpdeskOps.bak"

# Attachments and data protection keys; without the keys, everyone has to sign in again after a restore
docker compose exec -T app tar -C /app -czf - App_Data > "$target/App_Data.tar.gz"

find backups -mindepth 1 -maxdepth 1 -type d -mtime +"$keep_days" -exec rm -rf {} +

echo "Backup written to $target"
ls -lh "$target"
