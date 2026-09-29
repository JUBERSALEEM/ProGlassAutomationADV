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
  | "proforma"
  | "calculators"
  | "optimization"
  | "sheet-store"
  | "production"
  | "delivery"
  | "reports"
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
  { key: "proforma", label: "Proforma" },
  { key: "calculators", label: "Calculators" },
  { key: "optimization", label: "Optimization" },
  { key: "sheet-store", label: "Sheet Store" },
  { key: "production", label: "Production" },
  { key: "delivery", label: "Delivery" },
  { key: "reports", label: "Reports" },
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
];

export function screenshotsByCategory(category: ScreenshotCategory | "all"): Screenshot[] {
  if (category === "all") return screenshots;
  return screenshots.filter((s) => s.category === category);
}

export function screenshotById(id: string): Screenshot | undefined {
  return screenshots.find((s) => s.id === id);
}
