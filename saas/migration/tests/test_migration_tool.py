"""End-to-end migration tool test: the WPF-shaped fixture → live
PostgreSQL. Run from saas/migration:

    ../../.venv/bin/python tests/test_migration_tool.py

Boots a disposable pgserver, applies the SaaS migrations, creates a
tenant, runs the importer in dry-run then apply, and asserts the loaded
rows, findings and the validate report.
"""

from __future__ import annotations

import importlib.util
import json
import sqlite3
import sys
import uuid
from decimal import Decimal
from pathlib import Path

HERE = Path(__file__).resolve().parent
MIGRATION_DIR = HERE.parent
SAAS_ROOT = MIGRATION_DIR.parent

sys.path.insert(0, str(MIGRATION_DIR))

from proglass_migrate.cli import main as cli_main          # noqa: E402
from build_fixture import build                            # noqa: E402


def load_harness():
    spec = importlib.util.spec_from_file_location(
        "harness", SAAS_ROOT / "database" / "verify" / "apply_and_test.py")
    harness = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(harness)
    return harness


def run_cli(*argv: str) -> int:
    code = cli_main([*argv])
    assert code == 0, f"CLI exited {code}: {argv}"
    return code


def main() -> None:
    import shutil
    # A disposable instance per test run (pgserver reuses /tmp/pgdata —
    # earlier iterations' tenants would pollute unscoped counts).
    shutil.rmtree("/tmp/pgdata", ignore_errors=True)
    harness = load_harness()
    db_uri = harness.boot_pgserver()
    harness.apply_migrations(db_uri, verbose=False)

    import psycopg2
    conn = psycopg2.connect(db_uri)
    conn.autocommit = True
    tenant_id = uuid.uuid4()
    slug = f"mig-{tenant_id.hex[:10]}"
    with conn.cursor() as cur:
        cur.execute(
            "INSERT INTO saas.tenants (id, name, slug) VALUES (%s,%s,%s)",
            (tenant_id, "Migrated Glass Co", slug))

    fixture = build(HERE / "fixture-data-root")
    report_dir = HERE / "out"

    # ── dry-run first: nothing persists ─────────────────────────────
    run_cli("import", "--source", fixture, "--db-uri", db_uri,
            "--tenant", slug, "--dry-run",
            "--report-dir", str(report_dir / "dry"))
    with conn.cursor() as cur:
        cur.execute("SELECT count(*) FROM saas.users"
                    " WHERE tenant_id = %s", (tenant_id,))
        assert cur.fetchone()[0] == 0, "dry-run must not persist rows"

    # ── apply ────────────────────────────────────────────────────────
    run_cli("import", "--source", fixture, "--db-uri", db_uri,
            "--tenant", slug, "--apply",
            "--report-dir", str(report_dir / "apply"))
    report = json.loads(
        (report_dir / "apply" / "migration_report.json").read_text())
    by_module = {m["name"]: m for m in report["modules"]}

    # users: 2 of 3 (bad role 'SpaceMarine' skipped)
    assert by_module["users"]["loaded"] == 2, by_module["users"]
    with conn.cursor() as cur:
        cur.execute(
            "SELECT username, role, must_reset_password, email FROM"
            " saas.users ORDER BY username")
        users = cur.fetchall()
    assert [u[0] for u in users] == ["cutter1", "jubers"]
    assert all(u[2] for u in users), "passwords must force a reset"
    assert users[1][3] == "jubers@apex.ae"

    # masterdata: 1+1+2+1
    assert by_module["masterdata"]["loaded"] == 5, by_module["masterdata"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT p.identifier_name, c.customer_name IS NOT NULL
            FROM saas.projects p
            LEFT JOIN saas.clients c ON c.id = p.customer_id
            ORDER BY p.identifier_name""")
        projects = cur.fetchall()
    assert projects == [("Marina Tower Glazing", True),
                        ("Orphan Project", False)], projects

    # sheetstore: 2 sheets + 5 ledger rows + 2 of 3 items (Film skipped)
    assert by_module["sheetstore"]["loaded"] == 9, by_module["sheetstore"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT g.category, g.thickness_mm, g.stock, g.used,
                   (SELECT t.balance_after FROM saas.stock_transactions t
                    WHERE t.glass_sheet_id = g.id
                    ORDER BY t.created_at DESC LIMIT 1)
            FROM saas.glass_sheets g ORDER BY g.thickness_mm""")
        sheets = cur.fetchall()
    # Sheet 1: history replays to 15 = stored 20-5 ✓.
    assert sheets[0] == ("Float", 6, 20, 5, Decimal(15)), sheets
    # Sheet 2: history replays to 8 but stored is 18 — stored imports,
    # ledger tail reflects the history (the mismatch finding).
    assert sheets[1] == ("Float", 8, 20, 2, Decimal(8)), sheets
    with conn.cursor() as cur:
        cur.execute(
            "SELECT category, unit, stock FROM saas.inventory_items"
            " ORDER BY name")
        items = cur.fetchall()
    assert [i[0] for i in items] == ["desiccant", "spacer"], items
    assert items[1][1] == "LinearMetre" and items[1][2] == Decimal("45.55")

    # tax invoices: 2 of 3 (unknown status skipped), totals verbatim.
    assert by_module["taxinvoices"]["loaded"] == 2, by_module["taxinvoices"]
    with conn.cursor() as cur:
        cur.execute(
            "SELECT invoice_no, vat_amount, grand_total, balance_amount"
            " FROM saas.tax_invoices ORDER BY invoice_no")
        invoices = cur.fetchall()
    assert invoices[0] == ("INV-2026-001", Decimal(500), Decimal(10500),
                           Decimal(10500))
    assert invoices[1] == ("INV-2026-002", Decimal(44), Decimal(944),
                           Decimal(944))     # mismatch PRESERVED + reported

    # credit notes: both loaded; the second has a null invoice link.
    assert by_module["creditnotes"]["loaded"] == 2
    with conn.cursor() as cur:
        cur.execute(
            "SELECT credit_note_no, tax_invoice_id IS NOT NULL"
            " FROM saas.credit_notes ORDER BY credit_note_no")
        notes = cur.fetchall()
    assert notes == [("CN-2026-001", True), ("CN-2026-002", False)], notes

    # statements: 1 header + ledger lines (invoice+payment per line).
    with conn.cursor() as cur:
        cur.execute("""
            SELECT line_type, debit, credit, running_balance
            FROM saas.statement_lines ORDER BY line_no""")
        lines = cur.fetchall()
    assert lines == [
        ("Invoice", Decimal(10500), Decimal(0), Decimal(10500)),
        ("Payment", Decimal(0), Decimal(10500), Decimal(0)),
        ("Invoice", Decimal(944), Decimal(0), Decimal(944)),
    ], lines

    # activity log: all 4 rows, after_json shapes.
    with conn.cursor() as cur:
        cur.execute(
            "SELECT count(*), count(after_json), count(user_id)"
            " FROM saas.audit_logs")
        counts = cur.fetchone()
    # 4 rows; 3 with payloads (2 free-text → after_json, 1 JSON changes);
    # 3 with known users (the 4th is user 'nobody').
    assert counts == (4, 3, 3), counts

    # findings: the fixture's deliberate problems are all reported.
    all_findings = [f for m in report["modules"]
                    for f in m["findings"]]
    texts = " | ".join(f["message"] for f in all_findings)
    assert "SpaceMarine" in texts                      # bad role
    assert "balance mismatch" in texts                 # sheet 2
    assert "PVB interlayer clear" in texts             # Film category
    assert "SomewhereElse" in texts                    # unknown status
    assert "differs from recomputed 45.00" in texts    # VAT mismatch
    assert "INV-2026-003" in texts                     # orphan credit note
    errors = [f for f in all_findings if f["severity"] == "error"]
    assert len(errors) == 3, errors   # bad role, Film category, bad status

    # ── validate ─────────────────────────────────────────────────────
    # The deliberate sheet-2 ledger/stored drift is reported as FAIL
    # (the operator sees it), everything else passes.
    code = cli_main(["validate", "--db-uri", db_uri, "--tenant", slug,
                     "--report-dir", str(report_dir / "validate")])
    assert code == 1, "validate must flag the known ledger drift"
    vreport = json.loads(
        (report_dir / "validate" / "migration_report.json").read_text())
    findings = vreport["modules"][0]["findings"]
    failed = [f for f in findings if f["row"] == "FAIL"]
    assert len(failed) == 1 and "ledger tail" in failed[0]["message"], failed

    # ── the source is never written ─────────────────────────────────
    with sqlite3.connect(f"file:{fixture}/audit.db?mode=ro", uri=True) as src:
        rows = src.execute("SELECT count(*) FROM Users").fetchone()[0]
    assert rows == 3

    # ── dry-run == apply (extracted/loaded identical) ───────────────
    dry = json.loads(
        (report_dir / "dry" / "migration_report.json").read_text())
    for apply_m, dry_m in zip(report["modules"], dry["modules"],
                              strict=True):
        assert apply_m["extracted"] == dry_m["extracted"]
        assert apply_m["loaded"] == dry_m["loaded"]

    print("ALL MIGRATION TOOL TESTS PASSED")


if __name__ == "__main__":
    main()
