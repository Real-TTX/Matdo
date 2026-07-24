#!/usr/bin/env bash
# Matdo – Datenbank-Wiederherstellung aus einem Backup von scripts/backup.sh.
#
#   ./scripts/restore.sh <backup.sql.gz> [container]
#     backup.sql.gz : von backup.sh erzeugte Datei
#     container     : Name/ID des DB-Containers (Standard: matdo-db)
#
# ACHTUNG: ueberschreibt vorhandene Daten. Vorher die App stoppen.
set -euo pipefail

BACKUP="${1:?Bitte Backup-Datei angeben}"
CONTAINER="${2:-matdo-db}"

gunzip -c "$BACKUP" | docker exec -i "$CONTAINER" psql -U matdo -d matdo
echo "Wiederherstellung aus $BACKUP abgeschlossen."
