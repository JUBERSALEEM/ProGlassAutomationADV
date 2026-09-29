/**
 * Screenshot asset registry.
 *
 * HONESTY CONTRACT (see the project brief, §41):
 * The WPF application cannot be launched in the environment this website was
 * built in, so no genuine screen captures of the running application exist
 * yet. This site therefore does NOT present mock-ups, design targets or
 * generated images as if they were captures of ProGlass.
 *
 * Every entry either:
 *   • points at a REAL asset that ships inside the WPF repository
 *     (`source: "repo"`), or
 *   • is a declared placeholder (`source: "pending"`) that renders an
 *     explicit "capture pending" plate inside the application frame.
 *
 * When real captures are added, drop the file into
 * `public/images/<category>/` and set `src` + `source: "capture"`.
 */

export type ScreenshotCategory =
  | "dashboard"
  | "inquiry"
  | "proforma"
  | "calculators"
  | "specifications"
  | "optimization"
  | "sheet-store"
  | "production"
  | "delivery"
  | "finance"
  | "reports"
  | "master-data"
  | "settings"
  | "licensing";

export type ScreenshotSource = "capture" | "repo" | "pending";

export type Screenshot = {
  id: string;
  /** Title shown on the card and in the lightbox. */
  title: string;
  /** One-line description of what the screen does — taken from the app. */
  caption: string;
  /** The module key the application's navigation rail sends. */
  moduleKey: string;
  category: ScreenshotCategory;
  /** Real file path under /public. Real repo assets only. */
  src: string | null;
  width: number;
  height: number;
  source: ScreenshotSource;
  /** Where in the WPF repository this screen is defined. */
  evidence: string;
  /** For `source: "repo"` — what the asset actually is. */
  assetNote?: string;
  /** Highlights shown beside the preview. Sourced from the view + view-model. */
  highlights: string[];
  tags: string[];
};

export const screenshotCategories: { key: ScreenshotCategory | "all"; label: string }[] = [
  { key: "all", label: "All" },
  { key: "dashboard", label: "Dashboard" },
  { key: "inquiry", label: "Inquiry" },
  { key: "proforma", label: "Proforma" },
  { key: "calculators", label: "Calculators" },
  { key: "specifications", label: "Specifications" },
  { key: "optimization", label: "Optimization" },
  { key: "sheet-store", label: "Sheet Store" },
  { key: "production", label: "Production" },
  { key: "delivery", label: "Delivery" },
  { key: "finance", label: "Finance" },
  { key: "reports", label: "Reports" },
  { key: "master-data", label: "Master Data" },
  { key: "settings", label: "Settings" },
  { key: "licensing", label: "Licensing" },
];

export const screenshots: Screenshot[] = [
  {
    id: "overview-dashboard",
    title: "Overview Dashboard",
    caption:
      "One shared dashboard grammar across seven module dashboards, with an order-to-cash pipeline rail on Overview.",
    moduleKey: "Dashboard",
    category: "dashboard",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Dashboard/Overview/, Styles/DashboardKit.xaml",
    highlights: [
      "Quote → Production → Delivery → Invoice → Payment pipeline rail",
      "Seven module dashboards on one shared grammar",
      "Every page renders loading, error, empty and data states",
    ],
    tags: ["KPI", "Pipeline", "Overview"],
  },
  {
    id: "proforma-wizard",
    title: "Proforma Invoice — 9-step wizard",
    caption:
      "Company, client, project, pricing, spec grid, charges, summary, attachments and a profit & loss snapshot.",
    moduleKey: "ProformaInvoice",
    category: "proforma",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/ProformaInvoice/Step1…Step9, ViewModels/ProformaInvoice/",
    highlights: [
      "Step 5 multi-spec grid: W1/H1/W2/H2/qty → SQM + value",
      "Step 6 auto-imports sheet colours from Step 5",
      "Step 9 is the single pricing engine — everything downstream reads its snapshot",
    ],
    tags: ["Estimation", "Quotation", "VAT"],
  },
  {
    id: "glass-calculator-sgu",
    title: "SGU calculator",
    caption:
      "Single-glazed build-up: thickness, colour, treatment, edge polish, sheet size, price per SQM, yield, margin.",
    moduleKey: "ProformaInvoice",
    category: "calculators",
    src: "/images/glass3d/sgu-3d-1536.webp",
    width: 1536,
    height: 1024,
    source: "repo",
    assetNote:
      "Application asset Resources/Glass3D/sgu-3d.png — the single-glazed illustration shipped inside the product, declared in ProGlassAutomation.csproj.",
    evidence: "Resources/Glass3D/sgu-3d.png · ViewModels/ProformaInvoice/Step4PricingEstimator/SguParameters/",
    highlights: [
      "Thickness · COLOUR · TREATMENT · EDGE POLISH · PANEL EXTRA",
      "SHEET W · SHEET H · PRICE / SQM · YIELD · MARGIN %",
      "Sheet-store rates pulled into the estimator",
    ],
    tags: ["SGU", "Single lite", "Calculator"],
  },
  {
    id: "glass-calculator-dgu",
    title: "DGU calculator",
    caption:
      "Outer and inner pane, air space in millimetres, spacer system, argon, and DWG-driven extras.",
    moduleKey: "ProformaInvoice",
    category: "calculators",
    src: "/images/glass3d/dgu-3d-1536.webp",
    width: 1536,
    height: 1024,
    source: "repo",
    assetNote:
      "Application asset Resources/Glass3D/dgu-3d.png — the double-glazed unit illustration shipped inside the product.",
    evidence: "Resources/Glass3D/dgu-3d.png · ViewModels/ProformaInvoice/Step4PricingEstimator/DguParameters/",
    highlights: [
      "6–32 mm air space (ASP) options",
      "Normal Mill Aluminum · Black Aluminum · Stainless Steel · TGI Spacer · Warm Edge (Super Spacer) · Black Warm Edge",
      "U-Insert, argon gas, fanhole, cutouts and holes — each with a “per DWG” variant",
      "SPACER / SQM and PANEL EXTRA",
    ],
    tags: ["DGU", "Air space", "Spacer"],
  },
  {
    id: "glass-calculator-lam",
    title: "Lamination calculator",
    caption: "Laminated build-up with PVB thickness, colours, treatment and processing.",
    moduleKey: "ProformaInvoice",
    category: "calculators",
    src: "/images/glass3d/lam-3d-1536.webp",
    width: 1536,
    height: 1024,
    source: "repo",
    assetNote:
      "Application asset Resources/Glass3D/lam-3d.png — the laminated glass illustration shipped inside the product.",
    evidence: "Resources/Glass3D/lam-3d.png · ViewModels/ProformaInvoice/Step4PricingEstimator/LamParameters/",
    highlights: ["PVB THK (PVB thickness)", "Per-pane colour and treatment", "Feeds the same spec grid as SGU and DGU"],
    tags: ["Lamination", "PVB"],
  },
  {
    id: "glass-calculator-dgulam",
    title: "DGU + Lamination calculator",
    caption: "The combined build-up: a laminated pane inside a double-glazed unit.",
    moduleKey: "ProformaInvoice",
    category: "calculators",
    src: "/images/glass3d/dgulam-3d-1536.webp",
    width: 1536,
    height: 1024,
    source: "repo",
    assetNote:
      "Application asset Resources/Glass3D/dgulam-3d.png — the DGU + lamination illustration shipped inside the product.",
    evidence: "Resources/Glass3D/dgulam-3d.png · ViewModels/ProformaInvoice/Step4PricingEstimator/DguLamParameters/",
    highlights: [
      "Outer and inner pane with independent thickness, colour and treatment",
      "Air space and spacer selection",
      "Rates per build type on one pricing estimator",
    ],
    tags: ["DGU+LAM", "Combined"],
  },
  {
    id: "optimization-settings",
    title: "Optimization — engine configuration",
    caption:
      "House margins per thickness bucket, kerf, breakout, cutting complexity, strategy family and plate pattern.",
    moduleKey: "Optimization",
    category: "optimization",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Optimization/Tabs/SettingsTabView.xaml, OptimizationViewModel.Margins.cs",
    highlights: [
      "SAFETY EDGES: LM / RM / TM / BM per thickness",
      "KERF (blade gap) and MINIMUM BREAKOUT",
      "CUTTING COMPLEXITY: free guillotine → 3-stage → Z/W trims",
      "OPTIMIZE EFFORT 1–5 and the six plate patterns",
    ],
    tags: ["Engine", "Margins", "Strategy"],
  },
  {
    id: "optimization-2d",
    title: "Optimization — guillotine 2D view",
    caption: "The cutting layout per plate, with piece dimensions, rotation and cut tags.",
    moduleKey: "Optimization",
    category: "optimization",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Optimization/Tabs/TwoDViewTabView.xaml",
    highlights: [
      "Per-plate placements with DIMENSIONS (W×H)",
      "90° rotation marks",
      "CUT TAG and Breakout indicators",
      "Fit-the-whole-sheet zoom",
    ],
    tags: ["2D", "Layout", "Cutting"],
  },
  {
    id: "optimization-report",
    title: "Optimization — industrial report",
    caption:
      "Four printable sections plus CNC export, cut-list CSV and shop-floor tickets.",
    moduleKey: "Optimization",
    category: "optimization",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Optimization/Tabs/ReportTabView.xaml",
    highlights: [
      "1 · Industrial cut schedule",
      "2 · Nested sheets summary",
      "3 · Merged specifications summary",
      "4 · Stock plates allocation",
      "Exports: Bottero .OPT · Intermac .ISO · Lisec .DAT · generic DXF · CSV · Excel",
    ],
    tags: ["Report", "CNC", "Export"],
  },
  {
    id: "sheet-store-overview",
    title: "Sheet Store",
    caption:
      "Raw glass inventory with buy/use movements, balance at cost and at sell, and reorder levels.",
    moduleKey: "SheetStore",
    category: "sheet-store",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/SheetStore/, Services/SheetStore/",
    highlights: [
      "OPENING STOCK · BUY · USE · BALANCE AFTER",
      "CATEGORY · COLOUR · THICKNESS · WIDTH × HEIGHT",
      "PURCHASE / SQM and SELL / SQM · BALANCE AT COST / AT SELL",
      "CONSUMPTION, MOVEMENTS, ANALYSIS and REORDER LEVEL",
    ],
    tags: ["Stock", "Inventory", "Purchasing"],
  },
  {
    id: "production-stages",
    title: "Production stages",
    caption:
      "Cutting, Holes & Cutouts, Polish, Edge Deletion, Mitring, Tempering, Pairing and Doubling / Lamination, with a Process Work Center and a Production Control Center.",
    moduleKey: "ProductionControlCenter",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/, ViewModels/Production/",
    highlights: [
      "Stage-by-stage workstation views",
      "Production Control Center: assign, start, complete, hold, release hold, priority, stage",
      "Blocked reasons recorded against the job",
    ],
    tags: ["Production", "Workstation", "Control Center"],
  },
  {
    id: "delivery-note",
    title: "Delivery Note",
    caption: "Multi-movement dispatch with pending-piece tracking and a signed/received trail.",
    moduleKey: "DeliveryNote",
    category: "delivery",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/DeliveryNote/, Services/DeliveryNote/",
    highlights: [
      "Draft · Dispatching · Dispatched · Signed · Received · Delivered",
      "Partial deliveries with balance tracking",
      "Prevents over-delivery at validation time",
      "Delivery types, FREE charge rows, PDF / Excel / CSV export",
    ],
    tags: ["Dispatch", "Balance", "Delivery"],
  },
  {
    id: "vat-submission",
    title: "VAT Submission report",
    caption:
      "VAT figures gathered from the tax invoices for a period, ready to transcribe into a return.",
    moduleKey: "VatSubmission",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "ViewModels/Reports/VatSubmission/VatSubmissionViewModel.cs, Services/Reporting/VatCalculator.cs",
    highlights: [
      "One centralised VAT calculator — a single rounding rule",
      "5% UAE VAT, VAT-from-subtotal and VAT-from-gross back-calculation",
      "Tolerance-checked to 1 fils (0.01 AED)",
      "No direct filing to any tax authority portal",
    ],
    tags: ["VAT", "FTA", "Finance"],
  },
  {
    id: "reports-hub",
    title: "Reporting",
    caption:
      "Sales, Proforma, Job Order, Delivery, Tax Invoice and Statement reports reading persisted figures.",
    moduleKey: "SalesReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "ViewModels/Reports/, Services/Reporting/",
    highlights: [
      "Ranked by value, by area (SQM) or by pieces",
      "Ageing analysis and customer statements",
      "Reporting reads money — it never recomputes it",
      "PDF, Excel (.xlsx) and CSV export",
    ],
    tags: ["Reports", "Analytics", "Export"],
  },
  {
    id: "settings-appearance",
    title: "Settings",
    caption:
      "Appearance, themes, module wallpapers, feature toggles, backup & restore, updates and licensing.",
    moduleKey: "Settings",
    category: "settings",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Settings/, ViewModels/Settings/",
    highlights: [
      "Light and dark themes with an appearance section",
      "Backup sets with a documented retention and a restore drill",
      "Feature toggles that change what the modules show",
      "Updates from a release channel",
    ],
    tags: ["Settings", "Theme", "Backup"],
  },
  {
    id: "licensing-activation",
    title: "Licensing & activation",
    caption:
      "Offline activation of a signed licence key, an automatic evaluation trial, and edition-based feature gating.",
    moduleKey: "About",
    category: "licensing",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Licensing/, Services/Licensing/, Licensing/Contracts/",
    highlights: [
      "RSA-2048-PSS signed licence files with no v1.5 fallback",
      "Automatic 30-day evaluation trial recorded by a tamper-evident ledger",
      "Editions: Trial · Basic · Business · Factory · Enterprise",
      "Expired licence degrades to read-only rather than losing data",
    ],
    tags: ["Licensing", "Activation", "Trial"],
  },
  {
    id: "inquiries",
    title: "Inquiry intake",
    caption:
      "Scope of supply, customer and project captured once, with attachments held by content hash.",
    moduleKey: "Inquiry",
    category: "inquiry",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Inquiry/, ViewModels/Dashboard/Inquiry/, Services/Inquiry/",
    highlights: [
      "Scope-of-supply capture with the customer and project cards",
      "Attachments deduplicated by SHA-256",
      "Status vocabulary with an audited transition trail",
    ],
    tags: ["Intake", "Attachments"],
  },
  {
    id: "job-order",
    title: "Job Order",
    caption:
      "The confirmed sale expressed as work, sharing one specification record with the quotation.",
    moduleKey: "JobOrder",
    category: "proforma",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/JobOrder/, ViewModels/Dashboard/JobOrder/, Services/JobOrder/Mapper/",
    highlights: [
      "Created only from an approved proforma invoice",
      "Release to production from the job order",
      "No re-typing — the specifications are the quotation's own",
    ],
    tags: ["Work order", "Release"],
  },
  {
    id: "specifications",
    title: "Specifications library",
    caption:
      "Every build-up line with its dimensions, area and value — what traceability resolves against.",
    moduleKey: "Specifications",
    category: "specifications",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Specifications/, ViewModels/Specifications/",
    highlights: [
      "SGU, DGU, LAM and DGU + LAM lines with W1/H1 and W2/H2",
      "Area (SQM) and value computed per line",
      "Feeds the cutting list, production distribution and reports",
    ],
    tags: ["Build-up", "Library"],
  },
  {
    id: "process-work-center",
    title: "Process Work Center",
    caption:
      "The whole floor in one filterable table, whatever stage a piece is standing at.",
    moduleKey: "ProcessWorkCenter",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/ProcessWorkCenterView.xaml, ViewModels/Production/ProcessWorkCenterViewModel.cs",
    highlights: [
      "Filter by process, job, customer, PI, JO, spec, material, colour, thickness, status, date",
      "Columns: job, spec, piece, process, previous stage, status, qty, progress, QC, operator",
      "Actions: Start, Pause, Complete, Breakage, Rework, QC, View Traceability",
    ],
    tags: ["Shop floor", "Filtering"],
  },
  {
    id: "production-control-center",
    title: "Production Control Center",
    caption:
      "The floor from above: KPI cards, a live production queue and traceability for the selection.",
    moduleKey: "ProductionControlCenter",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/ProductionControlCenterView.xaml, ViewModels/Production/ProductionControlCenterViewModel.cs",
    highlights: [
      "KPI cards across the floor rather than per stage",
      "Production queue with selection",
      "Selected work item traced back to its documents",
    ],
    tags: ["Floor", "Traceability"],
  },
  {
    id: "stage-cutting",
    title: "Production stage — Cutting",
    caption:
      "Stage one of eight: the two-tier job list and the full-page job detail the stages share.",
    moduleKey: "Cutting",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/CuttingView.xaml, ViewModels/Production/CuttingViewModel.cs",
    highlights: [
      "Job list: # · job no · customer · total · done · balance with a status chip",
      "Detail grid with the job's outer and inner cut lists",
      "Qty Done clamped to what arrived upstream; recent entries log",
    ],
    tags: ["Stage 1", "Piece flow"],
  },
  {
    id: "stage-holes-cutouts",
    title: "Production stage — Holes & Cutouts",
    caption:
      "Fan holes, holes, cutouts and countersunk holes, from the quotation's own charge lines.",
    moduleKey: "HolesCutouts",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/HolesCutoutsView.xaml, Services/Production/ProductionDistributionEngine.cs",
    highlights: [
      "Work generated from FAN_HOLES, HOLES, CUTOUT and COUNTERSUNK_HOLES lines",
      "Quantities carried per specification row",
      "Same piece-flow accounting as every station",
    ],
    tags: ["Stage 2", "Openings"],
  },
  {
    id: "stage-polish",
    title: "Production stage — Polish",
    caption:
      "Edge polishing measured in the linear metres the quotation charged for.",
    moduleKey: "Polish",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/PolishView.xaml, ViewModels/Production/PolishViewModel.cs",
    highlights: [
      "Linear-metre work lines per specification row",
      "Arrived / done / balance per row and per job",
      "Operator attribution on every entry",
    ],
    tags: ["Stage 3", "Edges"],
  },
  {
    id: "stage-edge-deletion",
    title: "Production stage — Edge Deletion",
    caption:
      "Coating removal where a sealed unit or a bonded edge needs the surface taken back.",
    moduleKey: "EdgeDeletion",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/EdgeDeletionView.xaml, ViewModels/Production/EdgeDeletionViewModel.cs",
    highlights: [
      "Lines derived from the built-up specification",
      "Job totals and per-row balances",
      "Piece flow into the next stage",
    ],
    tags: ["Stage 4", "Coating"],
  },
  {
    id: "stage-mitring",
    title: "Production stage — Mitring",
    caption:
      "Mitred edges, generated from the quotation's mitring lines.",
    moduleKey: "Mitring",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/MitringView.xaml, ViewModels/Production/MitringViewModel.cs",
    highlights: [
      "Mitring work from the quotation's charge lines",
      "Linear-metre quantities with balance tracking",
      "Operator, timestamp and quantity on each entry",
    ],
    tags: ["Stage 5", "Edges"],
  },
  {
    id: "stage-tempering",
    title: "Production stage — Tempering",
    caption:
      "The thermal stage, queued only for the treatments that require it.",
    moduleKey: "Tempering",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/TemperingView.xaml, Views/Production/README.md",
    highlights: [
      "FT, Heat Strengthened and Heat Soaked panes route here",
      "Annealed panes bypass the stage rather than queueing",
      "Completion gated on what arrived from cutting",
    ],
    tags: ["Stage 6", "Thermal"],
  },
  {
    id: "stage-pairing",
    title: "Production stage — Pairing",
    caption:
      "Bringing the pane occurrences of one unit back together before sealing.",
    moduleKey: "Pairing",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/PairingView.xaml, ViewModels/Production/PairingViewModel.cs",
    highlights: [
      "Outer W1/H1 and inner or laminate pair W2/H2",
      "DGU, LAM and DGU + LAM rows split into pane occurrences",
      "Units paired before the final stage",
    ],
    tags: ["Stage 7", "Assembly"],
  },
  {
    id: "stage-doubling-lamination",
    title: "Production stage — Doubling / Lamination",
    caption:
      "The final stage: the unit is sealed, and the charge notes that describe how are on the work item.",
    moduleKey: "DoublingLamination",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Production/DoublingLaminationView.xaml, Services/Production/ProductionDistributionEngine.cs",
    highlights: [
      "ARGON GAS FILL · SILICON BED SEAL · U-INSERT SEALING",
      "STEPPED OVERLAP EDGES · SHUTTER BONDING",
      "Completing the chain marks the unit ready for dispatch",
    ],
    tags: ["Stage 8", "Assembly"],
  },
  {
    id: "report-generator",
    title: "Order Report Generator",
    caption:
      "The four-step wizard that reconciles the whole document chain for one proforma or many.",
    moduleKey: "OrderReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "ViewModels/Reports/OrderReport/, Views/Reports/OrderReport/",
    highlights: [
      "Filter → Preview → Pipeline → Review",
      "Single-PI or multi-PI batches",
      "Reconciliation and attachment-completeness engine",
    ],
    tags: ["Reconciliation", "Batch"],
  },
  {
    id: "statement-generator",
    title: "Statement Generator",
    caption:
      "Customer statements with ageing, saved so a statement can be re-sent without changing.",
    moduleKey: "StatementGenerator",
    category: "finance",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "ViewModels/Reports/StatementGenerator/, Views/Reports/StatementGenerator/",
    highlights: [
      "Filter → Preview → Pipeline → Review",
      "Summary statement PDF with ageing analysis",
      "Combined PDF including signed PI and signed delivery notes",
    ],
    tags: ["Statement", "Ageing"],
  },
  {
    id: "job-profitability",
    title: "Job Profitability",
    caption:
      "Margin per job, read from the snapshot written at the moment of quoting.",
    moduleKey: "JobProfitability",
    category: "finance",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "ViewModels/JobProfitability/, ViewModels/ProformaInvoice/Step9ProfitLoss/",
    highlights: [
      "Per-job P&L from the stored Step 9 snapshot",
      "Cost basis recorded with the figure",
      "Moved prices flagged rather than rescored",
    ],
    tags: ["Margin", "Snapshot"],
  },
  {
    id: "report-glass-spec",
    title: "Glass Specifications & Traceability",
    caption:
      "Search a specification and follow it back through every document that carries it.",
    moduleKey: "SalesReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "ViewModels/Reports/GlassSpecReport/, Services/Reporting/SpecificationMatcher.cs",
    highlights: [
      "Search by build-up, thickness, colour and treatment",
      "Row-level trace back to PI, job, delivery and invoice",
      "The report the whole product exists for",
    ],
    tags: ["Traceability", "Specs"],
  },
  {
    id: "report-executive",
    title: "Reports — Executive Summary",
    caption:
      "The one-page order-to-cash funnel for the whole business.",
    moduleKey: "SalesReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "ViewModels/Reports/ExecutiveReport/, Views/Reports/ExecutiveReport/",
    highlights: [
      "Quoted → ordered → produced → delivered → invoiced → collected",
      "Company-level view over one central data layer",
      "Behind the reports hub rail",
    ],
    tags: ["Funnel", "Summary"],
  },
  {
    id: "report-customer",
    title: "Reports — Customer Directory",
    caption:
      "One row per customer with value, receivables and ageing attached.",
    moduleKey: "SalesReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "ViewModels/Reports/CustomerReport/, Views/Reports/CustomerReport/",
    highlights: [
      "Rank by value, area (SQM) or pieces",
      "Outstanding and overdue on the same row",
      "Shared sorting, search and export",
    ],
    tags: ["Customers", "Receivables"],
  },
  {
    id: "company-center",
    title: "Company Center",
    caption:
      "The company record every printed document is issued from.",
    moduleKey: "CompanyCenter",
    category: "master-data",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/CompanyCenter/, ViewModels/CompanyCenter/",
    highlights: [
      "Company identity and tax registration used on printed documents",
      "Branding across every document type",
      "The record the company reports group by",
    ],
    tags: ["Master data", "Documents"],
  },
  {
    id: "user-center",
    title: "User Center",
    caption:
      "Users, the eight assignable roles and the permissions each role carries.",
    moduleKey: "UserCenter",
    category: "master-data",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/UserCenter/, ViewModels/UserCenter/, Services/Audit/Permissions.cs",
    highlights: [
      "8 assignable roles plus the Operator role",
      "34 live Module.Capability permission constants",
      "Enforced at the service boundary, not the screen",
    ],
    tags: ["Users", "Permissions"],
  },
  {
    id: "system-center",
    title: "System Center",
    caption:
      "Users and the activity log — where the audit trail is read.",
    moduleKey: "SystemCenter",
    category: "settings",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/SystemCenter/, ViewModels/SystemCenter/, Services/Audit/",
    highlights: [
      "Activity log with module and operation context",
      "Create, edit, delete, print and login recorded",
      "No keystroke or mouse recording — actions only",
    ],
    tags: ["Audit", "Admin"],
  },
  {
    id: "diagnostics",
    title: "Diagnostics",
    caption:
      "The health surface, including crash reports, reachable from the shell.",
    moduleKey: "Diagnostics",
    category: "settings",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Views/Diagnostics/, ViewModels/Diagnostics/, Views/CrashReports/",
    highlights: [
      "Crash report capture and review",
      "Application health and state checks",
      "Log surfaces for support conversations",
    ],
    tags: ["Health", "Support"],
  },
];

export function screenshotsByCategory(category: ScreenshotCategory | "all"): Screenshot[] {
  if (category === "all") return screenshots;
  return screenshots.filter((s) => s.category === category);
}

export function screenshotById(id: string): Screenshot | undefined {
  return screenshots.find((s) => s.id === id);
}
