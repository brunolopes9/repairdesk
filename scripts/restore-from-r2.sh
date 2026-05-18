#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  scripts/restore-from-r2.sh <r2-key> [database-name]

Required env vars:
  R2_ACCOUNT_ID
  R2_ACCESS_KEY_ID
  R2_SECRET_ACCESS_KEY
  R2_BUCKET
  DB_SA_PASSWORD

Optional env vars:
  SQL_CONTAINER=repairdesk-db
  BACKUP_DIR=/backups
  DATA_LOGICAL_NAME=RepairDesk
  LOG_LOGICAL_NAME=RepairDesk_log

Example:
  R2_ACCOUNT_ID=... R2_ACCESS_KEY_ID=... R2_SECRET_ACCESS_KEY=... \
  R2_BUCKET=repairdesk-prod-backups DB_SA_PASSWORD=... \
  scripts/restore-from-r2.sh backups/repairdesk-20260518-0300.bak RepairDesk
USAGE
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" || $# -lt 1 ]]; then
  usage
  exit 0
fi

: "${R2_ACCOUNT_ID:?R2_ACCOUNT_ID is required}"
: "${R2_ACCESS_KEY_ID:?R2_ACCESS_KEY_ID is required}"
: "${R2_SECRET_ACCESS_KEY:?R2_SECRET_ACCESS_KEY is required}"
: "${R2_BUCKET:?R2_BUCKET is required}"
: "${DB_SA_PASSWORD:?DB_SA_PASSWORD is required}"

R2_KEY="$1"
DB_NAME="${2:-RepairDesk}"
SQL_CONTAINER="${SQL_CONTAINER:-repairdesk-db}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
DATA_LOGICAL_NAME="${DATA_LOGICAL_NAME:-RepairDesk}"
LOG_LOGICAL_NAME="${LOG_LOGICAL_NAME:-RepairDesk_log}"
LOCAL_DIR="${LOCAL_DIR:-./backups/restore}"
LOCAL_FILE="${LOCAL_DIR}/$(basename "$R2_KEY")"
CONTAINER_FILE="${BACKUP_DIR}/$(basename "$R2_KEY")"
ENDPOINT="https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com"

mkdir -p "$LOCAL_DIR"

echo "Downloading s3://${R2_BUCKET}/${R2_KEY} from ${ENDPOINT}"
AWS_ACCESS_KEY_ID="$R2_ACCESS_KEY_ID" \
AWS_SECRET_ACCESS_KEY="$R2_SECRET_ACCESS_KEY" \
aws --endpoint-url "$ENDPOINT" s3 cp "s3://${R2_BUCKET}/${R2_KEY}" "$LOCAL_FILE"

echo "Copying backup into ${SQL_CONTAINER}:${CONTAINER_FILE}"
docker cp "$LOCAL_FILE" "${SQL_CONTAINER}:${CONTAINER_FILE}"

echo "Backup file list:"
docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$DB_SA_PASSWORD" -No \
  -Q "RESTORE FILELISTONLY FROM DISK = N'${CONTAINER_FILE}';"

echo "Restoring database ${DB_NAME}"
docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$DB_SA_PASSWORD" -No \
  -Q "IF DB_ID(N'${DB_NAME}') IS NOT NULL ALTER DATABASE [${DB_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [${DB_NAME}] FROM DISK = N'${CONTAINER_FILE}' WITH REPLACE, RECOVERY, CHECKSUM, MOVE N'${DATA_LOGICAL_NAME}' TO N'/var/opt/mssql/data/${DB_NAME}.mdf', MOVE N'${LOG_LOGICAL_NAME}' TO N'/var/opt/mssql/data/${DB_NAME}_log.ldf'; ALTER DATABASE [${DB_NAME}] SET MULTI_USER;"

echo "Running DBCC CHECKDB"
docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$DB_SA_PASSWORD" -No \
  -Q "DBCC CHECKDB([${DB_NAME}]) WITH NO_INFOMSGS;"

echo "Restore completed."
