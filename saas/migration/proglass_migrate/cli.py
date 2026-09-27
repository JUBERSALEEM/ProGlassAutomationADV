"""proglass-migrate — the Phase 14 CLI.

Usage (saas/docs/SAAS_MIGRATION_PLAN.md §1 shape):
  python -m proglass_migrate import \
    --source "C:\\...\\ProGlassAutomation" \\      # the WPF data root
    --db-uri "<postgres>" \\                       # the SaaS database
    --tenant "company-slug" \\                     # slug or uuid
    [--dry-run | --apply] [--modules m1,m2] [--report-dir out]

  python -m proglass_migrate validate --db-uri ... --tenant ... \
    --report-dir out        # post-import validation report

  python -m proglass_migrate list-modules          # registry + deferred

The source root is opened READ-ONLY (mode=ro). Each module imports
inside one transaction; a failed module rolls back only itself.
"""

from __future__ import annotations

import argparse
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

import psycopg2

from .engine import MigrationRun, Module, write_report
import os
from pathlib import Path

from .modules import DEFERRED_MODULES, IMPLEMENTED_MODULES


def resolve_tenant(cursor, tenant: str) -> uuid.UUID:
    """--tenant accepts a slug or a UUID."""
    try:
        return uuid.UUID(tenant)
    except ValueError:
        pass
    cursor.execute(
        "SELECT id FROM saas.tenants WHERE slug = %s", (tenant.strip(),))
    row = cursor.fetchone()
    if row is None:
        raise SystemExit(
            f"error: no tenant with slug '{tenant}' — create the tenant "
            "(POST /api/v1/auth/register) or pass its UUID")
    return row[0]


def cmd_import(args: argparse.Namespace) -> int:
    source = Path(args.source).expanduser().resolve()
    if not source.is_dir():
        raise SystemExit(f"error: source root '{source}' is not a directory")
    if not args.dry_run and not args.apply:
        raise SystemExit("error: choose --dry-run or --apply")

    selected = {m.strip() for m in args.modules.split(",") if m.strip()} \
        if args.modules else None
    modules: list[Module] = []
    for cls in IMPLEMENTED_MODULES:
        if selected is not None and cls.name not in selected:
            continue
        modules.append(cls(source))
    if selected:
        known = {cls.name for cls in IMPLEMENTED_MODULES}
        unknown = selected - known
        if unknown:
            raise SystemExit(
                "error: unknown module(s): "
                + ", ".join(sorted(unknown))
                + " (see: proglass-migrate list-modules)")

    if getattr(args, "attachments_dir", None):
        os.environ["SAAS_ATTACHMENT_ROOT"] = str(
            Path(args.attachments_dir).resolve())
    connection = psycopg2.connect(args.db_uri)
    connection.autocommit = False
    try:
        with connection.cursor() as cursor:
            tenant_id = resolve_tenant(cursor, args.tenant)
        run = MigrationRun(connection, tenant_id, apply=args.apply)
        for module in modules:
            result = run.run_module(module)
            skipped = result.skipped or sum(
                1 for f in result.findings if f.severity == "error")
            print(f"[{result.status:>11}] {result.name:<14} "
                  f"extracted={result.extracted:<6} loaded={result.loaded:<6}"
                  f" skipped={skipped}"
                  + (f"  ({result.message})" if result.message else ""))
        run.finalize()   # dry-run: discard the shared transaction
        report = run.report()
        report["source_root"] = str(source)
        paths = write_report(report, Path(args.report_dir),
                             datetime.now(timezone.utc))
        for path in paths:
            print(f"report: {path}")
        failed = [r for r in run.results if r.status == "rolled_back"]
        print(f"{'APPLIED' if args.apply else 'DRY-RUN'} complete — "
              f"{len(failed)} module(s) rolled back")
        return 1 if failed else 0
    finally:
        connection.close()


def cmd_validate(args: argparse.Namespace) -> int:
    """Post-import validation: row counts + the ledger/balance spot
    checks (plan §4)."""
    connection = psycopg2.connect(args.db_uri)
    try:
        with connection.cursor() as cursor:
            tenant_id = resolve_tenant(cursor, args.tenant)
            cursor.execute(
                "SELECT set_config('app.tenant_id', %s, false)",
                (str(tenant_id),))
            checks: list[tuple[str, str, str]] = []
            for label, sql in (
                ("users", "SELECT count(*) FROM saas.users"),
                ("companies", "SELECT count(*) FROM saas.companies"),
                ("clients", "SELECT count(*) FROM saas.clients"),
                ("projects", "SELECT count(*) FROM saas.projects"),
                ("salesmen", "SELECT count(*) FROM saas.salesmen"),
                ("glass_sheets", "SELECT count(*) FROM saas.glass_sheets"),
                ("inventory_items",
                 "SELECT count(*) FROM saas.inventory_items"),
                ("stock_transactions",
                 "SELECT count(*) FROM saas.stock_transactions"),
                ("specifications",
                 "SELECT count(*) FROM saas.specifications"),
                ("specification_usages",
                 "SELECT count(*) FROM saas.specification_usages"),
                ("inquiries", "SELECT count(*) FROM saas.inquiries"),
                ("proforma_invoices",
                 "SELECT count(*) FROM saas.proforma_invoices"),
                ("spec_groups", "SELECT count(*) FROM saas.spec_groups"),
                ("spec_group_items",
                 "SELECT count(*) FROM saas.spec_group_items"),
                ("other_charges", "SELECT count(*) FROM saas.other_charges"),
                ("wastage_rows", "SELECT count(*) FROM saas.wastage_rows"),
                ("custom_charges", "SELECT count(*) FROM saas.custom_charges"),
                ("optimization_sessions",
                 "SELECT count(*) FROM saas.optimization_sessions"),
                ("job_orders", "SELECT count(*) FROM saas.job_orders"),
                ("production_pieces",
                 "SELECT count(*) FROM saas.production_pieces"),
                ("production_events",
                 "SELECT count(*) FROM saas.production_events"),
                ("material_reservations",
                 "SELECT count(*) FROM saas.material_reservations"),
                ("delivery_notes", "SELECT count(*) FROM saas.delivery_notes"),
                ("delivery_note_items",
                 "SELECT count(*) FROM saas.delivery_note_items"),
                ("tax_invoices", "SELECT count(*) FROM saas.tax_invoices"),
                ("credit_notes", "SELECT count(*) FROM saas.credit_notes"),
                ("statements", "SELECT count(*) FROM saas.statements"),
                ("statement_lines",
                 "SELECT count(*) FROM saas.statement_lines"),
                ("audit_logs", "SELECT count(*) FROM saas.audit_logs"),
            ):
                cursor.execute(sql)
                checks.append((label, str(cursor.fetchone()[0]), "PASS"))
            # Ledger sanity: every sheet's balance ≥ 0 and the latest
            # stock transaction's balance_after = stock − used.
            cursor.execute("""
                SELECT count(*) FROM saas.glass_sheets
                WHERE stock - used < 0
            """)
            negative = cursor.fetchone()[0]
            checks.append(("sheets with negative balance", str(negative),
                           "PASS" if negative == 0 else "FAIL"))
            cursor.execute("""
                SELECT count(*) FROM saas.glass_sheets g
                WHERE EXISTS (SELECT 1 FROM saas.stock_transactions t
                              WHERE t.glass_sheet_id = g.id)
                  AND COALESCE((SELECT t.balance_after
                                FROM saas.stock_transactions t
                                WHERE t.glass_sheet_id = g.id
                                ORDER BY t.created_at DESC LIMIT 1), 0)
                      <> g.stock - g.used
            """)
            drift = cursor.fetchone()[0]
            checks.append(("ledger tail ≠ stored balance", str(drift),
                           "PASS" if drift == 0 else "FAIL"))
            # Plan §4 financial integrity: for every PI recompute
            # glass + other − discount → vat (at the stored vat_percent
            # when vatable) → grand, tolerance 0.01.
            cursor.execute("""
                SELECT count(*) FROM saas.proforma_invoices p
                WHERE p.is_vat_applicable
                  AND abs(p.vat_amount - round((p.glass_materials_subtotal
                        + p.other_charges_subtotal - p.discount_amount)
                        * p.vat_percent / 100, 2)) > 0.01
                   OR NOT p.is_vat_applicable AND p.vat_amount <> 0
                   OR abs(p.glass_materials_subtotal
                          + p.other_charges_subtotal - p.discount_amount
                          + p.vat_amount - p.grand_total_vat_inclusive)
                      > 0.01
            """)
            pi_drift = cursor.fetchone()[0]
            checks.append(("PI totals ≠ recomputed (±0.01)",
                           str(pi_drift), "PASS" if pi_drift == 0 else "FAIL"))
            # Plan §4 referential: a revision whose root is null must be
            # a root itself (revision 0); orphaned chains are reported.
            cursor.execute("""
                SELECT count(*) FROM saas.proforma_invoices
                WHERE root_proforma_id IS NULL AND revision_number > 0
            """)
            orphan_revisions = cursor.fetchone()[0]
            checks.append(("revisions without an imported root",
                           str(orphan_revisions),
                           "PASS" if orphan_revisions == 0 else "FAIL"))
            # Delivery arithmetic: delivered must never exceed
            # ordered + previously delivered (BUG-05 guard shape).
            cursor.execute("""
                SELECT count(*) FROM saas.delivery_note_items
                WHERE delivered_qty < 0
                   OR delivered_qty > ordered_qty + previously_delivered_qty
            """)
            dn_drift = cursor.fetchone()[0]
            checks.append(("DN delivered outside [0, ordered+previously]",
                           str(dn_drift), "PASS" if dn_drift == 0 else "FAIL"))
    finally:
        connection.close()

    from .engine import Finding, ModuleResult
    result = ModuleResult(name="validate", status="ok")
    result.findings = [
        Finding("info" if status == "PASS" else "error",
                "validate", f"{label}: {count}", row=status)
        for label, count, status in checks]
    report = {"tenant": str(tenant_id), "mode": "validate",
              "modules": [{
                  "name": "validate", "status": "ok", "extracted": 0,
                  "loaded": 0, "skipped": 0, "message": "",
                  "findings": [f.as_dict() for f in result.findings]}]}
    paths = write_report(report, Path(args.report_dir),
                         datetime.now(timezone.utc))
    for label, count, status in checks:
        print(f"[{status}] {label:<28} {count}")
    for path in paths:
        print(f"report: {path}")
    return 0 if all(s == "PASS" for _, _, s in checks) else 1


def cmd_list_modules(_: argparse.Namespace) -> int:
    print("implemented (plan §2 order):")
    for cls in IMPLEMENTED_MODULES:
        print(f"  {cls.name:<15} source={cls.source_db:<18} "
              f"depends_on={','.join(cls.depends_on) or '—'}")
    print("deferred (documented, never silently skipped):")
    for name, reason in DEFERRED_MODULES:
        print(f"  {name:<15} {reason}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="proglass-migrate")
    sub = parser.add_subparsers(dest="command", required=True)

    p_import = sub.add_parser(
        "import", help="import a WPF data root into a SaaS tenant")
    p_import.add_argument("--source", required=True,
                          help="the WPF data root (read-only input)")
    p_import.add_argument("--db-uri", required=True)
    p_import.add_argument("--tenant", required=True,
                          help="tenant slug or UUID")
    p_import.add_argument(
        "--attachments-dir", default=None,
        help="SaaS attachment storage root for imported bytes "
             "(default: $SAAS_ATTACHMENT_ROOT or ./attachments)")
    mode = p_import.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--apply", action="store_true")
    p_import.add_argument("--modules", default="",
                          help="comma-separated module names (default: all)")
    p_import.add_argument("--report-dir", default="migration-out")
    p_import.set_defaults(func=cmd_import)

    p_validate = sub.add_parser(
        "validate", help="post-import validation report")
    p_validate.add_argument("--db-uri", required=True)
    p_validate.add_argument("--tenant", required=True)
    p_validate.add_argument("--report-dir", default="migration-out")
    p_validate.set_defaults(func=cmd_validate)

    p_list = sub.add_parser("list-modules", help="show the module registry")
    p_list.set_defaults(func=cmd_list_modules)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
