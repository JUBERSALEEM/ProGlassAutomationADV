# ProGlass SaaS API — multi-stage image (Phase 27).
#
# Build (from the saas/ directory — see docker-compose.yml build context):
#   docker build -f devops/docker/api.Dockerfile -t pglass-api .
#
# The migrations are EMBEDDED in ProGlass.Saas.Infrastructure
# (EmbeddedResource on database/migrations/*.sql), so the runtime image
# needs no SQL files. dotnet build/test runs in CI
# (saas/devbox/dotnet-ci.yml — the release gate); the image build only
# publishes.

# ── stage 1: build ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Project files first so `dotnet restore` layer-caches independently of
# source edits.
COPY backend/ProGlass.Saas.sln ./
COPY backend/src/ProGlass.Saas.Domain/ProGlass.Saas.Domain.csproj src/ProGlass.Saas.Domain/
COPY backend/src/ProGlass.Saas.Application/ProGlass.Saas.Application.csproj src/ProGlass.Saas.Application/
COPY backend/src/ProGlass.Saas.Infrastructure/ProGlass.Saas.Infrastructure.csproj src/ProGlass.Saas.Infrastructure/
COPY backend/src/ProGlass.Saas.Api/ProGlass.Saas.Api.csproj src/ProGlass.Saas.Api/
RUN dotnet restore src/ProGlass.Saas.Api/ProGlass.Saas.Api.csproj

# Sources + publish (Release, no tests — CI is the test gate).
# database/migrations MUST be present: ProGlass.Saas.Infrastructure
# embeds ..\..\..\database\migrations\*.sql (relative to its csproj), so
# it has to land at /database/migrations in the build stage — a missing
# wildcard match would publish an assembly WITHOUT migrations (silently).
COPY database/migrations/ /database/migrations/
COPY backend/ ./
# Prove the migrations actually embedded before shipping the image:
# manifest resource names are plain strings in the dll, and
# SqlMigrationRunner only loads names under this exact prefix.
RUN dotnet publish src/ProGlass.Saas.Api/ProGlass.Saas.Api.csproj \
    --no-restore --configuration Release --output /app \
 && test "$(grep -a -o 'ProGlass\.Saas\.Infrastructure\.Migrations\.00[0-9][0-9]_[a-z0-9_]*\.sql' \
      /app/ProGlass.Saas.Infrastructure.dll | sort -u | wc -l)" = "22"

# ── stage 2: runtime ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --system --no-create-home --uid 1654 pglass \
    && mkdir -p /var/lib/pglass/attachments \
    && chown -R pglass:pglass /var/lib/pglass

WORKDIR /app
COPY --from=build --chown=pglass:pglass /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    SAAS_ATTACHMENT_ROOT=/var/lib/pglass/attachments

USER pglass
EXPOSE 8080

# The same liveness probe nginx and the load balancer see.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ProGlass.Saas.Api.dll"]
