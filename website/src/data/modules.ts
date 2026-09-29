/**
 * Module registry — the complete application surface.
 *
 * `moduleKey` values are the exact strings the application's navigation sends
 * to `MainViewModel.Navigate`. `railGroup` is the sidebar grouping the shell
 * actually draws. Both were cross-checked against:
 *
 *   Views/Personal/GlassShell.xaml     — the real rail: 9 groups, every
 *                                        CommandParameter in order
 *   Themes/Languages/en-US.xaml        — Nav.* and Reports.Nav.* display strings
 *   ViewModels/MainViewModel.Part2.cs  — the Navigate switch that consumes them
 *
 * Every capability line is a behaviour that exists in the WPF source; the
 * `evidence` field names where to look. Where the application has a screen but
 * no published capture, the module still appears here and its card says so
 * rather than being left off the site.
 */

export type ModuleGroup =
  | "Main"
  | "Inventory"
  | "Sales"
  | "Production"
  | "Dispatch"
  | "Finance"
  | "Reports"
  | "Master Data"
  | "System";

export type ProductModule = {
  id: string;
  number: string;
  name: string;
  /** Verbatim navigation label from Themes/Languages/en-US.xaml. */
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

/** The sidebar groups, in the order the shell draws them. */
export const moduleGroups: ModuleGroup[] = [
  "Main",
  "Inventory",
  "Sales",
  "Production",
  "Dispatch",
  "Finance",
  "Reports",
  "Master Data",
  "System",
];

/** What each group is for — the rail's own headers, explained. */
export const groupNotes: Record<ModuleGroup, string> = {
  Main: "Where the day starts.",
  Inventory: "What is on the racks and what is specified.",
  Sales: "Inquiry to invoice — the commercial chain.",
  Production: "Release the job, run the eight stages, watch the floor.",
  Dispatch: "What left the yard, and the report that reconciles it.",
  Finance: "Statements, profitability and VAT.",
  Reports: "Fourteen report surfaces over one data layer.",
  "Master Data": "Companies, users and permissions.",
  System: "Health, logs and application settings.",
};

export const modules: ProductModule[] = [
  /* ══════════════════════════ MAIN ══════════════════════════════════════ */
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

  /* ══════════════════════════ INVENTORY ═════════════════════════════════ */
  {
    id: "sheet-store",
    number: "02",
    name: "Sheet Store",
    railLabel: "Sheet Store",
    moduleKey: "SheetStore",
    railGroup: "Inventory",
    summary: "The rack, as records: buy and use movements with history, never an edited number.",
    detail:
      "Every plate that arrives and every plate that is consumed is a movement row with a reason, a reference and an author. Balances are derived from movements, so a stock figure can always be explained rather than merely displayed.",
    capabilities: [
      "Buy and use movements with per-row history and author",
      "Per-rack-row size, thickness, colour and optional balance cap",
      "Reorder levels and low-stock signalling",
      "History views: value change with monetary impact in AED",
    ],
    evidence:
      "Views/SheetStore/, ViewModels/Dashboard/SheetStore/, Services/SheetStore/SheetStoreHistory/",
    screenshotId: "sheet-store-overview",
    href: "/features#showcase-sheet-store",
    tags: ["Stock", "Movements"],
  },
  {
    id: "specifications",
    number: "03",
    name: "Specifications",
    railLabel: "Specifications",
    moduleKey: "Specifications",
    railGroup: "Inventory",
    summary: "The glass specification library that the calculator writes into and the reports read back.",
    detail:
      "A specification is the full build-up in one line — panes, thicknesses, colour, treatment, air space and spacer — with its area and its value. Specifications are what the cutting list, the production distribution and the row-level traceability report all resolve against.",
    capabilities: [
      "Build-up lines for SGU, DGU, LAM and DGU + LAM",
      "W1/H1 and W2/H2 per unit with quantity",
      "Area (SQM) and value per line",
      "Feeds the cutting list, production distribution and reports",
    ],
    evidence:
      "Views/Specifications/, ViewModels/Specifications/, Services/Reporting/SpecificationIntelligenceService.cs, Services/Reporting/SpecificationMatcher.cs",
    href: "/features#specifications",
    tags: ["Build-up", "Traceability"],
  },

  /* ══════════════════════════ SALES ═════════════════════════════════════ */
  {
    id: "inquiries",
    number: "04",
    name: "Inquiry",
    railLabel: "Inquiry",
    moduleKey: "Inquiry",
    railGroup: "Sales",
    summary: "Where a job begins: scope of supply, the customer, and what they actually asked for.",
    detail:
      "An inquiry carries the customer, the project, a scope of supply and the attachments that came with the request. It is the record that survives the phone call, so the quotation that follows can be checked against what was asked.",
    capabilities: [
      "Customer, project and scope-of-supply capture",
      "Attachments held with SHA-256 deduplication",
      "Status vocabulary with an audited transition trail",
      "Converts forward into a proforma invoice",
    ],
    evidence: "Views/Inquiry/, ViewModels/Dashboard/Inquiry/, Services/Inquiry/",
    screenshotId: "inquiries",
    href: "/features#inquiries",
    tags: ["Intake", "Attachments"],
  },
  {
    id: "proforma",
    number: "05",
    name: "Proforma Invoice",
    railLabel: "Proforma Invoice",
    moduleKey: "ProformaInvoice",
    railGroup: "Sales",
    summary: "The nine-step estimation wizard — the single pricing engine the whole application reads from.",
    detail:
      "Company, client, project, pricing, specification grid, charges, summary, attachments, profit. Step 4 prices the four build families; Step 5 turns dimensions and quantities into area and value under two rules — every panel bills at least 0.5 m² and a panel that reaches 4 m² takes the specification's surcharge rate; Step 9 asks whether the given price is above target and writes the profit-and-loss snapshot that job profitability, statements and reports later read instead of recomputing.",
    capabilities: [
      "9-step wizard with company and client cards",
      "SGU / DGU / LAM / DGU + LAM build-up pricing",
      "Dimension grid (W1·H1·W2·H2 × Qty) with SQM and value, plus BOQ import",
      "Grid commands: find and replace, paste from Excel or a BOQ, generate rows, per-row delete",
      "Grand total summary — one row per specification and the grand row, alongside every spec footer",
      "0.5 m² minimum billable area per panel; base + surcharge rate at 4 m²",
      "QUICK / OPTIMIZE / VIEW RESULT on the grid, feeding Step 6 its sheet quantity",
      "Step 9 profit & loss: overview, services, sheets and a spec-by-spec check",
      "Charges, VAT 5% round-safe summary, attachments, stored P&L snapshot",
    ],
    evidence:
      "Views/ProformaInvoice/, ViewModels/ProformaInvoice/, Services/ProformaInvoice/GlassPriceStamper.cs",
    screenshotId: "proforma-wizard",
    href: "/features#showcase-proforma",
    tags: ["Estimation", "Pricing"],
  },
  {
    id: "job-order",
    number: "06",
    name: "Job Order",
    railLabel: "Job Order",
    moduleKey: "JobOrder",
    railGroup: "Sales",
    summary: "The confirmed sale, expressed as work the factory can be handed.",
    detail:
      "A job order only exists for an approved proforma. It carries the same specifications forward without re-typing, and it is the object the production stages, the delivery notes and the reconciliation report all hang from.",
    capabilities: [
      "Created only from an approved proforma invoice",
      "Status flow with release to production",
      "Shares one specification record with the quotation",
      "Feeds delivery notes, tax invoices and the JO report",
    ],
    evidence: "Views/JobOrder/, ViewModels/Dashboard/JobOrder/, Services/JobOrder/Mapper/JobOrderMapper.cs",
    screenshotId: "job-order",
    href: "/features#job-order",
    tags: ["Work order", "Handoff"],
  },
  {
    id: "tax-invoice",
    number: "07",
    name: "Tax Invoice",
    railLabel: "Tax Invoice",
    moduleKey: "TaxInvoice",
    railGroup: "Sales",
    summary: "VAT-compliant invoicing with one rounding rule for the whole application.",
    detail:
      "Customer TRN, per-line VAT and a single centralised calculator apply the 5% rate with VAT-from-subtotal and VAT-from-gross back-calculation, tolerance-checked to one fils. Credit notes are a separate, auditable document type rather than a negative invoice.",
    capabilities: [
      "VAT 5% through one central calculator, tolerance 0.01 AED",
      "Customer TRN and VAT-compliant invoice fields",
      "Credit notes as first-class documents",
      "Export and print, with the figures the reports read",
    ],
    evidence: "Views/TaxInvoice/, ViewModels/Dashboard/TaxInvoice/, Services/Reporting/VatCalculator.cs",
    screenshotId: "vat-submission",
    href: "/features#tax-invoice",
    tags: ["VAT", "Invoicing"],
  },

  /* ══════════════════════════ PRODUCTION ════════════════════════════════ */
  {
    id: "optimization",
    number: "08",
    name: "Optimization",
    railLabel: "Optimization",
    moduleKey: "Optimization",
    railGroup: "Production",
    summary: "The guillotine nesting engine: eight tabs, two oracles, five machine export formats.",
    detail:
      "Stock plates and a cutting list in, a cuttable plan out — with per-thickness safety edges, blade kerf and minimum breakout honoured, the guillotine cut sequence recorded as it is produced, and every result checked by a geometry oracle and a conservation oracle before it can leave the module.",
    capabilities: [
      "8 tabs: Jobs, Stocks, Sizes, Cut List, 2D View, Summary, Report, Settings",
      "Thickness-aware margins, kerf and minimum breakout, LM/RM/TM/BM",
      "Six plate patterns, six cut-complexity levels, five search stages",
      "CSV, Excel, DXF and Bottero / Intermac / Lisec machine files",
    ],
    evidence: "Services/Optimization/ (29 files), Views/Optimization/, ViewModels/Optimization/",
    screenshotId: "optimization-2d",
    href: "/optimization",
    tags: ["Engine", "Nesting"],
  },
  {
    id: "process-work-center",
    number: "09",
    name: "Process Work Center",
    railLabel: "Process Work Center",
    moduleKey: "ProcessWorkCenter",
    railGroup: "Production",
    summary: "Every piece on the floor in one filterable table, whichever stage it is standing at.",
    detail:
      "A single reusable work center instead of eight unrelated screens: filter the whole floor by process, job, customer, proforma, job order, specification, material, colour, thickness, status or date, then act on a row rather than navigating to it.",
    capabilities: [
      "Filter by process, job, customer, PI, JO, spec, material, colour, thickness, status, date",
      "Rows carry job, spec, piece, process, previous stage, current status, qty and progress",
      "Per-row QC, operator and traceability columns",
      "Actions: Start, Pause, Complete, Breakage, Rework, QC, View Traceability",
    ],
    evidence: "Views/Production/ProcessWorkCenterView.xaml, ViewModels/Production/ProcessWorkCenterViewModel.cs",
    screenshotId: "process-work-center",
    href: "/features#showcase-optimization",
    tags: ["Shop floor", "Filtering"],
  },
  {
    id: "production-control-center",
    number: "10",
    name: "Production Control Center",
    railLabel: "Production Control Center",
    moduleKey: "ProductionControlCenter",
    railGroup: "Production",
    summary: "The floor seen from above: KPI cards, a live queue, and traceability for whatever is selected.",
    detail:
      "Where the work center is a table, the control center is a picture: headline counts for the floor, a production queue that reflects what is actually outstanding, and a detail panel that traces the selected work item back through the documents it came from.",
    capabilities: [
      "KPI cards across the floor rather than per stage",
      "Production queue with selection",
      "Selected work item detail with document traceability",
      "Reuses the work-center components rather than duplicating them",
    ],
    evidence:
      "Views/Production/ProductionControlCenterView.xaml, ViewModels/Production/ProductionControlCenterViewModel.cs",
    screenshotId: "production-control-center",
    href: "/features#production-control-center",
    tags: ["Floor", "Traceability"],
  },
  {
    id: "stage-cutting",
    number: "11",
    name: "Cutting",
    railLabel: "Cutting",
    moduleKey: "Cutting",
    railGroup: "Production",
    summary: "Stage one: the pieces come off the optimized plan and are marked done as they are cut.",
    detail:
      "Cutting is fed by the production distribution engine from the job's specification rows, so the quantities on screen are the job's own. Marking done moves the completed quantity — and only the completed quantity — to the next stage in the chain.",
    capabilities: [
      "Job list: # · job no · customer · total · done · balance with a status chip",
      "Piece flow clamped to what arrived upstream",
      "Per-row Qty Done entry with save",
      "Recent entries log: who marked done, and when",
    ],
    evidence: "Views/Production/CuttingView.xaml, ViewModels/Production/CuttingViewModel.cs",
    screenshotId: "stage-cutting",
    href: "/features#stage-cutting",
    tags: ["Stage 1", "Piece flow"],
  },
  {
    id: "stage-holes-cutouts",
    number: "12",
    name: "Holes & Cutouts",
    railLabel: "Holes & Cutouts",
    moduleKey: "HolesCutouts",
    railGroup: "Production",
    summary: "Fan holes, holes, cutouts and countersunk holes — the work Step 6 of the quotation priced.",
    detail:
      "The distribution engine reads the charge lines that create hole work (FAN_HOLES, HOLES, CUTOUT, COUNTERSUNK_HOLES) so the station sees exactly the openings the quotation sold, against the right specification rows.",
    capabilities: [
      "Work generated from the quotation's own hole charge lines",
      "Per-row balance across all units of the job",
      "Detail grid with the job's outer and inner cut lists",
      "Same piece-flow accounting as every other stage",
    ],
    evidence: "Views/Production/HolesCutoutsView.xaml, Services/Production/ProductionDistributionEngine.cs",
    screenshotId: "stage-holes-cutouts",
    href: "/features#stage-holes-cutouts",
    tags: ["Stage 2", "Openings"],
  },
  {
    id: "stage-polish",
    number: "13",
    name: "Polish",
    railLabel: "Polish",
    moduleKey: "Polish",
    railGroup: "Production",
    summary: "Edge polishing at the station, in the linear metres the quotation charged for.",
    detail:
      "Polished edges are dimensioned work: the quantity is the edge length the pricing step used, not a piece count. The station records what was actually polished, and the balance stays honest about what is left.",
    capabilities: [
      "Linear-metre work lines per specification row",
      "Arrived / done / balance per row and per job",
      "Completion writes only the delivered quantity onward",
      "Operator attribution on every entry",
    ],
    evidence: "Views/Production/PolishView.xaml, ViewModels/Production/PolishViewModel.cs",
    screenshotId: "stage-polish",
    href: "/features#stage-polish",
    tags: ["Stage 3", "Edges"],
  },
  {
    id: "stage-edge-deletion",
    number: "14",
    name: "Edge Deletion",
    railLabel: "Edge Deletion",
    moduleKey: "EdgeDeletion",
    railGroup: "Production",
    summary: "Coating removal where a sealed unit or a bonded edge needs the surface taken back.",
    detail:
      "Edge deletion is called out in the build-up and priced there, so the station is working to the specification rather than to a verbal instruction. The stage behaves like the rest of the chain: arrived, done, balance.",
    capabilities: [
      "Lines derived from the quotation's build-up",
      "Job totals and per-row balances",
      "Full-page job detail with the cut-list grids",
      "Piece flow into whatever stage follows",
    ],
    evidence: "Views/Production/EdgeDeletionView.xaml, ViewModels/Production/EdgeDeletionViewModel.cs",
    screenshotId: "stage-edge-deletion",
    href: "/features#stage-edge-deletion",
    tags: ["Stage 4", "Coating"],
  },
  {
    id: "stage-mitring",
    number: "15",
    name: "Mitring",
    railLabel: "Mitring",
    moduleKey: "Mitring",
    railGroup: "Production",
    summary: "Mitred edges, again driven by the charge lines the quotation created.",
    detail:
      "Mitring work is generated from the quotation's mitring lines and carried against the correct specification rows, so a mitred pane reaches the right station with the right length and the right quantity.",
    capabilities: [
      "Mitring work from the quotation's own charge lines",
      "Linear-metre quantities with balance tracking",
      "Detail grid across all units of the job",
      "Operator, timestamp and quantity on each entry",
    ],
    evidence: "Views/Production/MitringView.xaml, ViewModels/Production/MitringViewModel.cs",
    screenshotId: "stage-mitring",
    href: "/features#stage-mitring",
    tags: ["Stage 5", "Edges"],
  },
  {
    id: "stage-tempering",
    number: "16",
    name: "Tempering",
    railLabel: "Tempering",
    moduleKey: "Tempering",
    railGroup: "Production",
    summary: "The thermal stage — and the routing rule that decides which panes ever reach it.",
    detail:
      "Float, heat-strengthened and heat-soaked panes route through tempering; annealed panes are dispatch-ready straight from cutting. The stage queues only what the build-up's treatments actually require.",
    capabilities: [
      "Routing by pane treatment: FT / Heat Strengthened / Heat Soaked",
      "Annealed panes bypass the stage rather than queueing",
      "Full quantity accounting per specification row",
      "Completion gated on what arrived from upstream",
    ],
    evidence: "Views/Production/TemperingView.xaml, Views/Production/README.md (routing rules)",
    screenshotId: "stage-tempering",
    href: "/features#stage-tempering",
    tags: ["Stage 6", "Thermal"],
  },
  {
    id: "stage-pairing",
    number: "17",
    name: "Pairing",
    railLabel: "Pairing",
    moduleKey: "Pairing",
    railGroup: "Production",
    summary: "Matching the panes that belong together before they are sealed or bonded.",
    detail:
      "Double-glazed, laminated and double-glazed-plus-laminated rows split into pane occurrences — outer W1/H1, inner or laminate pair W2/H2 — and pairing is where those occurrences are brought back together as one unit.",
    capabilities: [
      "Pane occurrences per unit: outer and inner / laminate",
      "W1·H1 and W2·H2 carried from Step 5",
      "Units paired before sealing",
      "Same balance accounting as every station",
    ],
    evidence: "Views/Production/PairingView.xaml, ViewModels/Production/PairingViewModel.cs",
    screenshotId: "stage-pairing",
    href: "/features#stage-pairing",
    tags: ["Stage 7", "Assembly"],
  },
  {
    id: "stage-doubling-lamination",
    number: "18",
    name: "Doubling / Lamination",
    railLabel: "Doubling / Lamination",
    moduleKey: "DoublingLamination",
    railGroup: "Production",
    summary: "The final stage: the unit is sealed — and the charge notes that describe how are printed on it.",
    detail:
      "Assembly is where the job finishes its chain and a unit becomes ready for dispatch. The work item carries the quotation's own assembly notes — argon gas fill, silicon bed seal, U-insert sealing, stepped overlap edges, shutter bonding — so the station is not guessing at the specification.",
    capabilities: [
      "Assembly item decorated with the quotation's charge notes",
      "ARGON GAS FILL · SILICON BED SEAL · U-INSERT SEALING",
      "STEPPED OVERLAP EDGES · SHUTTER BONDING",
      "Completing the chain marks the unit ready for dispatch",
    ],
    evidence:
      "Views/Production/DoublingLaminationView.xaml, Services/Production/ProductionDistributionEngine.cs",
    screenshotId: "stage-doubling-lamination",
    href: "/features#stage-doubling-lamination",
    tags: ["Stage 8", "Assembly"],
  },

  /* ══════════════════════════ DISPATCH ══════════════════════════════════ */
  {
    id: "delivery-note",
    number: "19",
    name: "Delivery Note",
    railLabel: "Delivery Note",
    moduleKey: "DeliveryNote",
    railGroup: "Dispatch",
    summary: "Multi-movement dispatch with a balance that is computed, never remembered.",
    detail:
      "A job rarely leaves in one load. Each delivery note records what actually went on that vehicle, and the balance the office quotes on the phone is derived from the order and the movements against it — including replacements for breakages, which are handled explicitly rather than silently.",
    capabilities: [
      "Multi-movement delivery with per-movement balance",
      "Replacement handling for broken pieces",
      "Driver, vehicle and weight capture",
      "Feeds the tax invoice and the delivery report",
    ],
    evidence: "Views/DeliveryNote/, ViewModels/Dashboard/DeliveryNote/, Services/DeliveryNote/",
    screenshotId: "delivery-note",
    href: "/features#showcase-delivery-note",
    tags: ["Dispatch", "Balance"],
  },
  {
    id: "report-generator",
    number: "20",
    name: "Report Generator",
    railLabel: "Order Report",
    moduleKey: "OrderReport",
    railGroup: "Dispatch",
    summary: "The overall order report: a four-step wizard that reconciles the whole document chain.",
    detail:
      "Filter, preview, pipeline, review. Pick one proforma or many, and the generator builds a report that puts the invoice, the delivery weights, the tax-invoice balance, the reconciliation and the attachment completeness in one document. Each run is saved so it can be reopened or re-exported.",
    capabilities: [
      "4 steps: Filter → Preview → Pipeline → Review",
      "Single-PI or multi-PI batches",
      "Reconciliation and attachment-completeness engine",
      "Saved runs in orderreports.db, re-openable and re-exportable",
    ],
    evidence: "ViewModels/Reports/OrderReport/, Views/Reports/OrderReport/",
    screenshotId: "report-generator",
    href: "/features#report-generator",
    tags: ["Report", "Reconciliation"],
  },

  /* ══════════════════════════ FINANCE ═══════════════════════════════════ */
  {
    id: "statement-generator",
    number: "21",
    name: "Statement Generator",
    railLabel: "Statement Generator",
    moduleKey: "StatementGenerator",
    railGroup: "Finance",
    summary: "The account statement a customer accepts: filter, preview, pipeline, review.",
    detail:
      "The same four-step generator pattern as the order report, over the customer's invoices: what was issued, what is outstanding, what is overdue and an ageing analysis — with an optional combined PDF that merges the signed documents into the same file.",
    capabilities: [
      "4-step generator: Filter → Preview → Pipeline → Review",
      "Summary statement PDF plus ageing analysis",
      "Combined PDF including signed PI and signed delivery notes",
      "Saved statements list — re-open, re-print, re-send",
    ],
    evidence: "ViewModels/Reports/StatementGenerator/, Views/Reports/StatementGenerator/, Views/Reports/README.md",
    screenshotId: "statement-generator",
    href: "/features#statement-generator",
    tags: ["Statement", "Ageing"],
  },
  {
    id: "job-profitability",
    number: "22",
    name: "Job Profitability",
    railLabel: "Job Profitability",
    moduleKey: "JobProfitability",
    railGroup: "Finance",
    summary: "Margin per job, read from the snapshot written at the moment of quoting.",
    detail:
      "The profit figure is not recomputed from today's prices. It is the stored snapshot from Step 9 of the proforma, so a report opened next year still shows the margin the business actually quoted at, and a moved cost basis is visible rather than silently absorbed.",
    capabilities: [
      "Per-job P&L from the stored Step 9 snapshot",
      "Cost basis recorded with the figure",
      "Moved prices flagged rather than rescored",
      "Read by statements, the profitability report and the executive summary",
    ],
    evidence: "ViewModels/JobProfitability/, ViewModels/ProformaInvoice/Step9ProfitLoss/, Services/Reporting/VatCalculator.cs",
    screenshotId: "job-profitability",
    href: "/features#showcase-job-profitability",
    tags: ["Margin", "Snapshot"],
  },
  {
    id: "vat-submission",
    number: "23",
    name: "VAT Submission",
    railLabel: "VAT Submission",
    moduleKey: "VatSubmission",
    railGroup: "Finance",
    summary: "The period's VAT figures, gathered from one calculator so they agree with the invoices.",
    detail:
      "A report that collects the period's output figures from the same centralised calculator the invoices used. Filing is still something you do — this makes the numbers you file agree with the numbers you issued.",
    capabilities: [
      "Period figures built from the centralised VAT calculator",
      "One rounding rule, 1 fils tolerance, across every document",
      "Per-period view for filing preparation",
      "Cross-checks against issued tax invoices",
    ],
    evidence: "ViewModels/Reports/VatSubmission/, Services/Reporting/VatCalculator.cs, Views/Reports/VatSubmission/",
    screenshotId: "vat-submission",
    href: "/features#vat-submission",
    tags: ["VAT", "Filing"],
  },

  /* ══════════════════════════ REPORTS ═══════════════════════════════════ */
  {
    id: "reports-hub",
    number: "24",
    name: "Reports Hub",
    railLabel: "Sales Report",
    moduleKey: "SalesReport",
    railGroup: "Reports",
    summary: "One rail, fourteen report surfaces and a KPI overview over one central data layer.",
    detail:
      "Rather than fourteen separate modules in the sidebar, the reports live behind one hub with its own collapsible rail: an overview, four relationship reports, five document reports and four financial surfaces. Every one of them reads the same stored records.",
    capabilities: [
      "Rail with Overview plus Relationships, Documents and Financial groups",
      "Collapsible navigation rail so a report can use the full width",
      "Sorting, search and column controls shared by every report",
      "Async generation with progress and cancellation",
    ],
    evidence: "ViewModels/Reports/SalesReportViewModel.cs (NavItems), Views/Reports/SalesReportView.xaml",
    screenshotId: "reports-hub",
    href: "/features#showcase-reports-hub",
    tags: ["Hub", "14 surfaces"],
  },
  {
    id: "report-executive",
    number: "25",
    name: "Executive Summary",
    railLabel: "Reports · Executive",
    moduleKey: "SalesReport#Executive",
    railGroup: "Reports",
    summary: "The one-page order-to-cash funnel for anyone who does not want fourteen screens.",
    detail:
      "A single view over the commercial chain: how much is quoted, how much became work, how much shipped, how much was invoiced and how much is collected — the same funnel the dashboards show, at company level.",
    capabilities: [
      "Order-to-cash funnel across the whole business",
      "Single-page cross-module view",
      "Reads stored records, not a parallel calculation",
      "Behind the Reports hub rail",
    ],
    evidence: "ViewModels/Reports/ExecutiveReport/, Views/Reports/ExecutiveReport/",
    href: "/features#report-executive",
    tags: ["Funnel", "Summary"],
  },
  {
    id: "report-customer",
    number: "26",
    name: "Customer Directory & Receivables",
    railLabel: "Reports · Customers",
    moduleKey: "SalesReport#Customer",
    railGroup: "Reports",
    summary: "One row per customer: what they bought, what they owe, what is overdue.",
    detail:
      "The account view a credit controller wants — customers ranked by value, by area or by pieces, with receivables and ageing attached to the same row. Sortable, searchable, exportable like every other report.",
    capabilities: [
      "Per-customer value, invoiced and outstanding",
      "Rank by value, area (SQM) or pieces",
      "Ageing alongside the customer row",
      "Export and print from the shared report chrome",
    ],
    evidence: "ViewModels/Reports/CustomerReport/, Views/Reports/CustomerReport/",
    href: "/features#report-customer",
    tags: ["Customers", "Receivables"],
  },
  {
    id: "report-salesman",
    number: "27",
    name: "Salesman Performance",
    railLabel: "Reports · Salesmen",
    moduleKey: "SalesReport#Salesman",
    railGroup: "Reports",
    summary: "Who quoted what, who converted it, and what the margin was.",
    detail:
      "The sales representative is carried on the inquiry and the proforma, so performance is measured from the documents themselves rather than from a separate tally. Value, conversion and profitability sit on the same row.",
    capabilities: [
      "Per-salesman quotation and conversion figures",
      "Value and margin from the stored snapshots",
      "Ranked with the shared sort controls",
      "No separate data entry — the documents are the source",
    ],
    evidence: "ViewModels/Reports/SalesmanReport/, Views/Reports/SalesmanReport/",
    href: "/features#report-salesman",
    tags: ["Sales", "Performance"],
  },
  {
    id: "report-company",
    number: "28",
    name: "Company Book & Receivables",
    railLabel: "Reports · Companies",
    moduleKey: "SalesReport#Company",
    railGroup: "Reports",
    summary: "The same receivables picture, grouped by the issuing company instead.",
    detail:
      "For businesses that invoice from more than one entity: the book and its receivables per issuing company, with the same ranking, ageing and export behaviour as the customer view.",
    capabilities: [
      "Per-issuing-company book and receivables",
      "Ageing attached to the company row",
      "Shares the report hub's sorting and export",
      "Built from issued tax invoices",
    ],
    evidence: "ViewModels/Reports/CompanyReport/, Views/Reports/CompanyReport/",
    href: "/features#report-company",
    tags: ["Companies", "Receivables"],
  },
  {
    id: "report-project",
    number: "29",
    name: "Project Register & Progress",
    railLabel: "Reports · Projects",
    moduleKey: "SalesReport#Project",
    railGroup: "Reports",
    summary: "Projects as first-class records, with progress through the chain on the same row.",
    detail:
      "A project register that shows where each project has reached — quoted, ordered, in production, dispatched, invoiced — so a business with long-running sites can see progress rather than reconstruct it.",
    capabilities: [
      "Per-project register with progress",
      "Quantities and value per project",
      "Cross-references the documents behind the progress",
      "Sortable, searchable, exportable",
    ],
    evidence: "ViewModels/Reports/ProjectReport/, Views/Reports/ProjectReport/",
    href: "/features#report-project",
    tags: ["Projects", "Progress"],
  },
  {
    id: "report-pi",
    number: "30",
    name: "Proforma Invoice Report",
    railLabel: "Reports · Proforma Invoice",
    moduleKey: "SalesReport#Pi",
    railGroup: "Reports",
    summary: "Quotation volume, value and conversion — the quotes-to-orders view.",
    detail:
      "Every proforma with its status, value and what became of it, so the gap between what was quoted and what was ordered is a number rather than a suspicion.",
    capabilities: [
      "All proformas with status and value",
      "Quote-to-order conversion",
      "Filter by period, customer and salesman",
      "Export and print",
    ],
    evidence: "ViewModels/Reports/PiReport/, Views/Reports/PiReport/",
    href: "/features#report-pi",
    tags: ["Quotes", "Conversion"],
  },
  {
    id: "report-jo",
    number: "31",
    name: "Job Order Report",
    railLabel: "Reports · Job Order",
    moduleKey: "SalesReport#Jo",
    railGroup: "Reports",
    summary: "Production status across every released job.",
    detail:
      "Which job orders are released, how far each has travelled through the eight stages, and what is outstanding — from the same production records the floor writes to.",
    capabilities: [
      "Per-job production status",
      "Stage progress from the production records",
      "Outstanding quantities per job",
      "Export and print",
    ],
    evidence: "ViewModels/Reports/JoReport/, Views/Reports/JoReport/",
    href: "/features#report-jo",
    tags: ["Jobs", "Production status"],
  },
  {
    id: "report-delivery",
    number: "32",
    name: "Delivery Note Report",
    railLabel: "Reports · Delivery",
    moduleKey: "SalesReport#Delivery",
    railGroup: "Reports",
    summary: "What was dispatched, and what is still pending.",
    detail:
      "Dispatched against pending, per job and per movement — the view that answers 'what have we actually sent them' without opening each delivery note.",
    capabilities: [
      "Dispatched and pending per job",
      "Per-movement detail with weights",
      "Filter by period and customer",
      "Export and print",
    ],
    evidence: "ViewModels/Reports/DeliveryReport/, Views/Reports/DeliveryReport/",
    href: "/features#report-delivery",
    tags: ["Dispatch", "Pending"],
  },
  {
    id: "report-tax-invoice",
    number: "33",
    name: "Tax Invoice Report",
    railLabel: "Reports · Tax Invoice",
    moduleKey: "SalesReport#TaxInvoice",
    railGroup: "Reports",
    summary: "Billed, overdue and collected, from the issued invoices themselves.",
    detail:
      "The invoicing view: what has been billed, what is overdue and what has been settled, with credit notes accounted for rather than netted off silently.",
    capabilities: [
      "Billed, overdue and settled",
      "Credit notes included",
      "Period and customer filters",
      "Export and print",
    ],
    evidence: "ViewModels/Reports/TaxInvoiceReport/, Views/Reports/TaxInvoiceReport/",
    href: "/features#report-tax-invoice",
    tags: ["Billing", "Overdue"],
  },
  {
    id: "report-glass-spec",
    number: "34",
    name: "Glass Specifications & Traceability",
    railLabel: "Reports · Glass Specs",
    moduleKey: "SalesReport#GlassSpec",
    railGroup: "Reports",
    summary: "Row-level traceability from a glass specification back to the documents that carry it.",
    detail:
      "Search a specification — build-up, thickness, colour, treatment — and see every document, job and movement it appears in. This is the report the whole product exists for: the same glass, provable at every step.",
    capabilities: [
      "Search by build-up, thickness, colour and treatment",
      "Row-level trace back to PI, job, delivery and invoice",
      "Specification matching across parsed build-up text",
      "Export and print",
    ],
    evidence:
      "ViewModels/Reports/GlassSpecReport/, Services/Reporting/SpecificationIntelligenceService.cs, Services/Reporting/SpecificationMatcher.cs",
    screenshotId: "report-glass-spec",
    href: "/features#report-glass-spec",
    tags: ["Traceability", "Specs"],
  },
  {
    id: "report-profitability",
    number: "35",
    name: "Profitability & P&L",
    railLabel: "Reports · Profitability",
    moduleKey: "SalesReport#Profit",
    railGroup: "Reports",
    summary: "The per-proforma profit and loss the engine already computed, over a period.",
    detail:
      "Profitability aggregated from the stored Step 9 snapshots, so the numbers reconcile with what each job was quoted at rather than with a fresh recalculation at today's cost basis.",
    capabilities: [
      "Profit and loss per proforma over a period",
      "Revenue, cost and margin from stored snapshots",
      "Grouped for the period being reviewed",
      "Export and print",
    ],
    evidence: "ViewModels/Reports/ProfitabilityReport/, Views/Reports/ProfitabilityReport/",
    screenshotId: "job-profitability",
    href: "/features#report-profitability",
    tags: ["Profit", "Period"],
  },
  {
    id: "report-statement",
    number: "36",
    name: "Statement Report",
    railLabel: "Reports · Statement",
    moduleKey: "SalesReport#Statement",
    railGroup: "Reports",
    summary: "Generated statements, listed and re-openable.",
    detail:
      "Every statement the generator has produced, with its period and its customer, so a statement can be re-printed or re-sent without regenerating it and risking a different figure.",
    capabilities: [
      "Saved statement list with period and customer",
      "Re-open, re-print, re-export",
      "Ageing summary per statement",
      "Combined signed-document PDF support",
    ],
    evidence: "ViewModels/Reports/StatementReport/, Views/Reports/StatementReport/, Views/Reports/StatementList/",
    href: "/features#report-statement",
    tags: ["Statements", "Archive"],
  },
  {
    id: "report-vat",
    number: "37",
    name: "VAT Submission Report",
    railLabel: "Reports · VAT Submission",
    moduleKey: "SalesReport#VatSubmission",
    railGroup: "Reports",
    summary: "The period's VAT figures inside the reporting hub.",
    detail:
      "The same submission figures as the finance module, presented within the reporting rail so a period close does not mean switching tools twice.",
    capabilities: [
      "Period VAT figures in the reporting hub",
      "One centralised calculator behind the numbers",
      "Cross-checked against issued invoices",
      "Export and print",
    ],
    evidence: "ViewModels/Reports/VatSubmission/, Views/Reports/VatSubmission/",
    href: "/features#report-vat",
    tags: ["VAT", "Period"],
  },

  /* ══════════════════════════ MASTER DATA ═══════════════════════════════ */
  {
    id: "company-center",
    number: "38",
    name: "Company Center",
    railLabel: "Company Center",
    moduleKey: "CompanyCenter",
    railGroup: "Master Data",
    summary: "The company record every printed document is issued from.",
    detail:
      "Company details, tax registration, branding and the document identity that appears on proformas, invoices and statements. Change it once and the documents follow — which is why it is master data rather than a setting buried in a print dialog.",
    capabilities: [
      "Company identity and tax registration for printed documents",
      "Branding used across every document type",
      "Document numbering identity",
      "The record the reports group by",
    ],
    evidence: "Views/CompanyCenter/, ViewModels/CompanyCenter/, Views/Reports/CompanyReport/",
    screenshotId: "company-center",
    href: "/features#company-center",
    tags: ["Master data", "Documents"],
  },
  {
    id: "user-center",
    number: "39",
    name: "User Center",
    railLabel: "User Center",
    moduleKey: "UserCenter",
    railGroup: "Master Data",
    summary: "Users, the eight assignable roles and the permissions each one carries.",
    detail:
      "Accounts are created here and bound to a role; roles map to granular Module.Capability permissions enforced at the service boundary rather than at the screen — so a permission cannot be bypassed by reaching the same data another way.",
    capabilities: [
      "User accounts with role assignment",
      "8 assignable roles (AppRoles.All) plus the Operator role",
      "34 live Module.Capability permission constants",
      "Passwords as PBKDF2-HMAC-SHA256, 210,000 iterations, per-user salt",
    ],
    evidence: "Views/UserCenter/, ViewModels/UserCenter/, Services/Audit/Permissions.cs",
    screenshotId: "user-center",
    href: "/features#user-center",
    tags: ["Users", "Permissions"],
  },

  /* ══════════════════════════ SYSTEM ════════════════════════════════════ */
  {
    id: "system-center",
    number: "40",
    name: "System Center",
    railLabel: "System Center",
    moduleKey: "SystemCenter",
    railGroup: "System",
    summary: "Users and the activity log — the audit surface.",
    detail:
      "The place the audit trail is read. Meaningful actions are recorded with their module, operation and context, so the question 'who changed this, and when' has an answer that is not a recollection.",
    capabilities: [
      "Activity log with module and operation context",
      "Audit records for create, edit, delete, print and login",
      "User administration",
      "No keystroke or mouse recording — actions only",
    ],
    evidence: "Views/SystemCenter/, ViewModels/SystemCenter/, Services/Audit/",
    screenshotId: "system-center",
    href: "/features#system-center",
    tags: ["Audit", "Admin"],
  },
  {
    id: "diagnostics",
    number: "41",
    name: "Diagnostics",
    railLabel: "Diagnostics",
    moduleKey: "Diagnostics",
    railGroup: "System",
    summary: "The health surface, including crash reports.",
    detail:
      "Diagnostics exposes what the application knows about itself: logged conditions, crash reports and the state checks that would otherwise only be visible in a log file nobody opens until something is wrong.",
    capabilities: [
      "Crash report capture and review",
      "Application health and state checks",
      "Log surfaces for support conversations",
      "Reachable from the shell and from the About tabs",
    ],
    evidence: "Views/Diagnostics/, ViewModels/Diagnostics/, Views/CrashReports/",
    screenshotId: "diagnostics",
    href: "/features#diagnostics",
    tags: ["Health", "Support"],
  },
  {
    id: "settings",
    number: "42",
    name: "Settings",
    railLabel: "Settings",
    moduleKey: "Settings",
    railGroup: "System",
    summary: "Appearance, language, data and the switches the business actually owns.",
    detail:
      "Four visual theme families, 16 accent presets plus a free colour, 15 languages, wallpaper modes, card-edge definition and the company's own defaults. It is the same design system the rest of the application is drawn from, exposed rather than hard-coded.",
    capabilities: [
      "Visual families: Industrial, Monet, Crystal Fluid, iOS 26 (light/dark)",
      "16 accent presets + custom colour, with derived text hierarchy",
      "15 languages as pure resource dictionaries",
      "Wallpaper modes and card-edge definition",
    ],
    evidence: "Views/Settings/, ViewModels/Settings/, Services/Theme/AccentPresets.cs, Services/Theme/VisualThemeEngine.Part1.cs",
    screenshotId: "settings-appearance",
    href: "/features#settings",
    tags: ["Appearance", "Preferences"],
  },
];

/** Modules in one rail group, in registry order. */
/** Lookup by the application's own navigation key. */
export function moduleByKey(key: string): ProductModule | undefined {
  return modules.find((item) => item.moduleKey === key);
}

export function modulesByGroup(group: ModuleGroup): ProductModule[] {
  return modules.filter((m) => m.railGroup === group);
}

/** Count of modules per group — used by the module browser chips. */
export function moduleCounts(): { group: ModuleGroup; count: number; note: string }[] {
  return moduleGroups.map((group) => ({
    group,
    count: modulesByGroup(group).length,
    note: groupNotes[group],
  }));
}

/**
 * The shell's top-bar utilities. They are real navigation targets in
 * GlassShell.xaml but they are tools rather than business modules, so they are
 * listed separately instead of padding the module catalogue.
 */
export const shellUtilities: { label: string; moduleKey: string; description: string }[] = [
  { label: "Notifications", moduleKey: "Notifications", description: "In-application notices raised by the shell." },
  { label: "User Switch", moduleKey: "UserSwitch", description: "Switch the signed-in user without closing the app." },
  { label: "Screenshot", moduleKey: "Screenshot", description: "Capture the current surface from inside the application." },
  { label: "Screen Record", moduleKey: "ScreenRecord", description: "Record a session — used for support conversations." },
  { label: "More", moduleKey: "More", description: "The overflow menu for secondary shell actions." },
  { label: "Logout", moduleKey: "Logout", description: "Return to the login screen." },
];
