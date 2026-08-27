#!/usr/bin/env bash
# Per-boot startup for the nopCommerce Cloud Agent environment.
# Brings PostgreSQL online and (re)writes the git-ignored connection settings so
# the freshly checked-out working tree points at the provisioned database.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${REPO_ROOT}/.cursor/env.sh"

# Start PostgreSQL and make sure the role/database/extensions exist.
"${REPO_ROOT}/.cursor/pg.sh" ensure

# Restore the connection settings that the app needs to recognise the database.
"${REPO_ROOT}/.cursor/pg.sh" write-appsettings

echo "PostgreSQL is running and nopCommerce connection settings are in place."
