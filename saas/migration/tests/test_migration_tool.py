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
import shutil
import sqlite3
import tempfile
from pathlib import Path
import sys
import uuid
from decimal import Decimal

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

    # ── dry-run first: nothing persists (files included) ────────────
    dry_attachments = Path(tempfile.mkdtemp(prefix="pg-att-dry-"))
    run_cli("import", "--source", fixture, "--db-uri", db_uri,
            "--tenant", slug, "--dry-run",
            "--attachments-dir", str(dry_attachments),
            "--report-dir", str(report_dir / "dry"))
    assert not any(dry_attachments.rglob("*")), \
        "dry-run must not write attachment bytes"
    with conn.cursor() as cur:
        cur.execute("SELECT count(*) FROM saas.users"
                    " WHERE tenant_id = %s", (tenant_id,))
        assert cur.fetchone()[0] == 0, "dry-run must not persist rows"

    # ── apply ────────────────────────────────────────────────────────
    att_root = Path(tempfile.mkdtemp(prefix="pg-att-apply-"))
    run_cli("import", "--source", fixture, "--db-uri", db_uri,
            "--tenant", slug, "--apply",
            "--attachments-dir", str(att_root),
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

    # Phase 17: the print/bank/TRN columns round-trip from the WPF entity
    # (0019) — populated for INV-2026-001, empty defaults for INV-2026-002.
    with conn.cursor() as cur:
        cur.execute(
            "SELECT company_name, company_trn, purchase_order_number,"
            " bank_name, bank_account_number, iban, swift_code"
            " FROM saas.tax_invoices ORDER BY invoice_no")
        print_fields = cur.fetchall()
    assert print_fields[0] == (
        "ProGlass Automation LLC", "1001234567893", "PO-EM-4471",
        "Emirates NBD", "AE-001-2345678",
        "AE070331234567890123456", "EBILAEAD"), print_fields[0]
    assert print_fields[1] == ("ProGlass Automation LLC", "1001234567893",
                               "", "", "", "", ""), print_fields[1]

    # Phase 19: the WPF TaxInvoiceItems round-trip (group/serial identity,
    # spec-group-item link resolved, qty/sqm/rate/value verbatim).
    with conn.cursor() as cur:
        cur.execute(
            "SELECT i.spec_group_number, i.serial_number, i.qty,"
            " i.w1_mm, i.h1_mm, i.rate, i.value,"
            " (i.spec_group_item_id IS NOT NULL) AS linked"
            " FROM saas.tax_invoice_items i"
            " JOIN saas.tax_invoices t ON t.id = i.tax_invoice_id"
            " WHERE t.invoice_no = 'INV-2026-001'"
            " ORDER BY i.spec_group_number, i.serial_number")
        ti_items = cur.fetchall()
    assert len(ti_items) == 2, ti_items
    assert ti_items[0][0:7] == (1, 1, 2, 1000, 500,
                                Decimal(200), Decimal(400)), ti_items[0]
    assert ti_items[1][0:7] == (2, 1, 1, 800, 600,
                                Decimal(180), Decimal("86.40")), ti_items[1]
    # group 1 serial 1 exists on the imported PI → linked; group 2 does
    # not (the fixture PI has only group 1) → unlinked.
    assert ti_items[0][7] is True, "group 1/1 should link to the PI item"
    assert ti_items[1][7] is False, "group 2/1 has no PI counterpart"

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

    # ── Phase 16: specifications ────────────────────────────────────
    assert by_module["specifications"]["loaded"] == 2, \
        by_module["specifications"]        # TRIPLE + duplicate rejected
    with conn.cursor() as cur:
        cur.execute("""
            SELECT s.glass_type, s.spec_name, s.rate_per_sqm, s.category,
                   s.color, s.thickness_mm, s.sheet_id,
                   g.category IS NOT NULL, s.panes_json IS NOT NULL,
                   s.last_proforma_no
            FROM saas.specifications s
            LEFT JOIN saas.glass_sheets g ON g.id = s.sheet_id
            ORDER BY s.spec_name""")
        specs = cur.fetchall()
    assert specs[0] == ("DGU", "Clear 6+12+6", Decimal("125.50"),
                        "Float", "Clear", 6, specs[0][6], True, True,
                        "PI-2026-001"), specs
    assert specs[0][6] is not None, "sheet link must resolve"
    # The SGU spec: unknown sheet → null, bad PanesJson → null.
    assert specs[1][6] is None and specs[1][8] is False, specs

    # ── Phase 16: inquiries ─────────────────────────────────────────
    assert by_module["inquiries"]["loaded"] == 4, by_module["inquiries"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT i.inquiry_no, i.status, i.status_text, i.priority,
                   i.customer_id IS NOT NULL, i.converted_proforma_id,
                   jsonb_array_length(i.boq_json),
                   i.boq_json -> 0 ->> 'specName',
                   i.boq_json -> 1 ->> 'qty'
            FROM saas.inquiries i ORDER BY i.inquiry_no""")
        inq = cur.fetchall()
    assert inq[0][:6] == ("INQ-2026-001", "Received", "Received", "high",
                          True, inq[0][5]), inq
    assert inq[0][5] is not None, "converted_proforma_id backlink"
    assert inq[0][6:] == (2, "Clear 6+12+6", "4"), inq  # BOQ camelCase
    # 'On Hold' → Hold (the WPF's own Normalize), original in status_text.
    assert inq[1][:4] == ("INQ-2026-002", "Hold", "On Hold", "normal"), inq
    assert inq[1][4] is False, "unresolved client → null"

    # ── Phase 16: proformas + the Step 5/6/9 projection ─────────────
    # 3 PIs + 1 group + 2 items (step 5) + 2 charges + 1 wastage
    # + 1 custom (step 6).
    assert by_module["proformas"]["loaded"] == 10, by_module["proformas"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT p.proforma_no, p.status, p.lifecycle_state,
                   p.glass_materials_subtotal, p.other_charges_subtotal,
                   p.vat_amount, p.grand_total_vat_inclusive,
                   p.discount_type, p.vat_percent, p.furthest_step,
                   p.inquiry_id IS NOT NULL, p.snapshot_json IS NOT NULL,
                   p.pnl_profit, p.pnl_wastage_percent,
                   p.pnl_calculated_at IS NOT NULL
            FROM saas.proforma_invoices p ORDER BY p.proforma_no""")
        pis = cur.fetchall()
        cur.execute("""
            SELECT r.proforma_no, root.proforma_no, r.revision_number
            FROM saas.proforma_invoices r
            JOIN saas.proforma_invoices root ON root.id = r.root_proforma_id
            ORDER BY r.proforma_no""")
        chains = cur.fetchall()
    assert pis[0] == ("PI-2026-001", "Sent", "Sent", Decimal("1399.20"),
                      Decimal("842.80"), Decimal("113.00"),
                      Decimal("2355.00"), "Percentage", Decimal("5.00"),
                      9, True, True, Decimal("942.00"), Decimal("15.00"),
                      True), pis[0]
    assert pis[1][0] == "PI-2026-001-R1" \
        and pis[1][10:] == (False, True, Decimal("0.00"),
                            Decimal("15.00"), False), pis[1]
    assert pis[2][0] == "PI-2026-003"   # header loads, group rejected
    assert chains == [("PI-2026-001-R1", "PI-2026-001", 1)], chains

    with conn.cursor() as cur:
        cur.execute("""
            SELECT g.group_number, g.glass_type, g.spec_name, g.spec_id,
                   g.rate_per_sqm, g.price_source, g.outer_sheet_width_mm,
                   (SELECT count(*) FROM saas.spec_group_items i
                    WHERE i.spec_group_id = g.id)
            FROM saas.spec_groups g""")
        groups = cur.fetchall()
    assert len(groups) == 1, groups
    assert groups[0][:3] == (1, "DGU", "Clear 6+12+6"), groups
    assert groups[0][3] is not None, "spec_id link to the library"
    assert groups[0][4:] == (Decimal("110.00"), "formula", 3210, 2), groups
    with conn.cursor() as cur:
        cur.execute("""
            SELECT row_serial, sqm, total_sqm, unit_price, total, qty,
                   perimeter_factor
            FROM saas.spec_group_items i
            JOIN saas.spec_groups g ON g.id = i.spec_group_id
            ORDER BY row_serial""")
        items = cur.fetchall()
    # WPF row math: 1500×2400 → 3.600 m² ×3 → 10.800; 800×1200 → 0.960
    # (above the 0.5 floor) ×2 → 1.920. Values at MoneyRounding 2 dp.
    assert items == [(1, Decimal("3.600"), Decimal("10.800"),
                      Decimal("110.00"), Decimal("1188.00"), 3, 2),
                     (2, Decimal("0.960"), Decimal("1.920"),
                      Decimal("110.00"), Decimal("211.20"), 2, 2)], items
    with conn.cursor() as cur:
        cur.execute("""
            SELECT charge_name, template_key, metric_key, unit,
                   metric_qty, amount, sort_order, polish_sub_type
            FROM saas.other_charges ORDER BY sort_order""")
        charges = cur.fetchall()
    assert charges == [
        ("Argon Gas (Sqm)", "ARGON_GAS", "SQM1", "Sqm", Decimal("12.720"),
         Decimal("572.40"), 0, ""),
        ("Polish (lm)", "POLISH", "LM1", "Lm", Decimal("8.400"),
         Decimal("50.40"), 1, "AllSidePolish")], charges
    with conn.cursor() as cur:
        cur.execute("""
            SELECT color_name, rate, total_sqm, result_amount,
                   is_manually_added, use_optimization_formula,
                   checked_source_keys
            FROM saas.wastage_rows""")
        wastage = cur.fetchall()
    assert wastage == [("Clear", Decimal("20"), Decimal("3.5"),
                        Decimal("70.00"), True, False,
                        ["1-Outer"])], wastage
    with conn.cursor() as cur:
        cur.execute("""
            SELECT charge_name, qty, unit, rate, amount, is_active
            FROM saas.custom_charges""")
        customs = cur.fetchall()
    assert customs == [("Crane hire", Decimal("1"), "Qty", Decimal("150"),
                        Decimal("150.00"), True)], customs

    # ── Phase 16: specification usages (after proformas) ────────────
    assert by_module["specusages"]["loaded"] == 1, by_module["specusages"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT u.spec_name, s.spec_name, p.proforma_no,
                   u.price_per_sqm
            FROM saas.specification_usages u
            JOIN saas.specifications s ON s.id = u.spec_id
            JOIN saas.proforma_invoices p ON p.id = u.proforma_id""")
        usages = cur.fetchall()
    assert usages == [("Clear 6+12+6", "Clear 6+12+6", "PI-2026-001",
                       Decimal("125.50"))], usages

    # ── Phase 16: optimization (historical runs, oracles documented) ─
    assert by_module["optimization"]["loaded"] == 2, by_module["optimization"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT status, strategy, sheets_used, unplaced_count,
                   utilization_pct, conservation_verified,
                   geometry_verified, params_json ->> 'wpfSessionId',
                   validation_problems -> 0
            FROM saas.optimization_sessions ORDER BY created_at""")
        sessions = cur.fetchall()
    for row in sessions:
        assert row[0] == "completed" and row[5] is False and row[6] is False
        assert "cannot re-run" in row[8], row[8]
    assert sessions[0][7] == "opt-session-1", sessions

    # ── Phase 16: job orders ────────────────────────────────────────
    assert by_module["joborders"]["loaded"] == 1, by_module["joborders"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT j.job_no, j.status, j.priority,
                   j.grand_total_vat_inclusive, j.site_delivery_contact,
                   j.assigned_to, j.cutting_instructions,
                   p.proforma_no, p.job_order_id IS NOT NULL
            FROM saas.job_orders j
            JOIN saas.proforma_invoices p ON p.id = j.proforma_id""")
        jobs = cur.fetchall()
    assert jobs == [("JOB-2026-001", "InProduction", "High",
                     Decimal("2355.00"), "Site Engineer Ali", "Crew A",
                     "Cut all outer panes first", "PI-2026-001", True)], jobs

    # ── Phase 16: production (pieces / events / reservations) ───────
    # 4 base rows + 14 ported 26a/26b rows (execution, pairing,
    # assembly, workstation, hold, assignment, override, 2 allocations,
    # cost, replacement, cutting session/sheet/placed piece). The
    # bad-enum execution and the ghost-work-item hold are skipped.
    assert by_module["production"]["loaded"] == 18, by_module["production"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT pc.unique_key, pc.stage, pc.status, pc.qty, pc.qty_done,
                   pc.unit_sqm, pc.spec_group_item_id IS NOT NULL,
                   pc.is_cancelled, j.job_no
            FROM saas.production_pieces pc
            JOIN saas.job_orders j ON j.id = pc.job_order_id
            ORDER BY pc.unique_key""")
        pieces = cur.fetchall()
    assert pieces[0][:9] == (
        "JOB-2026-001|Clear 6+12+6|1||Cutting", "Cutting", "Completed",
        3, 3, Decimal("3.600"), True, False, "JOB-2026-001"), pieces
    assert pieces[1][4] == 1 and pieces[1][2] == "InProgress", pieces
    with conn.cursor() as cur:
        cur.execute("""
            SELECT e.event_type, e.stage, e.qty, e.done_by, u.username
            FROM saas.production_events e
            LEFT JOIN saas.users u ON u.id = e.done_by""")
        events = cur.fetchall()
    assert events == [("Stage", "Cutting", 3, events[0][3],
                       "cutter1")], events
    assert events[0][3] is not None, "done_by resolved to a user"
    with conn.cursor() as cur:
        cur.execute("""
            SELECT r.reservation_key, r.status, r.reserved_qty,
                   g.category, g.thickness_mm, o.params_json ->> 'wpfSessionId',
                   u.username
            FROM saas.material_reservations r
            JOIN saas.glass_sheets g ON g.id = r.glass_sheet_id
            JOIN saas.optimization_sessions o
                 ON o.id = r.optimization_session_id
            LEFT JOIN saas.users u ON u.id = r.reserved_by""")
        reservations = cur.fetchall()
    assert reservations == [("JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1",
                             "Reserved", 2, "Float", 6, "opt-session-1",
                             "jubers")], reservations

    # ── 26a port: process control rows ──────────────────────────────
    with conn.cursor() as cur:
        cur.execute("""
            SELECT w.code, w.name, w.process_type, w.process_name,
                   w.is_active
            FROM saas.workstations w""")
        stations = cur.fetchall()
    assert stations == [("TMP-01", "Tempering Furnace 1", 6, "Tempering",
                         True)], stations
    with conn.cursor() as cur:
        cur.execute("""
            SELECT e.execution_id, e.status, e.entry_type, e.qc_status,
                   e.process_type, pc.unique_key IS NOT NULL
            FROM saas.process_executions e
            LEFT JOIN saas.production_pieces pc
                 ON pc.id = e.production_piece_id""")
        execs = cur.fetchall()
    assert execs == [("EXEC-2026-0001", "Completed", "Normal", "Passed",
                      1, True)], execs
    with conn.cursor() as cur:
        cur.execute("""
            SELECT p.pairing_id, p.status, po.unique_key, pi.unique_key
            FROM saas.process_pairings p
            LEFT JOIN saas.production_pieces po
                 ON po.id = p.outer_production_piece_id
            LEFT JOIN saas.production_pieces pi
                 ON pi.id = p.inner_production_piece_id""")
        pairs = cur.fetchall()
    assert pairs == [("PAIR-2026-0001", "Completed",
                      "JOB-2026-001|Clear 6+12+6|1||Cutting",
                      "JOB-2026-001|Clear 6+12+6|2||Cutting")], pairs
    with conn.cursor() as cur:
        cur.execute("""
            SELECT a.assembly_id, a.status, a.pairing_id,
                   a.completed_at IS NOT NULL
            FROM saas.process_assemblies a""")
        asms = cur.fetchall()
    assert asms == [("ASM-2026-0001", "Completed", "PAIR-2026-0001",
                     True)], asms
    with conn.cursor() as cur:
        cur.execute("""
            SELECT h.hold_id, h.stage, h.is_released, pc.unique_key
            FROM saas.production_control_holds h
            JOIN saas.production_pieces pc
                 ON pc.id = h.production_piece_id""")
        holds = cur.fetchall()
    assert holds == [("HOLD-2026-0001", "Tempering", True,
                      "JOB-2026-001|Clear 6+12+6|2||Cutting")], holds
    with conn.cursor() as cur:
        cur.execute("""
            SELECT a.assignment_id, a.stage, a.workstation_id,
                   a.operator_name, a.is_active
            FROM saas.production_control_assignments a""")
        assigns = cur.fetchall()
    assert assigns == [("ASSIGN-2026-0001", "Tempering", "WS-TEMP-01",
                        "cutter1", True)], assigns
    with conn.cursor() as cur:
        cur.execute("""
            SELECT o.control_id, o.stage, o.priority
            FROM saas.production_control_overrides o""")
        overrides = cur.fetchall()
    assert overrides == [("CTRL-2026-0001", "Tempering", 2)], overrides

    # ── 26b port: cutting + cost rows ───────────────────────────────
    with conn.cursor() as cur:
        cur.execute("""
            SELECT m.allocation_id, m.status, m.reservation_key,
                   m.reservation_id IS NOT NULL, r.reservation_key
            FROM saas.material_allocations m
            LEFT JOIN saas.material_reservations r
                 ON r.id = m.reservation_id
            ORDER BY m.allocation_id""")
        allocs = cur.fetchall()
    assert allocs == [
        ("JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1|S1|A-01",
         "Consumed", "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1",
         True, "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1"),
        ("JOB-2026-001|Clear 6+12+6|1|1|opt-session-9|9|S1|X-99",
         "Allocated", "JOB-2026-001|Clear 6+12+6|1|1|opt-session-9|9",
         False, None)], allocs
    with conn.cursor() as cur:
        cur.execute("""
            SELECT c.cost_id, c.cost_type, c.quantity, c.unit_cost,
                   c.total_cost, c.selling_value, g.category
            FROM saas.production_actual_costs c
            JOIN saas.glass_sheets g ON g.id = c.stock_sheet_id""")
        costs = cur.fetchall()
    assert costs == [("JOB-2026-001|Clear 6+12+6|1|ActualMaterial",
                      "ActualMaterial", Decimal("3.600"),
                      Decimal("95.50"), Decimal("343.80"),
                      Decimal("504.00"), "Float")], costs
    with conn.cursor() as cur:
        cur.execute("""
            SELECT r.replacement_id, r.entry_type, r.status, r.stage,
                   po.unique_key IS NOT NULL,
                   pr.unique_key IS NOT NULL
            FROM saas.production_replacements r
            LEFT JOIN saas.production_pieces po
                 ON po.id = r.original_piece_id
            LEFT JOIN saas.production_pieces pr
                 ON pr.id = r.replacement_piece_id""")
        repls = cur.fetchall()
    assert repls == [("JOB-2026-001|Clear 6+12+6|1|1|Cutting|RPL:1",
                      "Rework", "Completed", "Cutting", True,
                      True)], repls
    with conn.cursor() as cur:
        cur.execute("""
            SELECT s.cutting_session_id, s.status, s.rotation_policy,
                   s.split_mode, s.effort_level,
                   o.params_json ->> 'wpfSessionId'
            FROM saas.cutting_sessions s
            JOIN saas.optimization_sessions o
                 ON o.id = s.optimization_session_id""")
        sessions = cur.fetchall()
    assert sessions == [("CUT-JOB-2026-001-1-20260905090000",
                         "Completed", "Free90", "HorizontalFirst", 3,
                         "opt-session-1")], sessions
    with conn.cursor() as cur:
        cur.execute("""
            SELECT sh.cutting_sheet_id, sh.sheet_no, g.category,
                   g.thickness_mm, sh.utilization_pct
            FROM saas.cutting_sheets sh
            JOIN saas.glass_sheets g ON g.id = sh.stock_sheet_id""")
        sheets = cur.fetchall()
    assert sheets == [("CUT-JOB-2026-001-1-20260905090000|S1", 1,
                       "Float", 6, Decimal("64.80"))], sheets
    with conn.cursor() as cur:
        cur.execute("""
            SELECT pp.placed_piece_id, pp.status,
                   pc.unique_key IS NOT NULL, pp.reservation_key
            FROM saas.cutting_placed_pieces pp
            LEFT JOIN saas.production_pieces pc
                 ON pc.id = pp.source_piece_id""")
        placed = cur.fetchall()
    assert placed == [("CUT-JOB-2026-001-1-20260905090000|S1|PIECE-A-01",
                       "Completed", True,
                       "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1"
                       )], placed

    # ── Phase 16: delivery notes ────────────────────────────────────
    assert by_module["deliverynotes"]["loaded"] == 6, by_module["deliverynotes"]
    with conn.cursor() as cur:
        cur.execute("""
            SELECT d.delivery_no, d.status, d.delivery_type, d.is_master_dn,
                   d.driver, d.vehicle, d.total_ordered_qty,
                   d.total_delivered_qty, d.total_balance_qty,
                   j.job_no
            FROM saas.delivery_notes d
            JOIN saas.job_orders j ON j.id = d.job_order_id
            ORDER BY d.delivery_no""")
        notes = cur.fetchall()
    assert notes == [
        ("DN-2026-001-01", "Dispatching", "Original", False, "Rashid",
         "D-12345", 5, 3, 2, "JOB-2026-001"),
        ("MDN-2026-001", "Draft", "Dummy", True, "", "", 5, 0, 5,
         "JOB-2026-001")], notes
    with conn.cursor() as cur:
        cur.execute("""
            SELECT i.serial_number, i.ordered_qty, i.delivered_qty,
                   i.sqm, i.spec_group_item_id IS NOT NULL, j.job_no
            FROM saas.delivery_note_items i
            JOIN saas.delivery_notes d ON d.id = i.delivery_note_id
            JOIN saas.job_orders j ON j.id = i.job_order_id
            WHERE d.delivery_no = 'DN-2026-001-01' ORDER BY i.serial_number""")
        ditems = cur.fetchall()
    assert ditems == [(1, 3, 3, Decimal("10.800"), True, "JOB-2026-001"),
                      (2, 2, 0, Decimal("1.920"), True, "JOB-2026-001")], \
        ditems

    # ── tax invoices now LINK to the imported proforma ──────────────
    with conn.cursor() as cur:
        cur.execute("""
            SELECT t.invoice_no, p.proforma_no
            FROM saas.tax_invoices t
            LEFT JOIN saas.proforma_invoices p ON p.id = t.proforma_id
            ORDER BY t.invoice_no""")
        links = cur.fetchall()
    assert links == [("INV-2026-001", "PI-2026-001"),
                     ("INV-2026-002", "PI-2026-001")], links

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
    # Phase 16 finding paths
    assert "TRIPLE" in texts                           # spec glass type
    assert "not valid JSON" in texts                   # PI-2026-002
    assert "QUAD" in texts                             # spec group CHECK
    assert "phantom row dropped" in texts              # qty-0 grid row
    assert "differs from recomputed 112.10" in texts   # PI VAT mismatch
    assert "PI-2026-999" in texts                      # unimported root
    assert "In Production" in texts                    # legacy spelling
    assert "reserved latest-run" in texts              # optimization feed
    assert "derived 'LM1'" in texts                    # metric fallback
    assert "cannot re-run" in texts                    # oracle difference
    assert "ProductionReleases" in texts               # deferred counts
    assert "ProductionQcEntries" in texts              # absent deferred
    assert "EXEC-2026-0002" in texts                   # bad entry type
    assert "HOLD-2026-0002" in texts                   # ghost work item
    assert "reservation link null" in texts            # orphan allocation
    assert "not portable" in texts                     # replacement link
    assert "under skipped note DN-2026-009-01" in texts
    errors = [f for f in all_findings if f["severity"] == "error"]
    # 15 before Phase 24; the attachments module adds its two deliberate
    # error paths (missing file on disk + attachment on a skipped note);
    # the 26a/26b port adds three (bad-enum execution, hold on a work
    # item that was never imported, allocation whose reservation was
    # never imported — the row itself is kept with a null link).
    assert len(errors) == 20, [e["message"] for e in errors]

    # ── validate ─────────────────────────────────────────────────────
    # The deliberate drifts are reported as FAIL (the operator sees
    # them): the sheet-2 ledger, the PI-2026-001 VAT mismatch and the
    # revision whose root was never imported.
    code = cli_main(["validate", "--db-uri", db_uri, "--tenant", slug,
                     "--report-dir", str(report_dir / "validate")])
    assert code == 1, "validate must flag the known drifts"
    vreport = json.loads(
        (report_dir / "validate" / "migration_report.json").read_text())
    findings = vreport["modules"][0]["findings"]
    failed = [f for f in findings if f["row"] == "FAIL"]
    failed_labels = " | ".join(f["message"] for f in failed)
    assert len(failed) == 3, failed
    assert "ledger tail" in failed_labels
    assert "PI totals" in failed_labels
    assert "revisions without an imported root" in failed_labels

    # ── module 13: attachments (registry rows + disk bytes) ─────────
    att = by_module["attachments"]
    assert att["extracted"] == 5, att          # 4 DN rows + 1 inquiry row
    assert att["loaded"] == 3, att             # signed.pdf + site-photo.jpg
    att_errors = [f["message"] for f in att["findings"]
                  if f["severity"] == "error"]
    assert len(att_errors) == 2, att_errors    # missing file + skipped note
    assert any("was not found on disk" in m for m in att_errors)
    assert any("which was not imported" in m for m in att_errors)
    att_infos = [f["message"] for f in att["findings"]
                 if f["severity"] == "info"]
    assert any("102400" in m and "512" in m for m in att_infos), \
        "the registered-vs-disk size drift must be reported"
    assert any("ghost" in m for m in att_infos), \
        "an unimported uploader must be reported"
    with conn.cursor() as cur:
        cur.execute(
            "SELECT entity_type, original_name, mime_type, size_bytes,"
            " uploaded_by FROM saas.attachments WHERE tenant_id = %s"
            " ORDER BY original_name", (tenant_id,))
        att_rows = cur.fetchall()
    assert len(att_rows) == 3, att_rows
    by_name = {r[1]: r for r in att_rows}
    signed = by_name["signed.pdf"]
    assert signed[0] == "delivery_note"
    assert signed[2] == "application/pdf"
    assert signed[3] == 512, signed            # on-disk size, not registry
    assert signed[4] is not None               # jubers → imported user
    photo = by_name["site-photo.jpg"]
    assert photo[2] == "image/jpeg" and photo[4] is None, photo
    boq = by_name["boq.pdf"]
    assert boq[0] == "inquiry" and boq[3] == 20480 and \
        boq[4] is not None, boq
    # the bytes actually landed under the tenant-scoped storage key
    with conn.cursor() as cur:
        cur.execute(
            "SELECT storage_key FROM saas.attachments"
            " WHERE tenant_id = %s AND original_name = 'signed.pdf'",
            (tenant_id,))
        storage_key = cur.fetchone()[0]
    assert storage_key.startswith(f"{tenant_id.hex}/delivery_note/"), \
        storage_key
    stored = att_root / storage_key
    assert stored.is_file(), storage_key
    assert stored.read_bytes().startswith(b"%PDF-1.4")
    boq_path = list(att_root.rglob("boq.pdf"))
    assert len(boq_path) == 1 and boq_path[0].stat().st_size == 20480
    # the WPF source files are untouched (copies, never moves)
    assert (Path(fixture) / "DeliveryNotes" / "Attachments" / "PDFs" /
            "DN-2026-001-01" / "signed.pdf").stat().st_size == 512

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
