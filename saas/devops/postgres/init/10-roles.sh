# ProGlass SaaS — PostgreSQL first-boot bootstrap (Phase 27).
#
# Mounted at /docker-entrypoint-initdb.d by docker-compose.yml; runs ONCE
# on the very first boot of the db volume, as the container superuser.
#
# Why this exists: migration 0005 creates the saas_owner/saas_app GROUP
# roles (NOLOGIN) under IF NOT EXISTS guards, but nothing in the repo
# creates the LOGIN roles production actually connects with — the CI
# workflow and the Python harness create their stand-ins themselves.
# This script pre-creates the group roles (so 0005's guards simply skip)
# and the two LOGIN roles the compose topology uses:
#
#   saas_owner_login  IN ROLE saas_owner — the elevated connection
#                     (SAAS_PLATFORM_DB + the one-shot migrate step)
#   saas_app_login    IN ROLE saas_app   — the DML-only runtime
#                     connection (SAAS_DB, RLS path)
#
# Passwords come from SAAS_APP_PASSWORD / SAAS_OWNER_PASSWORD in .env
# (never committed). To rotate: `ALTER ROLE … LOGIN PASSWORD '…'` and
# update .env — this script only runs on an empty data volume.
set -euo pipefail

: "${SAAS_APP_PASSWORD:?SAAS_APP_PASSWORD must be set (see .env.example)}"
: "${SAAS_OWNER_PASSWORD:?SAAS_OWNER_PASSWORD must be set (see .env.example)}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<SQL
-- Group roles first (mirrors 0005's guarded block; 0005 skips when present).
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_owner') THEN
        CREATE ROLE saas_owner NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_app') THEN
        CREATE ROLE saas_app NOLOGIN;
    END IF;
END
\$\$;

-- Login roles (idempotent so a re-initialized volume with the same
-- script never fails halfway).
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_app_login') THEN
        CREATE ROLE saas_app_login LOGIN PASSWORD '${SAAS_APP_PASSWORD}' IN ROLE saas_app;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_owner_login') THEN
        CREATE ROLE saas_owner_login LOGIN PASSWORD '${SAAS_OWNER_PASSWORD}' IN ROLE saas_owner;
    END IF;
END
\$\$;

-- 0001/0005 grant the privileges (schema usage, DML, default
-- privileges for future tables) to the GROUP roles; members inherit.
SQL

echo "pglass bootstrap: group + login roles ready."
