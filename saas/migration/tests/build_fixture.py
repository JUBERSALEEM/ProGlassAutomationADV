"""Builds a WPF-shaped SQLite data root for the migration tests.

Every table/column below is the EF Core SQLite shape read from the WPF
source on 2026-09-27 (see proglass_migrate/modules.py header for the
file provenance). The fixture deliberately includes: a balance mismatch
(the findings path), an unknown status (the skip path), an unimportable
inventory category (WPF 'Film'), a credit note referencing a skipped
invoice, and UTC/local timestamp mixes.
"""

from __future__ import annotations

import sqlite3
from pathlib import Path


def build(root: Path) -> None:
    root.mkdir(parents=True, exist_ok=True)
    for name in ("audit.db", "masterdata.db", "proglass.db",
                 "taxinvoices.db", "creditnotes.db", "statements.db"):
        (root / name).unlink(missing_ok=True)

    # ── audit.db (AuditDbContext: Users, ActivityLog) ────────────────
    audit = sqlite3.connect(root / "audit.db")
    audit.executescript("""
    CREATE TABLE Users (
        Id INTEGER PRIMARY KEY,
        Username TEXT NOT NULL,
        DisplayName TEXT NOT NULL,
        Role TEXT NOT NULL DEFAULT 'Operator',
        Mobile TEXT NOT NULL DEFAULT '',
        Email TEXT NOT NULL DEFAULT '',
        PasswordHash TEXT NOT NULL DEFAULT '',
        PasswordSalt TEXT NOT NULL DEFAULT '',
        CreatedAtUtc TEXT NOT NULL DEFAULT '2026-01-01 00:00:00'
    );
    CREATE TABLE ActivityLog (
        Id INTEGER PRIMARY KEY,
        TimestampUtc TEXT NOT NULL,
        Username TEXT NOT NULL,
        UserDisplayName TEXT NOT NULL,
        UserRole TEXT NOT NULL,
        MachineName TEXT NOT NULL,
        AppVersion TEXT NOT NULL,
        LocalTimeZone TEXT NOT NULL,
        OperationId TEXT NOT NULL,
        Section TEXT NOT NULL,
        Action TEXT NOT NULL,
        Details TEXT NOT NULL,
        EntityType TEXT NOT NULL,
        EntityId TEXT NOT NULL,
        SessionId TEXT NOT NULL,
        ChangesJson TEXT NOT NULL,
        Reason TEXT NOT NULL
    );
    """)
    audit.executemany(
        "INSERT INTO Users (Username, DisplayName, Role, Mobile, Email)"
        " VALUES (?, ?, ?, ?, ?)",
        [("jubers", "Juber Saleem", "Administrator", "+971501234567",
          "jubers@apex.ae"),
         ("cutter1", "Floor Cutter", "Production", "", ""),
         ("ghost", "Ghost Role", "SpaceMarine", "", "")])   # bad role
    def log(ts, user, section, action, details, entity_type, entity_id,
            op, changes):
        return (ts, user, user, "Administrator", "FLOOR-PC", "3.4.0",
                "Arabian Standard Time", op, section, action, details,
                entity_type, entity_id, "session-1", changes, "")

    audit.executemany(
        "INSERT INTO ActivityLog (TimestampUtc, Username, UserDisplayName,"
        " UserRole, MachineName, AppVersion, LocalTimeZone, OperationId,"
        " Section, Action, Details, EntityType, EntityId, SessionId,"
        " ChangesJson, Reason) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [log("2026-09-01 09:00:00", "jubers", "Settings", "user.created",
             "Created user cutter1", "User", "42", "op-1", ""),
         # free-text only → preserved inside after_json
         log("2026-09-02 10:30:00", "jubers", "SheetStore", "sheet.bought",
             "Bought 20 sheets", "GlassSheet", "not-a-uuid", "op-2", ""),
         # valid JSON changes
         log("2026-09-03 11:00:00", "cutter1", "TaxInvoice",
             "invoice.issued", "", "TaxInvoice", "7", "op-3",
             '{"status":{"from":"Draft","to":"Issued"}}'),
         log("2026-09-04 12:00:00", "nobody", "Unknown", "x", "", "", "",
             "op-4", "")])                                     # unknown user
    audit.commit()

    # ── masterdata.db (Companies/Clients/Projects/Salesmen) ──────────
    md = sqlite3.connect(root / "masterdata.db")
    md.executescript("""
    CREATE TABLE Companies (Id INTEGER PRIMARY KEY, CompanyName TEXT NOT NULL,
        CompanyTrn TEXT NOT NULL DEFAULT '', CompanyLocation TEXT NOT NULL DEFAULT '',
        UsageCount INTEGER NOT NULL DEFAULT 1, LastUsedAt TEXT NOT NULL,
        CreatedAt TEXT NOT NULL);
    CREATE TABLE Clients (Id INTEGER PRIMARY KEY, CustomerName TEXT NOT NULL,
        CustomerTrn TEXT NOT NULL DEFAULT '', CustomerReferenceCode TEXT NOT NULL DEFAULT '',
        SalesRepresentative TEXT NOT NULL DEFAULT '', DocumentPreparedBy TEXT NOT NULL DEFAULT '',
        BillingAddress TEXT NOT NULL DEFAULT '', AttentionExecutiveName TEXT NOT NULL DEFAULT '',
        ClientContactPhone TEXT NOT NULL DEFAULT '', UsageCount INTEGER NOT NULL DEFAULT 1,
        LastUsedAt TEXT NOT NULL, CreatedAt TEXT NOT NULL);
    CREATE TABLE Projects (Id INTEGER PRIMARY KEY, ProjectIdentifierName TEXT NOT NULL,
        ProjectContractNo TEXT NOT NULL DEFAULT '', ProjectReference TEXT NOT NULL DEFAULT '',
        SiteDeliveryLocation TEXT NOT NULL DEFAULT '', LpoNumber TEXT NOT NULL DEFAULT '',
        CustomerName TEXT NOT NULL DEFAULT '', UsageCount INTEGER NOT NULL DEFAULT 1,
        LastUsedAt TEXT NOT NULL, CreatedAt TEXT NOT NULL);
    CREATE TABLE Salesmen (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL,
        Department TEXT NOT NULL DEFAULT '', UsageCount INTEGER NOT NULL DEFAULT 1,
        LastUsedAt TEXT NOT NULL, CreatedAt TEXT NOT NULL);
    """)
    md.executemany(
        "INSERT INTO Companies (CompanyName, CompanyTrn, CompanyLocation,"
        " LastUsedAt, CreatedAt) VALUES (?,?,?,?,?)",
        [("Apex Glazing LLC", "100123456700003", "Dubai, UAE",
          "2026-09-01 08:00:00", "2026-01-01 08:00:00")])
    md.executemany(
        "INSERT INTO Clients (CustomerName, CustomerTrn, SalesRepresentative,"
        " BillingAddress, ClientContactPhone, LastUsedAt, CreatedAt)"
        " VALUES (?,?,?,?,?,?,?)",
        [("Emaar Properties", "100987654300003", "Ahmed",
          "Dubai Marina", "+97141234567", "2026-09-01 09:00:00",
          "2026-01-02 09:00:00")])
    md.executemany(
        "INSERT INTO Projects (ProjectIdentifierName, ProjectContractNo,"
        " CustomerName, LastUsedAt, CreatedAt) VALUES (?,?,?,?,?)",
        [("Marina Tower Glazing", "CTR-2026-11", "Emaar Properties",
          "2026-09-01 10:00:00", "2026-01-03 10:00:00"),
         ("Orphan Project", "", "Unknown Client",           # no client link
          "2026-09-01 10:00:00", "2026-01-03 10:00:00")])
    md.executemany(
        "INSERT INTO Salesmen (Name, Department, LastUsedAt, CreatedAt)"
        " VALUES (?,?,?,?)",
        [("Ahmed", "Facade", "2026-09-01 07:00:00", "2026-01-01 07:00:00")])
    md.commit()

    # ── proglass.db (GlassSheets / SheetHistories / InventoryItems) ──
    pg = sqlite3.connect(root / "proglass.db")
    pg.executescript("""
    CREATE TABLE GlassSheets (Id INTEGER PRIMARY KEY, Category TEXT NOT NULL,
        ThicknessMm INTEGER NOT NULL, Color TEXT NOT NULL DEFAULT 'Clear',
        WidthMm INTEGER NOT NULL, HeightMm INTEGER NOT NULL,
        Supplier TEXT NOT NULL DEFAULT '', PurchasePricePerSqm TEXT NOT NULL,
        SellPricePerSqm TEXT NOT NULL, Stock INTEGER NOT NULL, Used INTEGER NOT NULL,
        CreatedAt TEXT NOT NULL, LastUpdated TEXT NOT NULL, LastPurchaseDate TEXT);
    CREATE TABLE SheetHistories (Id INTEGER PRIMARY KEY, SheetId INTEGER NOT NULL,
        SheetDescription TEXT NOT NULL, Action TEXT NOT NULL, Details TEXT NOT NULL,
        QuantityChanged INTEGER NOT NULL, UserName TEXT NOT NULL,
        DocumentReference TEXT, CustomerName TEXT, ProjectName TEXT,
        Salesman TEXT, PanePosition TEXT, Timestamp TEXT NOT NULL,
        ReversesHistoryId INTEGER, IsReversed INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE InventoryItems (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL,
        Category INTEGER NOT NULL DEFAULT 0, PartNumber TEXT NOT NULL DEFAULT '',
        Supplier TEXT NOT NULL DEFAULT '', Specification TEXT NOT NULL DEFAULT '',
        Uom INTEGER NOT NULL DEFAULT 0, Stock TEXT NOT NULL DEFAULT '0',
        Used TEXT NOT NULL DEFAULT '0', ReorderLevel TEXT NOT NULL DEFAULT '0',
        PurchasePricePerUnit TEXT NOT NULL DEFAULT '0',
        SellPricePerUnit TEXT NOT NULL DEFAULT '0', CreatedAt TEXT NOT NULL,
        LastUpdated TEXT NOT NULL, LastPurchaseDate TEXT);
    """)
    # Sheet 1: clean history (2 buys of 10, 1 use of 5 → balance 15).
    pg.executemany(
        "INSERT INTO GlassSheets (Category, ThicknessMm, Color, WidthMm,"
        " HeightMm, Supplier, PurchasePricePerSqm, SellPricePerSqm, Stock,"
        " Used, CreatedAt, LastUpdated, LastPurchaseDate)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [("Float", 6, "Clear", 3210, 2250, "GlasImport", "95.50", "140.00",
          20, 5, "2026-01-05 08:00:00", "2026-09-05 08:00:00",
          "2026-09-05"),
         # Sheet 2: stored balance DISAGREES with its history (fixture
         # for the mismatch finding): buys 10, uses 2 → 8, stored 20-2=18.
         ("Float", 8, "Bronze", 2250, 3210, "GlasImport", "110.00", "160.00",
          20, 2, "2026-01-06 08:00:00", "2026-09-06 08:00:00", None)])
    def hist(sheet, action, details, qty, user, ts):
        return (sheet, "6mm Clear 3210x2250", action, details, qty, user,
                None, None, None, None, None, ts, None, 0)

    pg.executemany(
        "INSERT INTO SheetHistories (SheetId, SheetDescription, Action,"
        " Details, QuantityChanged, UserName, DocumentReference,"
        " CustomerName, ProjectName, Salesman, PanePosition, Timestamp,"
        " ReversesHistoryId, IsReversed) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [hist(1, "Bought", "Bought 10 sheets", 10, "jubers",
              "2026-01-05 08:00:00"),
         hist(1, "Bought", "Bought 10 sheets", 10, "jubers",
              "2026-02-05 08:00:00"),
         hist(1, "Used", "Used 5 sheets", 5, "cutter1",
              "2026-03-05 08:00:00"),
         hist(2, "Bought", "Bought 10 sheets", 10, "jubers",
              "2026-01-06 08:00:00"),
         hist(2, "Used", "Used 2 sheets", 2, "cutter1",
              "2026-03-06 08:00:00")])
    pg.executemany(
        "INSERT INTO InventoryItems (Name, Category, Uom, Stock, Used,"
        " ReorderLevel, PurchasePricePerUnit, SellPricePerUnit, CreatedAt,"
        " LastUpdated) VALUES (?,?,?,?,?,?,?,?,?,?)",
        [("Warm-edge spacer 16mm", 1, 1, "45.55", "5.55", "10", "12.00",
          "18.00", "2026-01-07 08:00:00", "2026-09-07 08:00:00"),
         ("Desiccant molecular sieve", 2, 3, "2.40", "0.40", "1", "9.00",
          "15.00", "2026-01-07 08:00:00", "2026-09-07 08:00:00"),
         ("PVB interlayer clear", 7, 2, "30.00", "0", "5", "40.00",
          "60.00", "2026-01-07 08:00:00", "2026-09-07 08:00:00")])
    pg.commit()

    # ── taxinvoices.db (TaxInvoices) ─────────────────────────────────
    ti = sqlite3.connect(root / "taxinvoices.db")
    ti.executescript("""
    CREATE TABLE TaxInvoices (Id INTEGER PRIMARY KEY, InvoiceNumber TEXT NOT NULL,
        ProformaId TEXT NOT NULL DEFAULT '', DeliveryNoteReference TEXT NOT NULL DEFAULT '',
        JobOrderId TEXT NOT NULL DEFAULT '', PurchaseOrderNumber TEXT NOT NULL DEFAULT '',
        InvoiceType TEXT NOT NULL DEFAULT '', SequenceLetter TEXT NOT NULL DEFAULT '',
        SequenceNumber INTEGER NOT NULL DEFAULT 0, Status TEXT NOT NULL,
        IsVoid INTEGER NOT NULL DEFAULT 0, VoidedAtUtc TEXT,
        CustomerName TEXT NOT NULL DEFAULT '', ProjectName TEXT NOT NULL DEFAULT '',
        CompanyName TEXT NOT NULL DEFAULT '', CompanyTrn TEXT NOT NULL DEFAULT '',
        IssueDate TEXT NOT NULL, DueDate TEXT NOT NULL, PaymentTerms TEXT NOT NULL DEFAULT '',
        PaymentTermsDays INTEGER NOT NULL DEFAULT 30, Subtotal TEXT NOT NULL,
        DiscountAmount TEXT NOT NULL, DiscountPercent TEXT NOT NULL,
        AfterDiscount TEXT NOT NULL, VatAmount TEXT NOT NULL, GrandTotal TEXT NOT NULL,
        TotalQty INTEGER NOT NULL DEFAULT 0, TotalSqm TEXT NOT NULL,
        FullSnapshotJson TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL, RowVersion INTEGER NOT NULL DEFAULT 0);
    """)
    ti.executemany(
        "INSERT INTO TaxInvoices (InvoiceNumber, ProformaId, Status,"
        " CustomerName, ProjectName, IssueDate, DueDate, PaymentTerms,"
        " Subtotal, DiscountAmount, DiscountPercent, AfterDiscount,"
        " VatAmount, GrandTotal, TotalSqm, FullSnapshotJson, CreatedAt,"
        " UpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [("INV-2026-001", "PI-2026-001", "Paid", "Emaar Properties",
          "Marina Tower Glazing", "2026-08-01 00:00:00",
          "2026-08-31 00:00:00", "Net 30", "10000", "0", "0", "10000",
          "500", "10500", "12.5", '{"lines":[]}', "2026-08-01 10:00:00",
          "2026-08-31 10:00:00"),
         # VAT mismatch fixture: 900 after discount → 45 expected.
         ("INV-2026-002", "PI-2026-001", "Issued", "Emaar Properties",
          "Marina Tower Glazing", "2026-09-01 00:00:00",
          "2026-10-01 00:00:00", "Net 30", "1000", "100", "10", "900",
          "44", "944", "1.25", "", "2026-09-01 10:00:00",
          "2026-09-01 10:00:00"),
         # Unknown status fixture — must be reported + skipped.
         ("INV-2026-003", "", "SomewhereElse", "Walk-in", "",
          "2026-09-02 00:00:00", "2026-10-02 00:00:00", "Net 30", "10",
          "0", "0", "10", "0.50", "10.50", "0", "", "2026-09-02 10:00:00",
          "2026-09-02 10:00:00")])
    ti.commit()

    # ── creditnotes.db (CreditNotes) ─────────────────────────────────
    cn = sqlite3.connect(root / "creditnotes.db")
    cn.executescript("""
    CREATE TABLE CreditNotes (Id INTEGER PRIMARY KEY, CreditNoteNumber TEXT NOT NULL,
        TaxInvoiceReference TEXT NOT NULL DEFAULT '', ProformaId TEXT NOT NULL DEFAULT '',
        JobOrderId TEXT NOT NULL DEFAULT '', PurchaseOrderNumber TEXT NOT NULL DEFAULT '',
        SequenceNumber INTEGER NOT NULL DEFAULT 0, Reason TEXT NOT NULL DEFAULT '',
        Status TEXT NOT NULL, IsVoid INTEGER NOT NULL DEFAULT 0, VoidedAtUtc TEXT,
        CustomerName TEXT NOT NULL DEFAULT '', ProjectName TEXT NOT NULL DEFAULT '',
        CompanyName TEXT NOT NULL DEFAULT '', CompanyTrn TEXT NOT NULL DEFAULT '',
        IssueDate TEXT NOT NULL, Subtotal TEXT NOT NULL, DiscountAmount TEXT NOT NULL,
        AfterDiscount TEXT NOT NULL, VatAmount TEXT NOT NULL, GrandTotal TEXT NOT NULL,
        TotalQty INTEGER NOT NULL DEFAULT 0, TotalSqm TEXT NOT NULL,
        FullSnapshotJson TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL, RowVersion INTEGER NOT NULL DEFAULT 0);
    """)
    cn.executemany(
        "INSERT INTO CreditNotes (CreditNoteNumber, TaxInvoiceReference,"
        " Reason, Status, CustomerName, IssueDate, Subtotal,"
        " DiscountAmount, AfterDiscount, VatAmount, GrandTotal, TotalSqm,"
        " CreatedAt, UpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [("CN-2026-001", "INV-2026-001", "Broken panel credit", "Applied",
          "Emaar Properties", "2026-09-10 00:00:00", "100", "0", "100",
          "5", "105", "0.5", "2026-09-10 10:00:00",
          "2026-09-10 10:00:00"),
         # References the skipped invoice — warning + null FK.
         ("CN-2026-002", "INV-2026-003", "Credit the void", "Draft",
          "Walk-in", "2026-09-11 00:00:00", "10", "0", "10", "0.50",
          "10.50", "0", "2026-09-11 10:00:00", "2026-09-11 10:00:00")])
    cn.commit()

    # ── statements.db (Statements / LineItems) ───────────────────────
    st = sqlite3.connect(root / "statements.db")
    st.executescript("""
    CREATE TABLE Statements (Id INTEGER PRIMARY KEY, StatementNumber TEXT NOT NULL,
        Title TEXT NOT NULL DEFAULT '', FilterCompany TEXT NOT NULL DEFAULT '',
        FilterCustomer TEXT NOT NULL DEFAULT '', FilterProject TEXT NOT NULL DEFAULT '',
        FilterSalesman TEXT NOT NULL DEFAULT '', FilterDateFrom TEXT,
        FilterDateTo TEXT, InvoiceCount INTEGER NOT NULL DEFAULT 0,
        TotalInvoiced TEXT NOT NULL, TotalOutstanding TEXT NOT NULL,
        OverdueAmount TEXT NOT NULL, PdfPath TEXT NOT NULL DEFAULT '',
        HasAttachments INTEGER NOT NULL DEFAULT 0, FullSnapshotJson TEXT NOT NULL DEFAULT '',
        FilterJson TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
    CREATE TABLE LineItems (Id INTEGER PRIMARY KEY, StatementId INTEGER NOT NULL,
        InvoiceNumber TEXT NOT NULL DEFAULT '', ProformaId TEXT NOT NULL DEFAULT '',
        DeliveryNoteRef TEXT NOT NULL DEFAULT '', InvoiceType TEXT NOT NULL DEFAULT '',
        Status TEXT NOT NULL DEFAULT '', IssueDate TEXT NOT NULL, DueDate TEXT NOT NULL,
        TotalQty INTEGER NOT NULL DEFAULT 0, TotalSqm TEXT NOT NULL,
        Subtotal TEXT NOT NULL, VatAmount TEXT NOT NULL, GrandTotal TEXT NOT NULL,
        PaidAmount TEXT NOT NULL, DaysOverdue INTEGER NOT NULL DEFAULT 0);
    """)
    st.execute(
        "INSERT INTO Statements (StatementNumber, Title, FilterCustomer,"
        " FilterDateFrom, FilterDateTo, InvoiceCount, TotalInvoiced,"
        " TotalOutstanding, OverdueAmount, CreatedAt, UpdatedAt)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?)",
        ("ST-2026-001", "Statement — Emaar Properties", "Emaar Properties",
         "2026-08-01", "2026-09-30", 2, "11449", "9344", "0",
         "2026-09-20 12:00:00", "2026-09-20 12:00:00"))
    st.executemany(
        "INSERT INTO LineItems (StatementId, InvoiceNumber, Status,"
        " IssueDate, DueDate, TotalSqm, Subtotal, VatAmount, GrandTotal,"
        " PaidAmount, DaysOverdue) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "INV-2026-001", "Paid", "2026-08-01 00:00:00",
          "2026-08-31 00:00:00", "12.5", "10000", "500", "10500", "10500",
          0),
         (1, "INV-2026-002", "Issued", "2026-09-01 00:00:00",
          "2026-10-01 00:00:00", "1.25", "900", "44", "944", "0", 0)])
    st.commit()

    for conn in (audit, md, pg, ti, cn, st):
        conn.close()
    print(f"fixture data root built at {root}")
    return str(root)


if __name__ == "__main__":
    import sys
    build(Path(sys.argv[1] if len(sys.argv) > 1 else "fixture-data-root"))
