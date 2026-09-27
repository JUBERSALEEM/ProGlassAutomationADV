"""The import modules (saas/docs/SAAS_MIGRATION_PLAN.md §2 order).

WPF schema ground truth (read 2026-09-27, EF Core SQLite — table =
DbSet property name, column = C# property name):
  • audit.db       Users (Models/Audit/AppUser.cs), ActivityLog
                   (Models/Audit/ActivityLogEntry.cs) — AuditDbContext
  • masterdata.db  Companies/Clients/Projects/Salesmen
                   (Models/MasterData/Saved{Company,Client,Project,
                   Salesman}.cs)
  • proglass.db    GlassSheets (Models/SheetStore/GlassSheet.cs),
                   SheetHistories (SheetHistory.cs), InventoryItems
                   (InventoryItem.cs; enums InventoryCategory 0-7,
                   StockUom 0-3)
  • taxinvoices.db TaxInvoices (Models/TaxInvoice/SavedTaxInvoice/
                   SavedTaxInvoice.cs)
  • creditnotes.db CreditNotes (Models/CreditNote/SavedCreditNote/
                   SavedCreditNote.cs)
  • statements.db  Statements (Models/Statement/SavedStatement.cs),
                   LineItems (StatementLineItem.cs)

Deferred modules (listed by the CLI, never silently skipped):
inquiries, proformas, specifications, optimization, joborders,
production, deliverynotes, attachments — they import into document
tables whose WPF readers (FullSnapshotJson parsing, BOQ grids, the
oracle re-runs) are later slices.
"""

from __future__ import annotations

import json
import secrets
import sqlite3
import uuid
from decimal import Decimal

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

        for row in companies:
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.companies
                  (id, tenant_id, name, trn, location, usage_count,
                   last_used_at, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (uuid.uuid4(), tenant_id, row["CompanyName"] or "",
                 row["CompanyTrn"] or "", row["CompanyLocation"] or "",
                 row["UsageCount"] or 1, to_utc(row["LastUsedAt"]),
                 to_utc(row["CreatedAt"]), to_utc(row["CreatedAt"])))
            if ok:
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
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.projects
                  (id, tenant_id, identifier_name, contract_no, reference,
                   site_delivery_location, lpo_number, customer_id,
                   usage_count, last_used_at, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (uuid.uuid4(), tenant_id,
                 row["ProjectIdentifierName"] or "",
                 row["ProjectContractNo"] or "",
                 row["ProjectReference"] or "",
                 row["SiteDeliveryLocation"] or "",
                 row["LpoNumber"] or "", customer_id,
                 row["UsageCount"] or 1, to_utc(row["LastUsedAt"]),
                 to_utc(row["CreatedAt"]), to_utc(row["CreatedAt"])))
            if ok:
                loaded += 1
            else:
                _reject(self.finding,
                        f"Project '{row['ProjectIdentifierName']}'",
                        row["Id"], "Projects", err)

        for row in salesmen:
            ok, err = insert_with_savepoint(
                pg,
                """
                INSERT INTO saas.salesmen
                  (id, tenant_id, name, department, usage_count,
                   last_used_at, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (uuid.uuid4(), tenant_id, row["Name"] or "",
                 row["Department"] or "", row["UsageCount"] or 1,
                 to_utc(row["LastUsedAt"]), to_utc(row["CreatedAt"]),
                 to_utc(row["CreatedAt"])))
            if ok:
                loaded += 1
            else:
                _reject(self.finding, f"Salesman '{row['Name']}'",
                        row["Id"], "Salesmen", err)
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
        return extracted, loaded


# ── modules 10a/10b: tax invoices + credit notes ─────────────────────

class TaxInvoiceModule(Module):
    name = "taxinvoices"
    source_db = "taxinvoices.db"
    depends_on = ["users"]

    def run(self, pg: PgCursor, tenant_id: uuid.UUID,
            id_map: dict) -> tuple[int, int]:
        with self.open_source() as src:
            src.row_factory = sqlite3.Row
            rows = src.execute(
                "SELECT Id, InvoiceNumber, ProformaId,"
                " DeliveryNoteReference, JobOrderId, PurchaseOrderNumber,"
                " InvoiceType, SequenceLetter, SequenceNumber, Status,"
                " IsVoid, VoidedAtUtc, CustomerName, ProjectName,"
                " CompanyName, CompanyTrn, IssueDate, DueDate,"
                " PaymentTerms, PaymentTermsDays, Subtotal,"
                " DiscountAmount, DiscountPercent, AfterDiscount,"
                " VatAmount, GrandTotal, TotalQty, TotalSqm,"
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
                  (id, tenant_id, invoice_no, delivery_note_reference,
                   status, issue_date, due_date, subtotal, discount_amount,
                   vat_amount, grand_total, paid_amount, balance_amount,
                   snapshot_json, customer_name, project_name,
                   payment_terms, discount_percent, is_void, voided_at,
                   void_reason, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, 0, %s,
                        %s, %s, %s, %s, %s, %s, %s, '', %s, %s)
                """,
                (new_id, tenant_id, number,
                 row["DeliveryNoteReference"] or "", status,
                 to_date(row["IssueDate"]), to_date(row["DueDate"]),
                 subtotal, discount, vat, grand, grand,
                 row["FullSnapshotJson"] or None,
                 row["CustomerName"] or "", row["ProjectName"] or "",
                 row["PaymentTerms"] or "Net 30",
                 money(row["DiscountPercent"]), bool(row["IsVoid"]),
                 to_utc(row["VoidedAtUtc"], source_is_utc=True),
                 to_utc(row["CreatedAt"]), to_utc(row["UpdatedAt"])))
            if ok:
                invoice_ids[number] = new_id
                loaded += 1
                if (row["ProformaId"] or "").strip():
                    self.finding(
                        "info",
                        f"Invoice {number} references WPF proforma"
                        f" '{row['ProformaId']}' — the link imports when"
                        " the proforma module lands (kept in the snapshot)",
                        f"TaxInvoices.Id={row['Id']}")
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
    depends_on = ["users", "masterdata", "sheetstore", "taxinvoices",
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


#: The module registry in the plan's dependency order. Deferred modules
#: are declared with their reason so the CLI can list them honestly.
DEFERRED_MODULES: list[tuple[str, str]] = [
    ("specifications", "priced-spec registry import — later slice"),
    ("inquiries", "BOQ JSON → proforma_invoices (inquiry kind) — later "
                  "slice"),
    ("proformas", "FullSnapshotJson → normalized spec groups/items — "
                  "later slice"),
    ("optimization", "sessions re-run through both oracles — later slice"),
    ("joborders", "job order + cutting plan import — later slice"),
    ("production", "pieces/stages/events/reservations — later slice"),
    ("deliverynotes", "items + delivered/returned — later slice"),
    ("attachments", "disk files → object storage — later slice"),
]

IMPLEMENTED_MODULES: list[type[Module]] = [
    UsersModule, MasterDataModule, SheetStoreModule, TaxInvoiceModule,
    CreditNoteModule, StatementsModule, ActivityLogModule,
]
