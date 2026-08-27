#!/usr/bin/env bash
# Shared configuration for the nopCommerce Cloud Agent environment scripts.

# Database connection used by both the setup scripts and the running app.
export NOP_DB_HOST="${NOP_DB_HOST:-127.0.0.1}"
export NOP_DB_PORT="${NOP_DB_PORT:-5432}"
export NOP_DB_NAME="${NOP_DB_NAME:-nopcommerce}"
export NOP_DB_USER="${NOP_DB_USER:-nop}"
export NOP_DB_PASSWORD="${NOP_DB_PASSWORD:-nopCommerce_db_password}"

# Admin account created during installation.
export NOP_ADMIN_EMAIL="${NOP_ADMIN_EMAIL:-admin@yourStore.com}"
export NOP_ADMIN_PASSWORD="${NOP_ADMIN_PASSWORD:-Admin@123456}"

# Web app.
export NOP_WEB_DIR="${NOP_WEB_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/src/Presentation/Nop.Web}"
export NOP_WEB_PORT="${NOP_WEB_PORT:-5000}"

# Detected PostgreSQL major version (falls back to 16).
nop_pg_version() {
    ls /etc/postgresql 2>/dev/null | sort -n | tail -1 || echo 16
}
