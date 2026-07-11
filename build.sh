#!/usr/bin/env bash
# Baut das Matdo-Docker-Image und deployt den Stack.
# Versionsschema:
#   release : <major>.<minor>.<build>-<builddate>
#   nightly : nightly-<build>-<builddate>
#   local   : local-<builddate>
#
# Verwendung:
#   ./build.sh                 # lokaler Build + Deploy (Port 6006)
#   ./build.sh nightly
#   ./build.sh release
#   ./build.sh local --no-deploy
set -euo pipefail
cd "$(dirname "$0")"

CHANNEL="${1:-local}"
NO_DEPLOY="${2:-}"
# Erlaube './build.sh --no-deploy' (Channel bleibt 'local'), analog zu build.ps1 -NoDeploy.
if [ "$CHANNEL" = "--no-deploy" ]; then CHANNEL="local"; NO_DEPLOY="--no-deploy"; fi
BUILD_DATE="$(date +%Y%m%d)"

case "$CHANNEL" in
  local)
    VERSION="local-${BUILD_DATE}"
    ;;
  nightly)
    BUILD=$(( $(cat build/buildnumber.txt | tr -d '[:space:]') + 1 ))
    echo "$BUILD" > build/buildnumber.txt
    VERSION="nightly-${BUILD}-${BUILD_DATE}"
    ;;
  release)
    BUILD=$(( $(cat build/buildnumber.txt | tr -d '[:space:]') + 1 ))
    echo "$BUILD" > build/buildnumber.txt
    MM="$(cat build/version.txt | tr -d '[:space:]')"
    VERSION="${MM}.${BUILD}-${BUILD_DATE}"
    ;;
  *)
    echo "Unbekannter Channel: $CHANNEL (local|nightly|release)"; exit 1 ;;
esac

echo "==> Matdo Build"
echo "    Channel : $CHANNEL"
echo "    Version : $VERSION"

export MATDO_VERSION="$VERSION"
if [ "$CHANNEL" = "release" ]; then export ASPNETCORE_ENVIRONMENT=Production; else export ASPNETCORE_ENVIRONMENT=Development; fi

if [ "$NO_DEPLOY" = "--no-deploy" ]; then
  docker compose build
  echo "==> Image gebaut: matdo:$VERSION"
elif [ "$CHANNEL" = "release" ]; then
  docker compose -f docker-compose.yml -f docker-compose.release.yml up -d --build
  echo "==> Stack läuft: http://localhost:6006 (Version $VERSION)"
else
  docker compose up -d --build
  echo "==> Stack läuft: http://localhost:6006 (Version $VERSION)"
fi
