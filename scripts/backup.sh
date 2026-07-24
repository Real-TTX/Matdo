#!/usr/bin/env bash
# Matdo – Datenbank-Backup (pg_dump aus dem laufenden Postgres-Container).
#
#   ./scripts/backup.sh [container] [zielDatei]
#     container  : Name/ID des DB-Containers (Standard: matdo-db)
#     zielDatei  : Ausgabedatei (Standard: ./matdo-backup-<zeitstempel>.sql.gz)
#
# Wiederherstellen: siehe scripts/restore.sh
# Tipp: per Cron regelmaessig laufen lassen und die Dumps ausser Haus sichern.
# Hinweis: Sichere zusaetzlich das /data-Volume (enthaelt Konfiguration inkl.
# DataProtection-Schluessel – ohne diese lassen sich verschluesselte Secrets nicht lesen).
set -euo pipefail

CONTAINER="${1:-matdo-db}"
OUT="${2:-./matdo-backup-$(date +%Y%m%d-%H%M%S).sql.gz}"

docker exec "$CONTAINER" pg_dump -U matdo -d matdo | gzip > "$OUT"
echo "Backup geschrieben: $OUT"
