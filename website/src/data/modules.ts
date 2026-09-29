/**
 * Module registry.
 *
 * `moduleKey` values are the exact strings the application's navigation rail
 * sends to `MainViewModel.Navigate` (MainWindow.xaml → CommandParameter).
 * `railGroup` follows the real sidebar grouping in MainWindow.xaml.
 * Every capability line below is a feature that exists in the WPF source; the
 * `evidence` field names where to look.
 */

export type ModuleGroup = "Main" | "Sales & Orders" | "Production" | "Dispatch" | "Finance" | "Reports" | "Master Data" | "System";

export type ProductModule = {
  id: string;
  number: string;
  name: string;
  /** Verbatim navigation-rail label from Themes/Languages/en-US.xaml. */
  railLabel: string;
  moduleKey: string;
  railGroup: ModuleGroup;
  /** One sentence: what the module actually is. */
  summary: string;
  /** Two or three sentences of product explanation, kept factual. */
  detail: string;
  capabilities: string[];
  /** Repo paths that back the claims above. */
  evidence: string;
  /** Screenshot registry id, when a real asset exists. */
  screenshotId?: string;
  /** Illustration asset for the module card, when one exists. */
  illustration?: { src: string; alt: string };
  href: string;
  tags: string[];
};

export const modules: ProductModule[] = [
  {
    id: "dashboard",
    number: "01",
    name: "Dashboard",
    railLabel: "Dashboard",
    moduleKey: "Dashboard",
    railGroup: "Main",
    summary: "Seven module dashboards on one shared grammar, plus an order-to-cash pipeline rail.",
    detail:
      "Overview, Proforma Invoice, Job Order, Tax Invoice, Delivery Note, Statement and Sheet Store each get their own dashboard built from the same kit. Every page renders all four states — loading, error, empty and data — so a quiet week never looks like a broken screen.",
    capabilities: [
      "Order-to-cash pipeline rail: Quote → Production → Delivery → Invoice → Payment",
      "Per-module KPI cards, trend and bar chart controls",
      "Activity feed and cross-module statistics",
      "Loading, error, empty and data states on every page",
    ],
    evidence: "Views/Dashboard/, ViewModels/Dashboard/, Styles/DashboardKit.xaml",
    screenshotId: "overview-dashboard",
    href: "/features#dashboard",
    tags: ["KPI", "Overview"],
  },
  {
    id: "inquiries",
    number: "02",
    name: "Inquiries",
    railLabel: "Inquiry",
    moduleKey: "Inquiry",
    railGroup: "Sales & Orders",
    summary: "A pre-sales register that carries a scope of supply into the quotation.",
    detail:
      "An inquiry holds the customer, project, priority, salesman and a scope-of-supply (BOQ) line list: build type, specification, colours, W×H, quantity and expected rate. Converting it to a proforma copies those rows onto the spec grid atomically — a row that cannot be imported stays on the inquiry as a note rather than being dropped or guessed at.",
    capabilities: [
      "Scope of supply (BOQ) line list with build type, spec, colours, W×H, qty and expected rate",
      "Attachments with SHA-256 manifest deduplication",
      "Lifecycle log with status vocabulary Received · Prepared · Sent · Confirmed · Hold · Cancelled",
      "Permission-gated, logged conversion into a proforma invoice",
    ],
    evidence: "Views/Inquiry/, Services/Inquiry/, Services/ProformaInvoice/InquiryBoqConversion.cs",
    href: "/features#inquiries",
    tags: ["Pre-sales", "BOQ", "Conversion"],
  },
  {
    id: "proforma",
    number: "03",
    name: "Proforma Invoice",
    railLabel: "Proforma Invoice",
    moduleKey: "ProformaInvoice",
    railGroup: "Sales & Orders",
    summary: "The 9-step estimation wizard where the quotation is priced.",
    detail:
      "Company, client, project, pricing, spec grid, charges, summary, attachments and profit & loss. Step 5 holds the multi-spec grid (W1/H1/W2/H2/qty → SQM + value) with Excel-like paste; Step 6 auto-imports the sheet colours used in Step 5; Step 9 is the application's single pricing engine, and everything downstream reads its snapshot rather than recomputing.",
    capabilities: [
      "Nine steps: Company · Client · Project · Pricing · Spec Grid · Charges · Summary · Attachments · Profit & Loss",
      "Rates per build type: SGU, DGU, LAM, DGU+LAM",
      "Multi-spec grid with Excel-like paste and BOQ import",
      "VAT 5% round-safe totals, discount, predefined / wastage / custom charges",
      "Step 9 P&L snapshot: sheet cost, buy-vs-sell cost basis, target price, wastage accounting",
    ],
    evidence: "Views/ProformaInvoice/Step1…Step9, Services/ProformaInvoice/",
    screenshotId: "proforma-wizard",
    illustration: {
      src: "/images/glass3d/dgu-3d-1536.webp",
      alt: "Double-glazed unit illustration shipped with the ProGlass application",
    },
    href: "/features#proforma",
    tags: ["Estimation", "Quotation", "Wizard"],
  },
  {
    id: "specifications",
    number: "04",
    name: "Specifications",
    railLabel: "Specifications",
    moduleKey: "Specifications",
    railGroup: "Main",
    summary: "A reusable glass specification catalogue that lives inside the pricing step.",
    detail:
      "Specifications you build once are stored with their usage history and offered live in Step 5, so a repeat build is a selection rather than a re-entry. The catalogue tracks where each specification has been used.",
    capabilities: [
      "Saved specification library with usage tracking",
      "Live in the proforma Step 5 spec grid",
      "Price history per specification",
    ],
    evidence: "Views/Specifications/, Services/Specifications/, specifications.db",
    href: "/features#specifications",
    tags: ["Library", "Reuse"],
  },
  {
    id: "job-order",
    number: "05",
    name: "Job Order",
    railLabel: "Job Order",
    moduleKey: "JobOrder",
    railGroup: "Sales & Orders",
    summary: "The factory workload view, converted from a confirmed proforma.",
    detail:
      "A job order carries real grid geometry across from the proforma — sheet selections and spacer gaps survive the handoff — plus priority and production status. The saved list and detail panes track status through InProduction · Completed · Delivered · Approved.",
    capabilities: [
      "Converted from the proforma with a persisted source reference",
      "Priority and production status tracking",
      "Factory sheet and saved job order list",
      "Version history and activity per job order",
    ],
    evidence: "Views/JobOrder/, Services/JobOrder/Mapper/JobOrderMapper.cs",
    href: "/features#job-order",
    tags: ["Production", "Workload"],
  },
  {
    id: "optimization",
    number: "06",
    name: "Optimization",
    railLabel: "Optimization",
    moduleKey: "Optimization",
    railGroup: "Production",
    summary: "A guillotine cutting-layout engine that turns stock plates and cutting lists into nested sheets.",
    detail:
      "Eight workspace tabs take the job from confirmed orders to placed pieces. The engine honours per-thickness edge trims, blade kerf and minimum breakout, then verifies every plan with two independent oracles before it can be exported or fed to production.",
    capabilities: [
      "Guillotine nesting across five stage families plus an automatic mode",
      "Per-thickness safety edges, kerf and minimum breakout",
      "Six plate patterns and six cutting-complexity levels",
      "Two oracles: geometry and piece conservation",
      "CNC export: Bottero .OPT · Intermac .ISO · Lisec .DAT · generic DXF",
    ],
    evidence: "Services/Optimization/, Views/Optimization/, ViewModels/Optimization/",
    screenshotId: "optimization-2d",
    href: "/optimization",
    tags: ["Cutting", "Nesting", "CNC"],
  },
  {
    id: "production",
    number: "07",
    name: "Production Stages",
    railLabel: "Production",
    moduleKey: "ProcessWorkCenter",
    railGroup: "Production",
    summary: "Eight shop-floor workstations with a Process Work Center and a Production Control Center.",
    detail:
      "Cutting, Holes & Cutouts, Polish, Edge Deletion, Mitring, Tempering, Pairing and Doubling / Lamination each have their own workstation view. The Production Control Center assigns work, starts and completes tasks, holds and releases jobs, and records a blocked reason against the job.",
    capabilities: [
      "Workstations: Cutting · Holes & Cutouts · Polish · Edge Deletion · Mitring · Tempering · Pairing · Doubling / Lamination",
      "Production Control Center: assign, start, complete, hold, release hold, priority, stage",
      "Blocked reasons recorded against the job (materials, customer clarification, documentation, equipment)",
      "Per-stage views with their own operational detail",
    ],
    evidence: "Views/Production/, ViewModels/Production/",
    screenshotId: "production-stages",
    href: "/features#production",
    tags: ["Shop floor", "Stages", "Control"],
  },
  {
    id: "sheet-store",
    number: "08",
    name: "Sheet Store",
    railLabel: "Sheet Store",
    moduleKey: "SheetStore",
    railGroup: "Main",
    summary: "Raw glass inventory with purchase and usage movements, priced both ways.",
    detail:
      "Every sheet is stored with category, colour, thickness and dimensions, a purchase price per SQM and a sell price per SQM, a reorder level and a running balance. Buys and uses are recorded as movements, so the balance can be read at cost and at sell and the consumption analysed per job or invoice reference.",
    capabilities: [
      "Stock rows: item, category, colour, thickness, W×H, part number, reorder level",
      "Record purchase and record usage movements with a balance-after",
      "Purchase / SQM and sell / SQM, balance at cost and balance at sell",
      "Consumption and movements history, analysis view, potential profit and profit margin",
      "CSV / Excel import and PDF export",
    ],
    evidence: "Views/SheetStore/, Services/SheetStore/, proglass.db",
    screenshotId: "sheet-store-overview",
    href: "/features#sheet-store",
    tags: ["Stock", "Purchasing", "Consumption"],
  },
  {
    id: "delivery-note",
    number: "09",
    name: "Delivery Note",
    railLabel: "Delivery Note",
    moduleKey: "DeliveryNote",
    railGroup: "Dispatch",
    summary: "Multi-movement dispatch with pending-piece tracking and a signed trail.",
    detail:
      "A job can be delivered in several movements. Balance tracking lives in one place and the validation service prevents over-delivery, so the maths is double-count-safe. Each delivery note can move through Draft · Dispatching · Dispatched · Signed · Received · Delivered.",
    capabilities: [
      "Multi-movement dispatch accumulating from the job order",
      "Pending-piece and balance tracking with over-delivery prevention",
      "Delivery types and free-of-charge rows",
      "Attachments, version history, PDF / Excel / CSV export",
      "GODVIEW cross-document inspection",
    ],
    evidence: "Views/DeliveryNote/, Services/DeliveryNote/DeliveryNoteValidationService.cs",
    screenshotId: "delivery-note",
    href: "/features#delivery",
    tags: ["Dispatch", "Balance"],
  },
  {
    id: "tax-invoice",
    number: "10",
    name: "Tax Invoice",
    railLabel: "Tax Invoice",
    moduleKey: "TaxInvoice",
    railGroup: "Sales & Orders",
    summary: "VAT-compliant invoicing converted from a proforma or a delivery-note family.",
    detail:
      "Tax invoices carry their own numbering family and sequence, support partial and incremental invoicing, and track status through Issued · Paid · Overdue with version history. One centralised VAT calculator handles every money path so rounding cannot drift between screens.",
    capabilities: [
      "Converted from a proforma or a delivery-note family with persisted references",
      "Invoice family + sequence numbering, including partial and incremental invoices",
      "Customer TRN and VAT-compliant fields, VAT 5% with one rounding rule",
      "Status Issued · Paid · Overdue, version history, attachment store",
    ],
    evidence: "Views/TaxInvoice/, Services/TaxInvoice/, Services/Reporting/VatCalculator.cs",
    href: "/features#tax-invoice",
    tags: ["Invoicing", "VAT"],
  },
  {
    id: "statement",
    number: "11",
    name: "Statement",
    railLabel: "Statement Generator",
    moduleKey: "StatementGenerator",
    railGroup: "Finance",
    summary: "A step-by-step customer statement built from the invoice record.",
    detail:
      "The statement generator aggregates a customer's tax invoices into an account statement with running balances and ageing. Statements show balances rather than driving a lifecycle of their own.",
    capabilities: [
      "Step-by-step statement generation from tax invoices",
      "Running balances and ageing analysis",
      "Reconciliation view",
      "PDF export",
    ],
    evidence: "Views/Reports/, Services/Statement/, statements.db",
    href: "/features#statement",
    tags: ["Finance", "Balances", "Ageing"],
  },
  {
    id: "job-profitability",
    number: "12",
    name: "Job Profitability",
    railLabel: "Job Profitability",
    moduleKey: "JobProfitability",
    railGroup: "Finance",
    summary: "Reads the approved proforma's profit snapshot — and flags when the price basis has moved.",
    detail:
      "Job profitability does not recompute money. It reads the snapshot captured at Step 9 of the proforma, so yesterday's quote is never quietly re-costed at today's glass prices. When sheet-store prices move, the row is flagged so the change is visible rather than silently applied.",
    capabilities: [
      "Portfolio profit and loss with drill-down to a single job",
      "Glass-type cost basis comparison · service profitability",
      "Wastage analysis across the portfolio",
      "Staleness flag when the sheet-store price basis has moved",
    ],
    evidence: "ViewModels/JobProfitability/, Services/ProformaInvoice/GlassPriceStamper.cs",
    href: "/features#profitability",
    tags: ["Margin", "Snapshot", "Analysis"],
  },
  {
    id: "vat-submission",
    number: "13",
    name: "VAT Submission",
    railLabel: "VAT Submission",
    moduleKey: "VatSubmission",
    railGroup: "Finance",
    summary: "Gathers the VAT position from invoices for a period, ready to transcribe into a return.",
    detail:
      "A read-only report over the tax invoices in a date range. It is a preparation surface: the application does not connect to any tax-authority portal, and the brief says so plainly rather than implying a filing integration.",
    capabilities: [
      "Period filter over issued tax invoices",
      "VAT amounts computed by the single centralised calculator",
      "Validation tolerance of 1 fils (0.01 AED)",
      "Export for the person who files the return",
    ],
    evidence: "ViewModels/Reports/VatSubmission/, Services/Reporting/ReportingDataService.Vat.cs",
    screenshotId: "vat-submission",
    href: "/features#vat",
    tags: ["VAT", "Compliance"],
  },
  {
    id: "reports",
    number: "14",
    name: "Reports",
    railLabel: "Reports",
    moduleKey: "SalesReport",
    railGroup: "Reports",
    summary: "Sales, proforma, job order, delivery, tax invoice and statement reporting on persisted figures.",
    detail:
      "Reporting reads money from the record it was stored on; it never recomputes it. Reports rank by value, by area (SQM) or by pieces, filter by customer, project, salesman, glass type and date range, and export to PDF, Excel and CSV.",
    capabilities: [
      "Sales report with top customers, trend analytics and ageing",
      "Proforma · Job Order · Delivery · Tax Invoice · Statement report screens",
      "Ranked by value / area (SQM) / pieces",
      "Row-level export to PDF, Excel (.xlsx) and CSV",
    ],
    evidence: "ViewModels/Reports/, Services/Reporting/",
    screenshotId: "reports-hub",
    href: "/features#reports",
    tags: ["Reporting", "Analytics", "Export"],
  },
  {
    id: "company-center",
    number: "15",
    name: "Company Center",
    railLabel: "Company Center",
    moduleKey: "CompanyCenter",
    railGroup: "Master Data",
    summary: "Legal-entity profiles with a field-diff audit trail on every change.",
    detail:
      "A proforma's Step 1 copies all of the legal-entity fields from Company Center. Edits made on an invoice enrich the master record on save under a preserve-blank contract — a missed invoice field can never erase the company record.",
    capabilities: [
      "Full legal-entity profile: name, TRN, location, representative, mobile, e-mail, bank account, bank name, logo",
      "Copied into the proforma Step 1 in one action",
      "Preserve-blank contract when an invoice writes back",
      "Field-diff audit on every change set",
    ],
    evidence: "Views/CompanyCenter/, Services/MasterData/Repository/MasterDataRepository.cs",
    href: "/features#company-center",
    tags: ["Master data", "Audit"],
  },
  {
    id: "system-center",
    number: "16",
    name: "System Center",
    railLabel: "System Center",
    moduleKey: "SystemCenter",
    railGroup: "Master Data",
    summary: "Users, roles and operation-based permissions enforced at the service boundary.",
    detail:
      "Eight roles and twenty-nine granular capabilities decide what a person can do. Permission checks run at the service layer, not only in the interface: hiding a button is not treated as a permission check.",
    capabilities: [
      "User accounts with force-change and reset flows",
      "Eight roles and twenty-nine granular Module.Capability permissions",
      "OperationGuard authorises at the data boundary",
      "Session tracking with an audit trail",
    ],
    evidence: "Views/SystemCenter/, Services/Audit/OperationGuard.cs, Services/Audit/AppRoles.cs",
    href: "/features#system-center",
    tags: ["Users", "Roles", "Security"],
  },
  {
    id: "logs",
    number: "17",
    name: "Logs & Activity",
    railLabel: "Logs & Activity",
    moduleKey: "Diagnostics",
    railGroup: "System",
    summary: "Application log, activity log, audit with before/after diffs, and crash reports with error IDs.",
    detail:
      "Meaningful actions — create, edit, delete, print, login — are recorded with session, actor and module. Audit entries keep before/after diffs. Crash reports arrive with an error ID you can quote, and the application recovers rather than closing on you.",
    capabilities: [
      "Application log and activity log",
      "Audit database with before/after record diffs",
      "Crash reports with a copyable error ID",
      "Diagnostics tab for support",
    ],
    evidence: "Views/Diagnostics/, Views/CrashReports/, Services/Audit/",
    href: "/features#logs",
    tags: ["Audit", "Diagnostics", "Support"],
  },
  {
    id: "settings",
    number: "18",
    name: "Settings",
    railLabel: "Settings",
    moduleKey: "Settings",
    railGroup: "System",
    summary: "Appearance, themes, feature toggles, backup & restore, updates and licensing.",
    detail:
      "A settings section per concern, including an appearance surface for light and dark themes, a data section for backup sets with retention and a restore drill, an updates section, and the licence status with activation.",
    capabilities: [
      "Light and dark themes with an appearance section and module wallpapers",
      "Feature toggles that change what other modules show",
      "Backup sets with retention and a restore drill",
      "Update channel, diagnostics and licence status on one page",
    ],
    evidence: "Views/Settings/, ViewModels/Settings/, Services/Theme/",
    screenshotId: "settings-appearance",
    href: "/features#settings",
    tags: ["Settings", "Theme", "Backup"],
  },
  {
    id: "ai-assistant",
    number: "19",
    name: "AI Assistant",
    railLabel: "AI Assistant",
    moduleKey: "SystemCenter",
    railGroup: "System",
    summary: "An assistant that reads your data, keeps a local brain, and performs exactly one guarded write.",
    detail:
      "It can navigate the application, search documents, read inventory levels, activity, profitability, statements and analytics, and review a proforma. It has exactly one write: adjusting sheet-stock quantity, which requires the SheetStore.Edit permission and an explicit confirmation, and it is audited. Your questions stay local unless you configure a provider.",
    capabilities: [
      "Navigate the app and search proformas, job orders, delivery notes, tax invoices and customers",
      "Read stock, activity, profitability, statements, document chains and sales analytics",
      "One write only — sheet-stock quantity, permission-gated, confirmed and audited",
      "On-device local model, plus optional cloud providers you configure yourself",
      "Voice input and spoken replies",
    ],
    evidence: "Agent/Core/ProGlassAgent.cs, Agent/Tools/, Agent/Permissions/",
    href: "/features#ai",
    tags: ["AI", "Assistant", "Guarded"],
  },
  {
    id: "sharing",
    number: "20",
    name: "Sharing",
    railLabel: "Sharing",
    moduleKey: "SystemCenter",
    railGroup: "System",
    summary: "Explicit sharing surfaces for moving documents between people and machines.",
    detail:
      "Share views and view-models cover exporting and importing document packages, so a quotation or a job can leave one workstation deliberately rather than by pulling the database file around.",
    capabilities: ["Share export and import surfaces", "Scoped document packages", "Activity recorded on share operations"],
    evidence: "Views/Sharing/, ViewModels/Sharing/, Services/Sharing/",
    href: "/features#sharing",
    tags: ["Sharing", "Import/Export"],
  },
];

export const moduleGroups: ModuleGroup[] = [
  "Main",
  "Sales & Orders",
  "Production",
  "Dispatch",
  "Finance",
  "Reports",
  "Master Data",
  "System",
];

export function modulesByGroup(group: ModuleGroup): ProductModule[] {
  return modules.filter((m) => m.railGroup === group);
}
