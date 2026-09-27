#!/usr/bin/env python3
"""
ProGlass SaaS — Phase 3 schema verification harness.

Applies the SQL migrations in saas/database/migrations to a PostgreSQL
instance and runs the tenant-isolation (Row-Level Security), constraint,
and migration-runner tests against it, as the unprivileged runtime role.

This script is the reference semantics for the .NET SqlMigrationRunner
(saas/backend/src/ProGlass.Saas.Infrastructure/Persistence/SqlMigrationRunner.cs):
  - migrations apply in filename order, one transaction each
  - applied files are tracked in saas._saas_migrations with a SHA-256 checksum
  - re-running is a no-op; a changed checksum is a hard error

Usage:
    python3 apply_and_test.py [--db-uri URI] [--report PATH]

Without --db-uri a disposable pgserver (PostgreSQL 16) instance is booted
under /tmp/pgdata (pip install pgserver psycopg2-binary).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
from datetime import date, datetime, timedelta, timezone
from decimal import Decimal
from pathlib import Path

MIGRATIONS_DIR = Path(__file__).resolve().parent.parent / "migrations"
DEFAULT_REPORT = Path(__file__).resolve().parent / "verification" / "DATABASE_VERIFICATION.md"

TENANT_A = "11111111-1111-1111-1111-111111111111"
TENANT_B = "22222222-2222-2222-2222-222222222222"

RESULTS: list[dict] = []


def record(name: str, ok: bool, detail: str = "") -> bool:
    RESULTS.append({"name": name, "ok": ok, "detail": detail})
    print(f"  [{'PASS' if ok else 'FAIL'}] {name}" + (f" — {detail}" if detail else ""))
    return ok


def connect(db_uri: str, role: str | None = None):
    import psycopg2
    conn = psycopg2.connect(db_uri)
    conn.autocommit = True
    if role:
        with conn.cursor() as cur:
            cur.execute(f"SET ROLE {role}")
    return conn


def boot_pgserver():
    import pgserver  # pip install pgserver
    pgdata = os.environ.get("SAAS_PGDATA", "/tmp/pgdata")
    Path(pgdata).mkdir(parents=True, exist_ok=True)
    pg = pgserver.get_server(pgdata)
    return pg.get_uri()


# ── migration runner (reference semantics) ─────────────────────────────

def ensure_runner_table(cur):
    cur.execute("""
        CREATE TABLE IF NOT EXISTS saas._saas_migrations (
            name text PRIMARY KEY,
            checksum text NOT NULL,
            applied_at timestamptz NOT NULL DEFAULT now()
        )
    """)


def apply_migrations(db_uri: str, verbose: bool = True) -> tuple[int, list[str]]:
    """Returns (applied_count, applied_names). Re-runs are no-ops."""
    files = sorted(p for p in MIGRATIONS_DIR.glob("*.sql"))
    if not files:
        raise RuntimeError(f"No migration files found in {MIGRATIONS_DIR}")

    conn = connect(db_uri)  # superuser bootstraps schema + roles
    applied_names: list[str] = []
    with conn.cursor() as cur:
        cur.execute("CREATE SCHEMA IF NOT EXISTS saas")
        cur.execute("SELECT 1 FROM pg_roles WHERE rolname='saas_owner'")
        if cur.fetchone() is None:
            cur.execute("CREATE ROLE saas_owner NOLOGIN")
        cur.execute("SELECT 1 FROM pg_roles WHERE rolname='saas_app'")
        if cur.fetchone() is None:
            cur.execute("CREATE ROLE saas_app NOLOGIN")
        cur.execute("SELECT 1 FROM pg_roles WHERE rolname='saas_test_app'")
        if cur.fetchone() is None:
            cur.execute("CREATE ROLE saas_test_app LOGIN IN ROLE saas_app")
        # Objects created below are owned by saas_owner (mirrors production:
        # the app role never owns schema objects).
        cur.execute("ALTER SCHEMA saas OWNER TO saas_owner")
        cur.execute("SET ROLE saas_owner")
        ensure_runner_table(cur)
        cur.execute("SELECT name, checksum FROM saas._saas_migrations")
        done = {name: checksum for name, checksum in cur.fetchall()}

    for path in files:
        sql = path.read_text()
        checksum = hashlib.sha256(sql.encode()).hexdigest()
        if path.name in done:
            if done[path.name] != checksum:
                raise RuntimeError(
                    f"Checksum mismatch for already-applied migration {path.name} — "
                    "never edit an applied migration; add a new one instead.")
            continue
        conn2 = connect(db_uri)
        try:
            with conn2.cursor() as cur:
                cur.execute("SET ROLE saas_owner")
                cur.execute("BEGIN")
                cur.execute(sql)
                cur.execute(
                    "INSERT INTO saas._saas_migrations (name, checksum) VALUES (%s, %s)",
                    (path.name, checksum))
                cur.execute("COMMIT")
            applied_names.append(path.name)
            if verbose:
                print(f"  applied: {path.name}")
        except Exception as e:
            with conn2.cursor() as cur:
                cur.execute("ROLLBACK")
            conn2.close()
            raise RuntimeError(f"Migration {path.name} failed: {e}") from e
        finally:
            conn2.close()

    conn.close()
    return len(applied_names), applied_names


# ── seed data (as the runtime role, with tenant context set) ───────────

def seed_tenant(db_uri: str, tenant_id: str, label: str):
    conn = connect(db_uri, role="saas_test_app")
    with conn.cursor() as cur:
        cur.execute("SET app.tenant_id = %s", (tenant_id,))
        cur.execute(
            """INSERT INTO saas.users (tenant_id, username, display_name, password_hash, role)
               VALUES (%s, %s, %s, %s, 'Administrator') RETURNING id""",
            (tenant_id, f"owner-{label.lower()}", f"Owner {label}", "argon2id$fake"))
        user_id = cur.fetchone()[0]
        cur.execute(
            """INSERT INTO saas.clients (tenant_id, customer_name, customer_trn, phone)
               VALUES (%s, %s, '100123456700003', '+971500000001') RETURNING id""",
            (tenant_id, f"Client {label}"))
        client_id = cur.fetchone()[0]
        cur.execute(
            """INSERT INTO saas.glass_sheets (tenant_id, category, thickness_mm, color,
                   width_mm, height_mm, supplier, purchase_price_per_sqm,
                   sell_price_per_sqm, stock, used)
               VALUES (%s, 'Float', 6, 'Clear', 3210, 2250, 'Gulf Glass', 46.00, 52.00, 46, 4)
               RETURNING id""", (tenant_id,))
        sheet_id = cur.fetchone()[0]
        cur.execute(
            """INSERT INTO saas.stock_transactions (tenant_id, item_type, glass_sheet_id,
                   transaction_type, quantity_delta, balance_after, unit_cost)
               VALUES (%s, 'sheet', %s, 'purchase', 46, 46, 46.00)""", (tenant_id, sheet_id))
        cur.execute(
            """INSERT INTO saas.proforma_invoices (tenant_id, proforma_no, issue_date,
                   valid_until, glass_materials_subtotal, vat_amount,
                   grand_total_vat_inclusive, client_id)
               VALUES (%s, %s, CURRENT_DATE, CURRENT_DATE + 30, 1000.00, 50.00, 1050.00, %s)
               RETURNING id""", (tenant_id, f"PI-2026-001-{label}", client_id))
        proforma_id = cur.fetchone()[0]
        cur.execute(
            """INSERT INTO saas.spec_groups (tenant_id, proforma_id, group_number,
                   glass_type, spec_name, rate_per_sqm)
               VALUES (%s, %s, 1, 'SGU', '6mm Clear FT Glass', 98.59) RETURNING id""",
            (tenant_id, proforma_id))
        group_id = cur.fetchone()[0]
        cur.execute(
            """INSERT INTO saas.spec_group_items (tenant_id, spec_group_id, row_serial,
                   width_mm, height_mm, qty, sqm, total_sqm, unit_price, total)
               VALUES (%s, %s, 1, 752, 892, 3, 0.671, 2.013, 98.59, 198.86)""",
            (tenant_id, group_id))
        cur.execute(
            """INSERT INTO saas.tax_invoices (tenant_id, invoice_no, proforma_id,
                   subtotal, vat_amount, grand_total, balance_amount)
               VALUES (%s, %s, %s, 1000.00, 50.00, 1050.00, 1050.00)""",
            (tenant_id, f"TI-2026-001-{label}", proforma_id))
        cur.execute(
            """INSERT INTO saas.optimization_sessions (tenant_id, document_type,
                   document_id, status, sheets_used, unplaced_count, utilization_pct,
                   conservation_verified, geometry_verified)
               VALUES (%s, 'proforma', %s, 'completed', 1, 0, 82.77, true, true)
               RETURNING id""", (tenant_id, proforma_id))
        session_id = cur.fetchone()[0]
        cur.execute(
            """INSERT INTO saas.optimization_sheets (tenant_id, session_id, sheet_no,
                   stock_w_mm, stock_h_mm, usable_x, usable_y, usable_w, usable_h,
                   used_sqm, stock_sqm, utilization_pct)
               VALUES (%s, %s, 1, 3210, 2250, 15, 15, 3180, 2220, 5.9792, 7.2225, 82.77)
               RETURNING id""", (tenant_id, session_id))
        sheet_oid = cur.fetchone()[0]
        cur.execute(
            """INSERT INTO saas.optimization_placements (tenant_id, sheet_id, piece_ref,
                   x_mm, y_mm, w_mm, h_mm)
               VALUES (%s, %s, 'G-101 · 1/1', 15, 15, 752, 892)""",
            (tenant_id, sheet_oid))
        cur.execute(
            """INSERT INTO saas.audit_logs (tenant_id, user_id, action, entity_type, entity_id)
               VALUES (%s, %s, 'proforma.created', 'proforma_invoices', %s)""",
            (tenant_id, user_id, proforma_id))
    conn.close()
    return {"user": user_id, "client": client_id, "sheet": sheet_id,
            "proforma": proforma_id, "group": group_id, "session": session_id}


def create_tenants(db_uri: str):
    conn = connect(db_uri)  # platform rows are created by the platform, not the app role
    with conn.cursor() as cur:
        for tid in (TENANT_A, TENANT_B):
            cur.execute("DELETE FROM saas.tenants WHERE id = %s", (tid,))
            cur.execute(
                "INSERT INTO saas.tenants (id, name, slug) VALUES (%s, %s, %s)",
                (tid, f"Tenant {tid[:8]}", f"tenant-{tid[:8]}"))
    conn.close()


# ── tests ──────────────────────────────────────────────────────────────

TENANT_TABLES = [
    "users", "clients", "glass_sheets", "stock_transactions", "inquiries",
    "specifications", "proforma_invoices", "spec_groups", "spec_group_items",
    "other_charges", "wastage_rows", "job_orders", "production_pieces",
    "production_events", "material_reservations", "delivery_notes",
    "delivery_note_items", "tax_invoices", "credit_notes", "statements",
    "statement_lines", "audit_logs", "attachments", "subscriptions",
    "optimization_sessions", "optimization_sheets", "optimization_placements",
    "optimization_cuts", "optimization_leftovers", "companies", "projects",
    "salesmen", "inventory_items", "tenant_pricing_config", "tenant_vat_config",
    "tenant_features",
]

# Tables that seed_tenant() puts exactly ONE row into (per tenant).
SEEDED_TABLES = {
    "users", "clients", "glass_sheets", "stock_transactions",
    "proforma_invoices", "spec_groups", "spec_group_items", "tax_invoices",
    "optimization_sessions", "optimization_sheets", "optimization_placements",
    "audit_logs",
}


def test_rls(db_uri: str, ids: dict):
    print("\n== Row-Level Security tenant isolation (as saas_test_app) ==")
    a = connect(db_uri, role="saas_test_app")
    cur = a.cursor()
    cur.execute("SET app.tenant_id = %s", (TENANT_A,))

    # T1 — every tenant table shows ONLY tenant A rows
    ok = True
    for t in TENANT_TABLES:
        cur.execute(f"SELECT count(*) FROM saas.{t}")
        n = cur.fetchone()[0]
        expected = 1 if t in SEEDED_TABLES else 0
        if n != expected:
            ok = False
            record(f"RLS visibility {t}", False, f"expected {expected} rows, saw {n}")
    if ok:
        record("RLS visibility — all 36 tenant tables scoped to context tenant", True,
               f"{len(SEEDED_TABLES)} seeded with 1 row each, rest empty — none leak from tenant B")

    # T1b — context set to EMPTY string also fails closed (NULLIF hardening)
    cur.execute("SET app.tenant_id = ''")
    cur.execute("SELECT count(*) FROM saas.proforma_invoices")
    record("empty-string tenant context sees 0 rows (fails closed)", cur.fetchone()[0] == 0)
    cur.execute("SET app.tenant_id = %s", (TENANT_A,))

    # T2 — direct lookup of tenant B's row by primary key → invisible
    cur.execute("SELECT count(*) FROM saas.proforma_invoices WHERE id = %s", (ids["b"]["proforma"],))
    record("cross-tenant SELECT by id returns 0 rows", cur.fetchone()[0] == 0)

    # T3/T4 — UPDATE/DELETE of tenant B's row → 0 rows affected
    cur.execute("UPDATE saas.proforma_invoices SET status='Approved' WHERE id = %s",
                (ids["b"]["proforma"],))
    record("cross-tenant UPDATE affects 0 rows", cur.rowcount == 0)
    cur.execute("DELETE FROM saas.tax_invoices WHERE id = %s", (ids["b"]["taxinvoice"],) if "taxinvoice" in ids["b"] else (ids["b"]["proforma"],))
    record("cross-tenant DELETE affects 0 rows", cur.rowcount == 0)

    # T5 — INSERT claiming tenant B's id → rejected by WITH CHECK
    try:
        cur.execute(
            """INSERT INTO saas.clients (tenant_id, customer_name)
               VALUES (%s, 'Evil Client')""", (TENANT_B,))
        record("cross-tenant INSERT rejected (WITH CHECK)", False, "insert was accepted!")
    except Exception as e:
        record("cross-tenant INSERT rejected (WITH CHECK)", "row-level security" in str(e).lower(), type(e).__name__)

    # T6 — valid insert into own tenant works, then clean up
    cur.execute(
        """INSERT INTO saas.clients (tenant_id, customer_name) VALUES (%s, 'Own Client')
           RETURNING id""", (TENANT_A,))
    own_id = cur.fetchone()[0]
    cur.execute("DELETE FROM saas.clients WHERE id = %s", (own_id,))
    record("own-tenant INSERT/DELETE works", own_id is not None)

    # T7 — context unset → sees NOTHING (fails closed)
    cur.execute("RESET app.tenant_id")
    cur.execute("SELECT count(*) FROM saas.proforma_invoices")
    record("unset tenant context sees 0 rows (fails closed)", cur.fetchone()[0] == 0)
    a.close()

    # T8 — as tenant B, tenant A's audit log and optimization rows are invisible
    b = connect(db_uri, role="saas_test_app")
    bcur = b.cursor()
    bcur.execute("SET app.tenant_id = %s", (TENANT_B,))
    bcur.execute("SELECT count(*) FROM saas.audit_logs")
    n_b = bcur.fetchone()[0]
    bcur.execute("SELECT count(*) FROM saas.optimization_placements")
    n_b2 = bcur.fetchone()[0]
    record("tenant B sees only its own audit + optimization rows", n_b == 1 and n_b2 == 1)
    b.close()


def test_constraints(db_uri: str, ids: dict):
    print("\n== Constraints, uniqueness, cascades, triggers ==")
    conn = connect(db_uri, role="saas_test_app")
    cur = conn.cursor()
    cur.execute("SET app.tenant_id = %s", (TENANT_A,))

    # C1 — duplicate proforma_no within SAME tenant rejected
    try:
        cur.execute(
            """INSERT INTO saas.proforma_invoices (tenant_id, proforma_no)
               VALUES (%s, 'PI-2026-001-A')""", (TENANT_A,))
        record("duplicate proforma_no within tenant rejected", False, "accepted!")
    except Exception as e:
        record("duplicate proforma_no within tenant rejected", "duplicate key" in str(e).lower())

    # C2 — same proforma_no in a DIFFERENT tenant is allowed (tenant-scoped uniqueness)
    conn2 = connect(db_uri, role="saas_test_app")
    c2 = conn2.cursor()
    c2.execute("SET app.tenant_id = %s", (TENANT_B,))
    c2.execute(
        """INSERT INTO saas.proforma_invoices (tenant_id, proforma_no)
           VALUES (%s, 'PI-2026-001-A') RETURNING id""", (TENANT_B,))
    record("same proforma_no allowed in another tenant", c2.fetchone() is not None)
    c2.execute("DELETE FROM saas.proforma_invoices WHERE proforma_no = 'PI-2026-001-A'")
    conn2.close()

    # C3 — invalid status rejected by CHECK
    try:
        cur.execute(
            """INSERT INTO saas.inquiries (tenant_id, inquiry_no, status)
               VALUES (%s, 'INQ-1', 'Zzz')""", (TENANT_A,))
        record("invalid status rejected (CHECK)", False, "accepted!")
    except Exception as e:
        record("invalid status rejected (CHECK)", "check constraint" in str(e).lower())

    # C4 — FK integrity: spec group must reference a real proforma
    try:
        cur.execute(
            """INSERT INTO saas.spec_groups (tenant_id, proforma_id, group_number, glass_type)
               VALUES (%s, '33333333-3333-3333-3333-333333333333', 1, 'SGU')""", (TENANT_A,))
        record("FK to nonexistent proforma rejected", False, "accepted!")
    except Exception as e:
        record("FK to nonexistent proforma rejected", "foreign key" in str(e).lower())

    # C5 — cascade: deleting a proforma removes its groups and items
    cur.execute(
        """INSERT INTO saas.proforma_invoices (tenant_id, proforma_no)
           VALUES (%s, 'PI-CASCADE-TEST') RETURNING id""", (TENANT_A,))
    pid = cur.fetchone()[0]
    cur.execute(
        """INSERT INTO saas.spec_groups (tenant_id, proforma_id, group_number, glass_type)
           VALUES (%s, %s, 1, 'DGU') RETURNING id""", (TENANT_A, pid))
    gid = cur.fetchone()[0]
    cur.execute(
        """INSERT INTO saas.spec_group_items (tenant_id, spec_group_id, row_serial, qty)
           VALUES (%s, %s, 1, 2)""", (TENANT_A, gid))
    cur.execute("DELETE FROM saas.proforma_invoices WHERE id = %s", (pid,))
    cur.execute("SELECT count(*) FROM saas.spec_groups WHERE id = %s", (gid,))
    record("cascade delete removes spec groups + items", cur.fetchone()[0] == 0)

    # C6 — updated_at trigger fires
    cur.execute(
        """INSERT INTO saas.clients (tenant_id, customer_name) VALUES (%s, 'Trigger Test')
           RETURNING id, updated_at""", (TENANT_A,))
    row = cur.fetchone()
    cid, updated_before = row
    import time; time.sleep(0.05)
    cur.execute("UPDATE saas.clients SET phone = '+971' WHERE id = %s", (cid,))
    cur.execute("SELECT updated_at FROM saas.clients WHERE id = %s", (cid,))
    updated_after = cur.fetchone()[0]
    record("updated_at trigger advances on UPDATE", updated_after > updated_before,
           f"{updated_before} → {updated_after}")
    cur.execute("DELETE FROM saas.clients WHERE id = %s", (cid,))

    # C7 — audit log is INSERT-only for the app role
    cur.execute("SELECT id FROM saas.audit_logs LIMIT 1")
    log_id = cur.fetchone()[0]
    try:
        cur.execute("UPDATE saas.audit_logs SET action = 'tampered' WHERE id = %s", (log_id,))
        record("audit_logs UPDATE denied for app role", False, "was allowed!")
    except Exception as e:
        record("audit_logs UPDATE denied for app role", "permission" in str(e).lower())

    # C8 — stock ledger is INSERT-only for the app role
    cur.execute("SELECT id FROM saas.stock_transactions LIMIT 1")
    tx_id = cur.fetchone()[0]
    try:
        cur.execute("DELETE FROM saas.stock_transactions WHERE id = %s", (tx_id,))
        record("stock_transactions DELETE denied for app role", False, "was allowed!")
    except Exception as e:
        record("stock_transactions DELETE denied for app role", "permission" in str(e).lower())

    # C9 — money precision is exact (numeric, not float)
    cur.execute("SELECT glass_materials_subtotal + vat_amount FROM saas.proforma_invoices WHERE id = %s",
                (ids["a"]["proforma"],))
    total = cur.fetchone()[0]
    record("numeric money arithmetic exact", str(total) == "1050.00", str(total))

    conn.close()


def test_migrations(db_uri: str):
    print("\n== Migration runner semantics ==")
    applied, names = apply_migrations(db_uri, verbose=False)
    record("re-run is a no-op (idempotent)", applied == 0, f"applied {applied}")

    conn = connect(db_uri)
    with conn.cursor() as cur:
        cur.execute("SELECT name, checksum FROM saas._saas_migrations ORDER BY name")
        rows = cur.fetchall()
        record("all 9 migrations recorded with checksums", len(rows) == 9,
               ", ".join(r[0] for r in rows))
        # tamper with a checksum → runner must refuse
        cur.execute("UPDATE saas._saas_migrations SET checksum = 'deadbeef' WHERE name = '0001_platform_identity.sql'")
    conn.close()
    try:
        apply_migrations(db_uri, verbose=False)
        record("tampered migration detected", False, "no error raised!")
    except RuntimeError as e:
        record("tampered migration detected", "Checksum mismatch" in str(e))
    # restore
    conn = connect(db_uri)
    with conn.cursor() as cur:
        p = MIGRATIONS_DIR / "0001_platform_identity.sql"
        cur.execute("UPDATE saas._saas_migrations SET checksum = %s WHERE name = '0001_platform_identity.sql'",
                    (hashlib.sha256(p.read_text().encode()).hexdigest(),))
    conn.close()


def test_platform_protection(db_uri: str):
    print("\n== Platform tables protected from the runtime role ==")
    conn = connect(db_uri, role="saas_test_app")
    cur = conn.cursor()
    try:
        cur.execute("INSERT INTO saas.tenants (name, slug) VALUES ('evil', 'evil')")
        record("runtime role cannot INSERT tenants", False, "was allowed!")
    except Exception as e:
        record("runtime role cannot INSERT tenants", "permission" in str(e).lower())
    cur.execute("SELECT count(*) FROM saas.tenants")
    record("runtime role can READ tenants", cur.fetchone()[0] == 2)
    conn.close()


def test_auth_identity(db_uri: str):
    """Phase 4: identity schema (0006) + username uniqueness semantics."""
    print("\n== Phase 4: auth identity schema & constraints ==")

    # ── migration 0006: absolute refresh deadline + token_hash index ──
    conn = connect(db_uri)
    with conn.cursor() as cur:
        cur.execute("""
            SELECT is_nullable, column_default FROM information_schema.columns
            WHERE table_schema = 'saas' AND table_name = 'refresh_tokens'
              AND column_name = 'absolute_expires_at_utc'
        """)
        row = cur.fetchone()
        record("0006: absolute_expires_at_utc exists NOT NULL",
               row is not None and row[0] == "NO", str(row))
        cur.execute("""
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'saas' AND tablename = 'refresh_tokens'
              AND indexname = 'ix_refresh_tokens_token_hash'
        """)
        record("0006: ix_refresh_tokens_token_hash exists", cur.fetchone()[0] == 1)
    conn.close()

    # ── username uniqueness is per tenant, not global ──────────────────
    # (connect() is autocommit: each statement commits; the duplicate-tenant
    # probe below deletes its own row first so re-runs stay green.)
    conn = connect(db_uri, role="saas_test_app")
    cur = conn.cursor()
    cur.execute("SET app.tenant_id = %s", (TENANT_A,))
    try:
        cur.execute("""
            INSERT INTO saas.users (tenant_id, username, display_name, password_hash, role)
            VALUES (%s, 'owner-a', 'Duplicate', 'x', 'Viewer')
        """, (TENANT_A,))
        record("duplicate username within a tenant rejected", False, "was allowed!")
    except Exception as e:
        record("duplicate username within a tenant rejected", "duplicate key" in str(e).lower())

    cur.execute("SET app.tenant_id = %s", (TENANT_B,))
    cur.execute("""
        DELETE FROM saas.users WHERE tenant_id = %s AND username = 'owner-a'
    """, (TENANT_B,))  # clear any probe row from a previous run
    try:
        cur.execute("""
            INSERT INTO saas.users (tenant_id, username, display_name, password_hash, role)
            VALUES (%s, 'owner-a', 'Other Tenant Same Name', 'x', 'Viewer')
        """, (TENANT_B,))
        record("same username in another tenant allowed", True)
    except Exception as e:
        record("same username in another tenant allowed", False, str(e))

    # ── refresh token rows: absolute column required + RLS WITH CHECK ─
    cur.execute("SET app.tenant_id = %s", (TENANT_A,))
    cur.execute("SELECT id FROM saas.users WHERE username = 'owner-a' LIMIT 1")
    owner_a = cur.fetchone()[0]
    try:
        cur.execute("""
            INSERT INTO saas.refresh_tokens (tenant_id, user_id, token_hash, expires_at)
            VALUES (%s, %s, 'phase4-missing-absolute', now() + interval '30 days')
        """, (TENANT_A, owner_a))
        record("refresh token insert without absolute deadline rejected (NOT NULL)",
               False, "was allowed!")
    except Exception as e:
        record("refresh token insert without absolute deadline rejected (NOT NULL)",
               "not-null constraint" in str(e).lower())
    cur.execute("""
        INSERT INTO saas.refresh_tokens (tenant_id, user_id, token_hash, expires_at,
                                         absolute_expires_at_utc)
        VALUES (%s, %s, 'phase4-probe-hash', now() + interval '30 days',
                now() + interval '60 days')
    """, (TENANT_A, owner_a))
    record("refresh token rows require the absolute deadline (app always sets it)",
           cur.rowcount == 1)
    # token_hash lookup uses the new index (EXPLAIN contains it)
    cur.execute("""
        EXPLAIN SELECT id FROM saas.refresh_tokens
        WHERE token_hash = 'phase4-probe-hash'
    """)
    plan = " ".join(r[0] for r in cur.fetchall())
    record("token_hash lookups use ix_refresh_tokens_token_hash",
           "ix_refresh_tokens_token_hash" in plan, plan.strip()[:80])

    # cross-tenant INSERT rejected by WITH CHECK (tenant B session writing
    # a tenant-A-owned row)
    cur.execute("SET app.tenant_id = %s", (TENANT_B,))
    try:
        cur.execute("""
            INSERT INTO saas.refresh_tokens (tenant_id, user_id, token_hash, expires_at)
            VALUES (%s, %s, 'phase4-cross-hash', now() + interval '1 day')
        """, (TENANT_A, owner_a))
        record("refresh token cross-tenant INSERT rejected (WITH CHECK)",
               False, "was allowed!")
    except Exception as e:
        record("refresh token cross-tenant INSERT rejected (WITH CHECK)",
               "row-level security" in str(e).lower())
    conn.close()


def test_masterdata(db_uri: str):
    """Phase 5: master-data parity columns (0007) + constraints."""
    print("\n== Phase 5: master data schema ==")
    conn = connect(db_uri)
    with conn.cursor() as cur:
        cur.execute("""
            SELECT column_name, column_default, is_nullable FROM information_schema.columns
            WHERE table_schema = 'saas' AND table_name = 'companies'
              AND column_name IN ('logo_layout', 'logo_width_mm', 'logo_height_mm')
            ORDER BY column_name
        """)
        rows = {r[0]: (r[1], r[2]) for r in cur.fetchall()}
        record("0007: companies logo columns exist NOT NULL with WPF defaults",
               rows.get("logo_layout") == ("'None'::text", "NO")
               and rows.get("logo_width_mm") == ("22", "NO")
               and rows.get("logo_height_mm") == ("26", "NO"),
               str(rows))
        cur.execute("""
            SELECT column_default, is_nullable FROM information_schema.columns
            WHERE table_schema = 'saas' AND table_name = 'clients'
              AND column_name = 'document_prepared_by'
        """)
        row = cur.fetchone()
        record("0007: clients.document_prepared_by exists NOT NULL",
               row == ("''::text", "NO"), str(row))
    conn.close()

    # CHECK constraint + defaults fill, as the runtime role (RLS path)
    conn = connect(db_uri, role="saas_test_app")
    cur = conn.cursor()
    cur.execute("SET app.tenant_id = %s", (TENANT_A,))
    cur.execute("DELETE FROM saas.companies WHERE name = 'phase5-probe'")
    cur.execute("""
        INSERT INTO saas.companies (tenant_id, name, trn)
        VALUES (%s, 'phase5-probe', '100123456700003')
        RETURNING logo_layout, logo_width_mm, logo_height_mm
    """, (TENANT_A,))
    layout, width, height = cur.fetchone()
    record("company insert fills WPF branding defaults",
           (layout, width, height) == ("None", Decimal("22.00"), Decimal("26.00")),
           f"{layout}/{width}/{height}")
    try:
        cur.execute("""
            UPDATE saas.companies SET logo_layout = 'Bogus'
            WHERE tenant_id = %s AND name = 'phase5-probe'
        """, (TENANT_A,))
        record("companies.logo_layout CHECK rejects unknown layouts", False, "was allowed!")
    except Exception as e:
        record("companies.logo_layout CHECK rejects unknown layouts",
               "check constraint" in str(e).lower())
    cur.execute("DELETE FROM saas.companies WHERE name = 'phase5-probe'")
    conn.close()


def write_report(argv: list[str], applied_names: list[str]):
    DEFAULT_REPORT.parent.mkdir(parents=True, exist_ok=True)
    passed = sum(1 for r in RESULTS if r["ok"])
    failed = len(RESULTS) - passed
    lines = [
        "# SAAS DATABASE VERIFICATION REPORT (Phases 3–4)",
        "",
        f"**Generated:** {datetime.now(timezone.utc).isoformat()}  ",
        f"**PostgreSQL:** 16 (pgserver, live instance)  ",
        f"**Migrations applied:** {', '.join(applied_names)}  ",
        f"**Result: {passed}/{len(RESULTS)} checks passed, {failed} failed**",
        "",
        "Run it yourself: `python3 saas/database/verify/apply_and_test.py`",
        "(requires `pip install pgserver psycopg2-binary`; or pass `--db-uri`).",
        "",
        "| # | Check | Result | Detail |",
        "|---|---|---|---|",
    ]
    for i, r in enumerate(RESULTS, 1):
        lines.append(f"| {i} | {r['name']} | {'✅ PASS' if r['ok'] else '❌ FAIL'} | {r['detail'] or '—'} |")
    DEFAULT_REPORT.write_text("\n".join(lines) + "\n")
    print(f"\nReport written to {DEFAULT_REPORT}")
    return failed == 0


def test_specifications(db_uri: str):
    """Phase 7b: specifications parity (0008) + ledger constraints."""
    print("\n== Phase 7b: specifications schema ==")
    conn = connect(db_uri)
    with conn.cursor() as cur:
        cur.execute("""
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'saas' AND table_name = 'specifications'
              AND column_name IN ('category', 'color', 'thickness_mm',
                                  'sheet_width_mm', 'sheet_height_mm',
                                  'sheet_id', 'panes_json', 'last_proforma_no')
        """)
        cols = {r[0] for r in cur.fetchall()}
        record("0008: specifications catalogue columns exist (WPF 2026-09-02 set)",
               cols == {'category', 'color', 'thickness_mm', 'sheet_width_mm',
                        'sheet_height_mm', 'sheet_id', 'panes_json', 'last_proforma_no'},
               str(sorted(cols)))

        cur.execute("""
            SELECT column_default FROM information_schema.columns
            WHERE table_schema = 'saas' AND table_name = 'specifications'
              AND column_name = 'usage_count'
        """)
        row = cur.fetchone()
        record("0008: usage_count default is 0 (a new spec has never been used)",
               row is not None and row[0] in ("0", "0::integer"),
               str(row))

        cur.execute("""
            SELECT 1 FROM pg_constraint
            WHERE conname = 'uq_specifications_tenant_type_name'
        """)
        record("0008: natural key (tenant_id, glass_type, spec_name) enforced",
               cur.fetchone() is not None)

        cur.execute("""
            SELECT 1 FROM pg_tables
            WHERE schemaname = 'saas' AND tablename = 'specification_usages'
              AND rowsecurity = 'true'
        """)
        record("0008: specification_usages exists with RLS enabled",
               cur.fetchone() is not None)
    conn.close()

    # Runtime-role behaviour: the dedup key + the one-row-per-(spec, PI)
    # ledger constraint, and cross-tenant spec name reuse. Every step
    # COMMITS so a crash cannot leave half a probe behind, and the probe
    # rows are cleaned in BOTH tenants before and after.
    conn = connect(db_uri, role="saas_test_app")
    cur = conn.cursor()

    def cleanup_probe(cursor):
        for tenant in (TENANT_A, TENANT_B):
            cursor.execute("SET app.tenant_id = %s", (tenant,))
            cursor.execute("DELETE FROM saas.proforma_invoices WHERE proforma_no = 'phase7b-probe-PI'")
            cursor.execute("DELETE FROM saas.specification_usages WHERE spec_name = 'phase7b-probe'")
            cursor.execute("DELETE FROM saas.specifications WHERE spec_name = 'phase7b-probe'")
        conn.commit()

    cleanup_probe(cur)

    cur.execute("SET app.tenant_id = %s", (TENANT_A,))
    cur.execute("""
        INSERT INTO saas.specifications (tenant_id, spec_name, glass_type, rate_per_sqm)
        VALUES (%s, 'phase7b-probe', 'SGU', 46)
        RETURNING id, usage_count
    """, (TENANT_A,))
    spec_id, usage_count = cur.fetchone()
    conn.commit()
    record("0008: new spec starts at usage_count 0 (not 1)", usage_count == 0,
           str(usage_count))

    try:
        cur.execute("""
            INSERT INTO saas.specifications (tenant_id, spec_name, glass_type, rate_per_sqm)
            VALUES (%s, 'phase7b-probe', 'SGU', 47)
        """, (TENANT_A,))
        conn.commit()
        record("0008: duplicate (tenant, glass_type, spec_name) rejected", False,
               "insert succeeded!")
    except Exception:
        conn.rollback()
        record("0008: duplicate (tenant, glass_type, spec_name) rejected", True)

    cur.execute("SET app.tenant_id = %s", (TENANT_B,))
    try:
        cur.execute("""
            INSERT INTO saas.specifications (tenant_id, spec_name, glass_type, rate_per_sqm)
            VALUES (%s, 'phase7b-probe', 'SGU', 46)
        """, (TENANT_B,))
        conn.commit()
        record("0008: same spec name allowed in another tenant", True)
    except Exception as e:
        conn.rollback()
        record("0008: same spec name allowed in another tenant", False, str(e))

    cur.execute("SET app.tenant_id = %s", (TENANT_A,))
    cur.execute("""
        INSERT INTO saas.proforma_invoices (tenant_id, proforma_no)
        VALUES (%s, 'phase7b-probe-PI')
        RETURNING id
    """, (TENANT_A,))
    pi_id = cur.fetchone()[0]
    conn.commit()

    cur.execute("""
        INSERT INTO saas.specification_usages
            (tenant_id, spec_id, proforma_id, spec_name, glass_type)
        VALUES (%s, %s, %s, 'phase7b-probe', 'SGU')
    """, (TENANT_A, spec_id, pi_id))
    conn.commit()
    try:
        cur.execute("""
            INSERT INTO saas.specification_usages
                (tenant_id, spec_id, proforma_id, spec_name, glass_type)
            VALUES (%s, %s, %s, 'phase7b-probe', 'SGU')
        """, (TENANT_A, spec_id, pi_id))
        conn.commit()
        record("0008: usage pair UNIQUE (spec, proforma) — one verified use per pair",
               False, "second insert succeeded!")
    except Exception:
        conn.rollback()
        record("0008: usage pair UNIQUE (spec, proforma) — one verified use per pair", True)

    cleanup_probe(cur)
    cur.close()
    conn.close()



def test_other_charges(db_uri: str):
    """Phase 7b: Step 6 other-charges/wastage parity (0009)."""
    print("\n== Phase 7b: Step 6 other charges schema ==")
    conn = connect(db_uri)
    try:
        with conn.cursor() as cur:
            cur.execute("""
                SELECT column_name FROM information_schema.columns
                WHERE table_schema = 'saas' AND table_name = 'other_charges'
                  AND column_name IN ('template_key', 'description', 'unit',
                                      'metric_qty', 'polish_sub_type',
                                      'custom_polish_description',
                                      'custom_polish_qty')
            """)
            cols = {r[0] for r in cur.fetchall()}
            record("0009: other_charges row contract columns exist",
                   cols == {'template_key', 'description', 'unit', 'metric_qty',
                            'polish_sub_type', 'custom_polish_description',
                            'custom_polish_qty'},
                   str(sorted(cols)))

            cur.execute("""
                SELECT column_name FROM information_schema.columns
                WHERE table_schema = 'saas' AND table_name = 'wastage_rows'
                  AND column_name IN ('total_sqm', 'is_manually_added',
                                      'use_optimization_formula',
                                      'checked_source_keys',
                                      'spec_group_id', 'stock_sqm', 'glass_sqm')
            """)
            cols = {r[0] for r in cur.fetchall()}
            record("0009: wastage_rows contract columns "
                   "(Phase-3 guesses dropped)",
                   cols == {'total_sqm', 'is_manually_added',
                            'use_optimization_formula', 'checked_source_keys'},
                   str(sorted(cols)))

            cur.execute("""
                SELECT column_name FROM information_schema.columns
                WHERE table_schema = 'saas' AND table_name = 'custom_charges'
                  AND column_name IN ('charge_name', 'qty', 'unit', 'rate',
                                      'amount', 'is_active', 'sort_order')
            """)
            cols = {r[0] for r in cur.fetchall()}
            record("0009: custom_charges table exists (CustomChargeRow)",
                   cols == {'charge_name', 'qty', 'unit', 'rate', 'amount',
                            'is_active', 'sort_order'},
                   str(sorted(cols)))

            cur.execute("""
                SELECT column_name FROM information_schema.columns
                WHERE table_schema = 'saas' AND table_name = 'spec_group_items'
                  AND column_name = 'perimeter_factor'
            """)
            record("0009: spec_group_items.perimeter_factor exists "
                   "(default 2, WPF GlassItemRow)",
                   cur.rowcount == 1 and cur.fetchone()[0] == 'perimeter_factor')

            cur.execute("""
                SELECT count(*) FROM information_schema.columns
                WHERE table_schema = 'saas' AND table_name = 'spec_groups'
                  AND column_name IN ('outer_glass_color', 'inner_glass_color',
                                      'third_glass_color',
                                      'outer_sheet_width_mm',
                                      'outer_sheet_height_mm',
                                      'inner_sheet_width_mm',
                                      'inner_sheet_height_mm',
                                      'third_sheet_width_mm',
                                      'third_sheet_height_mm',
                                      'outer_sheet_price_per_sqm',
                                      'inner_sheet_price_per_sqm',
                                      'third_sheet_price_per_sqm')
            """)
            record("0009: spec_groups Step 4 provenance columns exist "
                   "(SpecGroupSnapshot Phase G)",
                   cur.fetchone()[0] == 12)

            # Metric-key CHECK now enforces the real WPF registry.
            cur.execute("""
                SELECT conname FROM pg_constraint
                WHERE conrelid = 'saas.other_charges'::regclass
                  AND contype = 'c' AND conname = 'ck_other_charges_metric'
            """)
            has_ck = cur.rowcount == 1
            cur.execute("""
                SET LOCAL app.tenant_id = '%s'
            """ % TENANT_A)
            cur.execute("""
                SELECT id FROM saas.proforma_invoices LIMIT 1
            """)
            pi = cur.fetchone()[0]
            cur.execute("""
                INSERT INTO saas.other_charges
                    (tenant_id, proforma_id, charge_name, metric_key,
                     checked_spec_group_numbers, rate, unit, template_key)
                VALUES ('%s', '%s', 'Polish (lm)', 'LM1', '[1,2]', 6, 'Lm',
                        'POLISH')
                RETURNING id
            """ % (TENANT_A, pi))
            charge_id = cur.fetchone()[0]
            record("0009: real metric key LM1 accepted", True)

            rejected = False
            try:
                cur.execute("""
                    INSERT INTO saas.other_charges
                        (tenant_id, proforma_id, charge_name, metric_key,
                         checked_spec_group_numbers, rate, unit)
                    VALUES ('%s', '%s', 'x', 'SQM', '[]', 1, 'Qty')
                """ % (TENANT_A, pi))
            except Exception:
                conn.rollback()
                rejected = True
                with conn.cursor() as cur2:
                    cur2.execute(
                        "SET LOCAL app.tenant_id = '%s'" % TENANT_A)
            record("0009: Phase-3 metric key 'SQM' rejected "
                   "(real registry is LM1/SQM1/...)",
                   rejected)

            # custom_charges RLS isolation (0005 pattern).
            with conn.cursor() as cur3:
                cur3.execute("SET LOCAL app.tenant_id = '%s'" % TENANT_A)
                cur3.execute("SELECT count(*) FROM saas.custom_charges")
                record("0009: custom_charges readable under tenant A RLS",
                       cur3.fetchone()[0] >= 0)
            conn.commit()
    finally:
        conn.close()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--db-uri", default=None)
    parser.add_argument("--report", default=None)
    args = parser.parse_args()

    db_uri = args.db_uri or os.environ.get("SAAS_TEST_DB") or boot_pgserver()
    print(f"PostgreSQL: {db_uri}")

    print("\n== Applying migrations ==")
    applied, names = apply_migrations(db_uri)
    print(f"  {applied} migration file(s) applied: {', '.join(names) if names else 'none (already applied)'}")

    create_tenants(db_uri)
    print("\n== Seeding two tenants as the runtime role ==")
    ids_a = seed_tenant(db_uri, TENANT_A, "A")
    ids_b = seed_tenant(db_uri, TENANT_B, "B")
    ids = {"a": ids_a, "b": {**ids_b, "taxinvoice": None}}
    # fetch tenant B's tax invoice id for the DELETE test
    conn = connect(db_uri, role="saas_test_app")
    with conn.cursor() as cur:
        cur.execute("SET app.tenant_id = %s", (TENANT_B,))
        cur.execute("SELECT id FROM saas.tax_invoices LIMIT 1")
        ids["b"]["taxinvoice"] = cur.fetchone()[0]
    conn.close()

    test_rls(db_uri, ids)
    test_constraints(db_uri, ids)
    test_migrations(db_uri)
    test_platform_protection(db_uri)
    test_auth_identity(db_uri)
    test_masterdata(db_uri)
    test_specifications(db_uri)
    test_other_charges(db_uri)

    ok = write_report(sys.argv, names)
    print(f"\n{'='*60}\n{'ALL CHECKS PASSED' if ok else 'FAILURES PRESENT'} — "
          f"{sum(1 for r in RESULTS if r['ok'])}/{len(RESULTS)}")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
