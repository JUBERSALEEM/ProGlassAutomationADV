"""The import modules (saas/docs/SAAS_MIGRATION_PLAN.md §2 order).

WPF schema ground truth (read 2026-09-27, EF Core SQLite — table =
DbSet property name or [Table] attribute, column = C# property name):
  • audit.db       Users (Models/Audit/AppUser.cs), ActivityLog
                   (Models/Audit/ActivityLogEntry.cs) — AuditDbContext
  • masterdata.db  Companies/Clients/Projects/Salesmen
                   (Models/MasterData/Saved{Company,Client,Project,
                   Salesman}.cs)
  • proglass.db    GlassSheets (Models/SheetStore/GlassSheet.cs),
                   SheetHistories (SheetHistory.cs), InventoryItems
                   (InventoryItem.cs; enums InventoryCategory 0-7,
                   StockUom 0-3)
  • specifications.db Specifications ([Table] none → DbSet name;
                   Models/Specifications/SavedSpecification.cs),
                   SpecificationUsages (SpecificationUsage.cs) —
                   SpecificationsDbContext
  • inquiries.db   Inquiries/InquiryLineItems/InquiryAttachments/
                   InquiryCommunications ([Table] attrs;
                   Models/Inquiry/InquiryRecord.cs) — InquiryDbContext
  • proformainvoices.db ProformaInvoices ([Table] attr;
                   Models/ProformaInvoice/SavedInvoice/
                   SavedProformaInvoice.cs) — ProformaInvoicesDbContext
                   (spec groups/items/charges/wastage/custom/P&L/terms
                   live INSIDE FullSnapshotJson —
                   ProformaInvoiceSnapshot.cs, PascalCase keys)
  • optimization.db Sessions (DbSet name; Models/Optimization/
                   OptimizationEntities.cs) — OptimizationDbContext;
                   reserved "__latest_run__*" rows are feed plumbing
  • joborders.db   JobOrders ([Table] attr; Models/JobOrder/SavedJobOrder/
                   SavedJobOrder.cs; ProductionDetails inside
                   FullSnapshotJson.JobOrderSnapshot.Production)
  • production.db  ProductionReleases/ProductionWorkItems/
                   ProductionEntries/MaterialReservations/... (18 tables,
                   [Table] attrs; ProductionDbContext) — Phase 16 imports
                   the four the SaaS materializes (§1.7b): work items →
                   production_pieces, entries → production_events,
                   reservations → material_reservations (+ releases are
                   the job_orders row, documented)
  • deliverynotes.db DeliveryNotes/DeliveryNoteItems/
                   DeliveryNoteAttachments ([Table] attrs) —
                   DeliveryNotesDbContext
  • taxinvoices.db TaxInvoices (Models/TaxInvoice/SavedTaxInvoice/
                   SavedTaxInvoice.cs)
  • creditnotes.db CreditNotes (Models/CreditNote/SavedCreditNote/
                   SavedCreditNote.cs)
  • statements.db  Statements (Models/Statement/SavedStatement.cs),
                   LineItems (StatementLineItem.cs)

Deferred module (listed by the CLI, never silently skipped):
attachments — disk files → object storage; there is no object storage in
this environment, so the module stays deferred with per-table counts
reported by the modules that read those source tables.
"""

from __future__ import annotations

import json
import secrets
import sqlite3
import uuid
from decimal import Decimal, ROUND_HALF_EVEN

from psycopg2.extensions import cursor as PgCursor

from .engine import Module, insert_with_savepoint, money, round2, to_date, to_utc

try:  # argon2-cffi — a REAL Argon2id hash (PHC string) the SaaS
    from argon2 import PasswordHasher  # verifier can parse; the password
except ImportError:                    # is a random secret nobody knows.
    PasswordHasher = None


def unusable_password_hash() -> str:
    """Plan §3: WPF SHA-256 hashes cannot migrate → must_reset_password.

    The SaaS password_hash column is NOT NULL and the API verifies with
    Argon2id — so the imported row gets a valid Argon2id PHC string of a
    random 32-byte secret (verifiable format, unknowable password);
    must_reset_password forces the real reset path on first login.
    """
    if PasswordHasher is None:
        raise RuntimeError(
            "argon2-cffi is required for the users module "
            "(.venv/bin/pip install argon2-cffi)")
    return PasswordHasher().hash(secrets.token_urlsafe(32))


SAAS_USER_ROLES = {"Administrator", "Manager", "Estimator", "Sales",
                   "Production", "Store", "Accountant", "Viewer",
                   "Operator"}          # ck_users_role (0001) — the WPF
                                         # catalog ported verbatim
TAX_INVOICE_STATUSES = {"Draft", "Issued", "Paid", "PartiallyPaid",
                        "Overdue", "Cancelled", "Void"}     # 0016 CHECK
CREDIT_NOTE_STATUSES = {"Draft", "Issued", "Applied", "Cancelled"}
VAT_RATE = Decimal("0.05")

# ── Phase 16 vocabularies (SaaS CHECKs, all ported from the WPF) ──────

GLASS_TYPES = {"SGU", "DGU", "LAM", "DGU+LAM"}   # ck_spec_groups_glass_type
INQUIRY_STATUSES = {"Received", "Prepared", "Sent", "Confirmed",
                    "Hold", "Cancelled"}           # ck_inquiries_status
INQUIRY_PRIORITIES = {"low", "normal", "high", "urgent"}
PROFORMA_STATUSES = {"Draft", "Sent", "Approved", "Rejected",
                     "Expired", "Cancelled"}       # ck_proforma_invoices
JOB_ORDER_STATUSES = {"Draft", "Released", "InProduction", "Completed",
                      "Delivered", "Rejected"}     # ck_job_orders (0012)
JOB_ORDER_PRIORITIES = {"Low", "Medium", "High"}  # ck_job_orders_priority
DELIVERY_NOTE_STATUSES = {"Draft", "Dispatching", "Dispatched",
                          "Signed", "Received",
                          "Delivered"}             # ck_delivery_notes
DELIVERY_TYPES = {"Original", "Dummy", "Rework",
                  "Return"}                        # ck_delivery_notes_type
#: WPF ProductionStage enum (Models/Production/ProductionModels.cs) →
#: ck_production_pieces_stage (0014) — the enum names ARE the CHECK.
PRODUCTION_STAGES = {1: "Cutting", 2: "HolesCutouts", 3: "Polish",
                     4: "EdgeDeletion", 5: "Mitring", 6: "Tempering",
                     7: "Pairing", 8: "DoublingLamination"}
#: WPF WorkItemStatus enum → ck_production_pieces_status (0014).
WORK_ITEM_STATUSES = {0: "Pending", 1: "InProgress", 2: "Completed",
                      3: "Paused", 4: "Rework", 5: "Rejected",
                      6: "Cancelled", 7: "QcPending", 8: "QcFailed"}
#: WPF ReservationStatus enum → ck_material_reservations_status (0015).
RESERVATION_STATUSES = {0: "Available", 1: "Reserved", 2: "Allocated",
                        3: "Consumed", 4: "Released", 5: "Cancelled"}
#: MetricKeys (Domain.Proforma.ChargeMetrics) — ck_other_charges_metric.
METRIC_KEYS = {"LM1", "LM2", "LM_COMBINED", "SQM1", "SQM2", "SQM_COMBINED",
               "DGU_CAVITY", "QTY", "DOUBLE_GLASS"}
#: WPF MetricOption.NormalizeKey legacy aliases → canonical.
METRIC_KEY_ALIASES = {"TOTAL_PIECES": "QTY", "TOTAL_QTY": "QTY"}

#: ChargeTemplates (Domain.Proforma.ChargeMetrics, ported from the WPF
#: PredefinedChargeTemplates) — key → (charge_name, unit, default metric).
#: The WPF snapshot stores template identity only; the name/unit/metric
#: defaults come from this registry exactly as the SaaS API resolves them.
CHARGE_TEMPLATES = {
    "ARGON_GAS": ("Argon Gas (Sqm)", "Sqm", "SQM1"),
    "SILICON_BED": ("Silicon bed (lm)", "Lm", "LM1"),
    "SILICON_BED_SQM": ("Silicon bed (Sqm)", "Sqm", "SQM1"),
    "OVERLAP": ("Overlap (lm)", "Lm", "LM1"),
    "OVERLAP_SQM": ("Overlap (Sqm)", "Sqm", "SQM1"),
    "SHUTTER_BONDING": ("Shutter Bonding (lm)", "Lm", "LM_COMBINED"),
    "MITRING": ("Mitring (lm)", "Lm", "LM1"),
    "POLISH": ("Polish (lm)", "Lm", ""),
    "POLISH_MATCHED_LAM": ("Polish — same-glass laminate (2×)", "Lm", ""),
    "CUTOUT": ("Cutout (Qty)", "Qty", "QTY"),
    "HOLES": ("Holes (Qty)", "Qty", "QTY"),
    "SANDBLASTING": ("Sandblasting (Sqm)", "Sqm", "SQM1"),
    "EDGE_GRINDING": ("Edge Grinding (lm)", "Lm", "LM1"),
    "COUNTERSUNK_HOLES": ("Countersunk Holes (Qty)", "Qty", "QTY"),
    "FAN_HOLES": ("Fan Holes (Qty)", "Qty", "QTY"),
    "DELIVERY": ("Delivery (per trip)", "Qty", "QTY"),
    "U_INSERT": ("U-Insert (Sqm)", "Sqm", "SQM1"),
}

#: WPF SqmCalculator (Utilities/SqmCalculator.cs): panel area rounded to
#: 3 dp with Math.Round's banker's rounding, floored at 0.5 m².
MIN_SQM_PER_PANEL = Decimal("0.5")


def round3(value: Decimal) -> Decimal:
    """Math.Round(x, 3) — C# default midpoint rounding (to even)."""
    return value.quantize(Decimal("0.001"), rounding=ROUND_HALF_EVEN)


def panel_sqm(width_mm: int, height_mm: int) -> Decimal:
    """SqmCalculator.CalculatePanel — 3 dp, 0.5 m² billable floor."""
    if width_mm <= 0 or height_mm <= 0:
        return Decimal("0")
    actual = round3(Decimal(width_mm) * Decimal(height_mm)
                    / Decimal(1_000_000))
    return MIN_SQM_PER_PANEL if actual < MIN_SQM_PER_PANEL else actual


def total_sqm(width_mm: int, height_mm: int, qty: int) -> Decimal:
    """SqmCalculator.CalculateTotal — panel SQM × qty at 3 dp."""
    if qty <= 0:
        return Decimal("0")
    return round3(panel_sqm(width_mm, height_mm) * qty)


def row_value(sqm1: Decimal, qty: int, rate: Decimal) -> Decimal:
    """SpecGroupRowMath.Value — MoneyRounding.Round(sqm1 × qty × rate)."""
    return round2(sqm1 * qty * rate)


def surcharge_rate(base_rate: Decimal, surcharge_percent: Decimal) -> Decimal:
    """WPF SpecGroup.SurchargeRate — Round(base × (1 + pct/100)), 2 dp."""
    return round2(base_rate * (Decimal(1) + surcharge_percent / Decimal(100)))


def normalize_metric_key(raw: str) -> str:
    """MetricKeys.Normalize — trim + the legacy TOTAL_* aliases."""
    text = (raw or "").strip()
    return METRIC_KEY_ALIASES.get(text.upper(), text)


def parse_unit(raw: str) -> str:
    """ProformaService.ParseUnit — case-insensitive, unknown → 'Qty'."""
    text = (raw or "").strip().capitalize()
    return text if text in ("Sqm", "Lm", "Qty") else "Qty"


def _reject(finding_cb, label: str, row_id, table: str,
            error: str = "") -> None:
    finding_cb("error", f"{label} not imported — {error or 'row rejected'}",
               f"{table}.Id={row_id}")


# ── module 1: audit.db Users ─────────────────────────────────────────

class UsersModule(Module):
    name = "users"
    source_db = "audit.db"
    depends_on: list[str] = []

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        users: dict[str, uuid.UUID] = {}
        loaded = 0
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, Username, DisplayName, Role, Mobile, Email,"
                " CreatedAtUtc FROM Users ORDER BY Id").fetchall()
        extracted = len(rows)
        for row in rows:
            username = (row["Username"] or "").strip()
            if not username:
                self.finding("error", "User without a username — skipped",
                             f"Users.Id={row['Id']}")
                continue
            role = (row["Role"] or "").strip()
            if role not in SAAS_USER_ROLES:
                # Plan §3 says unknown statuses pass through; a ROLE the
                # SaaS CHECK does not know can never load — the row is
                # reported and skipped (never silently remapped).
                self.finding("error",
                             f"Role '{role}' is not in the SaaS role "
                             "catalog — user skipped", f"Users.Id={row['Id']}")
                continue
            email = (row["Email"] or "").strip() or None   # "" → NULL
            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.users
                  (id, tenant_id, username, display_name, email, mobile,
                   password_hash, role, is_active, must_reset_password,
                   failed_attempt_count, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, true, true, 0,
                        %s, %s)
                """,
                (new_id, tenant_id, username,
                 row["DisplayName"] or username, email,
                 row["Mobile"] or "", unusable_password_hash(), role,
                 to_utc(row["CreatedAtUtc"], source_is_utc=True)
                 or to_utc("2000-01-01"),
                 to_utc(row["CreatedAtUtc"], source_is_utc=True)))
            if ok:
                users[username] = new_id
                loaded += 1
            else:
                _reject(self.finding, f"User '{username}'",
                        row["Id"], "Users", err)
        id_map["users"] = users
        id_map["users_total"] = extracted
        return extracted, loaded


# ── module 2: masterdata.db ──────────────────────────────────────────

class MasterDataModule(Module):
    name = "masterdata"
    source_db = "masterdata.db"
    depends_on = ["users"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            companies = src.execute(
                "SELECT Id, CompanyName, CompanyTrn, CompanyLocation,"
                " UsageCount, LastUsedAt, CreatedAt FROM Companies"
                " ORDER BY Id").fetchall()
            clients = src.execute(
                "SELECT Id, CustomerName, CustomerTrn,"
                " CustomerReferenceCode, SalesRepresentative,"
                " BillingAddress, AttentionExecutiveName,"
                " ClientContactPhone, UsageCount, LastUsedAt, CreatedAt"
                " FROM Clients ORDER BY Id").fetchall()
            projects = src.execute(
                "SELECT Id, ProjectIdentifierName, ProjectContractNo,"
                " ProjectReference, SiteDeliveryLocation, LpoNumber,"
                " CustomerName, UsageCount, LastUsedAt, CreatedAt"
                " FROM Projects ORDER BY Id").fetchall()
            salesmen = src.execute(
                "SELECT Id, Name, Department, UsageCount, LastUsedAt,"
                " CreatedAt FROM Salesmen ORDER BY Id").fetchall()

        extracted = len(companies) + len(clients) + len(projects) \
            + len(salesmen)
        loaded = 0
        client_ids: dict[str, uuid.UUID] = {}   # CustomerName → id
        company_ids: dict[str, uuid.UUID] = {}
        client_int_ids: dict[int, uuid.UUID] = {}
        project_ids: dict[str, uuid.UUID] = {}
        project_int_ids: dict[int, uuid.UUID] = {}
        salesman_ids: dict[str, uuid.UUID] = {}

        for row in companies:
            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.companies
                  (id, tenant_id, name, trn, location, usage_count,
                   last_used_at, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (new_id, tenant_id, row["CompanyName"] or "",
                 row["CompanyTrn"] or "", row["CompanyLocation"] or "",
                 row["UsageCount"] or 1, to_utc(row["LastUsedAt"]),
                 to_utc(row["CreatedAt"]), to_utc(row["CreatedAt"])))
            if ok:
                company_ids[(row["CompanyName"] or "").strip()] = new_id
                loaded += 1
            else:
                _reject(self.finding,
                        f"Company '{row['CompanyName']}'", row["Id"],
                        "Companies")

        for row in clients:
            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.clients
                  (id, tenant_id, customer_name, customer_trn,
                   reference_code, sales_representative, billing_address,
                   attention_executive_name, phone, usage_count,
                   last_used_at, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (new_id, tenant_id, row["CustomerName"] or "",
                 row["CustomerTrn"] or "",
                 row["CustomerReferenceCode"] or "",
                 row["SalesRepresentative"] or "",
                 row["BillingAddress"] or "",
                 row["AttentionExecutiveName"] or "",
                 row["ClientContactPhone"] or "",
                 row["UsageCount"] or 1, to_utc(row["LastUsedAt"]),
                 to_utc(row["CreatedAt"]), to_utc(row["CreatedAt"])))
            if ok:
                client_ids[(row["CustomerName"] or "").strip()] = new_id
                client_int_ids[int(row["Id"])] = new_id
                loaded += 1
            else:
                _reject(self.finding,
                        f"Client '{row['CustomerName']}'", row["Id"],
                        "Clients", err)

        for row in projects:
            # Projects reference clients by NAME in the WPF; the SaaS has
            # customer_id — resolved when the name matches an imported
            # client, otherwise null + finding (never a wrong link).
            customer_id = client_ids.get((row["CustomerName"] or "").strip())
            if (row["CustomerName"] or "").strip() and customer_id is None:
                self.finding("warning",
                             f"Project '{row['ProjectIdentifierName']}' has"
                             f" no matching imported client"
                             f" '{row['CustomerName']}' — customer_id null",
                             f"Projects.Id={row['Id']}")
            project_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.projects
                  (id, tenant_id, identifier_name, contract_no, reference,
                   site_delivery_location, lpo_number, customer_id,
                   usage_count, last_used_at, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (project_id, tenant_id,
                 row["ProjectIdentifierName"] or "",
                 row["ProjectContractNo"] or "",
                 row["ProjectReference"] or "",
                 row["SiteDeliveryLocation"] or "",
                 row["LpoNumber"] or "", customer_id,
                 row["UsageCount"] or 1, to_utc(row["LastUsedAt"]),
                 to_utc(row["CreatedAt"]), to_utc(row["CreatedAt"])))
            if ok:
                project_ids[(row["ProjectIdentifierName"] or "").strip()] = \
                    project_id
                project_int_ids[int(row["Id"])] = project_id
                loaded += 1
            else:
                _reject(self.finding,
                        f"Project '{row['ProjectIdentifierName']}'",
                        row["Id"], "Projects", err)

        for row in salesmen:
            salesman_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.salesmen
                  (id, tenant_id, name, department, usage_count,
                   last_used_at, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (salesman_id, tenant_id, row["Name"] or "",
                 row["Department"] or "", row["UsageCount"] or 1,
                 to_utc(row["LastUsedAt"]), to_utc(row["CreatedAt"]),
                 to_utc(row["CreatedAt"])))
            if ok:
                salesman_ids[(row["Name"] or "").strip()] = salesman_id
                loaded += 1
            else:
                _reject(self.finding, f"Salesman '{row['Name']}'",
                        row["Id"], "Salesmen", err)
        # Phase 16: the document modules resolve parties by name and by
        # WPF int id (inquiries carry Customers int FKs; documents carry
        # names) — same maps, both spellings.
        id_map["companies_by_name"] = company_ids
        id_map["clients_by_name"] = client_ids
        id_map["clients_by_int"] = client_int_ids
        id_map["projects_by_name"] = project_ids
        id_map["projects_by_int"] = project_int_ids
        id_map["salesmen_by_name"] = salesman_ids
        return extracted, loaded


# ── module 3: proglass.db (Sheet Store) ──────────────────────────────

#: WPF InventoryCategory enum → SaaS ck_inventory_items_category.
INVENTORY_CATEGORY = {
    1: "spacer",     # SpacerBar
    2: "desiccant",  # Desiccant
    3: "sealant",    # PrimarySeal (butyl/PIB)
    4: "sealant",    # SecondarySeal (silicone/polysulphide)
    5: "gas",        # Gas
    6: "hardware",   # Hardware
}
#: WPF StockUom enum → SaaS unit text.
INVENTORY_UOM = {0: "Piece", 1: "LinearMetre", 2: "SquareMetre",
                 3: "Kilogram"}


class SheetStoreModule(Module):
    name = "sheetstore"
    source_db = "proglass.db"
    depends_on = ["users"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            sheets = src.execute(
                "SELECT Id, Category, ThicknessMm, Color, WidthMm,"
                " HeightMm, Supplier, PurchasePricePerSqm,"
                " SellPricePerSqm, Stock, Used, CreatedAt, LastUpdated,"
                " LastPurchaseDate FROM GlassSheets ORDER BY Id").fetchall()
            history = src.execute(
                "SELECT Id, SheetId, Action, Details, QuantityChanged,"
                " UserName, DocumentReference, Timestamp, IsReversed"
                " FROM SheetHistories ORDER BY SheetId, Timestamp, Id"
                ).fetchall()
            items = src.execute(
                "SELECT Id, Name, Category, PartNumber, Supplier,"
                " Specification, Uom, Stock, Used, ReorderLevel,"
                " PurchasePricePerUnit, SellPricePerUnit, CreatedAt,"
                " LastUpdated, LastPurchaseDate FROM InventoryItems"
                " ORDER BY Id").fetchall()

        extracted = len(sheets) + len(history) + len(items)
        by_sheet: dict[int, list] = {}
        for h in history:
            by_sheet.setdefault(h["SheetId"], []).append(h)

        loaded = 0
        sheet_ids: dict[int, uuid.UUID] = {}   # WPF GlassSheets.Id → uuid
        for row in sheets:
            sheet_id = uuid.uuid4()
            stock = int(row["Stock"] or 0)
            used = int(row["Used"] or 0)

            # Plan §3: balances recomputed from history and COMPARED to
            # the stored Stock/Used — a mismatch is reported, the stored
            # values import as-is (they are the operator's ledger).
            events = by_sheet.get(row["Id"], [])
            crud = [e for e in events
                    if e["Action"] not in ("Bought", "Used")]
            buys = sum(int(e["QuantityChanged"] or 0)
                       for e in events if e["Action"] == "Bought"
                       and not e["IsReversed"])
            uses = sum(int(e["QuantityChanged"] or 0)
                       for e in events if e["Action"] == "Used"
                       and not e["IsReversed"])
            if crud:
                self.finding("info",
                             f"Sheet '{row['Category']} {row['ThicknessMm']}"
                             f"mm {row['Color']}' history contains "
                             f"{len(crud)} add/edit/delete row(s) — stored "
                             "Stock/Used imported as-is",
                             f"GlassSheets.Id={row['Id']}")
            elif buys - uses != stock - used:
                self.finding("warning",
                             f"Sheet '{row['Category']} {row['ThicknessMm']}"
                             f"mm {row['Color']}' balance mismatch: history"
                             f" says {buys - uses}, stored Stock-Used = "
                             f"{stock - used} — stored values imported",
                             f"GlassSheets.Id={row['Id']}")

            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.glass_sheets
                  (id, tenant_id, category, thickness_mm, color, width_mm,
                   height_mm, supplier, purchase_price_per_sqm,
                   sell_price_per_sqm, stock, used, last_purchase_date,
                   created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s)
                """,
                (sheet_id, tenant_id, row["Category"] or "",
                 int(row["ThicknessMm"] or 0), row["Color"] or "Clear",
                 int(row["WidthMm"] or 0), int(row["HeightMm"] or 0),
                 row["Supplier"] or "", money(row["PurchasePricePerSqm"]),
                 money(row["SellPricePerSqm"]), stock, used,
                 to_date(row["LastPurchaseDate"]),
                 to_utc(row["CreatedAt"]), to_utc(row["LastUpdated"])))
            if not ok:
                _reject(self.finding, f"Glass sheet Id={row['Id']}",
                        row["Id"], "GlassSheets", err)
                continue
            sheet_ids[int(row["Id"])] = sheet_id
            loaded += 1

            # The immutable stock ledger: one transaction per Buy/Use,
            # balance_after reconstructed from history order (plan §3).
            balance = 0
            for event in events:
                if event["IsReversed"]:
                    continue
                action = event["Action"]
                qty = int(event["QuantityChanged"] or 0)
                if action == "Bought":
                    balance += qty
                    tx_type, delta = "purchase", Decimal(qty)
                elif action == "Used":
                    balance -= qty
                    tx_type, delta = "issue", Decimal(-qty)
                else:
                    continue
                ok = insert_with_savepoint(
                    pg,
                    """
                    INSERT INTO saas.stock_transactions
                      (id, tenant_id, item_type, glass_sheet_id,
                       transaction_type, quantity_delta, balance_after,
                       unit_cost, notes, created_at)
                    VALUES (%s, %s, 'sheet', %s, %s, %s, %s, %s, %s, %s)
                    """,
                    (uuid.uuid4(), tenant_id, sheet_id, tx_type, delta,
                     Decimal(balance), money(row["PurchasePricePerSqm"]),
                     event["Details"] or "", to_utc(event["Timestamp"])))
                if ok:
                    loaded += 1
                else:
                    _reject(self.finding,
                            f"Sheet ledger row (sheet {row['Id']},"
                            f" {action})", event["Id"],
                            "SheetHistories", err)

        # Non-glass consumables.
        for row in items:
            category = INVENTORY_CATEGORY.get(int(row["Category"] or 0))
            if category is None:
                # WPF 'Other'(0) / 'Film'(7) have no SaaS category —
                # reported and skipped, never forced into a wrong bucket.
                self.finding("error",
                             f"Inventory item '{row['Name']}' has no SaaS"
                             " category (WPF Other/Film) — skipped",
                             f"InventoryItems.Id={row['Id']}")
                continue
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.inventory_items
                  (id, tenant_id, category, name, supplier, stock, used,
                   reorder_level, purchase_price_per_unit,
                   sell_price_per_unit, unit, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (uuid.uuid4(), tenant_id, category, row["Name"] or "",
                 row["Supplier"] or "", money(row["Stock"]),
                 money(row["Used"]), money(row["ReorderLevel"]),
                 money(row["PurchasePricePerUnit"]),
                 money(row["SellPricePerUnit"]),
                 INVENTORY_UOM.get(int(row["Uom"] or 0), "Piece"),
                 to_utc(row["CreatedAt"]), to_utc(row["LastUpdated"])))
            if ok:
                loaded += 1
            else:
                _reject(self.finding, f"Inventory item '{row['Name']}'",
                        row["Id"], "InventoryItems", err)
        id_map["glass_sheets_by_int"] = sheet_ids
        return extracted, loaded


# ── module 4: specifications.db (priced-spec library) ────────────────

class SpecificationsModule(Module):
    """WPF SavedSpecification → saas.specifications.

    The WPF registry row IS the SaaS library row (0008 added the
    catalogue columns for exactly this shape). sheet_id resolves against
    the imported glass sheets (the plan's "stock colors" dependency);
    UsageCount/LastUsedAt import verbatim — they are the operator's
    ledger, the SaaS API recomputes them on live saves only.
    """

    name = "specifications"
    source_db = "specifications.db"
    depends_on = ["users", "sheetstore"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        sheets: dict = id_map.get("glass_sheets_by_int", {})
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, GlassType, SpecName, PricePerSqm, CreatedAt,"
                " LastUsedAt, UsageCount, Category, Color, ThicknessMm,"
                " SheetWidthMm, SheetHeightMm, SheetId, ProformaId,"
                " PanesJson FROM Specifications ORDER BY Id").fetchall()
        extracted = len(rows)
        loaded = 0
        by_key: dict[tuple[str, str], uuid.UUID] = {}
        by_int: dict[int, uuid.UUID] = {}
        for row in rows:
            glass_type = (row["GlassType"] or "").strip()
            spec_name = (row["SpecName"] or "").strip()
            if not spec_name:
                self.finding("error",
                             "Specification without a SpecName — skipped",
                             f"Specifications.Id={row['Id']}")
                continue
            if glass_type not in GLASS_TYPES:
                self.finding("error",
                             f"Specification '{spec_name}' glass type"
                             f" '{glass_type}' is outside the SaaS CHECK"
                             " vocabulary — skipped",
                             f"Specifications.Id={row['Id']}")
                continue

            sheet_id = None
            if row["SheetId"]:
                sheet_id = sheets.get(int(row["SheetId"]))
                if sheet_id is None:
                    self.finding("info",
                                 f"Specification '{spec_name}' references"
                                 f" WPF sheet {row['SheetId']} which was"
                                 " not imported — sheet_id null",
                                 f"Specifications.Id={row['Id']}")

            panes_json = None
            panes = (row["PanesJson"] or "").strip()
            if panes:
                try:
                    json.loads(panes)
                    panes_json = panes
                except (ValueError, TypeError):
                    self.finding("warning",
                                 f"Specification '{spec_name}' PanesJson is"
                                 " not valid JSON — imported as null",
                                 f"Specifications.Id={row['Id']}")

            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.specifications
                  (id, tenant_id, spec_name, glass_type, rate_per_sqm,
                   usage_count, last_used_at, is_active, category, color,
                   thickness_mm, sheet_width_mm, sheet_height_mm,
                   sheet_id, panes_json, last_proforma_no, created_at,
                   updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, true, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s)
                """,
                (new_id, tenant_id, spec_name, glass_type,
                 money(row["PricePerSqm"]), int(row["UsageCount"] or 0),
                 to_utc(row["LastUsedAt"]), row["Category"] or None,
                 row["Color"] or None, int(row["ThicknessMm"] or 0),
                 int(row["SheetWidthMm"] or 0),
                 int(row["SheetHeightMm"] or 0), sheet_id, panes_json,
                 (row["ProformaId"] or "").strip() or None,
                 to_utc(row["CreatedAt"]), to_utc(row["LastUsedAt"])))
            if ok:
                by_key[(glass_type, spec_name)] = new_id
                by_int[int(row["Id"])] = new_id
                loaded += 1
            else:
                _reject(self.finding, f"Specification '{spec_name}'",
                        row["Id"], "Specifications", err)
        id_map["specs_by_key"] = by_key
        id_map["specs_by_int"] = by_int
        return extracted, loaded


# ── module 5: inquiries.db ───────────────────────────────────────────

class InquiriesModule(Module):
    """WPF InquiryRecord (+ LineItems) → saas.inquiries.

    The BOQ (InquiryLineItems) is rebuilt as boq_json in the exact
    InquiryBoqLine shape the SaaS ParseBoq reads (camelCase keys) —
    converted inquiries stay convertible in the SaaS. Communications and
    attachments have no SaaS table (the inquiry module beyond
    convert-to-PI is its own later phase, mapping §1.1) — counted and
    reported, never silently dropped. Status normalization is the WPF's
    own InquiryStatuses.Normalize (On Hold → Hold, Canceled → Cancelled,
    case-insensitive, unknown → Received) with the original spelling
    preserved in status_text.
    """

    name = "inquiries"
    source_db = "inquiries.db"
    depends_on = ["users", "masterdata"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        clients_int: dict = id_map.get("clients_by_int", {})
        projects_int: dict = id_map.get("projects_by_int", {})
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, InquiryNumber, InquiryReference, CustomerId,"
                " CustomerName, CustomerLocation, ProjectId, ProjectName,"
                " ProjectDescription, SalesmanId, SalesmanName,"
                " PreparedByUserId, PreparedByName, PreparedByRole,"
                " Status, Priority, InquiryValue, Notes, ReceivedAt,"
                " PreparedAt, SentAt, ConfirmedAt, CreatedAt, UpdatedAt,"
                " UpdatedBy, ProformaInvoiceId, ProformaNumber,"
                " JobOrderNumber FROM Inquiries ORDER BY Id").fetchall()
            lines = src.execute(
                "SELECT Id, InquiryId, SortOrder, GlassType, SpecName,"
                " OuterGlassColor, InnerGlassColor, WidthMm, HeightMm,"
                " Qty, RatePerSqm, Notes FROM InquiryLineItems"
                " ORDER BY InquiryId, SortOrder, Id").fetchall()
            comm_count = src.execute(
                "SELECT count(*) FROM InquiryCommunications").fetchone()[0]
            att_count = src.execute(
                "SELECT count(*) FROM InquiryAttachments").fetchone()[0]

        by_inquiry: dict[int, list] = {}
        for line in lines:
            by_inquiry.setdefault(line["InquiryId"], []).append(line)

        extracted = len(rows) + len(lines)
        loaded = 0
        by_int: dict[int, uuid.UUID] = {}
        for row in rows:
            number = (row["InquiryNumber"] or "").strip()
            if not number:
                self.finding("error",
                             "Inquiry without a number — skipped",
                             f"Inquiries.Id={row['Id']}")
                continue
            raw_status = (row["Status"] or "").strip()
            status = _normalize_inquiry_status(raw_status)
            if status is None:
                self.finding("error",
                             f"Inquiry {number} status '{raw_status}' has"
                             " no SaaS vocabulary member — skipped",
                             f"Inquiries.Id={row['Id']}")
                continue
            if status != raw_status:
                self.finding("info",
                             f"Inquiry {number} status '{raw_status}'"
                             f" normalized to '{status}' (the WPF's own"
                             " InquiryStatuses.Normalize; original kept"
                             " in status_text)",
                             f"Inquiries.Id={row['Id']}")
            raw_priority = (row["Priority"] or "").strip().lower()
            if raw_priority not in INQUIRY_PRIORITIES:
                self.finding("error",
                             f"Inquiry {number} priority"
                             f" '{row['Priority']}' is outside the SaaS"
                             " CHECK vocabulary — skipped",
                             f"Inquiries.Id={row['Id']}")
                continue

            customer_id = clients_int.get(int(row["CustomerId"])) \
                if row["CustomerId"] else None
            if row["CustomerId"] and customer_id is None:
                self.finding("warning",
                             f"Inquiry {number} references WPF client"
                             f" {row['CustomerId']} which was not imported"
                             " — customer_id null",
                             f"Inquiries.Id={row['Id']}")
            project_id = projects_int.get(int(row["ProjectId"])) \
                if row["ProjectId"] else None
            if row["ProjectId"] and project_id is None:
                self.finding("warning",
                             f"Inquiry {number} references WPF project"
                             f" {row['ProjectId']} which was not imported"
                             " — project_id null",
                             f"Inquiries.Id={row['Id']}")

            # BOQ rebuilt in the InquiryBoqLine (camelCase) shape so the
            # SaaS ParseBoq round-trips it exactly.
            boq = [{"sortOrder": line["SortOrder"] or 0,
                    "glassType": line["GlassType"] or "",
                    "specName": line["SpecName"] or "",
                    "outerGlassColor": line["OuterGlassColor"] or "",
                    "innerGlassColor": line["InnerGlassColor"] or "",
                    "widthMm": int(line["WidthMm"] or 0),
                    "heightMm": int(line["HeightMm"] or 0),
                    "qty": int(line["Qty"] or 0),
                    "ratePerSqm": str(money(line["RatePerSqm"])),
                    "notes": line["Notes"] or ""}
                   for line in by_inquiry.get(row["Id"], [])]

            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.inquiries
                  (id, tenant_id, inquiry_no, status, status_text,
                   priority, customer_id, project_id, customer_name,
                   project_name, salesman, notes, received_at, boq_json,
                   is_active, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, true, %s, %s)
                """,
                (new_id, tenant_id, number, status, raw_status,
                 raw_priority, customer_id, project_id,
                 row["CustomerName"] or "", row["ProjectName"] or "",
                 row["SalesmanName"] or "", row["Notes"] or "",
                 to_utc(row["ReceivedAt"]), json.dumps(boq),
                 to_utc(row["CreatedAt"]), to_utc(row["UpdatedAt"])))
            if not ok:
                _reject(self.finding, f"Inquiry {number}", row["Id"],
                        "Inquiries", err)
                continue
            by_int[int(row["Id"])] = new_id
            loaded += 1
            loaded += len(boq)     # the BOQ lines live inside boq_json

        if comm_count:
            self.finding("info",
                         f"{comm_count} inquiry communication row(s) have"
                         " no SaaS table (inquiry timeline is a later"
                         " phase, mapping §1.1) — counted, not imported",
                         "InquiryCommunications")
        if att_count:
            self.finding("info",
                         f"{att_count} inquiry attachment row(s) — the"
                         " attachments module is deferred (object storage)",
                         "InquiryAttachments")
        id_map["inquiries_by_int"] = by_int
        return extracted, loaded


def _normalize_inquiry_status(raw: str) -> str | None:
    """WPF InquiryStatuses.Normalize: On Hold → Hold, Canceled →
    Cancelled, case-insensitive member match, unknown → Received."""
    text = (raw or "").strip()
    if text.casefold() == "on hold":
        return "Hold"
    if text.casefold() == "canceled":
        return "Cancelled"
    for member in INQUIRY_STATUSES:
        if member.casefold() == text.casefold():
            return member
    return "Received"     # the WPF's own fallback


# ── module 6: proformainvoices.db ────────────────────────────────────

class ProformasModule(Module):
    """WPF SavedProformaInvoice → saas.proforma_invoices + the
    normalized Step 5/6/9 projections parsed out of FullSnapshotJson
    (ProformaInvoiceSnapshot, PascalCase keys — plan §2 module 6).

    The snapshot JSON is preserved verbatim in snapshot_json; the
    normalized spec_groups/spec_group_items/other_charges/wastage_rows/
    custom_charges are the additive projection (plan §6 risk #1). Row
    math is the ported WPF SqmCalculator/SpecGroupRowMath (3 dp banker's
    for SQM — Math.Round's default — and MoneyRounding 2 dp away-from-
    zero for money). Stored snapshot amounts import verbatim with
    recomputation mismatches reported (never "fixed").
    """

    name = "proformas"
    source_db = "proformainvoices.db"
    depends_on = ["users", "masterdata", "specifications", "inquiries"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        companies: dict = id_map.get("companies_by_name", {})
        clients: dict = id_map.get("clients_by_name", {})
        projects: dict = id_map.get("projects_by_name", {})
        salesmen: dict = id_map.get("salesmen_by_name", {})
        users: dict = id_map.get("users", {})
        specs: dict = id_map.get("specs_by_key", {})
        inquiries: dict = id_map.get("inquiries_by_int", {})

        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, ProformaId, InquiryId, CompanyName,"
                " CustomerName, ProjectName, Status, IsVoid, VoidedAtUtc,"
                " VoidedBy, VoidReason, IssueDate, ValidUntil,"
                " GlassMaterialsSubtotal, OtherChargesSubtotal,"
                " DiscountAmount, VatAmount, GrandTotalVatInclusive,"
                " FullSnapshotJson, CreatedAt, UpdatedAt, RowVersion,"
                " RootProformaId, RevisionNumber"
                " FROM ProformaInvoices ORDER BY Id").fetchall()
        extracted = len(rows)
        loaded = 0
        by_no: dict[str, uuid.UUID] = {}
        by_int: dict[int, uuid.UUID] = {}
        group_items: dict[tuple, uuid.UUID] = {}
        groups_map: dict[tuple, uuid.UUID] = {}
        glass_refs_lost = 0
        phantom_rows = 0

        for row in rows:
            number = (row["ProformaId"] or "").strip()
            if not number:
                self.finding("error",
                             "Proforma without a ProformaId — skipped",
                             f"ProformaInvoices.Id={row['Id']}")
                continue
            status = (row["Status"] or "").strip()
            if status not in PROFORMA_STATUSES:
                self.finding("error",
                             f"Proforma {number} status '{status}' is"
                             " outside the SaaS CHECK vocabulary —"
                             " skipped (plan §3's pass-through predates"
                             " the CHECKs; unknowns are reported, never"
                             " remapped)",
                             f"ProformaInvoices.Id={row['Id']}")
                continue

            snapshot = None
            raw_snapshot = (row["FullSnapshotJson"] or "").strip()
            if raw_snapshot:
                try:
                    snapshot = json.loads(raw_snapshot)
                except (ValueError, TypeError):
                    self.finding("error",
                                 f"Proforma {number} FullSnapshotJson is"
                                 " not valid JSON — the normalized"
                                 " projection is impossible; row skipped"
                                 " (the snapshot IS the document)",
                                 f"ProformaInvoices.Id={row['Id']}")
                    continue
            else:
                self.finding("warning",
                             f"Proforma {number} has an empty snapshot —"
                             " header imported, no Step 5/6 projection",
                             f"ProformaInvoices.Id={row['Id']}")

            # Revision chain: WPF blank root = the row IS the root
            # (Repository line 88); SaaS null = root (mapping §1.2a).
            root_no = (row["RootProformaId"] or "").strip()
            if not root_no or root_no == number:
                root_proforma_id = None
            else:
                root_proforma_id = by_no.get(root_no)
                if root_proforma_id is None:
                    self.finding("warning",
                                 f"Proforma {number} root '{root_no}' was"
                                 " not imported — root_proforma_id null",
                                 f"ProformaInvoices.Id={row['Id']}")

            glass_sub = money(row["GlassMaterialsSubtotal"])
            other_sub = money(row["OtherChargesSubtotal"])
            discount = money(row["DiscountAmount"])
            vat = money(row["VatAmount"])
            grand = money(row["GrandTotalVatInclusive"])
            summary = (snapshot or {}).get("Summary") or {}
            vat_pct = money(summary.get("VatPercent", 5))
            vat_applicable = bool(summary.get("IsVatApplicable", True))
            before_vat = glass_sub + other_sub - discount
            expected_vat = round2(before_vat * vat_pct / Decimal(100)) \
                if vat_applicable else Decimal("0")
            if expected_vat != vat:
                self.finding("warning",
                             f"Proforma {number}: stored VAT {vat} differs"
                             f" from recomputed {expected_vat} — stored"
                             " values imported",
                             f"ProformaInvoices.Id={row['Id']}")
            if round2(before_vat + vat) != grand:
                self.finding("warning",
                             f"Proforma {number}: grand total {grand} does"
                             " not equal glass + other − discount + VAT"
                             f" ({round2(before_vat + vat)}) — stored"
                             " values imported",
                             f"ProformaInvoices.Id={row['Id']}")

            # Party resolution by NAME (the WPF stores names on the row).
            company_name = (row["CompanyName"] or "").strip()
            customer_name = (row["CustomerName"] or "").strip()
            project_name = (row["ProjectName"] or "").strip()
            company_id = companies.get(company_name) or None
            client_id = clients.get(customer_name) or None
            project_id = projects.get(project_name) or None
            if company_name and company_id is None:
                self.finding("warning",
                             f"Proforma {number} company '{company_name}'"
                             " was not imported — company_id null",
                             f"ProformaInvoices.Id={row['Id']}")
            if customer_name and client_id is None:
                self.finding("warning",
                             f"Proforma {number} client '{customer_name}'"
                             " was not imported — client_id null",
                             f"ProformaInvoices.Id={row['Id']}")
            if project_name and project_id is None:
                self.finding("warning",
                             f"Proforma {number} project '{project_name}'"
                             " was not imported — project_id null",
                             f"ProformaInvoices.Id={row['Id']}")
            # The WPF PI carries no salesman — the client's sales
            # representative is the closest named fact (reported when it
            # resolves so the link's provenance is visible).
            rep = ((snapshot or {}).get("Client") or {}).get(
                "SalesRepresentative", "")
            salesman_id = salesmen.get((rep or "").strip()) or None
            if rep and salesman_id is not None:
                self.finding("info",
                             f"Proforma {number} salesman_id resolved from"
                             f" the snapshot client representative"
                             f" '{rep}' (the WPF PI has no salesman"
                             " column)",
                             f"ProformaInvoices.Id={row['Id']}")

            inquiry_id = inquiries.get(int(row["InquiryId"])) \
                if row["InquiryId"] else None
            if row["InquiryId"] and inquiry_id is None:
                self.finding("warning",
                             f"Proforma {number} references WPF inquiry"
                             f" {row['InquiryId']} which was not imported"
                             " — inquiry_id null",
                             f"ProformaInvoices.Id={row['Id']}")

            voided_by = users.get((row["VoidedBy"] or "").strip()) or None \
                if (row["VoidedBy"] or "").strip() else None
            pnl = (snapshot or {}).get("ProfitLoss") or {}
            furthest_step = int((snapshot or {}).get("FurthestStepReached")
                                or 0)
            lifecycle = status if status in {
                "Draft", "Approved", "Sent", "Confirmed", "Cancelled",
                "Rejected"} else "Sent"
            if lifecycle != status:
                self.finding("info",
                             f"Proforma {number} status 'Expired' has no"
                             " lifecycle member — lifecycle_state derived"
                             " 'Sent' (an expired quote was sent); the"
                             " original stays in status/status_text",
                             f"ProformaInvoices.Id={row['Id']}")

            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.proforma_invoices
                  (id, tenant_id, proforma_no, root_proforma_id,
                   revision_number, inquiry_id, company_id, client_id,
                   project_id, salesman_id, status, status_text,
                   lifecycle_state, issue_date, valid_until,
                   glass_materials_subtotal, other_charges_subtotal,
                   discount_amount, vat_amount, grand_total_vat_inclusive,
                   discount_type, discount_value, is_vat_applicable,
                   vat_percent, is_void, voided_at, voided_by,
                   void_reason, furthest_step, snapshot_json, row_version,
                   pnl_wastage_percent, pnl_net_margin_percent,
                   pnl_gross_vat_inclusive, pnl_net_ex_vat,
                   pnl_total_glass_cost, pnl_total_production_cost,
                   pnl_profit, pnl_margin_percent,
                   pnl_overall_margin_percent, pnl_calculated_at,
                   created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s)
                """,
                (new_id, tenant_id, number, root_proforma_id,
                 int(row["RevisionNumber"] or 0), inquiry_id, company_id,
                 client_id, project_id, salesman_id, status, status,
                 lifecycle, to_date(row["IssueDate"]),
                 to_date(row["ValidUntil"]), glass_sub, other_sub,
                 discount, vat, grand,
                 (summary.get("DiscountType") or "Percentage"),
                 money(summary.get("DiscountValue", 0)), vat_applicable,
                 vat_pct, bool(row["IsVoid"]),
                 to_utc(row["VoidedAtUtc"], source_is_utc=True),
                 voided_by, row["VoidReason"] or "", furthest_step,
                 raw_snapshot or None, int(row["RowVersion"] or 0),
                 money(pnl.get("WastagePercent", 15)),
                 money(pnl.get("NetMarginPercent", 0)),
                 money(pnl.get("GrossVatInclusive", 0)),
                 money(pnl.get("NetExVat", 0)),
                 money(pnl.get("TotalGlassCost", 0)),
                 money(pnl.get("TotalProductionCost", 0)),
                 money(pnl.get("Profit", 0)),
                 money(pnl.get("MarginPercent", 0)),
                 money(pnl.get("OverallMarginPercent", 0)),
                 to_utc(pnl.get("ComputedAtUtc"), source_is_utc=True),
                 to_utc(row["CreatedAt"]), to_utc(row["UpdatedAt"])))
            if not ok:
                _reject(self.finding, f"Proforma {number}", row["Id"],
                        "ProformaInvoices", err)
                continue
            by_no[number] = new_id
            by_int[int(row["Id"])] = new_id
            loaded += 1

            step5_loaded, refs_lost = self._load_step5(
                pg, tenant_id, row, new_id, number, snapshot, specs,
                groups_map, group_items)
            glass_refs_lost += refs_lost
            loaded += step5_loaded
            loaded += self._load_step6(pg, tenant_id, new_id, number,
                                       snapshot)
            # The inquiry backlink (the WPF stores it on the inquiry
            # row; converted_proforma_id is the SaaS column).
            if row["InquiryId"] and inquiry_id is not None:
                ok2, err2 = insert_with_savepoint(
                    pg,
                    "UPDATE saas.inquiries SET converted_proforma_id = %s"
                    " WHERE id = %s AND tenant_id = %s",
                    (new_id, inquiry_id, tenant_id))
                if not ok2:
                    self.finding("warning",
                                 f"Inquiry backlink for proforma {number}"
                                 f" failed: {err2}",
                                 f"ProformaInvoices.Id={row['Id']}")

        if glass_refs_lost:
            self.finding("info",
                         f"{glass_refs_lost} spec-group item row(s) carry"
                         " WPF GlassRef values — saas.spec_group_items has"
                         " no glass_ref column (0009 read-path parity uses"
                         " position); preserved in snapshot_json only")
        id_map["proformas_by_no"] = by_no
        id_map["proformas_by_int"] = by_int
        id_map["spec_groups_by_pi"] = groups_map
        id_map["spec_group_items_by_row"] = group_items
        return extracted, loaded

    # ── Step 5: spec groups + item rows ─────────────────────────────
    def _load_step5(self, pg, tenant_id, row, proforma_id, number,
                    snapshot, specs, groups_map, group_items) \
            -> tuple[int, int]:
        """Returns (rows loaded, GlassRef values lost to normalization)."""
        loaded = 0
        refs_lost = 0
        for group in (snapshot or {}).get("SpecGroups") or []:
            group_number = int(group.get("GroupNumber") or 0)
            glass_type = (group.get("SelectedGlassType") or "").strip()
            spec_name = (group.get("SelectedSpecName") or "").strip()
            if glass_type not in GLASS_TYPES:
                self.finding("error",
                             f"Proforma {number} group {group_number}"
                             f" glass type '{glass_type}' is outside the"
                             " SaaS CHECK vocabulary — group skipped"
                             " (proforma + snapshot kept)",
                             f"ProformaInvoices.Id={row['Id']}")
                continue
            base_rate = money(group.get("BaseRate", 0))
            rate = surcharge_rate(base_rate,
                                  money(group.get("SurchargePercent", 0)))
            price_source = "manual" \
                if group.get("HasManualStep5BaseRateOverride") \
                else "formula"
            spec_id = specs.get((glass_type, spec_name)) or None

            group_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.spec_groups
                  (id, tenant_id, proforma_id, group_number, glass_type,
                   spec_name, spec_id, rate_per_sqm, price_source,
                   outer_glass_color, inner_glass_color, third_glass_color,
                   outer_sheet_width_mm, outer_sheet_height_mm,
                   inner_sheet_width_mm, inner_sheet_height_mm,
                   third_sheet_width_mm, third_sheet_height_mm,
                   outer_sheet_price_per_sqm, inner_sheet_price_per_sqm,
                   third_sheet_price_per_sqm,
                   step4_spacer_price_per_sqm, step4_interlayer_cost_per_sqm,
                   step4_cutting_cost_per_sqm, step4_tempering_cost_per_sqm,
                   u_insert_price_per_sqm, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s)
                """,
                (group_id, tenant_id, proforma_id, group_number,
                 glass_type, spec_name, spec_id, rate, price_source,
                 group.get("OuterGlassColor") or "",
                 group.get("InnerGlassColor") or "",
                 group.get("ThirdGlassColor") or "",
                 int(group.get("OuterSheetWidthMm") or 0),
                 int(group.get("OuterSheetHeightMm") or 0),
                 int(group.get("InnerSheetWidthMm") or 0),
                 int(group.get("InnerSheetHeightMm") or 0),
                 int(group.get("ThirdSheetWidthMm") or 0),
                 int(group.get("ThirdSheetHeightMm") or 0),
                 money(group.get("OuterSheetPricePerSqm", 0)),
                 money(group.get("InnerSheetPricePerSqm", 0)),
                 money(group.get("ThirdSheetPricePerSqm", 0)),
                 money(group.get("Step4SpacerPricePerSqm", 0) or 0),
                 money(group.get("Step4InterlayerCostPerSqm", 0) or 0),
                 money(group.get("Step4CuttingCostPerSqm", 0) or 0),
                 money(group.get("Step4TemperingCostPerSqm", 0) or 0),
                 money(group.get("UInsertPricePerSqm", 0)),
                 to_utc(row["CreatedAt"]), to_utc(row["UpdatedAt"])))
            if not ok:
                self.finding("error",
                             f"Proforma {number} group {group_number}"
                             f" rejected: {err}",
                             f"ProformaInvoices.Id={row['Id']}")
                continue
            groups_map[(number, group_number)] = group_id
            loaded += 1

            for item in group.get("ItemRows") or []:
                qty = int(item.get("Qty") or 0)
                if qty <= 0:
                    # ck_spec_group_items_qty (qty > 0); the SaaS API
                    # drops phantom rows on save — same rule here.
                    self.finding("info",
                                 f"Proforma {number} group {group_number}"
                                 f" row {item.get('SerialNumber')} qty"
                                 f" {qty} ≤ 0 — phantom row dropped (the"
                                 " SaaS save contract)",
                                 f"ProformaInvoices.Id={row['Id']}")
                    continue
                w1 = int(item.get("W1") or 0)
                h1 = int(item.get("H1") or 0)
                rate_i = money(item.get("Rate", 0))
                sqm1 = panel_sqm(w1, h1)
                item_id = uuid.uuid4()
                ok, err = insert_with_savepoint(
                    pg,
                    """
                    INSERT INTO saas.spec_group_items
                      (id, tenant_id, spec_group_id, row_serial, position,
                       width_mm, height_mm, qty, w1, h1, w2, h2, sqm,
                       total_sqm, unit_price, total, notes,
                       perimeter_factor, created_at, updated_at)
                    VALUES (%s, %s, %s, %s, '', %s, %s, %s, %s, %s, %s,
                            %s, %s, %s, %s, %s, '', %s, %s, %s)
                    """,
                    (item_id, tenant_id, group_id,
                     int(item.get("SerialNumber") or 0), w1, h1, qty,
                     w1, h1, int(item.get("W2") or 0),
                     int(item.get("H2") or 0), sqm1,
                     total_sqm(w1, h1, qty), rate_i,
                     row_value(sqm1, qty, rate_i),
                     int(item.get("PerimeterFactor") or 2),
                     to_utc(row["CreatedAt"]), to_utc(row["UpdatedAt"])))
                if ok:
                    group_items[(number, group_number,
                                 int(item.get("SerialNumber") or 0))] = item_id
                    loaded += 1
                    if (item.get("GlassRef") or "").strip():
                        refs_lost += 1
                else:
                    self.finding("error",
                                 f"Proforma {number} group {group_number}"
                                 f" row {item.get('SerialNumber')}"
                                 f" rejected: {err}",
                                 f"ProformaInvoices.Id={row['Id']}")
        return loaded, refs_lost

    # ── Step 6: other charges / wastages / customs ──────────────────
    def _load_step6(self, pg, tenant_id, proforma_id, number,
                    snapshot) -> int:
        loaded = 0
        charges = ((snapshot or {}).get("OtherCharges") or {})
        sort = 0
        for charge in charges.get("PredefinedCharges") or []:
            template_key = (charge.get("TemplateKey") or "").strip()
            template = CHARGE_TEMPLATES.get(template_key)
            charge_name = template[0] if template else template_key
            unit = template[1] if template else "Qty"
            metric_key = normalize_metric_key(
                charge.get("SelectedMetricKey") or "")
            if metric_key not in METRIC_KEYS:
                # The legacy single-source field carries the same
                # vocabulary (SQM1/LM1/…) — the WPF's own stored source.
                metric_key = normalize_metric_key(
                    charge.get("SelectedSourceKey") or "")
            if metric_key not in METRIC_KEYS:
                metric_key = template[2] if template else ""
            if metric_key not in METRIC_KEYS:
                # Unit-derived fallback (Lm → LM1 etc.) — the CHECK needs
                # a member; the derivation is reported.
                metric_key = {"Lm": "LM1", "Qty": "QTY",
                              "Sqm": "SQM1"}[unit]
                self.finding("info",
                             f"Proforma {number} charge '{charge_name}'"
                             " carries no metric key (a legacy snapshot or"
                             f" sub-type-driven row) — derived '{metric_key}'"
                             " from the unit",
                             f"proforma={number}")
            if not template:
                self.finding("warning",
                             f"Proforma {number} charge template"
                             f" '{template_key}' is not in the registry —"
                             " identity imported verbatim, unit defaults"
                             " to Qty", f"proforma={number}")
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.other_charges
                  (id, tenant_id, proforma_id, charge_name, metric_key,
                   checked_spec_group_numbers, rate, amount, is_active,
                   sort_order, template_key, description, unit,
                   metric_qty, polish_sub_type, custom_polish_description,
                   custom_polish_qty, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, '',
                        %s, %s, %s, %s, %s, now(), now())
                """,
                (uuid.uuid4(), tenant_id, proforma_id, charge_name,
                 metric_key,
                 json.dumps(charge.get("CheckedSpecGroupNumbers") or []),
                 money(charge.get("Rate", 0)),
                 money(charge.get("ResultAmount", 0) or 0),
                 bool(charge.get("IsActive")),
                 sort, template_key, unit,
                 money(charge.get("MetricQty", 0) or 0),
                 charge.get("PolishSubType") or "",
                 charge.get("CustomPolishDescription") or "",
                 money(charge.get("CustomPolishQty", 0))))
            if ok:
                loaded += 1
                sort += 1
            else:
                self.finding("error",
                             f"Proforma {number} charge '{charge_name}'"
                             f" rejected: {err}", f"proforma={number}")
        if charges.get("PredefinedCharges"):
            self.finding("info",
                         f"Proforma {number}: per-spec charge targets are"
                         " not persisted in the WPF snapshot (the legacy"
                         " SelectedSourceKey is) — checked_spec_group"
                         "_numbers imported as []",
                         f"proforma={number}")

        sort = 0
        for wastage in charges.get("Wastages") or []:
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.wastage_rows
                  (id, tenant_id, proforma_id, color_name,
                   optimization_percent, sheet_quantity,
                   formula_price_per_sqm, rate, result_amount, is_active,
                   total_sqm, is_manually_added, use_optimization_formula,
                   checked_source_keys, sort_order, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, now(), now())
                """,
                (uuid.uuid4(), tenant_id, proforma_id,
                 wastage.get("ColorName") or "",
                 money(wastage.get("OptimizationPercent", 0)),
                 int(wastage.get("SheetQuantity") or 0) or 1,
                 money(wastage.get("FormulaPricePerSqm", 0)),
                 money(wastage.get("Rate", 0)),
                 money(wastage.get("ResultAmount", 0) or 0),
                 bool(wastage.get("IsActive")),
                 money(wastage.get("TotalSqm", 0) or 0),
                 bool(wastage.get("IsManuallyAdded")),
                 bool(wastage.get("UseOptimizationFormula")),
                 json.dumps(wastage.get("CheckedSourceKeys") or []),
                 sort))
            if ok:
                loaded += 1
                sort += 1
            else:
                self.finding("error",
                             f"Proforma {number} wastage row"
                             f" '{wastage.get('ColorName')}' rejected:"
                             f" {err}", f"proforma={number}")

        sort = 0
        for custom in charges.get("CustomCharges") or []:
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.custom_charges
                  (id, tenant_id, proforma_id, charge_name, qty, unit,
                   rate, amount, is_active, sort_order, created_at,
                   updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, now(),
                        now())
                """,
                (uuid.uuid4(), tenant_id, proforma_id,
                 custom.get("ChargeName") or "",
                 money(custom.get("Qty", 0)), parse_unit(custom.get("Unit")),
                 money(custom.get("Rate", 0)),
                 # ChargeAmount: active ? Round(qty × rate) : 0
                 round2(money(custom.get("Qty", 0))
                        * money(custom.get("Rate", 0)))
                 if custom.get("IsActive") else Decimal("0"),
                 bool(custom.get("IsActive")), sort))
            if ok:
                loaded += 1
                sort += 1
            else:
                self.finding("error",
                             f"Proforma {number} custom charge"
                             f" '{custom.get('ChargeName')}' rejected:"
                             f" {err}", f"proforma={number}")
        return loaded


# ── module 6b: specifications.db SpecificationUsages (after proformas) ─

class SpecificationUsagesModule(Module):
    """WPF SpecificationUsage → saas.specification_usages.

    Split from the library module because the usage ledger FKs to
    proforma_invoices (NOT NULL) — it can only load after the proformas
    module (plan §2's dependency order, made explicit).
    """

    name = "specusages"
    source_db = "specifications.db"
    depends_on = ["specifications", "proformas"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        specs_int: dict = id_map.get("specs_by_int", {})
        specs_key: dict = id_map.get("specs_by_key", {})
        proformas: dict = id_map.get("proformas_by_no", {})
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, SpecificationId, SpecName, GlassType,"
                " ProformaId, CompanyName, CustomerName, ProjectName,"
                " PricePerSqm, SheetWidthMm, SheetHeightMm, UserName,"
                " UsedAt FROM SpecificationUsages ORDER BY Id").fetchall()
        extracted = len(rows)
        loaded = 0
        for row in rows:
            spec_id = specs_int.get(int(row["SpecificationId"]))
            if spec_id is None:
                spec_id = specs_key.get(
                    ((row["GlassType"] or "").strip(),
                     (row["SpecName"] or "").strip()))
            if spec_id is None:
                self.finding("error",
                             f"Usage row references specification"
                             f" {row['SpecificationId']}"
                             f" ('{row['SpecName']}') which was not"
                             " imported — skipped",
                             f"SpecificationUsages.Id={row['Id']}")
                continue
            proforma_id = proformas.get((row["ProformaId"] or "").strip())
            if proforma_id is None:
                self.finding("error",
                             f"Usage row references proforma"
                             f" '{row['ProformaId']}' which was not"
                             " imported — skipped (proforma_id is NOT"
                             " NULL)",
                             f"SpecificationUsages.Id={row['Id']}")
                continue
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.specification_usages
                  (id, tenant_id, spec_id, proforma_id, spec_name,
                   glass_type, company_name, customer_name, project_name,
                   price_per_sqm, sheet_width_mm, sheet_height_mm, used_at,
                   created_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s)
                """,
                (uuid.uuid4(), tenant_id, spec_id, proforma_id,
                 row["SpecName"] or "", row["GlassType"] or "",
                 row["CompanyName"] or "", row["CustomerName"] or "",
                 row["ProjectName"] or "", money(row["PricePerSqm"]),
                 int(row["SheetWidthMm"] or 0),
                 int(row["SheetHeightMm"] or 0), to_utc(row["UsedAt"]),
                 to_utc(row["UsedAt"])))
            if ok:
                loaded += 1
            else:
                _reject(self.finding, "Specification usage row", row["Id"],
                        "SpecificationUsages", err)
        return extracted, loaded


# ── module 7: optimization.db ────────────────────────────────────────

#: WPF SavedOptimizationSession reserved-row predicate (feed plumbing —
#: hidden from Saved Sessions and pruned after 180 days by the WPF
#: itself; they are cache, not records).
def is_reserved_latest_run(session_id: str) -> bool:
    return session_id == "__latest_run__" \
        or session_id.startswith("__latest_run__:")


class OptimizationModule(Module):
    """WPF SavedOptimizationSession → saas.optimization_sessions.

    Documented difference (plan §2 module 7 said "sessions re-run
    through both oracles"): the WPF persists only AGGREGATE results
    (ResultJson = sheets used, utilization, per-sheet piece counts) —
    placement geometry is not in optimization.db, so neither oracle can
    re-run. Sessions import as historical completed runs with the
    limitation recorded in validation_problems; conservation_verified/
    geometry_verified stay false (the "quarantined unless verified" rule
    — an imported session is never exported as oracle-passed).
    optimization_sheets/placements/cuts/leftovers stay empty for
    imported runs (their per-sheet detail is not recoverable).
    """

    name = "optimization"
    source_db = "optimization.db"
    depends_on = ["users"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, Name, JobOrderRefs, EngineStage, EffortLevel,"
                " RotationEnabled, PieceCount, SheetsUsed, UnplacedCount,"
                " UtilizationPct, MarginsJson, StocksJson, PiecesJson,"
                " ResultJson, CreatedUtc, UpdatedUtc"
                " FROM Sessions ORDER BY UpdatedUtc, Id").fetchall()
        extracted = len(rows)
        loaded = 0
        reserved = 0
        by_str_id: dict[str, uuid.UUID] = {}
        for row in rows:
            wpf_id = (row["Id"] or "").strip()
            if is_reserved_latest_run(wpf_id):
                reserved += 1
                continue
            params = {
                "importedFromWpf": True,
                "wpfSessionId": wpf_id,
                "engineStage": row["EngineStage"] or "QUICK",
                "effortLevel": int(row["EffortLevel"] or 0),
                "rotationEnabled": bool(row["RotationEnabled"]),
                "pieceCount": int(row["PieceCount"] or 0),
                "jobOrderRefs": row["JobOrderRefs"] or "",
                "name": row["Name"] or "",
            }
            for key, column in (("margins", "MarginsJson"),
                                ("stocks", "StocksJson"),
                                ("pieces", "PiecesJson"),
                                ("wpfResult", "ResultJson")):
                raw = (row[column] or "").strip()
                if not raw or raw == "{}":
                    continue
                try:
                    params[key] = json.loads(raw)
                except (ValueError, TypeError):
                    params[key + "Raw"] = raw

            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.optimization_sessions
                  (id, tenant_id, document_type, document_id,
                   composition_key, params_json, status, strategy,
                   sheets_used, unplaced_count, total_used_sqm,
                   total_stock_sqm, utilization_pct, conservation_verified,
                   geometry_verified, validation_problems, scale_notes,
                   sheets_lower_bound, started_at, completed_at,
                   created_at, updated_at)
                VALUES (%s, %s, 'job_order', NULL, '', %s, 'completed',
                        %s, %s, %s, 0, 0, %s, false, false, %s, '[]', 0,
                        %s, %s, %s, %s)
                """,
                (new_id, tenant_id, json.dumps(params),
                 row["EngineStage"] or "QUICK",
                 int(row["SheetsUsed"] or 0),
                 int(row["UnplacedCount"] or 0),
                 money(row["UtilizationPct"]),
                 json.dumps([
                     "imported historical WPF run — placement geometry is"
                     " not persisted in optimization.db (ResultJson is"
                     " aggregate-only), so the conservation and geometry"
                     " oracles cannot re-run; the WPF's own results"
                     " import as data (documented difference, plan §2"
                     " module 7)"]),
                 to_utc(row["CreatedUtc"], source_is_utc=True),
                 to_utc(row["UpdatedUtc"], source_is_utc=True),
                 to_utc(row["CreatedUtc"], source_is_utc=True),
                 to_utc(row["UpdatedUtc"], source_is_utc=True)))
            if ok:
                by_str_id[wpf_id] = new_id
                loaded += 1
                self.finding("info",
                             f"Optimization session '{row['Name'] or wpf_id}'"
                             " imported as a historical completed run —"
                             " the oracles cannot re-run (geometry not"
                             " persisted in the WPF source)",
                             f"Sessions.Id={wpf_id}")
            else:
                _reject(self.finding,
                        f"Optimization session '{row['Name']}'", wpf_id,
                        "Sessions", err)
        if reserved:
            self.finding("info",
                         f"{reserved} reserved latest-run row(s) skipped —"
                         " feed plumbing the WPF itself hides and prunes"
                         " (SavedOptimizationSession.IsReservedLatestRunId)",
                         "Sessions")
        id_map["optimization_sessions_by_str_id"] = by_str_id
        return extracted, loaded


# ── module 8a: joborders.db ──────────────────────────────────────────

class JobOrdersModule(Module):
    """WPF SavedJobOrder → saas.job_orders.

    proforma_id is NOT NULL in the SaaS (0012: every job order comes
    from a PI) — a job order whose source PI did not import is skipped
    with an error, never linked to a wrong proforma. The WPF's legacy
    "In Production" spelling collapses to "InProduction"
    (JobOrderStatusValues.IsInProduction accepts both). The
    ProductionDetails columns come from the snapshot's Production block
    (JobOrderSnapshot). The PI backlink (proforma_invoices.job_order_id)
    is written here, after both sides exist.
    """

    name = "joborders"
    source_db = "joborders.db"
    depends_on = ["users", "masterdata", "proformas"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        proformas: dict = id_map.get("proformas_by_no", {})
        clients: dict = id_map.get("clients_by_name", {})
        projects: dict = id_map.get("projects_by_name", {})
        users: dict = id_map.get("users", {})
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, JobOrderId, SourceProformaId, CustomerName,"
                " ProjectName, GrandTotalVatInclusive, Status, IsVoid,"
                " VoidedAtUtc, VoidedBy, VoidReason, Priority,"
                " DeliveryDate, FullSnapshotJson, CreatedAt, UpdatedAt,"
                " RowVersion FROM JobOrders ORDER BY Id").fetchall()
        extracted = len(rows)
        loaded = 0
        by_no: dict[str, uuid.UUID] = {}
        by_int: dict[int, uuid.UUID] = {}
        for row in rows:
            number = (row["JobOrderId"] or "").strip()
            if not number:
                self.finding("error",
                             "Job order without a JobOrderId — skipped",
                             f"JobOrders.Id={row['Id']}")
                continue
            raw_status = (row["Status"] or "").strip()
            status = raw_status.replace(" ", "") \
                if raw_status.casefold() == "in production" else raw_status
            if status not in JOB_ORDER_STATUSES:
                self.finding("error",
                             f"Job order {number} status '{raw_status}' is"
                             " outside the SaaS CHECK vocabulary —"
                             " skipped",
                             f"JobOrders.Id={row['Id']}")
                continue
            if status != raw_status:
                self.finding("info",
                             f"Job order {number} status '{raw_status}'"
                             " collapsed to 'InProduction' (the WPF's own"
                             " two-spelling equivalence,"
                             " JobOrderStatusValues.IsInProduction)",
                             f"JobOrders.Id={row['Id']}")
            proforma_id = proformas.get(
                (row["SourceProformaId"] or "").strip())
            if proforma_id is None:
                self.finding("error",
                             f"Job order {number} references source"
                             f" proforma '{row['SourceProformaId']}' which"
                             " was not imported — skipped (proforma_id is"
                             " NOT NULL: a job order cannot exist without"
                             " its PI)",
                             f"JobOrders.Id={row['Id']}")
                continue
            priority = (row["Priority"] or "").strip() or "Medium"
            if priority not in JOB_ORDER_PRIORITIES:
                self.finding("error",
                             f"Job order {number} priority '{priority}' is"
                             " outside the SaaS CHECK vocabulary —"
                             " skipped",
                             f"JobOrders.Id={row['Id']}")
                continue

            snapshot = None
            raw_snapshot = (row["FullSnapshotJson"] or "").strip()
            if raw_snapshot:
                try:
                    snapshot = json.loads(raw_snapshot)
                except (ValueError, TypeError):
                    self.finding("warning",
                                 f"Job order {number} FullSnapshotJson is"
                                 " not valid JSON — snapshot_json null,"
                                 " row imported",
                                 f"JobOrders.Id={row['Id']}")
            details = (snapshot or {}).get("Production") or {}

            customer_name = (row["CustomerName"] or "").strip()
            project_name = (row["ProjectName"] or "").strip()
            client_id = clients.get(customer_name) or None
            project_id = projects.get(project_name) or None
            if customer_name and client_id is None:
                self.finding("warning",
                             f"Job order {number} client '{customer_name}'"
                             " was not imported — customer_id null",
                             f"JobOrders.Id={row['Id']}")
            if project_name and project_id is None:
                self.finding("warning",
                             f"Job order {number} project '{project_name}'"
                             " was not imported — project_id null",
                             f"JobOrders.Id={row['Id']}")
            voided_by = users.get((row["VoidedBy"] or "").strip()) or None \
                if (row["VoidedBy"] or "").strip() else None

            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.job_orders
                  (id, tenant_id, job_no, proforma_id, status, customer_id,
                   project_id, due_date, notes, row_version, customer_name,
                   project_name, grand_total_vat_inclusive, priority,
                   site_delivery_contact, site_delivery_phone,
                   site_delivery_address, assigned_to,
                   cutting_instructions, drawing_reference,
                   internal_notes, is_void, voided_at, voided_by,
                   void_reason, snapshot_json, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, '', %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s)
                """,
                (new_id, tenant_id, number, proforma_id, status,
                 client_id, project_id, to_date(row["DeliveryDate"]),
                 int(row["RowVersion"] or 0), customer_name, project_name,
                 money(row["GrandTotalVatInclusive"]), priority,
                 details.get("SiteDeliveryContact") or "",
                 details.get("SiteDeliveryPhone") or "",
                 details.get("SiteDeliveryAddress") or "",
                 details.get("AssignedTo") or "",
                 details.get("CuttingInstructions") or "",
                 details.get("DrawingReference") or "",
                 details.get("InternalNotes") or "",
                 bool(row["IsVoid"]),
                 to_utc(row["VoidedAtUtc"], source_is_utc=True),
                 voided_by, row["VoidReason"] or "",
                 raw_snapshot or None,
                 to_utc(row["CreatedAt"]), to_utc(row["UpdatedAt"])))
            if not ok:
                _reject(self.finding, f"Job order {number}", row["Id"],
                        "JobOrders", err)
                continue
            by_no[number] = new_id
            by_int[int(row["Id"])] = new_id
            loaded += 1
            # PI backlink: the SaaS stamps invoice.JobOrderId at
            # conversion; migration writes it once both rows exist.
            ok2, _ = insert_with_savepoint(
                pg,
                "UPDATE saas.proforma_invoices SET job_order_id = %s"
                " WHERE id = %s AND tenant_id = %s",
                (new_id, proforma_id, tenant_id))
            if not ok2:
                self.finding("warning",
                             f"Proforma {row['SourceProformaId']} job"
                             f" order backlink failed for {number}",
                             f"JobOrders.Id={row['Id']}")
        id_map["job_orders_by_no"] = by_no
        id_map["job_orders_by_int"] = by_int
        return extracted, loaded


# ── module 8b: production.db ─────────────────────────────────────────

class ProductionModule(Module):
    """WPF production.db → the four shapes the SaaS materializes
    (mapping §1.7b): ProductionWorkItems → production_pieces,
    ProductionEntries → production_events (the 'Stage' entry type),
    MaterialReservations → material_reservations.

    ProductionReleases are NOT imported: the SaaS models the release as
    the job_orders row itself (§1.7b, documented). The remaining tables
    (allocations, QC entries, actual costs, replacements, cutting
    sessions/sheets/pieces, process executions/pairings/assemblies,
    workstations, control holds/assignments/overrides) are the §1.7b
    deferred schema — counted and reported here, never silently
    dropped.
    """

    name = "production"
    source_db = "production.db"
    depends_on = ["users", "sheetstore", "proformas", "optimization",
                  "joborders"]

    #: source tables with no SaaS materialization (§1.7b deferred list)
    DEFERRED_TABLES = {
        "ProductionReleases": "the release IS the job_orders row",
        "MaterialAllocations": "allocation ledger — optimization slice",
        "ProductionQcEntries": "QC lives as typed events in the SaaS",
        "ProductionActualCosts": "actual costs — later slice",
        "ProductionReplacements": "replacement/FOC re-cut workflow — later slice",
        "CuttingSessions": "cutting session persistence — later slice",
        "CuttingSheets": "cutting session persistence — later slice",
        "CuttingPlacedPieces": "cutting session persistence — later slice",
        "ProcessExecutions": "process tracking — later slice",
        "ProcessPairings": "process tracking — later slice",
        "ProcessAssemblies": "process tracking — later slice",
        "Workstations": "workstation registry — later slice",
        "ProductionControlHolds": "production control — later slice",
        "ProductionControlAssignments": "production control — later slice",
        "ProductionControlOverrides": "production control — later slice",
    }

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        users: dict = id_map.get("users", {})
        sheets: dict = id_map.get("glass_sheets_by_int", {})
        job_orders: dict = id_map.get("job_orders_by_no", {})
        group_items: dict = id_map.get("spec_group_items_by_row", {})
        opt_sessions: dict = id_map.get(
            "optimization_sessions_by_str_id", {})

        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            work_items = src.execute(
                "SELECT Id, UniqueKey, JobRef, SourceProformaId,"
                " CustomerName, ProjectName, Salesman, SpecGroupNumber,"
                " SpecName, BuildType, SerialNumber, GlassRef, Position,"
                " WidthMm, HeightMm, Qty, QtyDone, UnitSqm, TotalSqm,"
                " TotalLm, Treatment, Stage, Status, PredecessorStage,"
                " ChargeTemplateKey, ChargeMetricQty, ChargeMetricUnit,"
                " PolishSubTypeLabel, Notes, CreatedAt, UpdatedAt,"
                " CompletedAt, CompletedBy, IsFoc"
                " FROM ProductionWorkItems ORDER BY Id").fetchall()
            entries = src.execute(
                "SELECT Id, WorkItemId, JobRef, Stage, SpecGroupNumber,"
                " SerialNumber, Position, SpecName, GlassRef, Qty,"
                " DoneBy, DoneAt FROM ProductionEntries"
                " ORDER BY DoneAt, Id").fetchall()
            reservations = src.execute(
                "SELECT Id, ReservationId, JobRef, SourceProformaId,"
                " SpecGroupNumber, SerialNumber, GlassRef, SpecName,"
                " OptimizationSessionId, GlassSheetId, StockSizeLabel,"
                " WidthMm, HeightMm, ThicknessMm, Color, Quantity,"
                " Status, CreatedBy, CreatedAt, ReleasedAt,"
                " ReleaseReason, Notes FROM MaterialReservations"
                " ORDER BY Id").fetchall()

            deferred_counts = {}
            for table in self.DEFERRED_TABLES:
                try:
                    deferred_counts[table] = src.execute(
                        f"SELECT count(*) FROM {table}").fetchone()[0]
                except sqlite3.OperationalError:
                    deferred_counts[table] = None   # table absent

        extracted = len(work_items) + len(entries) + len(reservations)
        loaded = 0
        pieces_by_int: dict[int, uuid.UUID] = {}

        for item in work_items:
            job_ref = (item["JobRef"] or "").strip()
            job_order_id = job_orders.get(job_ref)
            if job_order_id is None:
                self.finding("error",
                             f"Work item '{item['UniqueKey']}' references"
                             f" job order '{job_ref}' which was not"
                             " imported — skipped (job_order_id is NOT"
                             " NULL)",
                             f"ProductionWorkItems.Id={item['Id']}")
                continue
            stage = PRODUCTION_STAGES.get(int(item["Stage"] or 0))
            if stage is None:
                self.finding("error",
                             f"Work item '{item['UniqueKey']}' stage"
                             f" {item['Stage']} is outside the WPF"
                             " ProductionStage enum — skipped",
                             f"ProductionWorkItems.Id={item['Id']}")
                continue
            status = WORK_ITEM_STATUSES.get(int(item["Status"] or 0))
            if status is None:
                self.finding("error",
                             f"Work item '{item['UniqueKey']}' status"
                             f" {item['Status']} is outside the WPF"
                             " WorkItemStatus enum — skipped",
                             f"ProductionWorkItems.Id={item['Id']}")
                continue
            spec_group_item_id = group_items.get(
                ((item["SourceProformaId"] or "").strip(),
                 int(item["SpecGroupNumber"] or 0),
                 int(item["SerialNumber"] or 0)))
            if (item["SourceProformaId"] or "").strip() \
                    and spec_group_item_id is None:
                self.finding("info",
                             f"Work item '{item['UniqueKey']}' source row"
                             f" ({item['SourceProformaId']} group"
                             f" {item['SpecGroupNumber']} serial"
                             f" {item['SerialNumber']}) has no imported"
                             " spec_group_item — link null",
                             f"ProductionWorkItems.Id={item['Id']}")
            completed_by = users.get(
                (item["CompletedBy"] or "").strip()) or None \
                if (item["CompletedBy"] or "").strip() else None

            piece_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.production_pieces
                  (id, tenant_id, job_order_id, piece_label, stage, status,
                   width_mm, height_mm, qty, notes, unique_key,
                   spec_group_number, serial_number, position, build_type,
                   spec_name, glass_ref, unit_sqm, total_sqm, total_lm,
                   treatment, predecessor_stage, qty_done,
                   charge_template_key, charge_metric_qty,
                   charge_metric_unit, polish_sub_type_label, is_foc,
                   is_cancelled, cancelled_at, cancel_reason,
                   spec_group_item_id, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, NULL, '', %s, %s, %s)
                """,
                (piece_id, tenant_id, job_order_id,
                 item["GlassRef"] or "", stage, status,
                 int(item["WidthMm"] or 0), int(item["HeightMm"] or 0),
                 int(item["Qty"] or 0), item["Notes"] or "",
                 item["UniqueKey"] or "",
                 int(item["SpecGroupNumber"] or 0),
                 int(item["SerialNumber"] or 0), item["Position"] or "",
                 item["BuildType"] or "", item["SpecName"] or "",
                 item["GlassRef"] or "", money(item["UnitSqm"]),
                 money(item["TotalSqm"]), money(item["TotalLm"]),
                 item["Treatment"] or "", item["PredecessorStage"] or "",
                 int(item["QtyDone"] or 0),
                 item["ChargeTemplateKey"] or "",
                 money(item["ChargeMetricQty"]),
                 item["ChargeMetricUnit"] or "",
                 item["PolishSubTypeLabel"] or "", bool(item["IsFoc"]),
                 status == "Cancelled", spec_group_item_id,
                 to_utc(item["CreatedAt"]), to_utc(item["UpdatedAt"])))
            if ok:
                pieces_by_int[int(item["Id"])] = piece_id
                loaded += 1
            else:
                _reject(self.finding,
                        f"Work item '{item['UniqueKey']}'", item["Id"],
                        "ProductionWorkItems", err)

        for entry in entries:
            piece_id = pieces_by_int.get(int(entry["WorkItemId"]))
            if piece_id is None:
                self.finding("error",
                             f"Production entry references work item"
                             f" {entry['WorkItemId']} which was not"
                             " imported — skipped",
                             f"ProductionEntries.Id={entry['Id']}")
                continue
            stage = PRODUCTION_STAGES.get(int(entry["Stage"] or 0))
            if stage is None:
                self.finding("error",
                             f"Production entry (work item"
                             f" {entry['WorkItemId']}) stage"
                             f" {entry['Stage']} is outside the WPF"
                             " ProductionStage enum — skipped",
                             f"ProductionEntries.Id={entry['Id']}")
                continue
            done_by = users.get((entry["DoneBy"] or "").strip()) or None \
                if (entry["DoneBy"] or "").strip() else None
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.production_events
                  (id, tenant_id, production_piece_id, event_type,
                   payload, job_order_id, stage, spec_group_number,
                   serial_number, position, spec_name, glass_ref, qty,
                   done_by, reason, created_at)
                VALUES (%s, %s, %s, 'Stage', NULL, NULL, %s, %s, %s, %s,
                        %s, %s, %s, %s, '', %s)
                """,
                (uuid.uuid4(), tenant_id, piece_id, stage,
                 int(entry["SpecGroupNumber"] or 0),
                 int(entry["SerialNumber"] or 0), entry["Position"] or "",
                 entry["SpecName"] or "", entry["GlassRef"] or "",
                 int(entry["Qty"] or 0), done_by,
                 to_utc(entry["DoneAt"])))
            if ok:
                loaded += 1
            else:
                _reject(self.finding, "Production entry", entry["Id"],
                        "ProductionEntries", err)

        for res in reservations:
            job_ref = (res["JobRef"] or "").strip()
            job_order_id = job_orders.get(job_ref)
            if job_order_id is None:
                self.finding("error",
                             f"Reservation '{res['ReservationId']}'"
                             f" references job order '{job_ref}' which"
                             " was not imported — skipped (job_order_id"
                             " is NOT NULL)",
                             f"MaterialReservations.Id={res['Id']}")
                continue
            glass_sheet_id = sheets.get(int(res["GlassSheetId"]))
            if glass_sheet_id is None:
                self.finding("error",
                             f"Reservation '{res['ReservationId']}'"
                             f" references sheet {res['GlassSheetId']}"
                             " which was not imported — skipped"
                             " (glass_sheet_id is NOT NULL)",
                             f"MaterialReservations.Id={res['Id']}")
                continue
            status = RESERVATION_STATUSES.get(int(res["Status"] or 0))
            if status is None:
                self.finding("error",
                             f"Reservation '{res['ReservationId']}'"
                             f" status {res['Status']} is outside the"
                             " WPF ReservationStatus enum — skipped",
                             f"MaterialReservations.Id={res['Id']}")
                continue
            reserved_by = users.get((res["CreatedBy"] or "").strip()) or None \
                if (res["CreatedBy"] or "").strip() else None
            opt_session_id = opt_sessions.get(
                (res["OptimizationSessionId"] or "").strip()) or None \
                if (res["OptimizationSessionId"] or "").strip() else None
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.material_reservations
                  (id, tenant_id, job_order_id, glass_sheet_id,
                   reserved_qty, status, reserved_by, reservation_key,
                   spec_group_number, serial_number, glass_ref, spec_name,
                   optimization_session_id, stock_size_label, width_mm,
                   height_mm, thickness_mm, color, notes, released_at,
                   release_reason, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (uuid.uuid4(), tenant_id, job_order_id, glass_sheet_id,
                 int(res["Quantity"] or 0), status, reserved_by,
                 res["ReservationId"] or "",
                 int(res["SpecGroupNumber"] or 0),
                 int(res["SerialNumber"] or 0), res["GlassRef"] or "",
                 res["SpecName"] or "", opt_session_id,
                 res["StockSizeLabel"] or "",
                 int(res["WidthMm"] or 0), int(res["HeightMm"] or 0),
                 int(res["ThicknessMm"] or 0), res["Color"] or "",
                 res["Notes"] or "", to_utc(res["ReleasedAt"]),
                 res["ReleaseReason"] or "", to_utc(res["CreatedAt"]),
                 to_utc(res["CreatedAt"])))
            if ok:
                loaded += 1
            else:
                _reject(self.finding,
                        f"Reservation '{res['ReservationId']}'", res["Id"],
                        "MaterialReservations", err)

        for table, reason in self.DEFERRED_TABLES.items():
            count = deferred_counts.get(table)
            if count:
                self.finding("info",
                             f"{count} {table} row(s) not imported —"
                             f" {reason} (mapping §1.7b deferred list;"
                             " counted, never silent)", table)
            elif count is None:
                self.finding("info",
                             f"{table} absent from the source data root —"
                             f" {reason}", table)
        return extracted, loaded


# ── module 9: deliverynotes.db ───────────────────────────────────────

class DeliveryNotesModule(Module):
    """WPF SavedDeliveryNote + DeliveryNoteItems → saas.delivery_notes +
    delivery_note_items.

    job_order_id is NOT NULL on delivery_notes (0013) — a note whose
    WPF job did not import is skipped with an error. sqm is the total
    panel SQM (SaaS BuildLinesFromGrid uses row.TotalSqm). DN items
    link back to the PI's spec_group_item when the (proforma, group,
    serial) triple resolves. Attachments are the deferred object-storage
    module — counted and reported.
    """

    name = "deliverynotes"
    source_db = "deliverynotes.db"
    depends_on = ["users", "masterdata", "proformas", "joborders"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        job_orders: dict = id_map.get("job_orders_by_no", {})
        clients: dict = id_map.get("clients_by_name", {})
        projects: dict = id_map.get("projects_by_name", {})
        users: dict = id_map.get("users", {})
        group_items: dict = id_map.get("spec_group_items_by_row", {})

        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            notes = src.execute(
                "SELECT Id, DeliveryNoteId, JobOrderId, ProformaId,"
                " SequenceNumber, CustomerName, ProjectName, Status,"
                " IsVoid, VoidedAtUtc, VoidedBy, VoidReason, DeliveryType,"
                " IsFoc, IsMasterDn, LastUpdatedFromDnId, DriverName,"
                " VehicleNumber, ReceiverName, DeliveryDate, DeliveryTime,"
                " Notes, TotalOrderedQty, TotalDeliveredQty,"
                " TotalBalanceQty, TotalPreviouslyDeliveredQty,"
                " TaxInvoiceId, HasTaxInvoice, FullSnapshotJson,"
                " CreatedAt, UpdatedAt, RowVersion"
                " FROM DeliveryNotes ORDER BY Id").fetchall()
            items = src.execute(
                "SELECT Id, DeliveryNoteId, JobOrderId, SpecGroupNumber,"
                " SpecGroupName, SerialNumber, GlassRef, W1, H1, W2, H2,"
                " OrderedQty, PreviouslyDeliveredQty, DeliveredQty,"
                " BalanceQty, ReworkQty, ReturnedQty, WeightPerPiece,"
                " Notes FROM DeliveryNoteItems"
                " ORDER BY DeliveryNoteId, SpecGroupNumber, SerialNumber"
                ).fetchall()
            att_count = src.execute(
                "SELECT count(*) FROM DeliveryNoteAttachments").fetchone()[0]

        by_note: dict[str, tuple] = {}   # WPF DeliveryNoteId → (uuid, PI)
        for note in notes:
            by_note[(note["DeliveryNoteId"] or "").strip()] = note

        extracted = len(notes) + len(items)
        loaded = 0
        note_ids: dict[str, uuid.UUID] = {}
        skipped_note_items: dict[str, int] = {}

        for note in notes:
            number = (note["DeliveryNoteId"] or "").strip()
            if not number:
                self.finding("error",
                             "Delivery note without a DeliveryNoteId —"
                             " skipped", f"DeliveryNotes.Id={note['Id']}")
                continue
            status = (note["Status"] or "").strip()
            if status not in DELIVERY_NOTE_STATUSES:
                self.finding("error",
                             f"Delivery note {number} status '{status}'"
                             " is outside the SaaS CHECK vocabulary —"
                             " skipped",
                             f"DeliveryNotes.Id={note['Id']}")
                continue
            delivery_type = (note["DeliveryType"] or "").strip() \
                or "Original"
            if delivery_type not in DELIVERY_TYPES:
                self.finding("error",
                             f"Delivery note {number} delivery type"
                             f" '{delivery_type}' is outside the SaaS"
                             " CHECK vocabulary — skipped",
                             f"DeliveryNotes.Id={note['Id']}")
                continue
            job_ref = (note["JobOrderId"] or "").strip()
            job_order_id = job_orders.get(job_ref)
            if job_order_id is None:
                self.finding("error",
                             f"Delivery note {number} references job"
                             f" order '{job_ref}' which was not imported"
                             " — skipped (job_order_id is NOT NULL)",
                             f"DeliveryNotes.Id={note['Id']}")
                continue
            customer_name = (note["CustomerName"] or "").strip()
            project_name = (note["ProjectName"] or "").strip()
            client_id = clients.get(customer_name) or None
            project_id = projects.get(project_name) or None
            if customer_name and client_id is None:
                self.finding("warning",
                             f"Delivery note {number} client"
                             f" '{customer_name}' was not imported —"
                             " customer_id null",
                             f"DeliveryNotes.Id={note['Id']}")
            if project_name and project_id is None:
                self.finding("warning",
                             f"Delivery note {number} project"
                             f" '{project_name}' was not imported —"
                             " project_id null",
                             f"DeliveryNotes.Id={note['Id']}")
            voided_by = users.get((note["VoidedBy"] or "").strip()) or None \
                if (note["VoidedBy"] or "").strip() else None

            snapshot_json = None
            raw_snapshot = (note["FullSnapshotJson"] or "").strip()
            if raw_snapshot:
                try:
                    json.loads(raw_snapshot)
                    snapshot_json = raw_snapshot
                except (ValueError, TypeError):
                    self.finding("warning",
                                 f"Delivery note {number}"
                                 " FullSnapshotJson is not valid JSON —"
                                 " snapshot_json null, note imported",
                                 f"DeliveryNotes.Id={note['Id']}")

            note_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.delivery_notes
                  (id, tenant_id, delivery_no, job_order_id, status,
                   customer_id, project_id, delivery_date, driver,
                   vehicle, notes, row_version, sequence_number,
                   customer_name, project_name, delivery_type, is_foc,
                   is_master_dn, receiver_name, delivery_time,
                   total_ordered_qty, total_delivered_qty,
                   total_balance_qty, is_void, voided_at, voided_by,
                   void_reason, snapshot_json, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s)
                """,
                (note_id, tenant_id, number, job_order_id, status,
                 client_id, project_id, to_date(note["DeliveryDate"]),
                 note["DriverName"] or "", note["VehicleNumber"] or "",
                 note["Notes"] or "", int(note["RowVersion"] or 0),
                 int(note["SequenceNumber"] or 1), customer_name,
                 project_name, delivery_type, bool(note["IsFoc"]),
                 bool(note["IsMasterDn"]), note["ReceiverName"] or "",
                 note["DeliveryTime"] or "",
                 int(note["TotalOrderedQty"] or 0),
                 int(note["TotalDeliveredQty"] or 0),
                 int(note["TotalBalanceQty"] or 0), bool(note["IsVoid"]),
                 to_utc(note["VoidedAtUtc"], source_is_utc=True),
                 voided_by, note["VoidReason"] or "", snapshot_json,
                 to_utc(note["CreatedAt"]), to_utc(note["UpdatedAt"])))
            if not ok:
                _reject(self.finding, f"Delivery note {number}",
                        note["Id"], "DeliveryNotes", err)
                continue
            note_ids[number] = note_id
            loaded += 1

        for item in items:
            note = by_note.get((item["DeliveryNoteId"] or "").strip())
            if note is None:
                self.finding("error",
                             f"Delivery item references note"
                             f" '{item['DeliveryNoteId']}' which was not"
                             " imported — skipped",
                             f"DeliveryNoteItems.Id={item['Id']}")
                continue
            number = (note["DeliveryNoteId"] or "").strip()
            note_id = note_ids.get(number)
            if note_id is None:
                # The note was skipped (its own error finding exists) —
                # its items cannot load either; counted, never silent.
                skipped_note_items.setdefault(number, 0)
                skipped_note_items[number] += 1
                continue
            job_ref = (item["JobOrderId"] or "").strip() \
                or (note["JobOrderId"] or "").strip()
            job_order_id = job_orders.get(job_ref)
            if job_order_id is None:
                self.finding("error",
                             f"Delivery item (note {number}, serial"
                             f" {item['SerialNumber']}) references job"
                             f" order '{job_ref}' which was not imported"
                             " — skipped (job_order_id is NOT NULL)",
                             f"DeliveryNoteItems.Id={item['Id']}")
                continue
            spec_group_item_id = group_items.get(
                ((note["ProformaId"] or "").strip(),
                 int(item["SpecGroupNumber"] or 0),
                 int(item["SerialNumber"] or 0)))
            if (note["ProformaId"] or "").strip() \
                    and spec_group_item_id is None:
                self.finding("info",
                             f"Delivery item (note {number}, serial"
                             f" {item['SerialNumber']}) source row"
                             f" ({note['ProformaId']} group"
                             f" {item['SpecGroupNumber']}) has no"
                             " imported spec_group_item — link null",
                             f"DeliveryNoteItems.Id={item['Id']}")
            ordered = int(item["OrderedQty"] or 0)
            w1, h1 = int(item["W1"] or 0), int(item["H1"] or 0)
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.delivery_note_items
                  (id, tenant_id, delivery_note_id, spec_group_item_id,
                   description, width_mm, height_mm, qty, sqm,
                   delivered_qty, returned_qty, spec_group_number,
                   serial_number, spec_group_name, glass_ref, w2_mm,
                   h2_mm, ordered_qty, previously_delivered_qty,
                   balance_qty, rework_qty, weight_per_piece, line_notes,
                   job_order_id, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        now(), now())
                """,
                (uuid.uuid4(), tenant_id, note_id, spec_group_item_id,
                 item["SpecGroupName"] or "", w1, h1, ordered,
                 total_sqm(w1, h1, ordered),
                 int(item["DeliveredQty"] or 0),
                 int(item["ReturnedQty"] or 0),
                 int(item["SpecGroupNumber"] or 0),
                 int(item["SerialNumber"] or 0),
                 item["SpecGroupName"] or "", item["GlassRef"] or "",
                 int(item["W2"] or 0), int(item["H2"] or 0), ordered,
                 int(item["PreviouslyDeliveredQty"] or 0),
                 int(item["BalanceQty"] or 0),
                 int(item["ReworkQty"] or 0),
                 money(item["WeightPerPiece"]), item["Notes"] or "",
                 job_order_id))
            if ok:
                loaded += 1
            else:
                _reject(self.finding,
                        f"Delivery item (note {number}, serial"
                        f" {item['SerialNumber']})", item["Id"],
                        "DeliveryNoteItems", err)

        for number, count in sorted(skipped_note_items.items()):
            self.finding("info",
                         f"{count} delivery item(s) under skipped note"
                         f" {number} not imported (the note itself was"
                         " reported above)", f"DeliveryNoteItems→{number}")
        if att_count:
            self.finding("info",
                         f"{att_count} delivery-note attachment row(s) —"
                         " the attachments module is deferred (object"
                         " storage)", "DeliveryNoteAttachments")
        return extracted, loaded


# ── modules 10a/10b: tax invoices + credit notes ─────────────────────

class TaxInvoiceModule(Module):
    name = "taxinvoices"
    source_db = "taxinvoices.db"
    depends_on = ["users", "proformas", "joborders"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        proformas: dict = id_map.get("proformas_by_no", {})
        job_orders: dict = id_map.get("job_orders_by_no", {})
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, InvoiceNumber, ProformaId, JobOrderId,"
                " DeliveryNoteReference, PurchaseOrderNumber,"
                " InvoiceType, SequenceLetter, SequenceNumber, Status,"
                " IsVoid, VoidedAtUtc, CustomerName, ProjectName,"
                " CompanyName, CompanyTrn, IssueDate, DueDate,"
                " PaymentTerms, PaymentTermsDays, Subtotal,"
                " DiscountAmount, DiscountPercent, AfterDiscount,"
                " VatAmount, GrandTotal, TotalQty, TotalSqm,"
                " BankName, BankAccountNumber, Iban, SwiftCode,"
                " FullSnapshotJson, CreatedAt, UpdatedAt"
                " FROM TaxInvoices ORDER BY Id").fetchall()
        extracted = len(rows)
        loaded = 0
        invoice_ids: dict[str, uuid.UUID] = {}
        for row in rows:
            number = (row["InvoiceNumber"] or "").strip()
            if not number:
                self.finding("error", "Tax invoice without a number — skipped",
                             f"TaxInvoices.Id={row['Id']}")
                continue
            status = (row["Status"] or "").strip()
            if status not in TAX_INVOICE_STATUSES:
                self.finding("error",
                             f"Status '{status}' is outside the SaaS CHECK"
                             " vocabulary — invoice skipped (the Phase 2"
                             " plan predates the 0016 CHECK; unknown"
                             " statuses are reported, never remapped)",
                             f"TaxInvoices.Id={row['Id']}")
                continue

            # Phase 16: the WPF string references now resolve against the
            # imported documents (plan §2 order puts fiscal docs last);
            # unresolved names keep the link null and are reported.
            proforma_id = proformas.get((row["ProformaId"] or "").strip()) \
                if (row["ProformaId"] or "").strip() else None
            if (row["ProformaId"] or "").strip() and proforma_id is None:
                self.finding("warning",
                             f"Invoice {number} references WPF proforma"
                             f" '{row['ProformaId']}' which was not imported"
                             " — proforma_id null",
                             f"TaxInvoices.Id={row['Id']}")
            job_order_id = job_orders.get((row["JobOrderId"] or "").strip()) \
                if (row["JobOrderId"] or "").strip() else None
            if (row["JobOrderId"] or "").strip() and job_order_id is None:
                self.finding("warning",
                             f"Invoice {number} references WPF job order"
                             f" '{row['JobOrderId']}' which was not imported"
                             " — job_order_id null",
                             f"TaxInvoices.Id={row['Id']}")

            # Plan §3: totals re-verified by recomputation (a fiscal
            # record's stored values import verbatim; mismatches are
            # reported, never "fixed").
            subtotal = money(row["Subtotal"])
            discount = money(row["DiscountAmount"])
            vat = money(row["VatAmount"])
            grand = money(row["GrandTotal"])
            expected_vat = round2((subtotal - discount) * VAT_RATE)
            if expected_vat != vat:
                self.finding("warning",
                             f"Invoice {number}: stored VAT {vat} differs"
                             f" from recomputed {expected_vat} — stored"
                             " values imported", f"TaxInvoices.Id={row['Id']}")
            if subtotal - discount + vat != grand:
                self.finding("warning",
                             f"Invoice {number}: grand total {grand} does"
                             " not equal subtotal − discount + VAT"
                             f" ({subtotal - discount + vat}) — stored"
                             " values imported", f"TaxInvoices.Id={row['Id']}")

            new_id = uuid.uuid4()
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.tax_invoices
                  (id, tenant_id, invoice_no, proforma_id, job_order_id,
                   delivery_note_reference, purchase_order_number,
                   status, issue_date, due_date,
                   subtotal, discount_amount, vat_amount, grand_total,
                   paid_amount, balance_amount, snapshot_json,
                   customer_name, project_name, payment_terms,
                   discount_percent, is_void, voided_at, void_reason,
                   company_name, company_trn, bank_name,
                   bank_account_number, iban, swift_code,
                   created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        %s, %s, 0, %s, %s, %s, %s, %s, %s, %s, %s, '',
                        %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (new_id, tenant_id, number, proforma_id, job_order_id,
                 row["DeliveryNoteReference"] or "",
                 row["PurchaseOrderNumber"] or "",
                 status,
                 to_date(row["IssueDate"]), to_date(row["DueDate"]),
                 subtotal, discount, vat, grand, grand,
                 row["FullSnapshotJson"] or None,
                 row["CustomerName"] or "", row["ProjectName"] or "",
                 row["PaymentTerms"] or "Net 30",
                 money(row["DiscountPercent"]), bool(row["IsVoid"]),
                 to_utc(row["VoidedAtUtc"], source_is_utc=True),
                 row["CompanyName"] or "", row["CompanyTrn"] or "",
                 row["BankName"] or "", row["BankAccountNumber"] or "",
                 row["Iban"] or "", row["SwiftCode"] or "",
                 to_utc(row["CreatedAt"]), to_utc(row["UpdatedAt"])))
            if ok:
                invoice_ids[number] = new_id
                loaded += 1
            else:
                _reject(self.finding, f"Invoice {number}", row["Id"],
                        "TaxInvoices", err)
        id_map["tax_invoices"] = invoice_ids
        return extracted, loaded


class CreditNoteModule(Module):
    name = "creditnotes"
    source_db = "creditnotes.db"
    depends_on = ["taxinvoices"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        invoice_ids: dict = id_map.get("tax_invoices", {})
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, CreditNoteNumber, TaxInvoiceReference,"
                " ProformaId, JobOrderId, PurchaseOrderNumber,"
                " SequenceNumber, Reason, Status, IsVoid, VoidedAtUtc,"
                " CustomerName, ProjectName, CompanyName, CompanyTrn,"
                " IssueDate, Subtotal, DiscountAmount, AfterDiscount,"
                " VatAmount, GrandTotal, TotalQty, TotalSqm,"
                " FullSnapshotJson, CreatedAt, UpdatedAt"
                " FROM CreditNotes ORDER BY Id").fetchall()
        extracted = len(rows)
        loaded = 0
        for row in rows:
            number = (row["CreditNoteNumber"] or "").strip()
            if not number:
                self.finding("error",
                             "Credit note without a number — skipped",
                             f"CreditNotes.Id={row['Id']}")
                continue
            status = (row["Status"] or "").strip()
            if status not in CREDIT_NOTE_STATUSES:
                self.finding("error",
                             f"Status '{status}' is outside the SaaS CHECK"
                             " vocabulary — credit note skipped",
                             f"CreditNotes.Id={row['Id']}")
                continue
            # A credit note never exists on its own (WPF contract): the
            # corrected invoice must have imported.
            reference = (row["TaxInvoiceReference"] or "").strip()
            invoice_id = invoice_ids.get(reference)
            if reference and invoice_id is None:
                self.finding("warning",
                             f"Credit note {number} references invoice"
                             f" '{reference}' which was not imported —"
                             " tax_invoice_id null",
                             f"CreditNotes.Id={row['Id']}")

            subtotal = money(row["Subtotal"])
            discount = money(row["DiscountAmount"])
            vat = money(row["VatAmount"])
            grand = money(row["GrandTotal"])
            expected_vat = round2((subtotal - discount) * VAT_RATE)
            if expected_vat != vat:
                self.finding("warning",
                             f"Credit note {number}: stored VAT {vat}"
                             f" differs from recomputed {expected_vat} —"
                             " stored values imported",
                             f"CreditNotes.Id={row['Id']}")

            if (row["FullSnapshotJson"] or "").strip():
                self.finding("info",
                             f"Credit note {number}: the WPF snapshot JSON"
                             " cannot be preserved (saas.credit_notes has"
                             " no snapshot column) — kept in the source",
                             f"CreditNotes.Id={row['Id']}")
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.credit_notes
                  (id, tenant_id, credit_note_no, tax_invoice_id, reason,
                   subtotal, vat_amount, grand_total, status, voided_at,
                   created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (uuid.uuid4(), tenant_id, number, invoice_id,
                 row["Reason"] or "", subtotal, vat, grand, status,
                 to_utc(row["VoidedAtUtc"], source_is_utc=True),
                 to_utc(row["CreatedAt"]), to_utc(row["UpdatedAt"])))
            if ok:
                loaded += 1
            else:
                _reject(self.finding, f"Credit note {number}", row["Id"],
                        "CreditNotes", err)
        return extracted, loaded


# ── module 11: statements.db ─────────────────────────────────────────

class StatementsModule(Module):
    name = "statements"
    source_db = "statements.db"
    depends_on = ["taxinvoices"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            heads = src.execute(
                "SELECT Id, StatementNumber, Title, FilterCompany,"
                " FilterCustomer, FilterProject, FilterSalesman,"
                " FilterDateFrom, FilterDateTo, InvoiceCount,"
                " TotalInvoiced, TotalOutstanding, OverdueAmount,"
                " PdfPath, FullSnapshotJson, CreatedAt, UpdatedAt"
                " FROM Statements ORDER BY Id").fetchall()
            lines = src.execute(
                "SELECT Id, StatementId, InvoiceNumber, ProformaId,"
                " DeliveryNoteRef, InvoiceType, Status, IssueDate,"
                " DueDate, TotalQty, TotalSqm, Subtotal, VatAmount,"
                " GrandTotal, PaidAmount, DaysOverdue"
                " FROM LineItems ORDER BY StatementId, Id").fetchall()
        by_statement: dict[int, list] = {}
        for line in lines:
            by_statement.setdefault(line["StatementId"], []).append(line)

        extracted = len(heads) + len(lines)
        loaded = 0
        for head in heads:
            number = (head["StatementNumber"] or "").strip()
            if not number:
                self.finding("error",
                             "Statement without a number — skipped",
                             f"Statements.Id={head['Id']}")
                continue
            statement_id = uuid.uuid4()
            # Totals preserved as the WPF computed them.
            totals = {
                "invoiceCount": head["InvoiceCount"],
                "totalInvoiced": str(money(head["TotalInvoiced"])),
                "totalOutstanding": str(money(head["TotalOutstanding"])),
                "overdueAmount": str(money(head["OverdueAmount"])),
            }
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.statements
                  (id, tenant_id, statement_no, title, filter_company,
                   filter_customer, filter_project, filter_salesman,
                   filter_date_from, filter_date_to, generated_at,
                   totals_json, storage_key, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                        '', %s, %s)
                """,
                (statement_id, tenant_id, number, head["Title"] or "",
                 head["FilterCompany"] or "", head["FilterCustomer"] or "",
                 head["FilterProject"] or "",
                 head["FilterSalesman"] or "",
                 to_date(head["FilterDateFrom"]),
                 to_date(head["FilterDateTo"]), to_utc(head["CreatedAt"]),
                 json.dumps(totals), to_utc(head["CreatedAt"]),
                 to_utc(head["UpdatedAt"])))
            if not ok:
                _reject(self.finding, f"Statement {number}", head["Id"],
                        "Statements", err)
                continue
            loaded += 1

            # WPF line = one invoice row (gross + paid); SaaS line = the
            # ledger (debit the invoice, credit the payment) with the
            # running balance, in WPF line order.
            balance = Decimal("0")
            line_no = 0
            for line in by_statement.get(head["Id"], []):
                grand = money(line["GrandTotal"])
                paid = money(line["PaidAmount"])
                ledger_rows = (
                    ("Invoice", grand, Decimal("0"),
                     f"Invoice {line['InvoiceNumber']}"
                     + (f" — {line['Status']}" if line["Status"] else "")),
                    ("Payment", Decimal("0"), paid,
                     f"Payment — invoice {line['InvoiceNumber']}"),
                )
                for line_type, debit, credit, description in ledger_rows:
                    if debit == 0 and credit == 0:
                        continue
                    balance += debit - credit
                    line_no += 1
                    ok, err = insert_with_savepoint(
                        pg,
                        """
                        INSERT INTO saas.statement_lines
                          (id, tenant_id, statement_id, line_no, line_type,
                           document_no, document_date, description, debit,
                           credit, running_balance, created_at, updated_at)
                        VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                                %s, %s)
                        """,
                        (uuid.uuid4(), tenant_id, statement_id, line_no,
                         line_type, line["InvoiceNumber"] or "",
                         to_date(line["IssueDate"]), description, debit,
                         credit, round2(balance), to_utc(head["CreatedAt"]),
                         to_utc(head["CreatedAt"])))
                    if ok:
                        loaded += 1
                    else:
                        _reject(self.finding,
                                f"Statement {number} line {line_no}",
                                line["Id"], "LineItems", err)
                if line["DaysOverdue"]:
                    self.finding("info",
                                 f"Statement {number}: invoice"
                                 f" {line['InvoiceNumber']} was"
                                 f" {line['DaysOverdue']} days overdue in"
                                 " the WPF (overdue detail preserved in"
                                 " totals_json)",
                                 f"LineItems.Id={line['Id']}")
        return extracted, loaded


# ── module 12: audit.db ActivityLog ──────────────────────────────────

class ActivityLogModule(Module):
    name = "activitylog"
    source_db = "audit.db"
    depends_on = ["users", "masterdata", "sheetstore", "specifications",
                  "inquiries", "proformas", "specusages", "optimization",
                  "joborders", "production", "deliverynotes", "taxinvoices",
                  "creditnotes", "statements"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        users: dict = id_map.get("users", {})
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, TimestampUtc, Username, UserDisplayName,"
                " UserRole, MachineName, AppVersion, LocalTimeZone,"
                " OperationId, Section, Action, Details, EntityType,"
                " EntityId, SessionId, ChangesJson, Reason"
                " FROM ActivityLog ORDER BY Id").fetchall()
        extracted = len(rows)
        loaded = 0
        for row in rows:
            user_id = users.get((row["Username"] or "").strip())
            if (row["Username"] or "").strip() and user_id is None:
                self.finding("info",
                             f"Activity row references unknown user"
                             f" '{row['Username']}' — user_id null",
                             f"ActivityLog.Id={row['Id']}")

            changes = (row["ChangesJson"] or "").strip()
            after_json = None
            if changes:
                try:
                    json.loads(changes)
                    after_json = changes
                except (ValueError, TypeError):
                    after_json = None
            details = (row["Details"] or "").strip()
            if after_json is None and details:
                # The WPF free-text trail is preserved verbatim inside
                # after_json (the SaaS audit row has no Details column).
                after_json = json.dumps({"details": details,
                                         "reason": row["Reason"] or ""})

            entity_id = None
            raw_entity = (row["EntityId"] or "").strip()
            if raw_entity:
                try:
                    entity_id = uuid.UUID(raw_entity)
                except ValueError:
                    # WPF entity ids are ints/strings; only true UUIDs
                    # can land in the SaaS column.
                    entity_id = None

            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.audit_logs
                  (id, tenant_id, user_id, action, entity_type, entity_id,
                   after_json, correlation_id, created_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (uuid.uuid4(), tenant_id, user_id, row["Action"] or "",
                 row["Section"] or "" or (row["EntityType"] or ""),
                 entity_id, after_json, row["OperationId"] or None,
                 to_utc(row["TimestampUtc"], source_is_utc=True)))
            if ok:
                loaded += 1
            else:
                _reject(self.finding, "Activity row", row["Id"],
                        "ActivityLog", err)
        return extracted, loaded


#: The module registry in the plan's dependency order (§2). The one
#: deferred module is declared with its reason so the CLI lists it
#: honestly.
DEFERRED_MODULES: list[tuple[str, str]] = [
    ("attachments", "disk files → object storage — no object storage in"
                    " this environment; the source tables are counted and"
                    " reported by their owning modules"),
]

IMPLEMENTED_MODULES: list[type[Module]] = [
    UsersModule, MasterDataModule, SheetStoreModule, SpecificationsModule,
    InquiriesModule, ProformasModule, SpecificationUsagesModule,
    OptimizationModule, JobOrdersModule, ProductionModule,
    DeliveryNotesModule, TaxInvoiceModule, CreditNoteModule,
    StatementsModule, ActivityLogModule,
]
