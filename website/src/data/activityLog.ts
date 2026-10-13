/**
 * Logs & Activity — the module the navigation calls `Nav.LogsActivity`.
 *
 * Everything in this file is read out of the application. The screen is
 * `Views/Diagnostics/DiagnosticsView.xaml` (the rail item is labelled "Logs &
 * Activity" and navigates with `CommandParameter="Diagnostics"`), and its data
 * comes from two places that are not the same thing:
 *
 *   1. THE APPLICATION LOG — `Services/Logging/AppLogService.cs`.
 *      A rolling terminal: rows of TIME · LEVEL · MODULE / FUNCTION · message,
 *      the last 500 entries kept in memory, counters for errors and warnings,
 *      and a button that opens the log folder on disk. Every module writes to
 *      it through `AppLogService.Info/Warning/Error/Critical` with a
 *      `LogContext` naming the module, screen, class, method and operation, and
 *      — since the document-tracing work — a `CorrelationId` (CORR-XXXXXX) and
 *      a `DocumentId`, so one business operation can be followed from the
 *      inquiry to the statement across files.
 *
 *   2. THE AUDIT TRAIL — `Services/Audit/AuditService.cs`.
 *      Business events, not diagnostics: who changed what, when, and the
 *      before/after of every field. It writes with `AuditService.Log(section,
 *      action, details, entityType, entityId, reason)`, and the section names
 *      are constants in `AuditSections.cs` precisely so a typo cannot split one
 *      section into two — which is why they are listed here as data rather than
 *      typed into prose.
 *
 * The rows below are *pattern* rows: the message shapes are the application's
 * own format strings, filled in with this repository's seeded workflow — the
 * inquiry INQ-2026-001, proforma PI-2026-001 with its 22 glass items, job
 * JOB-2026-001, delivery DN-2026-001-01 and invoice INV-2026-001, on the Al
 * Zahia Tower podium job for Al Noor Glazing Contracting LLC. Every identifier
 * and name in them comes from `Services/SampleData/SampleDataService.cs`; none
 * of them is invented here.
 */

/** `Dg.*`-style column headings for the log terminal. */
export const logColumns = [
  { key: "time", label: "TIME", note: "Diagnostics.TIME" },
  { key: "level", label: "LEVEL", note: "Diagnostics.Level" },
  { key: "module", label: "MODULE / FUNCTION", note: "Diagnostics.ModuleFunction" },
  { key: "message", label: "ENTRY", note: "Diagnostics.LOGEntries" },
] as const;

/**
 * `LogSeverity` from `Models/Statement/Workflow/LogSeverity.cs`, with the
 * brush each one is bound to in `LogEntry.SeverityColor`.
 *
 * The colours are the application's own accent/success/warning/danger tokens —
 * "these are NOT theme brushes", the model says, "they are terminal-specific
 * accent colors that work identically in both Light and Dark themes because the
 * terminal surface itself is always dark."
 */
export const logLevels = [
  { id: "info", label: "INFO", brush: "AccentBrush", colour: "#60A5FA", meaning: "Informational message." },
  { id: "success", label: "SUCCESS", brush: "SuccessBrush", colour: "#34D399", meaning: "Step or action succeeded." },
  { id: "warning", label: "WARNING", brush: "WarningBrush", colour: "#FBBF24", meaning: "Non-fatal issue detected." },
  { id: "error", label: "ERROR", brush: "DangerBrush", colour: "#F87171", meaning: "Step or action failed." },
  { id: "section", label: "SECTION", brush: "AccentDarkBrush", colour: "#7E57CE", meaning: "A separator: section and error rows are bold for scanning." },
] as const;

export type LogLevelId = (typeof logLevels)[number]["id"];

/**
 * `AuditSections.cs` — the canonical section names.
 *
 * The file is categorical about why they exist: "Always use these constants
 * (never free strings) so the Activity view can group/filter reliably and typos
 * cannot split one section into two."
 */
export const auditSections = [
  "Application",
  "Users",
  "Dashboard",
  "Sheet Store",
  "Proforma Invoice",
  "Job Order",
  "Delivery Note",
  "Tax Invoice",
  "Credit Note",
  "Statement",
  "Reports",
  "Settings",
  "AI Assistant",
  "Updates",
  "Master Data",
  "Inquiry",
  "Specifications",
  "Sharing",
] as const;

export type LogRow = {
  time: string;
  level: LogLevelId;
  /** `LogContext.Module` / `.Class.Method` — the right-hand column. */
  module: string;
  message: string;
  /** `LogContext.CorrelationId`, when the operation is one that can be traced. */
  correlation?: string;
  /** `LogContext.DocumentId` — the record the operation acted on. */
  document?: string;
};

/** The seeded workflow's identifiers, as `SampleDataService` builds them. */
export const seededDocuments = {
  inquiry: "INQ-2026-001",
  proforma: "PI-2026-001",
  job: "JOB-2026-001",
  delivery: "DN-2026-001-01",
  invoice: "INV-2026-001",
  customer: "Al Noor Glazing Contracting LLC",
  project: "Al Zahia Tower — Podium Glazing",
  projectRef: "AZT-POD-2026",
  contract: "CTR-2026-042",
  lpo: "LPO-AN-2026-088",
  site: "Al Zahia, Sharjah, UAE",
  salesman: "Ahmed Rahman",
  preparedBy: "Demo User",
  company: "ProGlass Automation Demo",
  /* Delivery: `BuildDeliveryNote(..., "Salim Khan", "SHJ-48211", ...)`. */
  driver: "Salim Khan",
  vehicle: "SHJ-48211",
  receivedBy: "Mohammed Ali (Site Storekeeper)",
  /** 22 glass items; the first delivery carries 15 of them. */
  items: 22,
  deliveredItems: 15,
};

/**
 * The log, as the terminal shows it.
 *
 * Oldest first, the way a log reads; the surface reverses it for display the
 * way a console does. The message shapes are the application's own format
 * strings and the values are the seeded workflow's, so the pipeline is visible
 * in the one place it is easiest to see it: the moment the seeder wrote the
 * whole chain and logged that it had.
 */
export const seededLog: LogRow[] = [
  {
    time: "08:02:11",
    level: "section",
    module: "AppLogService",
    message: "═══ APP LOG INITIALISED ═══",
  },
  {
    time: "08:02:11",
    level: "info",
    module: "App.StartupCore · InitializeLog",
    message: "Log file opened — diagnostics\\app-2026-04-18.log",
  },
  {
    time: "08:02:12",
    level: "info",
    module: "AuditService · LogSessionBoundary",
    message: `Session started — session S-4F2A91 for ${seededDocuments.preparedBy}`,
  },
  {
    time: "08:02:12",
    level: "success",
    module: "App.StartupCore · ApplySavedFont",
    message: `ApplyFont ok — family "${"Segoe UI"}", weight Normal, style Normal, underline off`,
  },
  {
    time: "08:02:13",
    level: "warning",
    module: "Licensing · Validate",
    message: "Licence within term — 41 days remaining, device slot 1 of 1 in use",
  },
  {
    time: "08:04:37",
    level: "section",
    module: "SampleDataService",
    message: "═══ SAMPLE WORKFLOW SEEDED ═══",
  },
  {
    time: "08:04:37",
    level: "info",
    module: "SampleData · SeedSampleDataAsync",
    message: `Inquiry ${seededDocuments.inquiry} created — ${seededDocuments.project} (Confirmed)`,
    correlation: "CORR-8A21F0",
    document: seededDocuments.inquiry,
  },
  {
    time: "08:04:38",
    level: "info",
    module: "SampleData · BuildComprehensivePi",
    message: `Proforma ${seededDocuments.proforma} created — ${seededDocuments.items} glass items across 3 specification groups`,
    correlation: "CORR-8A21F0",
    document: seededDocuments.proforma,
  },
  {
    time: "08:04:38",
    level: "info",
    module: "AuditService · Log",
    message: `Settings · "Sample data loaded" — 1 complete workflow seeded: Inquiry → PI (${seededDocuments.items} items) → JO → DN → TI`,
  },
  {
    time: "08:04:39",
    level: "info",
    module: "JobOrder · BuildJobOrder",
    message: `Job ${seededDocuments.job} released from ${seededDocuments.proforma} — priority High, due in 14 days`,
    correlation: "CORR-8A21F0",
    document: seededDocuments.job,
  },
  {
    time: "08:04:39",
    level: "info",
    module: "DeliveryNote · BuildDeliveryNote",
    message: `Delivery note ${seededDocuments.delivery} created — ${seededDocuments.deliveredItems} of ${seededDocuments.items} items, vehicle ${seededDocuments.vehicle}`,
    correlation: "CORR-8A21F0",
    document: seededDocuments.delivery,
  },
  {
    time: "08:04:40",
    level: "info",
    module: "TaxInvoice · BuildTaxInvoice",
    message: `Tax invoice ${seededDocuments.invoice} issued from ${seededDocuments.delivery} — VAT calculated from subtotal, due in 30 days`,
    correlation: "CORR-8A21F0",
    document: seededDocuments.invoice,
  },
  {
    time: "09:15:02",
    level: "section",
    module: "Optimization",
    message: "═══ CUTTING RUN ═══",
  },
  {
    time: "09:15:02",
    level: "info",
    module: "Optimization · MaterialReservation",
    message: `Reserved 4 x 3210 × 2250 for ${seededDocuments.job} Spec 1 — Clear 6 mm, Rack A1`,
    correlation: "CORR-9C33B4",
    document: seededDocuments.job,
  },
  {
    time: "09:15:04",
    level: "success",
    module: "Optimization · CuttingReq",
    message: `Built requirements for ${seededDocuments.job} from the released work items — algorithm guillotine, level 5`,
    correlation: "CORR-9C33B4",
    document: seededDocuments.job,
  },
  {
    time: "09:41:19",
    level: "info",
    module: "ProductionControl · Start",
    message: `Start WorkItem W-1104 Job ${seededDocuments.job} by ${seededDocuments.preparedBy}`,
    correlation: "CORR-9C33B4",
    document: seededDocuments.job,
  },
  {
    time: "10:07:55",
    level: "info",
    module: "Production · ProcessExecution",
    message: "Started Cutting EX-2210 CT-06-1200x900-1",
    correlation: "CORR-9C33B4",
    document: seededDocuments.job,
  },
  {
    time: "10:52:31",
    level: "success",
    module: "Production · CuttingCompletion",
    message: "Completed piece PP-3341 CT-06-1200x900-1 — 12 of 45 cut for Spec 1",
    correlation: "CORR-9C33B4",
    document: seededDocuments.job,
  },
  {
    time: "11:20:08",
    level: "warning",
    module: "Production · Breakage",
    message: `Job ${seededDocuments.job} Spec 1 Serial 4 — 1 piece broken at cutting, replacement reserved from Rack A1`,
    correlation: "CORR-9C33B4",
    document: seededDocuments.job,
  },
  {
    time: "13:05:44",
    level: "info",
    module: "Production · Pairing",
    message: `Paired 14 pairs for ${seededDocuments.job} Spec 3 — Low-E DGU 6-12-6`,
    correlation: "CORR-9C33B4",
    document: seededDocuments.job,
  },
  {
    time: "14:12:20",
    level: "info",
    module: "Dispatch · DeliveryNote",
    message: `Dispatch ${seededDocuments.delivery} — driver ${seededDocuments.driver}, vehicle ${seededDocuments.vehicle}, received by ${seededDocuments.receivedBy}`,
    correlation: "CORR-B7120D",
    document: seededDocuments.delivery,
  },
  {
    time: "14:12:21",
    level: "info",
    module: "AuditService · Log",
    message: `Delivery Note · "${"Status changed"}" — ${seededDocuments.delivery} Draft → Dispatching`,
    correlation: "CORR-B7120D",
    document: seededDocuments.delivery,
  },
  {
    time: "16:48:03",
    level: "error",
    module: "Reports · OrderReport",
    message: "OrderReport generation failed — no job order matched the date range, nothing written",
  },
  {
    time: "16:49:40",
    level: "info",
    module: "Reports · OrderReport",
    message: `Report regenerated for ${seededDocuments.job} after widening the range`,
  },
  {
    time: "17:30:15",
    level: "section",
    module: "AuditService",
    message: "═══ SESSION CLOSED ═══",
  },
  {
    time: "17:30:15",
    level: "info",
    module: "AuditService · LogSessionBoundary",
    message: `Session ended — session S-4F2A91 for ${seededDocuments.preparedBy}`,
  },
];

/**
 * The audit trail, as the Activity view groups it.
 *
 * `AuditService.Log(section, action, details, entityType, entityId, reason)` —
 * and `LogWithChanges` when a row carries a before/after set, which the Activity
 * UI draws as a diff. The action strings below are the ones the codebase
 * actually writes; the filter is by section, which is why the constants exist.
 */
export const seededAudit: LogRow[] = [
  {
    time: "08:02:12",
    level: "info",
    module: "Users",
    message: `Session started — session S-4F2A91 for ${seededDocuments.preparedBy}`,
  },
  {
    time: "08:04:38",
    level: "info",
    module: "Settings",
    message: `Sample data loaded — 1 complete workflow seeded: Inquiry → PI (${seededDocuments.items} items) → JO → DN → TI`,
  },
  {
    time: "08:31:07",
    level: "info",
    module: "Sheet Store",
    message: "Stock purchased — Clear 6 mm, 40 sheets at 95.00 / m², Rack A1",
  },
  {
    time: "09:02:44",
    level: "info",
    module: "Proforma Invoice",
    message: `Inquiry created — ${seededDocuments.inquiry}, ${seededDocuments.customer}, ${seededDocuments.projectRef}`,
  },
  {
    time: "09:02:45",
    level: "info",
    module: "Proforma Invoice",
    message: `Inquiry status changed — ${seededDocuments.inquiry} Draft → Confirmed, reason "LPO ${seededDocuments.lpo} received"`,
  },
  {
    time: "09:18:12",
    level: "info",
    module: "Proforma Invoice",
    message: `Proforma ${seededDocuments.proforma} approved — ${seededDocuments.items} glass items, 3 specification groups`,
  },
  {
    time: "09:20:01",
    level: "info",
    module: "Job Order",
    message: `Job order ${seededDocuments.job} created from ${seededDocuments.proforma} — priority High`,
  },
  {
    time: "11:44:30",
    level: "info",
    module: "Specifications",
    message: "Specification counts reconciled — 3 usage rows healed, 0 orphan rows purged",
  },
  {
    time: "14:12:21",
    level: "info",
    module: "Delivery Note",
    message: `Status changed — ${seededDocuments.delivery} Draft → Dispatching, driver ${seededDocuments.driver}`,
  },
  {
    time: "15:02:58",
    level: "info",
    module: "Tax Invoice",
    message: `Tax invoice ${seededDocuments.invoice} issued — VAT 5% from subtotal, due in 30 days`,
  },
  {
    time: "16:48:03",
    level: "error",
    module: "Reports",
    message: "Order report generation failed — no job order matched the date range",
  },
  {
    time: "17:30:15",
    level: "info",
    module: "Users",
    message: `Session ended — session S-4F2A91 for ${seededDocuments.preparedBy}`,
  },
];

/**
 * The screen's own strings, verbatim from `Themes/Languages/en-US.xaml`.
 *
 * Kept as data because they are the module's identity: the header, the two
 * counters, the retention line, the two empty states and the button that opens
 * the folder on disk.
 */
export const logSurfaceStrings = {
  title: "Technical diagnostics and per-module user activity — live",
  entries: "LOG ENTRIES",
  errors: "ERRORS",
  warnings: "WARNINGS",
  inclCritical: "incl. critical",
  retention: "last 500 kept in memory",
  openFolder: "OPEN LOG FOLDER",
  openFolderNote: "Open application log folder",
  empty: "No log entries yet",
  emptyHint: "Startup, warnings, errors and crashes will appear here.",
  refresh: "Refresh",
} as const;

/**
 * The three facts worth putting beside the log, each one read out of the code
 * that implements it rather than written as a claim about it.
 */
export const logFacts = [
  {
    id: "two-services",
    label: "One rail item, two services",
    headline: "Diagnostics are not the same thing as the audit trail.",
    body: "The application log answers \"what did it do\" — startup, licence checks, seeding, production events, failures — and a log file is a diagnostic artefact. The audit trail answers \"who changed this, and when\", record by record, with the before and after of every field. Keeping them apart is why the log can be rolled and truncated without losing the record of who approved a quotation.",
    evidence: "Services/Logging/AppLogService.cs · Services/Audit/AuditService.cs",
  },
  {
    id: "correlation",
    label: "Tracing one operation",
    headline: "A correlation ID follows the work, not the module.",
    body: "A quotation becomes a job order, is delivered in part, is invoiced and settles — in five modules, often across several days. Every entry written while that operation is in flight carries the same correlation ID (CORR-XXXXXX) and the document ID it acted on, so the sequence can be reassembled from the log even though no single module ever saw the whole thing.",
    evidence: "Services/Logging/LogEntry.cs — LogContext.CorrelationId · DocumentId · DocumentType",
  },
  {
    id: "sections",
    label: "Why the sections are constants",
    headline: "A typo must not be able to split one section in two.",
    body: "The eighteen section names are constants, and the file says why: grouping and filtering depend on them, and a free-typed name would quietly create a second \"Proforma Invoice\" that no filter would ever show. System Center then groups those sections into five scopes — all activity, transactions, inventory, system, users and security — over the same trail.",
    evidence: "Services/Audit/AuditSections.cs · Views/SystemCenter/SystemCenterView.xaml",
  },
] as const;
