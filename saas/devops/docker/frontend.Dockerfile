# ProGlass SaaS SPA — Phase 28.
#
# Multi-stage: node builds the Vite bundle (tsc -b && vite build — the
# same gate run in the devbox), then the artifacts land in the SAME
# nginx edge image the Phase 27 topology uses (conf.d still mounts from
# saas/devops/nginx — the site config's `location /` try_files already
# points at this root).
#
# Build context is the saas/ directory (docker-compose.yml builds with
# context: ..; .dockerignore allows backend/, database/migrations/ and
# frontend/).

# ── stage 1: build ────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /src

# Layer-cached dependency install (package-lock.json is committed).
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build          # tsc -b && vite build → dist/

# ── stage 2: the edge with the SPA ────────────────────────────────────
FROM nginx:1.27-alpine
COPY --from=build /src/dist /usr/share/nginx/html
