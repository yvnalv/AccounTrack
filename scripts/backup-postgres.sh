#!/usr/bin/env bash
#
# Nightly PostgreSQL backup for the dockerized `postgres` container (DEPLOYMENT.md §8).
# Dumps each database to a gzipped, timestamped file and prunes backups older than RETENTION_DAYS.
#
# Usage (typically from cron):
#   /path/to/AccounTrack/scripts/backup-postgres.sh
#
# Override defaults via environment variables:
#   PG_CONTAINER    docker container name of PostgreSQL   (default: postgres)
#   PG_SUPERUSER    a role that can read every database   (default: abc)
#   BACKUP_DIR      where dumps are written               (default: /root/backups)
#   RETENTION_DAYS  delete dumps older than this          (default: 14)
#   DATABASES       space-separated list to back up       (default: "accountrack postgresdb")
#
# Restore one database, e.g.:
#   gunzip -c /root/backups/accountrack_YYYYmmdd-HHMMSS.sql.gz \
#     | docker exec -i postgres psql -U abc -d accountrack
#
set -euo pipefail

PG_CONTAINER="${PG_CONTAINER:-postgres}"
PG_SUPERUSER="${PG_SUPERUSER:-yvnalvworks}"
BACKUP_DIR="${BACKUP_DIR:-/root/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
DATABASES="${DATABASES:-accountrack postgresdb}"

mkdir -p "$BACKUP_DIR"
stamp="$(date +%Y%m%d-%H%M%S)"

# Fail early if the container isn't running.
if ! docker ps --format '{{.Names}}' | grep -qx "$PG_CONTAINER"; then
  echo "ERROR: container '$PG_CONTAINER' is not running." >&2
  exit 1
fi

for db in $DATABASES; do
  out="$BACKUP_DIR/${db}_${stamp}.sql.gz"
  tmp="${out}.partial"
  echo "$(date '+%F %T')  backing up '$db' -> $out"
  # --clean --if-exists makes the dump self-contained and safely restorable over an existing db.
  # Write to a .partial file first so a crash never leaves a truncated dump that looks complete.
  if docker exec "$PG_CONTAINER" pg_dump -U "$PG_SUPERUSER" -d "$db" --clean --if-exists \
       | gzip > "$tmp"; then
    mv "$tmp" "$out"
  else
    echo "ERROR: pg_dump failed for '$db'." >&2
    rm -f "$tmp"
    exit 1
  fi
done

# Prune old backups.
deleted="$(find "$BACKUP_DIR" -name '*.sql.gz' -type f -mtime +"$RETENTION_DAYS" -print -delete | wc -l)"
echo "$(date '+%F %T')  backup complete. pruned ${deleted} file(s) older than ${RETENTION_DAYS} days. kept in $BACKUP_DIR"
