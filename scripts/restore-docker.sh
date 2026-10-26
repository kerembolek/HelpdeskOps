#!/usr/bin/env bash
# Restores a backup made by backup-docker.sh, replacing the current database and App_Data.
# Usage: scripts/restore-docker.sh backups/<date-time>
set -euo pipefail

cd "$(dirname "$0")/.."
source_dir="${1:?Usage: scripts/restore-docker.sh backups/<date-time>}"
for file in HelpdeskOps.bak App_Data.tar.gz; do
  [[ -f "$source_dir/$file" ]] || { echo "Missing $source_dir/$file" >&2; exit 1; }
done

read -r -p "This replaces all current HelpdeskOps data with $source_dir. Type EVET to continue: " answer
[[ "$answer" == "EVET" ]] || { echo "Cancelled."; exit 1; }

# The app must not write while the data is replaced
docker compose stop app

docker compose exec -T db mkdir -p /var/opt/mssql/backups
docker compose cp "$source_dir/HelpdeskOps.bak" db:/var/opt/mssql/backups/restore.bak
docker compose exec -T -u root db chown mssql /var/opt/mssql/backups/restore.bak
docker compose exec -T db bash -c '
  set -e
  sql() { /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -Q "$1"; }
  sql "IF DB_ID(N'\''HelpdeskOps'\'') IS NOT NULL ALTER DATABASE [HelpdeskOps] SET SINGLE_USER WITH ROLLBACK IMMEDIATE"
  sql "RESTORE DATABASE [HelpdeskOps] FROM DISK = N'\''/var/opt/mssql/backups/restore.bak'\'' WITH REPLACE, CHECKSUM"
  sql "ALTER DATABASE [HelpdeskOps] SET MULTI_USER"
  rm /var/opt/mssql/backups/restore.bak
'

# App_Data goes back into the app volume through a one-off container that does not start the app
docker compose run --rm --no-deps -T --entrypoint sh app -c 'find /app/App_Data -mindepth 1 -delete && tar -C /app -xzf -' \
  < "$source_dir/App_Data.tar.gz"

docker compose start app
echo "Restored from $source_dir"
