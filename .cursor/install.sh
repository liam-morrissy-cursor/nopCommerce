#!/usr/bin/env bash
# Idempotent Cloud Agent install script for nopCommerce.
# Installs the .NET 10 SDK and PostgreSQL, provisions a dev database,
# builds the solution, and performs the one-time nopCommerce installation
# (schema + sample data + admin user) so the store is ready to run.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${REPO_ROOT}/.cursor/env.sh"

echo "==> [1/5] Ensuring the .NET 10 SDK is installed"
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
    # builds.dotnet.microsoft.com (used by dotnet-install.sh) is not on the egress
    # allowlist, so install from the Microsoft apt feed (packages.microsoft.com is allowed).
    tmp_deb="$(mktemp --suffix=.deb)"
    wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O "$tmp_deb"
    sudo dpkg -i "$tmp_deb"
    rm -f "$tmp_deb"
    sudo apt-get update -qq
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0
fi
dotnet --version

echo "==> [2/5] Ensuring PostgreSQL is installed"
if ! command -v psql >/dev/null 2>&1; then
    sudo apt-get update -qq
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq postgresql postgresql-client
fi

echo "==> [3/5] Starting PostgreSQL and provisioning the dev database"
"${REPO_ROOT}/.cursor/pg.sh" ensure

echo "==> [4/5] Building the solution"
dotnet build "${REPO_ROOT}/src/NopCommerce.sln" -c Debug

echo "==> [5/5] Installing nopCommerce (schema + sample data) if needed"
if "${REPO_ROOT}/.cursor/pg.sh" is-seeded; then
    echo "    Database already contains nopCommerce data; skipping installation."
    "${REPO_ROOT}/.cursor/pg.sh" write-appsettings
else
    "${REPO_ROOT}/.cursor/seed.sh"
fi

echo "==> Install complete."
