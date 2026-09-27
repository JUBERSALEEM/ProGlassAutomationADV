"""Builds a WPF-shaped SQLite data root for the migration tests.

Every table/column below is the EF Core SQLite shape read from the WPF
source on 2026-09-27 (see proglass_migrate/modules.py header for the
file provenance). The fixture deliberately includes: a balance mismatch
(the findings path), an unknown status (the skip path), an unimportable
inventory category (WPF 'Film'), a credit note referencing a skipped
invoice, UTC/local timestamp mixes — and, for the Phase 16 document
modules: an invalid glass type, a duplicate (glass_type, spec_name)
pair, bad PanesJson, a legacy 'On Hold' inquiry status, a bad priority,
an inquiry reference to an unimported proforma, a proforma with invalid
snapshot JSON, a spec group with a non-CHECK glass type, a phantom qty-0
grid row, a VAT mismatch, a revision whose root was not imported, a
reserved optimization latest-run row, the legacy 'In Production' job
status spelling, a job order with an unimported source PI, out-of-enum
production stage/status values, a reservation on an unimported sheet, an
unknown delivery type, and a delivery item on a skipped note.
"""

from __future__ import annotations

import json
import sqlite3
from pathlib import Path


def build(root: Path) -> None:
    root.mkdir(parents=True, exist_ok=True)
    for name in ("audit.db", "masterdata.db", "proglass.db",
                 "specifications.db", "inquiries.db",
                 "proformainvoices.db", "optimization.db",
                 "joborders.db", "production.db", "deliverynotes.db",
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

    # ── specifications.db (Specifications / SpecificationUsages) ─────
    sp = sqlite3.connect(root / "specifications.db")
    sp.executescript("""
    CREATE TABLE Specifications (Id INTEGER PRIMARY KEY, GlassType TEXT NOT NULL,
        SpecName TEXT NOT NULL, PricePerSqm TEXT NOT NULL,
        CreatedAt TEXT NOT NULL, LastUsedAt TEXT NOT NULL,
        UsageCount INTEGER NOT NULL DEFAULT 0, Category TEXT,
        Color TEXT, ThicknessMm INTEGER NOT NULL DEFAULT 0,
        SheetWidthMm INTEGER NOT NULL DEFAULT 0, SheetHeightMm INTEGER NOT NULL DEFAULT 0,
        SheetId INTEGER, ProformaId TEXT, PanesJson TEXT);
    CREATE TABLE SpecificationUsages (Id INTEGER PRIMARY KEY,
        SpecificationId INTEGER NOT NULL, SpecName TEXT NOT NULL,
        GlassType TEXT NOT NULL, ProformaId TEXT NOT NULL,
        CompanyName TEXT NOT NULL DEFAULT '', CustomerName TEXT NOT NULL DEFAULT '',
        ProjectName TEXT NOT NULL DEFAULT '', PricePerSqm TEXT NOT NULL,
        SheetWidthMm INTEGER NOT NULL DEFAULT 0, SheetHeightMm INTEGER NOT NULL DEFAULT 0,
        UserName TEXT NOT NULL DEFAULT '', UsedAt TEXT NOT NULL);
    """)
    sp.executemany(
        "INSERT INTO Specifications (Id, GlassType, SpecName, PricePerSqm,"
        " CreatedAt, LastUsedAt, UsageCount, Category, Color, ThicknessMm,"
        " SheetWidthMm, SheetHeightMm, SheetId, ProformaId, PanesJson)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "DGU", "Clear 6+12+6", "125.50", "2026-08-01 09:00:00",
          "2026-09-01 10:00:00", 3, "Float", "Clear", 6, 3210, 2250, 1,
          "PI-2026-001", '{"outer":{"thickness":6},"inner":{"thickness":6}}'),
         # Invalid glass type — error + skip.
         (2, "TRIPLE", "Triple glazing", "200.00", "2026-08-02 09:00:00",
          "2026-08-02 09:00:00", 0, None, None, 12, 0, 0, None, None, None),
         # Duplicate (DGU, Clear 6+12+6) — UNIQUE(tenant, glass_type,
         # spec_name) reject.
         (3, "DGU", "Clear 6+12+6", "130.00", "2026-08-03 09:00:00",
          "2026-08-03 09:00:00", 0, None, None, 6, 3210, 2250, 2, None, None),
         # Unknown sheet + invalid PanesJson — info + warning.
         (4, "SGU", "Tinted 8mm", "80.00", "2026-08-04 09:00:00",
          "2026-08-04 09:00:00", 1, "Float", "Bronze", 8, 2250, 3210, 99,
          None, "not-json")])
    sp.executemany(
        "INSERT INTO SpecificationUsages (SpecificationId, SpecName,"
        " GlassType, ProformaId, CompanyName, CustomerName, ProjectName,"
        " PricePerSqm, SheetWidthMm, SheetHeightMm, UserName, UsedAt)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "Clear 6+12+6", "DGU", "PI-2026-001", "Apex Glazing LLC",
          "Emaar Properties", "Marina Tower Glazing", "125.50", 3210, 2250,
          "jubers", "2026-09-01 10:00:00"),
         # References a proforma that will NOT import — error + skip.
         (1, "Clear 6+12+6", "DGU", "PI-2026-999", "Apex Glazing LLC",
          "Emaar Properties", "Marina Tower Glazing", "125.50", 3210, 2250,
          "jubers", "2026-09-02 10:00:00"),
         # References a spec that did not import (Id 77) — error + skip.
         (77, "Missing Spec", "SGU", "PI-2026-001", "", "", "", "10.00",
          0, 0, "", "2026-09-03 10:00:00")])
    sp.commit()

    # ── inquiries.db (Inquiries / InquiryLineItems / …) ──────────────
    iq = sqlite3.connect(root / "inquiries.db")
    iq.executescript("""
    CREATE TABLE Inquiries (Id INTEGER PRIMARY KEY, InquiryNumber TEXT NOT NULL,
        InquiryReference TEXT NOT NULL DEFAULT '', CustomerId INTEGER,
        CustomerName TEXT NOT NULL DEFAULT '', CustomerLocation TEXT NOT NULL DEFAULT '',
        ProjectId INTEGER, ProjectName TEXT NOT NULL DEFAULT '',
        ProjectDescription TEXT NOT NULL DEFAULT '', SalesmanId INTEGER,
        SalesmanName TEXT NOT NULL DEFAULT '', PreparedByUserId INTEGER,
        PreparedByName TEXT NOT NULL DEFAULT '', PreparedByRole TEXT NOT NULL DEFAULT '',
        Status TEXT NOT NULL DEFAULT 'Received', Priority TEXT NOT NULL DEFAULT 'Normal',
        InquiryValue TEXT NOT NULL DEFAULT '0', Notes TEXT NOT NULL DEFAULT '',
        ReceivedAt TEXT NOT NULL, PreparedAt TEXT, SentAt TEXT, ConfirmedAt TEXT,
        CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
        UpdatedBy TEXT NOT NULL DEFAULT '', ProformaInvoiceId INTEGER,
        ProformaNumber TEXT NOT NULL DEFAULT '', JobOrderNumber TEXT NOT NULL DEFAULT '');
    CREATE TABLE InquiryLineItems (Id INTEGER PRIMARY KEY, InquiryId INTEGER NOT NULL,
        SortOrder INTEGER NOT NULL DEFAULT 0, GlassType TEXT NOT NULL DEFAULT '',
        SpecName TEXT NOT NULL DEFAULT '', OuterGlassColor TEXT NOT NULL DEFAULT '',
        InnerGlassColor TEXT NOT NULL DEFAULT '', WidthMm INTEGER NOT NULL DEFAULT 0,
        HeightMm INTEGER NOT NULL DEFAULT 0, Qty INTEGER NOT NULL DEFAULT 0,
        RatePerSqm TEXT NOT NULL DEFAULT '0', Notes TEXT NOT NULL DEFAULT '',
        CreatedAt TEXT NOT NULL);
    CREATE TABLE InquiryAttachments (Id INTEGER PRIMARY KEY, InquiryId INTEGER NOT NULL,
        FileName TEXT NOT NULL, Extension TEXT NOT NULL DEFAULT '',
        FileSize INTEGER NOT NULL DEFAULT 0, StoragePath TEXT NOT NULL,
        AttachmentType TEXT NOT NULL DEFAULT '', UploadedBy TEXT NOT NULL DEFAULT '',
        UploadedAt TEXT NOT NULL);
    CREATE TABLE InquiryCommunications (Id INTEGER PRIMARY KEY, InquiryId INTEGER NOT NULL,
        EventType TEXT NOT NULL, Title TEXT NOT NULL, Description TEXT NOT NULL DEFAULT '',
        OccurredAt TEXT NOT NULL, ActorUserId INTEGER, ActorName TEXT NOT NULL DEFAULT '');
    """)
    iq.executemany(
        "INSERT INTO Inquiries (Id, InquiryNumber, InquiryReference,"
        " CustomerId, CustomerName, ProjectId, ProjectName, SalesmanId,"
        " SalesmanName, PreparedByName, PreparedByRole, Status, Priority,"
        " InquiryValue, Notes, ReceivedAt, CreatedAt, UpdatedAt,"
        " ProformaInvoiceId, ProformaNumber) VALUES"
        " (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "INQ-2026-001", "EMA-BOQ-1", 1, "Emaar Properties", 1,
          "Marina Tower Glazing", 1, "Ahmed", "Juber Saleem", "Estimator",
          "Received", "High", "2355.00", "Marina podium glazing BOQ",
          "2026-08-30 09:00:00", "2026-08-30 09:00:00",
          "2026-09-01 09:00:00", 1, "PI-2026-001"),
         # Legacy 'On Hold' spelling — normalized to Hold + status_text.
         (2, "INQ-2026-002", "", 99, "Walk-in Client", None, "", None, "",
          "Juber Saleem", "Estimator", "On Hold", "normal", "500.00",
          "Small replacement job", "2026-08-31 09:00:00",
          "2026-08-31 09:00:00", "2026-08-31 09:00:00", None, ""),
         # Priority outside the CHECK vocabulary — error + skip.
         (3, "INQ-2026-003", "", None, "Walk-in Client", None, "", None, "",
          "Juber Saleem", "Estimator", "Received", "Whenever", "100.00",
          "", "2026-09-02 09:00:00", "2026-09-02 09:00:00",
          "2026-09-02 09:00:00", None, "")])
    iq.executemany(
        "INSERT INTO InquiryLineItems (InquiryId, SortOrder, GlassType,"
        " SpecName, OuterGlassColor, InnerGlassColor, WidthMm, HeightMm,"
        " Qty, RatePerSqm, Notes, CreatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, 0, "DGU", "Clear 6+12+6", "Clear", "Clear", 1500, 2400, 3,
          "125.50", "", "2026-08-30 09:05:00"),
         (1, 1, "SGU", "Tinted 8mm", "Bronze", "", 800, 1200, 4, "80.00",
          "Back pan", "2026-08-30 09:06:00")])
    iq.execute(
        "INSERT INTO InquiryAttachments (InquiryId, FileName, Extension,"
        " FileSize, StoragePath, AttachmentType, UploadedBy, UploadedAt)"
        " VALUES (1,'boq.pdf','.pdf',20480,'Attachments/INQ-2026-001/boq.pdf',"
        "'BOQ','jubers','2026-08-30 10:00:00')")
    iq.executemany(
        "INSERT INTO InquiryCommunications (InquiryId, EventType, Title,"
        " Description, OccurredAt, ActorUserId, ActorName)"
        " VALUES (?,?,?,?,?,?,?)",
        [(1, "Note", "Client called", "Requested tempered outer pane",
          "2026-08-31 14:00:00", 1, "jubers"),
         (1, "StatusChange", "Marked On Hold", "Awaiting client budget",
          "2026-09-01 08:00:00", 1, "jubers")])
    iq.commit()

    # ── proformainvoices.db (ProformaInvoices) ───────────────────────
    # PI-2026-001's snapshot: 1 DGU group, 2 real rows + 1 phantom row,
    # 2 predefined charges (one with an empty metric key → derived LM1),
    # 1 manual wastage, 1 custom charge, a P&L block, terms, prepared by.
    pi_snapshot = {
        "SchemaVersion": 5, "FurthestStepReached": 9,
        "Company": {"CompanyName": "Apex Glazing LLC",
                    "CompanyTrn": "100123456700003",
                    "ProformaId": "PI-2026-001"},
        "Project": {"ProjectIdentifierName": "Marina Tower Glazing"},
        "Client": {"CustomerName": "Emaar Properties",
                   "SalesRepresentative": "Ahmed"},
        "Summary": {"DiscountType": "Percentage", "DiscountValue": 0,
                    "IsVatApplicable": True, "VatPercent": 5},
        "SpecGroups": [{
            "GroupNumber": 1, "GroupName": "Spec Group 1",
            "SelectedGlassType": "DGU", "SelectedSpecName": "Clear 6+12+6",
            "BaseRate": 110.0, "SurchargePercent": 0,
            "HasManualStep5BaseRateOverride": False,
            "OuterGlassColor": "Clear", "InnerGlassColor": "Clear",
            "ThirdGlassColor": "",
            "OuterSheetWidthMm": 3210, "OuterSheetHeightMm": 2250,
            "InnerSheetWidthMm": 3210, "InnerSheetHeightMm": 2250,
            "ThirdSheetWidthMm": 0, "ThirdSheetHeightMm": 0,
            "OuterSheetPricePerSqm": 95.5, "InnerSheetPricePerSqm": 95.5,
            "ThirdSheetPricePerSqm": 0,
            "Step4SpacerPricePerSqm": 12.0, "Step4InterlayerCostPerSqm": 0,
            "Step4CuttingCostPerSqm": 5.0, "Step4TemperingCostPerSqm": 8.0,
            "UInsertPricePerSqm": 10.0,
            "ItemRows": [
                {"SerialNumber": 1, "GlassRef": "A-01", "W1": 1500,
                 "H1": 2400, "W2": 0, "H2": 0, "Qty": 3, "Rate": 110.0,
                 "PerimeterFactor": 2},
                {"SerialNumber": 2, "GlassRef": "A-02", "W1": 800,
                 "H1": 1200, "W2": 0, "H2": 0, "Qty": 2, "Rate": 110.0,
                 "PerimeterFactor": 2},
                # Phantom row (qty 0) — dropped by the save contract.
                {"SerialNumber": 3, "GlassRef": "", "W1": 0, "H1": 0,
                 "W2": 0, "H2": 0, "Qty": 0, "Rate": 0,
                 "PerimeterFactor": 2}]}],
        "OtherCharges": {
            "UseAdvancedAutomation": True,
            "PredefinedCharges": [
                {"TemplateKey": "ARGON_GAS", "IsActive": True,
                 "SelectedSourceKey": "SQM1", "Rate": 45.0,
                 "MetricQty": 12.72, "ResultAmount": 572.40,
                 "PolishSubType": "", "CustomPolishDescription": "",
                 "CustomPolishQty": 0, "SelectedMetricKey": "SQM1"},
                # No metric key (sub-type driven) — derived LM1 + info.
                {"TemplateKey": "POLISH", "IsActive": True,
                 "SelectedSourceKey": "", "Rate": 6.0, "MetricQty": 8.4,
                 "ResultAmount": 50.40, "PolishSubType": "AllSidePolish",
                 "CustomPolishDescription": "", "CustomPolishQty": 0,
                 "SelectedMetricKey": ""}],
            "Wastages": [
                {"IsActive": True, "ColorName": "Clear", "Rate": 20.0,
                 "TotalSqm": 3.5, "ResultAmount": 70.0,
                 "IsManuallyAdded": True, "UseOptimizationFormula": False,
                 "FormulaPricePerSqm": 0, "OptimizationPercent": 0,
                 "SheetQuantity": 2, "CheckedSourceKeys": ["1-Outer"]}],
            "CustomCharges": [
                {"IsActive": True, "ChargeName": "Crane hire", "Qty": 1.0,
                 "Unit": "Qty", "Rate": 150.0}]},
        "ProfitLoss": {
            "GrossVatInclusive": 2355.0, "NetExVat": 2242.0,
            "TotalGlassCost": 1000.0, "TotalProductionCost": 300.0,
            "Profit": 942.0, "MarginPercent": 42.0,
            "OverallMarginPercent": 42.0, "NetMarginPercent": 40.0,
            "WastagePercent": 15.0, "ComputedAtUtc": "2026-09-01 12:00:00"},
        "TermClauses": [{"Type": "PaymentTerms", "Order": 0,
                         "IsActive": True, "Title": "Payment",
                         "Body": "Net 30", "SubLines": [],
                         "ValidityDays": 7}],
        "PreparedBy": {"PreparedByName": "Juber Saleem",
                       "PreparedByDept": "Facade",
                       "PreparedByStatus": "Estimator", "TermsText": "",
                       "FooterDisclaimer": ""},
    }
    # Glass rows: 3.6 m² × 3 × 110 = 1188.00; 0.96 × 2 × 110 = 211.20
    # → glass subtotal 1399.20. Other charges 572.40 + 50.40 + 70 + 150
    # = 842.80. Recomputed VAT = round2(2242 × 5%) = 112.10; the stored
    # VAT is 113.00 (the deliberate mismatch) and the stored grand
    # total agrees with the STORED vat (2355.00).
    pif = sqlite3.connect(root / "proformainvoices.db")
    pif.executescript("""
    CREATE TABLE ProformaInvoices (Id INTEGER PRIMARY KEY, ProformaId TEXT NOT NULL,
        InquiryId INTEGER, CompanyName TEXT NOT NULL DEFAULT '',
        CustomerName TEXT NOT NULL DEFAULT '', ProjectName TEXT NOT NULL DEFAULT '',
        Status TEXT NOT NULL, IsVoid INTEGER NOT NULL DEFAULT 0, VoidedAtUtc TEXT,
        VoidedBy TEXT, VoidReason TEXT, IssueDate TEXT NOT NULL,
        ValidUntil TEXT NOT NULL, GlassMaterialsSubtotal TEXT NOT NULL,
        OtherChargesSubtotal TEXT NOT NULL, DiscountAmount TEXT NOT NULL,
        VatAmount TEXT NOT NULL, GrandTotalVatInclusive TEXT NOT NULL,
        FullSnapshotJson TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL, RowVersion INTEGER NOT NULL DEFAULT 0,
        RootProformaId TEXT NOT NULL DEFAULT '', RevisionNumber INTEGER NOT NULL DEFAULT 0);
    """)
    pif.executemany(
        "INSERT INTO ProformaInvoices (Id, ProformaId, InquiryId,"
        " CompanyName, CustomerName, ProjectName, Status, IssueDate,"
        " ValidUntil, GlassMaterialsSubtotal, OtherChargesSubtotal,"
        " DiscountAmount, VatAmount, GrandTotalVatInclusive,"
        " FullSnapshotJson, CreatedAt, UpdatedAt, RootProformaId,"
        " RevisionNumber) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "PI-2026-001", 1, "Apex Glazing LLC", "Emaar Properties",
          "Marina Tower Glazing", "Sent", "2026-09-01 00:00:00",
          "2026-09-15 00:00:00", "1399.20", "842.80", "0", "113.00",
          "2355.00", json.dumps(pi_snapshot), "2026-09-01 11:00:00",
          "2026-09-01 12:00:00", "", 0),
         # Revision of PI-2026-001 (minimal but valid snapshot).
         (2, "PI-2026-001-R1", None, "Apex Glazing LLC", "Emaar Properties",
          "Marina Tower Glazing", "Draft", "2026-09-05 00:00:00",
          "2026-09-19 00:00:00", "1399.20", "842.80", "0", "112.10",
          "2354.10", '{"SchemaVersion":5,"FurthestStepReached":2}',
          "2026-09-05 11:00:00", "2026-09-05 11:00:00", "PI-2026-001", 1),
         # Invalid snapshot JSON — the row cannot load — error + skip.
         (3, "PI-2026-002", None, "Apex Glazing LLC", "Walk-in", "",
          "Draft", "2026-09-03 00:00:00", "2026-09-17 00:00:00", "0", "0",
          "0", "0", "0", "not json{{", "2026-09-03 11:00:00",
          "2026-09-03 11:00:00", "", 0),
         # Root not imported + a spec group with a non-CHECK glass type.
         (4, "PI-2026-003", None, "Apex Glazing LLC", "Walk-in", "",
          "Draft", "2026-09-04 00:00:00", "2026-09-18 00:00:00", "100", "0",
          "0", "5", "105",
          json.dumps({"SchemaVersion": 5, "FurthestStepReached": 5,
                      "SpecGroups": [{"GroupNumber": 1,
                                      "SelectedGlassType": "QUAD",
                                      "SelectedSpecName": "Quad build",
                                      "BaseRate": 100.0,
                                      "SurchargePercent": 0,
                                      "ItemRows": []}]}),
          "2026-09-04 11:00:00", "2026-09-04 11:00:00", "PI-2026-999", 1)])
    pif.commit()

    # ── optimization.db (Sessions) ───────────────────────────────────
    op = sqlite3.connect(root / "optimization.db")
    op.executescript("""
    CREATE TABLE Sessions (Id TEXT PRIMARY KEY, Name TEXT NOT NULL DEFAULT '',
        JobOrderRefs TEXT NOT NULL DEFAULT '', EngineStage TEXT NOT NULL DEFAULT 'QUICK',
        EffortLevel INTEGER NOT NULL DEFAULT 0, RotationEnabled INTEGER NOT NULL DEFAULT 1,
        PieceCount INTEGER NOT NULL DEFAULT 0, SheetsUsed INTEGER NOT NULL DEFAULT 0,
        UnplacedCount INTEGER NOT NULL DEFAULT 0, UtilizationPct REAL NOT NULL DEFAULT 0,
        MarginsJson TEXT NOT NULL DEFAULT '{}', StocksJson TEXT NOT NULL DEFAULT '{}',
        PiecesJson TEXT NOT NULL DEFAULT '{}', ResultJson TEXT NOT NULL DEFAULT '{}',
        CreatedUtc TEXT NOT NULL, UpdatedUtc TEXT NOT NULL);
    """)
    op.executemany(
        "INSERT INTO Sessions (Id, Name, JobOrderRefs, EngineStage,"
        " EffortLevel, RotationEnabled, PieceCount, SheetsUsed,"
        " UnplacedCount, UtilizationPct, MarginsJson, StocksJson,"
        " PiecesJson, ResultJson, CreatedUtc, UpdatedUtc)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [("opt-session-1", "Marina cutting plan", "JOB-2026-001",
          "OPTIMIZE", 3, 1, 5, 2, 0, 82.5, "{}",
          json.dumps([{"WidthMm": 3210, "HeightMm": 2250,
                       "ThicknessMm": 6, "Color": "Clear",
                       "Label": "6mm Clear 3210x2250"}]),
          json.dumps([{"JobRef": "JOB-2026-001", "GlassRef": "A-01",
                       "SpecLabel": "Clear 6+12+6", "WidthMm": 1500,
                       "HeightMm": 2400, "PieceLabel": "1"}]),
          json.dumps({"SheetsUsed": 2, "Utilization": 82.5,
                      "Wastage": 17.5,
                      "Sheets": [{"SheetNo": 1,
                                  "Stock": "6mm Clear 3210x2250",
                                  "UtilizationPct": 85.1, "Pieces": 3},
                                 {"SheetNo": 2,
                                  "Stock": "6mm Clear 3210x2250",
                                  "UtilizationPct": 79.9, "Pieces": 2}]}),
          "2026-09-05 08:00:00", "2026-09-05 08:05:00"),
         ("opt-session-2", "Podium quick check", "JOB-2026-001", "QUICK",
          1, 0, 5, 3, 1, 71.2, "{}", "{}", "{}", "{}",
          "2026-09-06 08:00:00", "2026-09-06 08:01:00"),
         # Reserved latest-run plumbing — skipped + counted.
         ("__latest_run__:PI-2026-001", "", "JOB-2026-001", "QUICK", 1, 1,
          5, 2, 0, 80.0, "{}", "{}", "{}", "{}",
          "2026-09-07 08:00:00", "2026-09-07 08:01:00")])
    op.commit()

    # ── joborders.db (JobOrders) ─────────────────────────────────────
    jo = sqlite3.connect(root / "joborders.db")
    jo.executescript("""
    CREATE TABLE JobOrders (Id INTEGER PRIMARY KEY, JobOrderId TEXT NOT NULL,
        SourceProformaId TEXT NOT NULL DEFAULT '', CustomerName TEXT NOT NULL DEFAULT '',
        ProjectName TEXT NOT NULL DEFAULT '', GrandTotalVatInclusive TEXT NOT NULL DEFAULT '0',
        Status TEXT NOT NULL DEFAULT 'Draft', IsVoid INTEGER NOT NULL DEFAULT 0,
        VoidedAtUtc TEXT, VoidedBy TEXT, VoidReason TEXT,
        Priority TEXT NOT NULL DEFAULT 'Medium', DeliveryDate TEXT,
        FullSnapshotJson TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL, RowVersion INTEGER NOT NULL DEFAULT 0);
    """)
    jo.executemany(
        "INSERT INTO JobOrders (Id, JobOrderId, SourceProformaId,"
        " CustomerName, ProjectName, GrandTotalVatInclusive, Status,"
        " Priority, DeliveryDate, FullSnapshotJson, CreatedAt, UpdatedAt)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "JOB-2026-001", "PI-2026-001", "Emaar Properties",
          "Marina Tower Glazing", "2355.00", "In Production", "High",
          "2026-09-20 00:00:00",
          json.dumps({"JobOrderId": "JOB-2026-001",
                      "SourceProformaId": "PI-2026-001",
                      "ConvertedAt": "2026-09-02 09:00:00",
                      "Production": {
                          "Status": "In Production", "Priority": "High",
                          "DeliveryDate": "2026-09-20 00:00:00",
                          "SiteDeliveryContact": "Site Engineer Ali",
                          "SiteDeliveryPhone": "+971501112233",
                          "SiteDeliveryAddress": "Marina Tower, Dubai",
                          "AssignedTo": "Crew A",
                          "CuttingInstructions":
                              "Cut all outer panes first",
                          "DrawingReference": "DWG-MT-101",
                          "InternalNotes": "Double-check sealant",
                          "LastEditedAt": "2026-09-02 10:00:00",
                          "LastEditedBy": "jubers"}}),
          "2026-09-02 09:00:00", "2026-09-02 10:00:00"),
         # Source PI not imported — error + skip (proforma_id NOT NULL).
         (2, "JOB-2026-002", "PI-2026-999", "Walk-in", "", "0", "Draft",
          "Medium", None, "", "2026-09-03 09:00:00",
          "2026-09-03 09:00:00")])
    jo.commit()

    # ── production.db (the four materialized shapes + deferred tables)
    pr = sqlite3.connect(root / "production.db")
    pr.executescript("""
    CREATE TABLE ProductionReleases (Id INTEGER PRIMARY KEY, JobRef TEXT NOT NULL,
        SourceProformaId TEXT NOT NULL DEFAULT '', CustomerName TEXT NOT NULL DEFAULT '',
        ProjectName TEXT NOT NULL DEFAULT '', ItemCount INTEGER NOT NULL DEFAULT 0,
        WarningsJson TEXT NOT NULL DEFAULT '[]', FirstReleasedAt TEXT NOT NULL,
        LastReleasedAt TEXT NOT NULL);
    CREATE TABLE ProductionWorkItems (Id INTEGER PRIMARY KEY, UniqueKey TEXT NOT NULL,
        JobRef TEXT NOT NULL, SourceProformaId TEXT NOT NULL DEFAULT '',
        CustomerName TEXT NOT NULL DEFAULT '', ProjectName TEXT NOT NULL DEFAULT '',
        Salesman TEXT NOT NULL DEFAULT '', SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SpecName TEXT NOT NULL DEFAULT '', BuildType TEXT NOT NULL DEFAULT '',
        SerialNumber INTEGER NOT NULL DEFAULT 0, GlassRef TEXT NOT NULL DEFAULT '',
        Position TEXT NOT NULL DEFAULT '', WidthMm INTEGER NOT NULL DEFAULT 0,
        HeightMm INTEGER NOT NULL DEFAULT 0, Qty INTEGER NOT NULL DEFAULT 0,
        QtyDone INTEGER NOT NULL DEFAULT 0, UnitSqm TEXT NOT NULL DEFAULT '0',
        TotalSqm TEXT NOT NULL DEFAULT '0', TotalLm TEXT NOT NULL DEFAULT '0',
        Treatment TEXT NOT NULL DEFAULT '', Stage INTEGER NOT NULL DEFAULT 1,
        Status INTEGER NOT NULL DEFAULT 0, PredecessorStage TEXT NOT NULL DEFAULT '',
        ChargeTemplateKey TEXT NOT NULL DEFAULT '', ChargeMetricQty TEXT NOT NULL DEFAULT '0',
        ChargeMetricUnit TEXT NOT NULL DEFAULT '', PolishSubTypeLabel TEXT NOT NULL DEFAULT '',
        Notes TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
        CompletedAt TEXT, CompletedBy TEXT NOT NULL DEFAULT '', IsFoc INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE ProductionEntries (Id INTEGER PRIMARY KEY, WorkItemId INTEGER NOT NULL,
        JobRef TEXT NOT NULL DEFAULT '', Stage INTEGER NOT NULL DEFAULT 1,
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0, SerialNumber INTEGER NOT NULL DEFAULT 0,
        Position TEXT NOT NULL DEFAULT '', SpecName TEXT NOT NULL DEFAULT '',
        GlassRef TEXT NOT NULL DEFAULT '', Qty INTEGER NOT NULL DEFAULT 0,
        DoneBy TEXT NOT NULL DEFAULT '', DoneAt TEXT NOT NULL);
    CREATE TABLE MaterialReservations (Id INTEGER PRIMARY KEY, ReservationId TEXT NOT NULL,
        JobRef TEXT NOT NULL, SourceProformaId TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0, SerialNumber INTEGER NOT NULL DEFAULT 0,
        GlassRef TEXT NOT NULL DEFAULT '', SpecName TEXT NOT NULL DEFAULT '',
        OptimizationSessionId TEXT NOT NULL DEFAULT '', GlassSheetId INTEGER NOT NULL DEFAULT 0,
        StockSizeLabel TEXT NOT NULL DEFAULT '', WidthMm INTEGER NOT NULL DEFAULT 0,
        HeightMm INTEGER NOT NULL DEFAULT 0, ThicknessMm INTEGER NOT NULL DEFAULT 0,
        Color TEXT NOT NULL DEFAULT '', Quantity INTEGER NOT NULL DEFAULT 0,
        Status INTEGER NOT NULL DEFAULT 1, CreatedBy TEXT NOT NULL DEFAULT '',
        CreatedAt TEXT NOT NULL, ReleasedAt TEXT, ReleaseReason TEXT NOT NULL DEFAULT '',
        Notes TEXT NOT NULL DEFAULT '');
    CREATE TABLE Workstations (Id INTEGER PRIMARY KEY,
        WorkstationId TEXT NOT NULL, Code TEXT NOT NULL DEFAULT '',
        Name TEXT NOT NULL, ProcessType INTEGER NOT NULL DEFAULT 1,
        ProcessName TEXT NOT NULL DEFAULT '',
        IsActive INTEGER NOT NULL DEFAULT 1,
        Description TEXT NOT NULL DEFAULT '',
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL);
    -- 26a port tables (WPF SavedProductionPhase16/17 shapes — the
    -- columns proglass_migrate SELECTs).
    CREATE TABLE ProcessExecutions (Id INTEGER PRIMARY KEY,
        ExecutionId TEXT NOT NULL, JobRef TEXT NOT NULL DEFAULT '',
        SourceProformaId TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SpecName TEXT NOT NULL DEFAULT '',
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        Position TEXT NOT NULL DEFAULT '', PieceId TEXT NOT NULL DEFAULT '',
        InputPieceId TEXT NOT NULL DEFAULT '',
        OutputPieceId TEXT NOT NULL DEFAULT '',
        ProcessType INTEGER NOT NULL DEFAULT 1,
        Sequence INTEGER NOT NULL DEFAULT 0,
        InputWorkItemId INTEGER NOT NULL DEFAULT 0,
        OutputWorkItemId INTEGER NOT NULL DEFAULT 0,
        Status INTEGER NOT NULL DEFAULT 0,
        StartedBy TEXT NOT NULL DEFAULT '', StartedAt TEXT,
        CompletedBy TEXT NOT NULL DEFAULT '', CompletedAt TEXT,
        QtyRequired INTEGER NOT NULL DEFAULT 0,
        QtyDone INTEGER NOT NULL DEFAULT 0,
        EntryType INTEGER NOT NULL DEFAULT 0,
        Reason TEXT NOT NULL DEFAULT '', Notes TEXT NOT NULL DEFAULT '',
        QcStatus INTEGER NOT NULL DEFAULT 0,
        QcId TEXT NOT NULL DEFAULT '',
        ParentExecutionId TEXT NOT NULL DEFAULT '',
        ReworkSequence INTEGER NOT NULL DEFAULT 0,
        IsFoc INTEGER NOT NULL DEFAULT 0,
        PreviousStage TEXT NOT NULL DEFAULT '',
        NextStage TEXT NOT NULL DEFAULT '',
        ProcessDetailJson TEXT NOT NULL DEFAULT '',
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL);
    CREATE TABLE ProcessPairings (Id INTEGER PRIMARY KEY,
        PairingId TEXT NOT NULL, JobRef TEXT NOT NULL DEFAULT '',
        SourceProformaId TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        Position TEXT NOT NULL DEFAULT '', PairId TEXT NOT NULL DEFAULT '',
        OuterPieceId TEXT NOT NULL DEFAULT '',
        InnerPieceId TEXT NOT NULL DEFAULT '',
        OuterGlassRef TEXT NOT NULL DEFAULT '',
        InnerGlassRef TEXT NOT NULL DEFAULT '',
        OuterWorkItemId INTEGER NOT NULL DEFAULT 0,
        InnerWorkItemId INTEGER NOT NULL DEFAULT 0,
        OuterExecutionId TEXT NOT NULL DEFAULT '',
        InnerExecutionId TEXT NOT NULL DEFAULT '',
        Status INTEGER NOT NULL DEFAULT 0,
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        Notes TEXT NOT NULL DEFAULT '',
        IsFoc INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE ProcessAssemblies (Id INTEGER PRIMARY KEY,
        AssemblyId TEXT NOT NULL, JobRef TEXT NOT NULL DEFAULT '',
        SourceProformaId TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        Position TEXT NOT NULL DEFAULT '',
        AssemblyLabel TEXT NOT NULL DEFAULT '',
        AssemblyType TEXT NOT NULL DEFAULT 'DGU',
        ComponentsJson TEXT NOT NULL DEFAULT '[]',
        SpacerInfo TEXT NOT NULL DEFAULT '',
        InterlayerInfo TEXT NOT NULL DEFAULT '',
        SealantInfo TEXT NOT NULL DEFAULT '',
        PairingId TEXT NOT NULL DEFAULT '',
        Status INTEGER NOT NULL DEFAULT 0,
        QtyRequired INTEGER NOT NULL DEFAULT 1,
        QtyDone INTEGER NOT NULL DEFAULT 1,
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        CompletedAt TEXT, Notes TEXT NOT NULL DEFAULT '',
        IsFoc INTEGER NOT NULL DEFAULT 0, QcId TEXT NOT NULL DEFAULT '');
    CREATE TABLE ProductionControlHolds (Id INTEGER PRIMARY KEY,
        HoldId TEXT NOT NULL, WorkItemId INTEGER NOT NULL DEFAULT 0,
        JobRef TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        Position TEXT NOT NULL DEFAULT '', Stage INTEGER NOT NULL DEFAULT 1,
        HoldReason TEXT NOT NULL DEFAULT '',
        HeldBy TEXT NOT NULL DEFAULT '', HeldAt TEXT,
        ReleasedBy TEXT NOT NULL DEFAULT '', ReleasedAt TEXT,
        IsReleased INTEGER NOT NULL DEFAULT 0,
        Notes TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL);
    CREATE TABLE ProductionControlAssignments (Id INTEGER PRIMARY KEY,
        AssignmentId TEXT NOT NULL, WorkItemId INTEGER NOT NULL DEFAULT 0,
        JobRef TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        Position TEXT NOT NULL DEFAULT '', Stage INTEGER NOT NULL DEFAULT 1,
        WorkstationId TEXT NOT NULL DEFAULT '',
        Operator TEXT NOT NULL DEFAULT '',
        AssignedBy TEXT NOT NULL DEFAULT '', AssignedAt TEXT,
        UnassignedBy TEXT NOT NULL DEFAULT '', UnassignedAt TEXT,
        IsActive INTEGER NOT NULL DEFAULT 1,
        Notes TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL);
    CREATE TABLE ProductionControlOverrides (Id INTEGER PRIMARY KEY,
        ControlId TEXT NOT NULL, WorkItemId INTEGER NOT NULL DEFAULT 0,
        JobRef TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        Position TEXT NOT NULL DEFAULT '', Stage INTEGER NOT NULL DEFAULT 1,
        Priority INTEGER NOT NULL DEFAULT 0, DueDate TEXT,
        PrioritySetBy TEXT NOT NULL DEFAULT '', PrioritySetAt TEXT,
        Notes TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL);
    -- 26b port tables (WPF Phase 14/15 shapes).
    CREATE TABLE MaterialAllocations (Id INTEGER PRIMARY KEY,
        AllocationId TEXT NOT NULL, ReservationId TEXT NOT NULL DEFAULT '',
        JobRef TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        OptimizationSessionId TEXT NOT NULL DEFAULT '',
        SheetNo INTEGER NOT NULL DEFAULT 1,
        PlacedPieceLabel TEXT NOT NULL DEFAULT '',
        GlassRef TEXT NOT NULL DEFAULT '', WidthMm INTEGER NOT NULL DEFAULT 0,
        HeightMm INTEGER NOT NULL DEFAULT 0,
        Quantity INTEGER NOT NULL DEFAULT 1,
        UtilizationPct TEXT NOT NULL DEFAULT '0',
        WastagePct TEXT NOT NULL DEFAULT '0',
        Status INTEGER NOT NULL DEFAULT 2,
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        ConsumedAt TEXT, Notes TEXT NOT NULL DEFAULT '');
    CREATE TABLE ProductionActualCosts (Id INTEGER PRIMARY KEY,
        CostId TEXT NOT NULL, JobRef TEXT NOT NULL DEFAULT '',
        SourceProformaId TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        GlassRef TEXT NOT NULL DEFAULT '',
        SpecName TEXT NOT NULL DEFAULT '',
        CostType INTEGER NOT NULL DEFAULT 1,
        StockSheetId INTEGER NOT NULL DEFAULT 0,
        MaterialAllocationId TEXT NOT NULL DEFAULT '',
        Quantity TEXT NOT NULL DEFAULT '0',
        UnitCost TEXT NOT NULL DEFAULT '0',
        TotalCost TEXT NOT NULL DEFAULT '0',
        SellingValue TEXT NOT NULL DEFAULT '0',
        IsFoc INTEGER NOT NULL DEFAULT 0,
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        Notes TEXT NOT NULL DEFAULT '');
    CREATE TABLE ProductionReplacements (Id INTEGER PRIMARY KEY,
        ReplacementId TEXT NOT NULL, JobRef TEXT NOT NULL DEFAULT '',
        SourceProformaId TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        GlassRef TEXT NOT NULL DEFAULT '',
        SpecName TEXT NOT NULL DEFAULT '',
        Position TEXT NOT NULL DEFAULT '', Stage INTEGER NOT NULL DEFAULT 1,
        OriginalWorkItemId INTEGER NOT NULL DEFAULT 0,
        OriginalEntryId INTEGER NOT NULL DEFAULT 0,
        ReplacementWorkItemId INTEGER NOT NULL DEFAULT 0,
        EntryType INTEGER NOT NULL DEFAULT 2,
        Reason TEXT NOT NULL DEFAULT '', Quantity INTEGER NOT NULL DEFAULT 1,
        IsFoc INTEGER NOT NULL DEFAULT 0,
        ReplacementSequence INTEGER NOT NULL DEFAULT 1,
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        Status INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE CuttingSessions (Id INTEGER PRIMARY KEY,
        CuttingSessionId TEXT NOT NULL, JobRef TEXT NOT NULL DEFAULT '',
        SourceProformaId TEXT NOT NULL DEFAULT '',
        OptimizationSessionId TEXT NOT NULL DEFAULT '',
        MaterialGroupId TEXT NOT NULL DEFAULT '',
        TotalPieces INTEGER NOT NULL DEFAULT 0,
        SheetsUsed INTEGER NOT NULL DEFAULT 0,
        UtilizationPct TEXT NOT NULL DEFAULT '0',
        WastagePct TEXT NOT NULL DEFAULT '0',
        Status INTEGER NOT NULL DEFAULT 0,
        RotationPolicy INTEGER NOT NULL DEFAULT 1,
        SplitMode INTEGER NOT NULL DEFAULT 0,
        EffortLevel INTEGER NOT NULL DEFAULT 3,
        MarginProfileJson TEXT NOT NULL DEFAULT '',
        IsVerified INTEGER NOT NULL DEFAULT 0,
        VerificationResult TEXT NOT NULL DEFAULT '',
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        CompletedAt TEXT, Notes TEXT NOT NULL DEFAULT '');
    CREATE TABLE CuttingSheets (Id INTEGER PRIMARY KEY,
        CuttingSheetId TEXT NOT NULL, CuttingSessionId TEXT NOT NULL,
        JobRef TEXT NOT NULL DEFAULT '', SheetNo INTEGER NOT NULL DEFAULT 1,
        StockSheetId INTEGER NOT NULL DEFAULT 0,
        StockSizeLabel TEXT NOT NULL DEFAULT '',
        StockWidthMm INTEGER NOT NULL DEFAULT 0,
        StockHeightMm INTEGER NOT NULL DEFAULT 0,
        ThicknessMm INTEGER NOT NULL DEFAULT 0,
        Color TEXT NOT NULL DEFAULT '',
        UsableX INTEGER NOT NULL DEFAULT 0,
        UsableY INTEGER NOT NULL DEFAULT 0,
        UsableWidth INTEGER NOT NULL DEFAULT 0,
        UsableHeight INTEGER NOT NULL DEFAULT 0,
        UsedSqm TEXT NOT NULL DEFAULT '0',
        StockSqm TEXT NOT NULL DEFAULT '0',
        UtilizationPct TEXT NOT NULL DEFAULT '0',
        WastagePct TEXT NOT NULL DEFAULT '0',
        IsVerified INTEGER NOT NULL DEFAULT 0,
        VerificationResult TEXT NOT NULL DEFAULT '',
        PlacedPieceCount INTEGER NOT NULL DEFAULT 0,
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL);
    CREATE TABLE CuttingPlacedPieces (Id INTEGER PRIMARY KEY,
        PlacedPieceId TEXT NOT NULL, CuttingSheetId TEXT NOT NULL,
        CuttingSessionId TEXT NOT NULL,
        JobRef TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        GlassRef TEXT NOT NULL DEFAULT '',
        SpecName TEXT NOT NULL DEFAULT '',
        Position TEXT NOT NULL DEFAULT '', PieceId TEXT NOT NULL DEFAULT '',
        PieceLabel TEXT NOT NULL DEFAULT '',
        WidthMm INTEGER NOT NULL DEFAULT 0,
        HeightMm INTEGER NOT NULL DEFAULT 0,
        X INTEGER NOT NULL DEFAULT 0, Y INTEGER NOT NULL DEFAULT 0,
        IsRotated INTEGER NOT NULL DEFAULT 0,
        CutSequence INTEGER NOT NULL DEFAULT 0,
        Status INTEGER NOT NULL DEFAULT 0,
        SourceWorkItemId INTEGER NOT NULL DEFAULT 0,
        ReservationId TEXT NOT NULL DEFAULT '',
        AllocationId TEXT NOT NULL DEFAULT '',
        IsFoc INTEGER NOT NULL DEFAULT 0,
        CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        Notes TEXT NOT NULL DEFAULT '');
    """)
    pr.execute(
        "INSERT INTO ProductionReleases (JobRef, SourceProformaId,"
        " CustomerName, ProjectName, ItemCount, WarningsJson,"
        " FirstReleasedAt, LastReleasedAt) VALUES (?,?,?,?,?,?,?,?)",
        ("JOB-2026-001", "PI-2026-001", "Emaar Properties",
         "Marina Tower Glazing", 2, "[]", "2026-09-02 09:30:00",
         "2026-09-02 09:30:00"))
    pr.executemany(
        "INSERT INTO ProductionWorkItems (Id, UniqueKey, JobRef,"
        " SourceProformaId, CustomerName, ProjectName, Salesman,"
        " SpecGroupNumber, SpecName, BuildType, SerialNumber, GlassRef,"
        " WidthMm, HeightMm, Qty, QtyDone, UnitSqm, TotalSqm, TotalLm,"
        " Treatment, Stage, Status, CreatedAt, UpdatedAt, CompletedAt,"
        " CompletedBy) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "JOB-2026-001|Clear 6+12+6|1||Cutting", "JOB-2026-001",
          "PI-2026-001", "Emaar Properties", "Marina Tower Glazing",
          "Ahmed", 1, "Clear 6+12+6", "DGU", 1, "A-01", 1500, 2400, 3, 3,
          "3.600", "10.800", "23.400", "ANNEALED", 1, 2,
          "2026-09-02 10:00:00", "2026-09-03 10:00:00",
          "2026-09-03 10:00:00", "cutter1"),
         (2, "JOB-2026-001|Clear 6+12+6|2||Cutting", "JOB-2026-001",
          "PI-2026-001", "Emaar Properties", "Marina Tower Glazing",
          "Ahmed", 1, "Clear 6+12+6", "DGU", 2, "A-02", 800, 1200, 2, 1,
          "0.960", "1.920", "8.000", "ANNEALED", 1, 1,
          "2026-09-02 10:00:00", "2026-09-03 11:00:00", None, ""),
         # Status 99 is outside WorkItemStatus — error + skip.
         (3, "JOB-2026-001|bad|9||Cutting", "JOB-2026-001", "PI-2026-001",
          "Emaar Properties", "Marina Tower Glazing", "Ahmed", 1,
          "Clear 6+12+6", "DGU", 9, "X-99", 100, 100, 1, 0, "0.010",
          "0.010", "0.400", "", 1, 99, "2026-09-02 10:00:00",
          "2026-09-02 10:00:00", None, "")])
    pr.executemany(
        "INSERT INTO ProductionEntries (Id, WorkItemId, JobRef, Stage,"
        " SpecGroupNumber, SerialNumber, SpecName, GlassRef, Qty, DoneBy,"
        " DoneAt) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
        [(1, 1, "JOB-2026-001", 1, 1, 1, "Clear 6+12+6", "A-01", 3,
          "cutter1", "2026-09-03 10:00:00"),
         # Work item 77 was never imported — error + skip.
         (2, 77, "JOB-2026-001", 1, 1, 2, "Clear 6+12+6", "A-02", 1,
          "cutter1", "2026-09-03 11:00:00")])
    pr.executemany(
        "INSERT INTO MaterialReservations (Id, ReservationId, JobRef,"
        " SourceProformaId, SpecGroupNumber, SerialNumber, GlassRef,"
        " SpecName, OptimizationSessionId, GlassSheetId, StockSizeLabel,"
        " WidthMm, HeightMm, ThicknessMm, Color, Quantity, Status,"
        " CreatedBy, CreatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1", "JOB-2026-001",
          "PI-2026-001", 1, 1, "A-01", "Clear 6+12+6", "opt-session-1", 1,
          "6mm Clear 3210x2250", 3210, 2250, 6, "Clear", 2, 1, "jubers",
          "2026-09-05 09:00:00"),
         # Glass sheet 99 not imported — error + skip.
         (2, "JOB-2026-001|Clear 6+12+6|2|99|opt-session-1|1", "JOB-2026-001",
          "PI-2026-001", 1, 2, "A-02", "Clear 6+12+6", "opt-session-1", 99,
          "8mm Bronze 2250x3210", 2250, 3210, 8, "Bronze", 1, 1, "jubers",
          "2026-09-05 09:05:00")])
    pr.execute(
        "INSERT INTO Workstations (WorkstationId, Code, Name, ProcessType,"
        " ProcessName, IsActive, Description, CreatedBy, CreatedAt,"
        " UpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?)",
        ("WS-TEMP-01", "TMP-01", "Tempering Furnace 1", 6, "Tempering", 1,
         "Left furnace", "jubers", "2026-09-01 08:00:00",
         "2026-09-01 08:00:00"))
    pr.executemany(
        "INSERT INTO ProcessExecutions (Id, ExecutionId, JobRef,"
        " SourceProformaId, SpecGroupNumber, SpecName, SerialNumber,"
        " Position, PieceId, InputPieceId, OutputPieceId, ProcessType,"
        " Sequence, InputWorkItemId, OutputWorkItemId, Status, StartedBy,"
        " StartedAt, CompletedBy, CompletedAt, QtyRequired, QtyDone,"
        " EntryType, Reason, Notes, QcStatus, QcId, ParentExecutionId,"
        " ReworkSequence, IsFoc, PreviousStage, NextStage,"
        " ProcessDetailJson, CreatedBy, CreatedAt, UpdatedAt)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "EXEC-2026-0001", "JOB-2026-001", "PI-2026-001", 1,
          "Clear 6+12+6", 1, "1", "PIECE-A-01", "", "PIECE-A-01", 1, 1, 1,
          1, 2, "cutter1", "2026-09-03 09:30:00", "cutter1",
          "2026-09-03 10:00:00", 3, 3, 0, "", "Auto execution", 1, "", "",
          0, 0, "", "Cutting", "", "cutter1", "2026-09-03 09:30:00",
          "2026-09-03 10:00:00"),
         # EntryType 99 is outside ProductionEntryType — error + skip.
         (2, "EXEC-2026-0002", "JOB-2026-001", "PI-2026-001", 1,
          "Clear 6+12+6", 1, "1", "PIECE-A-02", "", "", 1, 2, 2, 2, 1,
          "cutter1", "2026-09-03 11:30:00", "", None, 2, 0, 99, "", "", 0,
          "", "", 0, 0, "", "Polish", "", "cutter1",
          "2026-09-03 11:30:00", "2026-09-03 11:30:00")])
    pr.execute(
        "INSERT INTO ProcessPairings (PairingId, JobRef,"
        " SourceProformaId, SpecGroupNumber, SerialNumber, Position,"
        " PairId, OuterPieceId, InnerPieceId, OuterGlassRef,"
        " InnerGlassRef, OuterWorkItemId, InnerWorkItemId,"
        " OuterExecutionId, InnerExecutionId, Status, CreatedBy,"
        " CreatedAt, Notes, IsFoc) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("PAIR-2026-0001", "JOB-2026-001", "PI-2026-001", 1, 1, "1",
         "JOB-2026-001|Clear 6+12+6|1", "OUT-A-01", "IN-A-01", "A-01",
         "A-02", 1, 2, "EXEC-2026-0001", "", 2, "jubers",
         "2026-09-03 12:00:00", "First DGU pair", 0))
    pr.execute(
        "INSERT INTO ProcessAssemblies (AssemblyId, JobRef,"
        " SourceProformaId, SpecGroupNumber, SerialNumber, Position,"
        " AssemblyLabel, AssemblyType, ComponentsJson, SpacerInfo,"
        " InterlayerInfo, SealantInfo, PairingId, Status, QtyRequired,"
        " QtyDone, CreatedBy, CreatedAt, CompletedAt, Notes, IsFoc, QcId)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("ASM-2026-0001", "JOB-2026-001", "PI-2026-001", 1, 1, "1",
         "DGU-1", "DGU", "[]", "12mm Aluminium", "", "Polysulfide",
         "PAIR-2026-0001", 2, 1, 1, "jubers", "2026-09-03 12:30:00",
         "2026-09-03 13:00:00", "", 0, ""))
    pr.executemany(
        "INSERT INTO ProductionControlHolds (HoldId, WorkItemId, JobRef,"
        " SpecGroupNumber, SerialNumber, Position, Stage, HoldReason,"
        " HeldBy, HeldAt, ReleasedBy, ReleasedAt, IsReleased, Notes,"
        " CreatedAt, UpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [("HOLD-2026-0001", 2, "JOB-2026-001", 1, 2, "2", 6,
          "Edge chip spotted before tempering", "jubers",
          "2026-09-04 09:00:00", "jubers", "2026-09-04 10:00:00", 1,
          "Released after re-check", "2026-09-04 09:00:00",
          "2026-09-04 10:00:00"),
         # Work item 77 was never imported — error + skip.
         ("HOLD-2026-0002", 77, "JOB-2026-001", 1, 2, "2", 6,
          "Hold on a ghost work item", "jubers", "2026-09-04 11:00:00",
          "", None, 0, "", "2026-09-04 11:00:00",
          "2026-09-04 11:00:00")])
    pr.execute(
        "INSERT INTO ProductionControlAssignments (AssignmentId,"
        " WorkItemId, JobRef, SpecGroupNumber, SerialNumber, Position,"
        " Stage, WorkstationId, Operator, AssignedBy, AssignedAt,"
        " UnassignedBy, UnassignedAt, IsActive, Notes, CreatedAt,"
        " UpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("ASSIGN-2026-0001", 2, "JOB-2026-001", 1, 2, "2", 6,
         "WS-TEMP-01", "cutter1", "jubers", "2026-09-04 12:00:00", "",
         None, 1, "", "2026-09-04 12:00:00", "2026-09-04 12:00:00"))
    pr.execute(
        "INSERT INTO ProductionControlOverrides (ControlId, WorkItemId,"
        " JobRef, SpecGroupNumber, SerialNumber, Position, Stage,"
        " Priority, DueDate, PrioritySetBy, PrioritySetAt, Notes,"
        " CreatedAt, UpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("CTRL-2026-0001", 2, "JOB-2026-001", 1, 2, "2", 6, 2,
         "2026-09-10 14:00:00", "jubers", "2026-09-04 13:00:00",
         "Customer escalation", "2026-09-04 13:00:00",
         "2026-09-04 13:00:00"))
    pr.executemany(
        "INSERT INTO MaterialAllocations (AllocationId, ReservationId,"
        " JobRef, SpecGroupNumber, SerialNumber, OptimizationSessionId,"
        " SheetNo, PlacedPieceLabel, GlassRef, WidthMm, HeightMm,"
        " Quantity, UtilizationPct, WastagePct, Status, CreatedBy,"
        " CreatedAt, ConsumedAt, Notes)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(# Links the imported reservation 1 (26b id scheme:
          # {reservationKey}|S{sheetNo}|{label}).
         "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1|S1|A-01",
         "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1", "JOB-2026-001",
         1, 1, "opt-session-1", 1, "A-01", "A-01", 1500, 2400, 1, "55.80",
         "44.20", 3, "jubers", "2026-09-05 09:10:00",
         "2026-09-05 11:00:00", ""),
         # Reservation was never imported — error finding, but the row
         # is kept with a null link (the WPF key survives in
         # reservation_key).
         ("JOB-2026-001|Clear 6+12+6|1|1|opt-session-9|9|S1|X-99",
          "JOB-2026-001|Clear 6+12+6|1|1|opt-session-9|9", "JOB-2026-001",
          1, 1, "opt-session-9", 1, "X-99", "X-99", 800, 1200, 1, "40.00",
          "60.00", 2, "jubers", "2026-09-05 09:15:00", None, "orphan")])
    pr.execute(
        "INSERT INTO ProductionActualCosts (CostId, JobRef,"
        " SourceProformaId, SpecGroupNumber, SerialNumber, GlassRef,"
        " SpecName, CostType, StockSheetId, MaterialAllocationId,"
        " Quantity, UnitCost, TotalCost, SellingValue, IsFoc, CreatedBy,"
        " CreatedAt, Notes) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("JOB-2026-001|Clear 6+12+6|1|ActualMaterial", "JOB-2026-001",
         "PI-2026-001", 1, 1, "A-01", "Clear 6+12+6", 1, 1,
         "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1|S1|A-01", "3.600",
         "95.50", "343.80", "504.00", 0, "jubers",
         "2026-09-05 11:05:00", ""))
    pr.execute(
        "INSERT INTO ProductionReplacements (ReplacementId, JobRef,"
        " SourceProformaId, SpecGroupNumber, SerialNumber, GlassRef,"
        " SpecName, Position, Stage, OriginalWorkItemId, OriginalEntryId,"
        " ReplacementWorkItemId, EntryType, Reason, Quantity, IsFoc,"
        " ReplacementSequence, CreatedBy, CreatedAt, Status)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("JOB-2026-001|Clear 6+12+6|1|1|Cutting|RPL:1", "JOB-2026-001",
         "PI-2026-001", 1, 1, "A-01", "Clear 6+12+6", "1", 1, 1, 1, 2, 2,
         "Chipped edge at cutting", 1, 0, 1, "cutter1",
         "2026-09-05 14:00:00", 2))
    pr.execute(
        "INSERT INTO CuttingSessions (CuttingSessionId, JobRef,"
        " SourceProformaId, OptimizationSessionId, MaterialGroupId,"
        " TotalPieces, SheetsUsed, UtilizationPct, WastagePct, Status,"
        " RotationPolicy, SplitMode, EffortLevel, MarginProfileJson,"
        " IsVerified, VerificationResult, CreatedBy, CreatedAt,"
        " CompletedAt, Notes) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("CUT-JOB-2026-001-1-20260905090000", "JOB-2026-001",
         "PI-2026-001", "opt-session-1", "Clear 6+12+6|1", 5, 1, "64.80",
         "35.20", 6, 1, 0, 3, "", 1, "passed", "jubers",
         "2026-09-05 09:00:00", "2026-09-05 11:00:00", ""))
    pr.execute(
        "INSERT INTO CuttingSheets (CuttingSheetId, CuttingSessionId,"
        " JobRef, SheetNo, StockSheetId, StockSizeLabel, StockWidthMm,"
        " StockHeightMm, ThicknessMm, Color, UsableX, UsableY,"
        " UsableWidth, UsableHeight, UsedSqm, StockSqm, UtilizationPct,"
        " WastagePct, IsVerified, VerificationResult, PlacedPieceCount,"
        " CreatedBy, CreatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("CUT-JOB-2026-001-1-20260905090000|S1",
         "CUT-JOB-2026-001-1-20260905090000", "JOB-2026-001", 1, 1,
         "6mm Clear 3210x2250", 3210, 2250, 6, "Clear", 0, 0, 3210, 2250,
         "4.680", "7.2225", "64.80", "35.20", 1, "passed", 1, "jubers",
         "2026-09-05 09:05:00"))
    pr.execute(
        "INSERT INTO CuttingPlacedPieces (PlacedPieceId, CuttingSheetId,"
        " CuttingSessionId, JobRef, SpecGroupNumber, SerialNumber,"
        " GlassRef, SpecName, Position, PieceId, PieceLabel, WidthMm,"
        " HeightMm, X, Y, IsRotated, CutSequence, Status,"
        " SourceWorkItemId, ReservationId, AllocationId, IsFoc,"
        " CreatedBy, CreatedAt, Notes)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        ("CUT-JOB-2026-001-1-20260905090000|S1|PIECE-A-01",
         "CUT-JOB-2026-001-1-20260905090000|S1",
         "CUT-JOB-2026-001-1-20260905090000", "JOB-2026-001", 1, 1,
         "A-01", "Clear 6+12+6", "1", "PIECE-A-01", "A-01", 1500, 2400,
         100, 100, 0, 1, 4, 1,
         "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1",
         "JOB-2026-001|Clear 6+12+6|1|1|opt-session-1|1|S1|A-01", 0,
         "jubers", "2026-09-05 09:06:00", ""))
    pr.commit()

    # ── deliverynotes.db (DeliveryNotes / DeliveryNoteItems) ─────────
    dn = sqlite3.connect(root / "deliverynotes.db")
    dn.executescript("""
    CREATE TABLE DeliveryNotes (Id INTEGER PRIMARY KEY, DeliveryNoteId TEXT NOT NULL,
        JobOrderId TEXT NOT NULL DEFAULT '', ProformaId TEXT NOT NULL DEFAULT '',
        SequenceNumber INTEGER NOT NULL DEFAULT 0, CustomerName TEXT NOT NULL DEFAULT '',
        ProjectName TEXT NOT NULL DEFAULT '', Status TEXT NOT NULL DEFAULT 'Draft',
        IsVoid INTEGER NOT NULL DEFAULT 0, VoidedAtUtc TEXT, VoidedBy TEXT,
        VoidReason TEXT, DeliveryType TEXT NOT NULL DEFAULT 'Original',
        IsFoc INTEGER NOT NULL DEFAULT 0, IsMasterDn INTEGER NOT NULL DEFAULT 0,
        LastUpdatedFromDnId TEXT NOT NULL DEFAULT '', DriverName TEXT NOT NULL DEFAULT '',
        VehicleNumber TEXT NOT NULL DEFAULT '', ReceiverName TEXT NOT NULL DEFAULT '',
        DeliveryDate TEXT NOT NULL, DeliveryTime TEXT NOT NULL DEFAULT '',
        Notes TEXT NOT NULL DEFAULT '', TotalOrderedQty INTEGER NOT NULL DEFAULT 0,
        TotalDeliveredQty INTEGER NOT NULL DEFAULT 0, TotalBalanceQty INTEGER NOT NULL DEFAULT 0,
        TotalPreviouslyDeliveredQty INTEGER NOT NULL DEFAULT 0,
        TaxInvoiceId TEXT NOT NULL DEFAULT '', HasTaxInvoice INTEGER NOT NULL DEFAULT 0,
        FullSnapshotJson TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL, RowVersion INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE DeliveryNoteItems (Id INTEGER PRIMARY KEY, DeliveryNoteId TEXT NOT NULL,
        JobOrderId TEXT NOT NULL DEFAULT '', SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SpecGroupName TEXT NOT NULL DEFAULT '', SerialNumber INTEGER NOT NULL DEFAULT 0,
        GlassRef TEXT NOT NULL DEFAULT '', W1 INTEGER NOT NULL DEFAULT 0,
        H1 INTEGER NOT NULL DEFAULT 0, W2 INTEGER NOT NULL DEFAULT 0,
        H2 INTEGER NOT NULL DEFAULT 0, OrderedQty INTEGER NOT NULL DEFAULT 0,
        PreviouslyDeliveredQty INTEGER NOT NULL DEFAULT 0, DeliveredQty INTEGER NOT NULL DEFAULT 0,
        BalanceQty INTEGER NOT NULL DEFAULT 0, ReworkQty INTEGER NOT NULL DEFAULT 0,
        ReturnedQty INTEGER NOT NULL DEFAULT 0, WeightPerPiece TEXT NOT NULL DEFAULT '0',
        Notes TEXT NOT NULL DEFAULT '');
    CREATE TABLE DeliveryNoteAttachments (Id INTEGER PRIMARY KEY, DeliveryNoteId TEXT NOT NULL,
        FileName TEXT NOT NULL, StoragePath TEXT NOT NULL, FileSize INTEGER NOT NULL DEFAULT 0,
        UploadedBy TEXT NOT NULL DEFAULT '', UploadedAt TEXT NOT NULL);
    """)
    dn.executemany(
        "INSERT INTO DeliveryNotes (Id, DeliveryNoteId, JobOrderId,"
        " ProformaId, SequenceNumber, CustomerName, ProjectName, Status,"
        " DeliveryType, IsMasterDn, DriverName, VehicleNumber,"
        " ReceiverName, DeliveryDate, DeliveryTime, Notes,"
        " TotalOrderedQty, TotalDeliveredQty, TotalBalanceQty,"
        " FullSnapshotJson, CreatedAt, UpdatedAt)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "MDN-2026-001", "JOB-2026-001", "PI-2026-001", 0,
          "Emaar Properties", "Marina Tower Glazing", "Draft", "Dummy", 1,
          "", "", "", "2026-09-02 00:00:00", "",
          "Master DN — auto-generated reference tracker", 5, 0, 5,
          '{"master": true}', "2026-09-02 09:00:00",
          "2026-09-02 09:00:00"),
         (2, "DN-2026-001-01", "JOB-2026-001", "PI-2026-001", 1,
          "Emaar Properties", "Marina Tower Glazing", "Dispatching",
          "Original", 0, "Rashid", "D-12345", "Ali",
          "2026-09-05 00:00:00", "10:30", "First dispatch", 5, 3, 2,
          '{"lines": []}', "2026-09-05 08:00:00",
          "2026-09-05 08:00:00"),
         # Unknown delivery type — error + skip.
         (3, "DN-2026-009-01", "JOB-2026-001", "PI-2026-001", 9,
          "Emaar Properties", "Marina Tower Glazing", "Dispatching",
          "Teleport", 0, "", "", "", "2026-09-06 00:00:00", "", "", 0, 0,
          0, "", "2026-09-06 08:00:00", "2026-09-06 08:00:00")])
    dn.executemany(
        "INSERT INTO DeliveryNoteItems (Id, DeliveryNoteId, JobOrderId,"
        " SpecGroupNumber, SpecGroupName, SerialNumber, GlassRef, W1, H1,"
        " OrderedQty, PreviouslyDeliveredQty, DeliveredQty, BalanceQty,"
        " WeightPerPiece) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [(1, "MDN-2026-001", "JOB-2026-001", 1, "Clear 6+12+6", 1, "A-01",
          1500, 2400, 3, 0, 0, 3, "18.000"),
         (2, "MDN-2026-001", "JOB-2026-001", 1, "Clear 6+12+6", 2, "A-02",
          800, 1200, 2, 0, 0, 2, "6.400"),
         (3, "DN-2026-001-01", "JOB-2026-001", 1, "Clear 6+12+6", 1,
          "A-01", 1500, 2400, 3, 0, 3, 0, "18.000"),
         (4, "DN-2026-001-01", "JOB-2026-001", 1, "Clear 6+12+6", 2,
          "A-02", 800, 1200, 2, 0, 0, 2, "6.400"),
         # Item on the SKIPPED note DN-2026-009-01 — error + skip.
         (5, "DN-2026-009-01", "JOB-2026-001", 1, "Clear 6+12+6", 1,
          "A-01", 1500, 2400, 3, 0, 3, 0, "18.000")])
    # Phase 24: attachment registry rows — signed.pdf resolves via the
    # AttachmentService PDFs layout (registered size deliberately ≠ the
    # on-disk bytes: the drift finding path), site-photo.jpg via Images
    # with an UNIMPORTED uploader (stored without an uploader), the
    # missing-reference row has no file on disk (error + skip), and the
    # orphan row sits on the skipped note DN-2026-009-01 (error + skip).
    dn.executemany(
        "INSERT INTO DeliveryNoteAttachments (DeliveryNoteId, FileName,"
        " StoragePath, FileSize, UploadedBy, UploadedAt)"
        " VALUES (?,?,?,?,?,?)",
        [("DN-2026-001-01", "signed.pdf",
          "DeliveryNotes/DN-2026-001-01/signed.pdf", 102400, "jubers",
          "2026-09-05 12:00:00"),
         ("DN-2026-001-01", "site-photo.jpg",
          "DeliveryNotes/Attachments/Images/DN-2026-001-01/site-photo.jpg",
          2048, "ghost", "2026-09-05 12:30:00"),
         ("DN-2026-001-01", "missing-reference.pdf",
          "DeliveryNotes/DN-2026-001-01/missing-reference.pdf", 512,
          "jubers", "2026-09-05 13:00:00"),
         ("DN-2026-009-01", "orphan.pdf",
          "DeliveryNotes/Attachments/PDFs/DN-2026-009-01/orphan.pdf", 256,
          "jubers", "2026-09-06 09:00:00")])
    dn.commit()

    # ── attachment bytes on disk (read-only input for module 13) ─────
    att_root = root / "DeliveryNotes" / "Attachments"
    (att_root / "PDFs" / "DN-2026-001-01").mkdir(parents=True, exist_ok=True)
    (att_root / "Images" / "DN-2026-001-01").mkdir(parents=True, exist_ok=True)
    (att_root / "PDFs" / "DN-2026-001-01" / "signed.pdf").write_bytes(
        b"%PDF-1.4\n% ProGlass fixture\n" + b"0" * 484)   # 512 bytes
    (att_root / "Images" / "DN-2026-001-01" / "site-photo.jpg").write_bytes(
        b"\xff\xd8\xff\xe0" + b"J" * 2044)              # 2048 bytes
    (root / "Attachments" / "INQ-2026-001").mkdir(parents=True, exist_ok=True)
    (root / "Attachments" / "INQ-2026-001" / "boq.pdf").write_bytes(
        b"%PDF-1.4\n% BOQ\n" + b"B" * 20465)              # 20480 bytes

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
        BankName TEXT NOT NULL DEFAULT '', BankAccountNumber TEXT NOT NULL DEFAULT '',
        Iban TEXT NOT NULL DEFAULT '', SwiftCode TEXT NOT NULL DEFAULT '',
        FullSnapshotJson TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
        UpdatedAt TEXT NOT NULL, RowVersion INTEGER NOT NULL DEFAULT 0);
    """)
    ti.executemany(
        "INSERT INTO TaxInvoices (InvoiceNumber, ProformaId, Status,"
        " CustomerName, ProjectName, CompanyName, CompanyTrn,"
        " PurchaseOrderNumber, BankName, BankAccountNumber, Iban,"
        " SwiftCode, IssueDate, DueDate, PaymentTerms,"
        " Subtotal, DiscountAmount, DiscountPercent, AfterDiscount,"
        " VatAmount, GrandTotal, TotalSqm, FullSnapshotJson, CreatedAt,"
        " UpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [("INV-2026-001", "PI-2026-001", "Paid", "Emaar Properties",
          "Marina Tower Glazing", "ProGlass Automation LLC", "1001234567893",
          "PO-EM-4471", "Emirates NBD", "AE-001-2345678",
          "AE070331234567890123456", "EBILAEAD",
          "2026-08-01 00:00:00",
          "2026-08-31 00:00:00", "Net 30", "10000", "0", "0", "10000",
          "500", "10500", "12.5", '{"lines":[]}', "2026-08-01 10:00:00",
          "2026-08-31 10:00:00"),
         # VAT mismatch fixture: 900 after discount → 45 expected.
         ("INV-2026-002", "PI-2026-001", "Issued", "Emaar Properties",
          "Marina Tower Glazing", "ProGlass Automation LLC", "1001234567893",
          "", "", "", "", "",
          "2026-09-01 00:00:00",
          "2026-10-01 00:00:00", "Net 30", "1000", "100", "10", "900",
          "44", "944", "1.25", "", "2026-09-01 10:00:00",
          "2026-09-01 10:00:00"),
         # Unknown status fixture — must be reported + skipped.
         ("INV-2026-003", "", "SomewhereElse", "Walk-in", "",
          "", "", "", "", "", "", "",
          "2026-09-02 00:00:00", "2026-10-02 00:00:00", "Net 30", "10",
          "0", "0", "10", "0.50", "10.50", "0", "", "2026-09-02 10:00:00",
          "2026-09-02 10:00:00")])
    ti.executescript("""
    CREATE TABLE TaxInvoiceItems (Id INTEGER PRIMARY KEY,
        InvoiceNumber TEXT NOT NULL, ProformaId TEXT NOT NULL DEFAULT '',
        SpecGroupNumber INTEGER NOT NULL DEFAULT 0,
        SpecGroupName TEXT NOT NULL DEFAULT '',
        GlassType TEXT NOT NULL DEFAULT '',
        SerialNumber INTEGER NOT NULL DEFAULT 0,
        GlassRef TEXT NOT NULL DEFAULT '',
        W1 INTEGER NOT NULL DEFAULT 0, H1 INTEGER NOT NULL DEFAULT 0,
        W2 INTEGER NOT NULL DEFAULT 0, H2 INTEGER NOT NULL DEFAULT 0,
        Qty INTEGER NOT NULL DEFAULT 1,
        Sqm TEXT NOT NULL DEFAULT '0', TotalSqm TEXT NOT NULL DEFAULT '0',
        Rate TEXT NOT NULL DEFAULT '0', Value TEXT NOT NULL DEFAULT '0',
        Notes TEXT NOT NULL DEFAULT '');
    """)
    # INV-2026-001 carries the WPF's exact over-invoice scenario material:
    # ordered 2 (PI group 1 serial 1), delivered 1 (DN), invoiced 2 here.
    ti.executemany(
        "INSERT INTO TaxInvoiceItems (InvoiceNumber, ProformaId,"
        " SpecGroupNumber, SpecGroupName, GlassType, SerialNumber,"
        " GlassRef, W1, H1, W2, H2, Qty, Sqm, TotalSqm, Rate, Value)"
        " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
        [("INV-2026-001", "PI-2026-001", 1, "Clear Double Glazed",
          "Clear", 1, "GR-101", 1000, 500, 0, 0, 2,
          "0.5", "1.0", "200", "400"),
         ("INV-2026-001", "PI-2026-001", 2, "Tinted Single",
          "Tinted", 1, "GR-201", 800, 600, 0, 0, 1,
          "0.48", "0.48", "180", "86.40")])
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

    for conn in (audit, md, pg, sp, iq, pif, op, jo, pr, dn, ti, cn, st):
        conn.close()
    print(f"fixture data root built at {root}")
    return str(root)


if __name__ == "__main__":
    import sys
    build(Path(sys.argv[1] if len(sys.argv) > 1 else "fixture-data-root"))
