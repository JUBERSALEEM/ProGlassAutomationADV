# ProGlass SaaS — devops (Phase 27: the nginx/ops layer)

Everything an operator needs to run the SaaS. Two supported topologies,
one edge:

```
client ─ nginx :80/:443 ── api (ASP.NET Core 8, Kestrel :8080)
                         │     SAAS_DB        = saas_app_login  (DML only, RLS)
                         │     SAAS_PLATFORM_DB = saas_owner_login (provisioning)
                         ├─ migrations: one-shot `migrate` step (owner login)
                         ├─ attachments: /var/lib/pglass/attachments (Phase 24)
                         └─ db (PostgreSQL 16, pglass database)
```

| Path | Files | When |
|---|---|---|
| Docker Compose | `docker-compose.yml`, `docker/api.Dockerfile`, `postgres/init/10-roles.sh`, `nginx/` | single host, fastest start |
| Bare metal / VM | `systemd/pglass-api.{service,env}`, `systemd/pglass-{backup,restore-drill}.*`, `nginx/` (upstream → `127.0.0.1:8081`) | existing Postgres, no Docker |

## 1. Compose quickstart

```bash
cd saas/devops
cp .env.example .env && vi .env        # 4 secrets — never commit .env
docker compose up -d db                # first boot: roles bootstrap
docker compose run --rm migrate        # applies all 22 migrations, exits
docker compose up -d                   # api + nginx
curl -f http://localhost/health        # {"status":"healthy","database":"connected"}
```

What happens on `db` first boot: `postgres/init/10-roles.sh` creates the
`saas_owner`/`saas_app` **group** roles (migration 0005's guarded block
then skips) and the two **login** roles (`saas_app_login`,
`saas_owner_login`) with the passwords from `.env`. The one-shot
`migrate` service runs the same API image with `SAAS_MIGRATE_AND_EXIT=true`
over the owner login (migrations are DDL); the serving `api` container
keeps the DML-only login — the same RLS path CI exercises. Nothing in
the repo creates login roles with passwords by design (no secrets in
SQL); this bootstrap is the compose expression of that.

## 2. The API image (`docker/api.Dockerfile`)

Multi-stage: `sdk:8.0` restore + `publish Release` → `aspnet:8.0`
runtime, non-root `pglass` user, `curl`-based `HEALTHCHECK` on
`/health`, attachments at `/var/lib/pglass/attachments` (named volume —
Docker copies the image's ownership on first mount, so the non-root user
can write). The migrations are **embedded resources** in
`ProGlass.Saas.Infrastructure`, so no SQL files ship in the image.
`dotnet build && dotnet test` is the CI release gate
(`saas/devbox/dotnet-ci.yml`), not the image build.

## 3. The edge (`nginx/`)

- `nginx.conf` — top level: gzip (JSON + SPA assets), `server_tokens
  off`, and the `auth_edge` rate-limit zone (10 req/min/IP) — the
  second credential-throttling layer above the API's own
  `LoginRateLimitMiddleware`, which is per-instance.
- `conf.d/proglass.conf` — the site: `/health` passthrough; `/api/` →
  `api:8080` (full URI preserved, `client_max_body_size 12m` for the
  10 MB attachment limit, `proxy_read_timeout 600s` for synchronous
  optimization runs); `/` → static root with a clearly-labelled holding
  page (the Phase 28 SPA replaces it — swap the volume, the config
  stays). A commented TLS/443 block documents the certbot flow; enable
  by uncommenting and mounting `/etc/letsencrypt`.
- `www/` — the holding page.

The API reads `X-Forwarded-*` only when
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (Program.cs, Phase 27) — set
by compose and the systemd env template. Direct exposure (no proxy)
keeps the default: off.

## 4. Configuration matrix (all env, never appsettings)

| Variable | Used by | Notes |
|---|---|---|
| `SAAS_DB` | runtime data access | DML-only login; RLS applies `app.tenant_id` |
| `SAAS_PLATFORM_DB` | registration + subscription admin (ADR-011) | owner login; keep separate |
| `SAAS_JWT_SIGNING_KEY` | JWT HS256 | ≥ 32 random bytes; startup refuses short keys |
| `SAAS_MIGRATE_ON_STARTUP` | boot-time migrations | `true` only for single-instance convenience; the compose path uses the one-shot `migrate` step instead |
| `SAAS_MIGRATE_AND_EXIT` | one-shot migrate container | applies pending migrations and exits (release-step mode) |
| `SAAS_ATTACHMENT_ROOT` | Phase 24 byte storage | volume-backed; never inside the image |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | behind nginx | trust `X-Forwarded-For/Proto` |

## 5. Upgrade procedure

```bash
cd saas && git pull
docker compose build api                # or publish to /opt/pglass on bare metal
docker compose run --rm migrate         # new migrations apply + exit
docker compose up -d                    # recreates the api container
```

Bare metal: `systemctl stop pglass-api` → publish/copy → the
migrate-and-exit command from `systemd/pglass-api.env` → `systemctl
start pglass-api`.

## 6. Backups + drills

- Compose: run the documented `devbox/backup_db.py` commands
  (SAAS_DEPLOYMENT.md §6) from cron against the `db` service.
- Bare metal: `systemd/pglass-backup.timer` (nightly 02:15) +
  `pglass-restore-drill.timer` (Sunday 04:00) — edit the connection in
  the service files first.
- Retention 10 sets; the manual-warn window refuses a second set within
  10 minutes unless `--force` (WPF-verbatim constants).

## 7. What is deliberately NOT here

- **No redis / minio containers** — the report cache is an in-process
  30s singleton (Phase 23); attachments are filesystem storage (Phase
  24). The architecture doc lists Redis/S3 as future options behind
  existing interfaces; nothing pretends to run them.
- **No worker container** — background jobs are a documented deferral
  (the WPF desktop app has none; scans run on demand).
- **No Kubernetes manifests** — compose is the Phase 27 scope ("→
  Kubernetes-ready", architecture §1); the image is stateless apart
  from the attachments volume, so the migration is mechanical later.
- **CI** stays at `saas/devbox/dotnet-ci.yml` (copy into
  `.github/workflows/` to activate — the note in that file explains
  why it cannot be pushed by the agent's token).
