#!/usr/bin/env bash
# Redeploys Hoshi Bot from the repo checkout on the host. See README.md's
# "Production deployment" section for why each step exists and why the order
# matters — this script just codifies that sequence so it's one command
# instead of four.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

echo "==> git pull"
git pull

echo "==> Building images (bot, web, migrator)"
docker compose --profile migrate build

echo "==> Applying pending EF Core migrations"
docker compose --profile migrate run --rm migrator

echo "==> Recreating bot/web against the migrated schema"
docker compose up -d

echo "==> Done."
