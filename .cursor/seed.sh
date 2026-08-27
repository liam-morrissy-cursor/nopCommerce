#!/usr/bin/env bash
# Performs the one-time nopCommerce installation by driving the built-in
# /install wizard over HTTP: creates the schema, sample data and admin user in
# PostgreSQL. Safe to run only when the database is empty.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${REPO_ROOT}/.cursor/env.sh"

PORT="${NOP_WEB_PORT}"
BASE="http://127.0.0.1:${PORT}"
DLL="${NOP_WEB_DIR}/bin/Debug/net10.0/Nop.Web.dll"
LOG="/tmp/nop-install-app.log"
COOKIES="$(mktemp)"

if [[ ! -f "${DLL}" ]]; then
    echo "Build output not found at ${DLL}; run a build first." >&2
    exit 1
fi

# Start with a clean config so the app boots into the (uninstalled) wizard state.
rm -f "${NOP_WEB_DIR}/App_Data/appsettings.json"

echo "    Starting Nop.Web to run the installation wizard..."
( cd "${NOP_WEB_DIR}" && \
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://127.0.0.1:${PORT}" \
  exec dotnet "${DLL}" >"${LOG}" 2>&1 ) &
APP_PID=$!

cleanup() {
    kill "${APP_PID}" >/dev/null 2>&1 || true
    wait "${APP_PID}" 2>/dev/null || true
    rm -f "${COOKIES}"
}
trap cleanup EXIT

# Wait until the install page is being served (HTTP 200, not yet installed).
echo "    Waiting for the app to become ready..."
ready=""
for _ in $(seq 1 60); do
    code="$(curl -s -o /dev/null -w '%{http_code}' "${BASE}/install" || true)"
    if [[ "${code}" == "200" ]]; then ready=1; break; fi
    sleep 2
done
if [[ -z "${ready}" ]]; then
    echo "    App did not serve the install page in time. Log tail:" >&2
    tail -30 "${LOG}" >&2 || true
    exit 1
fi

# Extract the antiforgery token + cookie and post the installation form.
html="$(curl -s -c "${COOKIES}" "${BASE}/install")"
token="$(printf '%s' "${html}" | grep -oP 'name="__RequestVerificationToken"[^>]*value="\K[^"]+' | head -1)"
if [[ -z "${token}" ]]; then
    echo "    Could not read antiforgery token from install page." >&2
    exit 1
fi

echo "    Submitting installation (PostgreSQL + sample data)..."
curl -s -m 900 -b "${COOKIES}" -c "${COOKIES}" -X POST "${BASE}/install" \
    --data-urlencode "__RequestVerificationToken=${token}" \
    --data-urlencode "AdminEmail=${NOP_ADMIN_EMAIL}" \
    --data-urlencode "AdminPassword=${NOP_ADMIN_PASSWORD}" \
    --data-urlencode "ConfirmPassword=${NOP_ADMIN_PASSWORD}" \
    --data-urlencode "DataProvider=3" \
    --data-urlencode "ConnectionStringRaw=false" \
    --data-urlencode "ServerName=${NOP_DB_HOST}" \
    --data-urlencode "DatabaseName=${NOP_DB_NAME}" \
    --data-urlencode "Username=${NOP_DB_USER}" \
    --data-urlencode "Password=${NOP_DB_PASSWORD}" \
    --data-urlencode "IntegratedSecurity=false" \
    --data-urlencode "CreateDatabaseIfNotExists=false" \
    --data-urlencode "InstallSampleData=true" \
    --data-urlencode "SubscribeNewsletters=false" \
    --data-urlencode "Country=" \
    -o /dev/null

# Wait until the database reports as seeded.
echo "    Waiting for installation to finish..."
for _ in $(seq 1 60); do
    if "${REPO_ROOT}/.cursor/pg.sh" is-seeded; then
        echo "    nopCommerce installation completed successfully."
        exit 0
    fi
    sleep 3
done

echo "    Installation did not complete in time. App log tail:" >&2
tail -40 "${LOG}" >&2 || true
exit 1
