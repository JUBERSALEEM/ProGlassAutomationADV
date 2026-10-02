/**
 * Screenshot asset registry and capability showcase configuration.
 *
 * Product Showcase Architecture:
 * - Curated into 6 customer-facing capability groups:
 *   1. Estimation & Commercial
 *   2. Glass Calculators
 *   3. Optimization
 *   4. Production
 *   5. Inventory & Dispatch
 *   6. Business & Finance
 * - Displays interactive module previews with sample data and real application chrome.
 * - Sourced from authentic desktop application layouts, business rules, and 3D assets.
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
  /** Title shown on the card and in the showcase viewer. */
  title: string;
  /** One-line description of what the screen does for a glass fabrication plant. */
  caption: string;
  /** The module key the application's navigation rail sends. */
  moduleKey: string;
  category: ScreenshotCategory;
  /** Real file path under /public for authentic repo assets. */
  src: string | null;
  width: number;
  height: number;
  source: ScreenshotSource;
  /** Customer-facing capability classification and architecture scope. */
  evidence: string;
  /** For 3D renders — what the asset illustrates. */
  assetNote?: string;
  /** Operational highlights shown beside the preview. */
  highlights: string[];
  tags: string[];
};

export type CapabilityGroupId =
  | "estimation-commercial"
  | "glass-calculators"
  | "optimization"
  | "production"
  | "inventory-dispatch"
  | "business-finance";

export type CapabilityGroup = {
  id: CapabilityGroupId;
  label: string;
  shortLabel: string;
  tagline: string;
  description: string;
  icon: "calculator" | "layers" | "scissors" | "factory" | "warehouse" | "chart";
  screenshotIds: string[];
};

export const capabilityGroups: CapabilityGroup[] = [
  {
    id: "estimation-commercial",
    label: "Estimation & Commercial",
    shortLabel: "Estimation",
    tagline: "Fast quotes, multi-tier glass specifications, and instant margin protection",
    description:
      "Accelerate quoting with 9-step proforma invoices, automatic 0.5 m² minimum panel billing rules, dynamic thickness and surcharge arithmetic, and seamless conversion to job orders.",
    icon: "calculator",
    screenshotIds: ["proforma-wizard", "inquiries", "job-order", "specifications"],
  },
  {
    id: "glass-calculators",
    label: "Glass Calculators",
    shortLabel: "Calculators",
    tagline: "3D architectural build-up modeling with real-time parametric costing",
    description:
      "Interactive 3D visual calculators for monolithic single glazed (SGU), double-glazed insulating units (DGU), laminated safety glass, and acoustic composite assemblies.",
    icon: "layers",
    screenshotIds: [
      "glass-calculator-sgu",
      "glass-calculator-dgu",
      "glass-calculator-lam",
      "glass-calculator-dgulam",
    ],
  },
  {
    id: "optimization",
    label: "Optimization",
    shortLabel: "Optimization",
    tagline: "Industrial 2D guillotine nesting engine achieving 92%+ cutting yields",
    description:
      "Automate rectangular sheet cutting patterns with kerf control, edge trims, grain rotation constraints, and direct DXF cutting table export to eliminate glass scrap.",
    icon: "scissors",
    screenshotIds: ["optimization-2d", "optimization-settings", "optimization-report"],
  },
  {
    id: "production",
    label: "Production",
    shortLabel: "Production",
    tagline: "End-to-end 8-stage shop floor tracking and machine work centers",
    description:
      "Track glass panels in real time across CNC cutting, edge polishing, drilling, tempering furnace beds, IGU pairing, and autoclave lamination with operator stage sign-offs.",
    icon: "factory",
    screenshotIds: [
      "production-stages",
      "production-control-center",
      "stage-cutting",
      "stage-tempering",
      "stage-pairing",
      "stage-doubling-lamination",
    ],
  },
  {
    id: "inventory-dispatch",
    label: "Inventory & Dispatch",
    shortLabel: "Inventory",
    tagline: "Stocked sheet warehouse management and delivery logistics",
    description:
      "Maintain real-time inventory of standard and jumbo glass packs. Generate vehicle loading manifests, delivery notes, and verified site delivery receipts.",
    icon: "warehouse",
    screenshotIds: ["sheet-store-overview", "delivery-note"],
  },
  {
    id: "business-finance",
    label: "Business & Finance",
    shortLabel: "Finance",
    tagline: "Executive KPI dashboards, job-by-job profitability, and VAT compliance",
    description:
      "Gain complete operational and fiscal visibility: pipeline throughput dashboards, order margin analysis, periodic customer statements, and UAE VAT-compliant audit filing.",
    icon: "chart",
    screenshotIds: [
      "overview-dashboard",
      "job-profitability",
      "statement-generator",
      "vat-submission",
      "reports-hub",
    ],
  },
];

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
    evidence: "Executive KPI & Operational Pipeline Module",
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
      "Company, client, project, pricing, spec grid, charges, summary, attachments and the profit-and-loss analysis.",
    moduleKey: "ProformaInvoice",
    category: "proforma",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Commercial Estimation & Dynamic Pricing Engine",
    highlights: [
      "The specification grid on step 5: # · glass ref · W1 H1 W2 H2 · qty · SQM · total SQM · rate · value, on a block carrying glass type, specification, base rate and SURCHARGE %",
      "Every panel bills at least 0.5 m², and a panel that reaches 4 m² prices at base + the specification's surcharge",
      "QUICK / OPTIMIZE / VIEW RESULT on the grid — with the engine's own progress readout — and Step 6 takes its sheet quantity and utilisation from the run",
      "Find and replace, Excel paste, bulk row generation and a grand total summary rolled up under the grid",
      "Step 9 profit & loss: overview, services, sheets and a spec-by-spec check — the single pricing engine, whose snapshot everything downstream reads",
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
      "Official 3D architectural illustration: single-glazed monolithic build-up with edge polishing and custom notch works.",
    evidence: "Single Glazed Monolithic Estimator · 3D Model",
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
      "Double-glazed unit: outer pane, air spacer / gas cavity, inner pane, secondary seal, thermal performance.",
    moduleKey: "ProformaInvoice",
    category: "calculators",
    src: "/images/glass3d/dgu-3d-1536.webp",
    width: 1536,
    height: 1024,
    source: "repo",
    assetNote:
      "Official 3D architectural illustration: double-glazed insulating glass unit with primary butyl and secondary silicone seal.",
    evidence: "Insulating Glass Unit (IGU) Estimator · 3D Model",
    highlights: [
      "Outer glass specification + inner glass specification",
      "Cavity width (6, 8, 10, 12, 14, 16, 18, 20 mm) and gas fill (air, argon 90%, krypton)",
      "Primary butyl seal + secondary silicone / polysulphide seal",
    ],
    tags: ["DGU", "Double glazing", "Insulating glass", "Calculator"],
  },
  {
    id: "glass-calculator-lam",
    title: "Lamination calculator",
    caption:
      "Multi-ply laminated build-up: pane 1, interlayer (PVB / SGP / EVA), pane 2, acoustic rating, security grade.",
    moduleKey: "ProformaInvoice",
    category: "calculators",
    src: "/images/glass3d/lam-3d-1536.webp",
    width: 1536,
    height: 1024,
    source: "repo",
    assetNote:
      "Official 3D architectural illustration: multi-ply laminated glass with PVB/SGP acoustic and structural interlayer.",
    evidence: "Laminated Safety Glass Estimator · 3D Model",
    highlights: [
      "Pane 1 thickness + pane 2 thickness + interlayer calibre (0.38, 0.76, 1.52, 2.28 mm)",
      "PVB acoustic / SGP structural / EVA decorative interlayer selection",
      "Autoclave cycle costing + edge trim waste factor",
    ],
    tags: ["Lamination", "Safety glass", "PVB", "SGP", "Calculator"],
  },
  {
    id: "glass-calculator-dgulam",
    title: "DGU + Lamination calculator",
    caption:
      "High-performance composite build-up: laminated outer pane, gas cavity, monolithic inner pane — acoustic + thermal.",
    moduleKey: "ProformaInvoice",
    category: "calculators",
    src: "/images/glass3d/dgulam-3d-1536.webp",
    width: 1536,
    height: 1024,
    source: "repo",
    assetNote:
      "Official 3D architectural illustration: composite double-glazed laminated unit for high-performance acoustic and thermal facades.",
    evidence: "High-Performance Composite Glass Estimator · 3D Model",
    highlights: [
      "Laminated pane (glass + interlayer + glass) + cavity + monolithic pane",
      "Combined acoustic (Rw) and thermal (U-value, SHGC) performance modeling",
      "Composite weight calculation for crane and transport dispatch",
    ],
    tags: ["DGU+LAM", "High performance", "Acoustic", "Facade", "Calculator"],
  },
  {
    id: "optimization-settings",
    title: "Optimization — engine configuration",
    caption:
      "Kerf width, trim margins (left/right/top/bottom), minimum cut size, blade constraints and grain direction.",
    moduleKey: "Optimization",
    category: "optimization",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Guillotine Cutting Configuration & Margin Parameters",
    highlights: [
      "Kerf width in millimetres (blade loss per cut line)",
      "Sheet trim margins: left 15 mm, right 15 mm, top 3 mm, bottom 3 mm",
      "Grain direction enforcement for patterned and Low-E coated glass",
    ],
    tags: ["Optimization", "Kerf", "Settings", "CNC"],
  },
  {
    id: "optimization-2d",
    title: "Optimization — guillotine 2D view",
    caption:
      "Full sheet cutting layout with cut sequences, panel positions, offcut recovery areas and utilization percentages.",
    moduleKey: "Optimization",
    category: "optimization",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Interactive 2D Sheet Layout & Nesting Pattern",
    highlights: [
      "True guillotine cuts — edge-to-edge passes executable on industrial tables",
      "Color-coded panels with piece labels, dimensions and job order references",
      "Offcut tagging: reusable remnants catalogued with dimensions and storage bay",
    ],
    tags: ["2D View", "Cutting pattern", "Nesting", "Yield"],
  },
  {
    id: "optimization-report",
    title: "Optimization — industrial report",
    caption:
      "Printable cutting pattern report with sheet summaries, cut sequence instructions, total yield and scrap breakdown.",
    moduleKey: "Optimization",
    category: "optimization",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Industrial Cutting Schedule & Yield Diagnostics",
    highlights: [
      "Pattern-by-pattern breakdown: sheets required, pieces cut, square metres used",
      "Overall yield percentage vs scrap percentage with cost impact",
      "Operator cut sequence: X-cut first, then Y-cut, then Z-trim",
    ],
    tags: ["Report", "Cutting ticket", "Shop floor", "Print"],
  },
  {
    id: "sheet-store-overview",
    title: "Sheet Store",
    caption:
      "Raw glass inventory: standard sheets, jumbo plates, split cases, reserved stock and automated reorder levels.",
    moduleKey: "SheetStore",
    category: "sheet-store",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Sheet Store Inventory & Jumbo Plate Ledger",
    highlights: [
      "Live plate count by thickness (4, 5, 6, 8, 10, 12, 15, 19 mm) and tint",
      "Reserved vs available stock linked to active proformas and job orders",
      "Jumbo (3660 × 2440), standard (3210 × 2250) and split-pack tracking",
    ],
    tags: ["Inventory", "Stock", "Raw glass", "Jumbo"],
  },
  {
    id: "production-stages",
    title: "Production stages",
    caption:
      "8-stage sequential tracking: Cutting → Holes & Cutouts → Polish → Edge Deletion → Mitring → Tempering → Pairing → Doubling.",
    moduleKey: "ProcessWorkCenter",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "8-Stage Industrial Glass Processing Flow",
    highlights: [
      "Sequential stage gates: a panel cannot skip tempering before pairing",
      "Per-stage operator sign-off with timestamp and station ID",
      "Defect and reject tracking at each stage with rework routing",
    ],
    tags: ["Workflow", "Stages", "Tracking", "Shop floor"],
  },
  {
    id: "delivery-note",
    title: "Delivery Note",
    caption:
      "Dispatch manifest: customer, site address, vehicle, driver, panel schedule with dimensions, weights and balance.",
    moduleKey: "DeliveryNote",
    category: "delivery",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Dispatch Logistics & Vehicle Loading Manifest",
    highlights: [
      "Auto-generated from completed production batches",
      "Total weight calculation for truck capacity compliance",
      "Partial delivery support: tracks delivered quantity vs remaining balance",
    ],
    tags: ["Delivery", "Dispatch", "Logistics", "Sign-off"],
  },
  {
    id: "vat-submission",
    title: "VAT Submission report",
    caption:
      "Standard-rated, zero-rated and exempt supply breakdown with input/output tax reconciliation for FTA filing.",
    moduleKey: "VatSubmission",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Tax Invoicing & UAE VAT Compliance Audit",
    highlights: [
      "Compliant with UAE Federal Tax Authority (FTA) 5% VAT rules",
      "Box-by-box breakdown matching official VAT return forms",
      "Drill-down to individual tax invoices and credit notes",
    ],
    tags: ["VAT", "Tax", "FTA", "Compliance", "Finance"],
  },
  {
    id: "reports-hub",
    title: "Reporting",
    caption:
      "Enterprise reports across sales, production, inventory, delivery and finance with multi-format export.",
    moduleKey: "SalesReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Multi-Dimensional Operational Reports Hub",
    highlights: [
      "Sales performance by representative, customer and glass type",
      "Production yield and furnace utilization reports",
      "Export to Excel, PDF and CSV with preserved formatting",
    ],
    tags: ["Reports", "Analytics", "Export", "BI"],
  },
  {
    id: "settings-appearance",
    title: "Settings",
    caption:
      "Application configuration: theme (Light, Dark, Glass, Industrial), locale (15 languages), station roles and peripherals.",
    moduleKey: "Settings",
    category: "settings",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Application Preferences & Station Configuration",
    highlights: [
      "15 interface languages including RTL support for Arabic and Urdu",
      "Theme customization with Mica/Acrylic glass-morphic styling",
      "Label printer and barcode scanner integration settings",
    ],
    tags: ["Settings", "Theme", "i18n", "Hardware"],
  },
  {
    id: "licensing-activation",
    title: "Licensing & activation",
    caption:
      "Hardware-bound cryptographic licensing: machine fingerprint, trial activation, perpetual key validation and station seat management.",
    moduleKey: "Settings",
    category: "licensing",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Licensing & Hardware Station Activation",
    highlights: [
      "SHA-256 machine fingerprinting (CPU, motherboard, NIC binding)",
      "Offline activation support via encrypted license certificate file",
      "Feature flags by edition: Starter, Professional, Enterprise",
    ],
    tags: ["License", "Security", "Activation", "Anti-tamper"],
  },
  {
    id: "inquiries",
    title: "Inquiry intake",
    caption:
      "Rapid inquiry logging: customer details, project reference, preliminary glass specifications, quantities and target delivery dates.",
    moduleKey: "Inquiry",
    category: "inquiry",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Inquiry Intake & Line Specification Tracking",
    highlights: [
      "Quick-entry grid for multi-line customer inquiries",
      "Automatic reference numbering: INQ-YYYY-NNNN",
      "One-click conversion into a formal Proforma Invoice",
    ],
    tags: ["Inquiry", "CRM", "Intake", "Sales"],
  },
  {
    id: "job-order",
    title: "Job Order",
    caption:
      "Confirmed order processing: converts approved proforma into production-ready job orders with barcode generation and schedule assignment.",
    moduleKey: "JobOrder",
    category: "proforma",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Production Work Order & Job Scheduling",
    highlights: [
      "Automatic generation from approved Proforma Invoices",
      "Unique panel barcode generation (Code 128 / QR)",
      "Stock reservation triggered automatically on job release",
    ],
    tags: ["Job Order", "Work order", "Scheduling", "Barcodes"],
  },
  {
    id: "specifications",
    title: "Specifications library",
    caption:
      "Master glass catalogue: single-glazed, DGU, laminated and composite definitions with base rates, surcharges and technical constraints.",
    moduleKey: "Specifications",
    category: "specifications",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Master Glass Specifications & Edge Treatment Rules",
    highlights: [
      "Master library of approved glass build-ups and configurations",
      "Rate matrices: base rate/m², oversize surcharge, processing add-ons",
      "Technical limit rules: max dimensions, aspect ratios, min hole distances",
    ],
    tags: ["Catalogue", "Master data", "Rules", "Rates"],
  },
  {
    id: "process-work-center",
    title: "Process Work Center",
    caption:
      "Station-level execution queue: displays pending panels, batch details, process instructions and allows barcode-driven completion sign-off.",
    moduleKey: "ProcessWorkCenter",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Work Center Dispatch & Machine Queues",
    highlights: [
      "Dedicated touch-friendly station view for machine operators",
      "Barcode scanner input for rapid panel check-in and check-out",
      "Real-time cycle time tracking per panel and batch",
    ],
    tags: ["Shop floor", "Operator", "Station", "Barcode"],
  },
  {
    id: "production-control-center",
    title: "Production Control Center",
    caption:
      "Plant-wide supervisor dashboard: active jobs across all 8 stages, machine bottlenecks, furnace queue depth and on-time delivery forecasts.",
    moduleKey: "ProductionControlCenter",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Production Scheduling & Throughput Center",
    highlights: [
      "Kanban-style board showing panels across all 8 processing stages",
      "Furnace load optimization: bed packing recommendations for tempering",
      "Bottleneck alerts when a stage queue exceeds threshold",
    ],
    tags: ["Supervisor", "Kanban", "Bottlenecks", "Throughput"],
  },
  {
    id: "stage-cutting",
    title: "Production stage — Cutting",
    caption:
      "Primary glass cutting station: sheet loading, CNC cutting file dispatch, breakout verification and initial barcode label application.",
    moduleKey: "Cutting",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "CNC Cutting Station & Breakout Tracking",
    highlights: [
      "Sheet pull from store: validates glass code, thickness and batch",
      "Breakout sequence guide matching the optimization cut order",
      "Scrap sheet and offcut registration at the cutting table",
    ],
    tags: ["Cutting", "CNC", "Breakout", "Labels"],
  },
  {
    id: "stage-holes-cutouts",
    title: "Production stage — Holes & Cutouts",
    caption:
      "CNC machining center: hole drilling, notch cutting, corner radius routing and socket cutout verification against technical drawings.",
    moduleKey: "HolesCutouts",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "CNC Drilling & Edge Cutout Work Center",
    highlights: [
      "Hole diameter and position validation against edge-distance rules",
      "Notch and cutout geometry checks to prevent tempering fractures",
      "Tool wear tracking: drill bit cycle counter and replacement alerts",
    ],
    tags: ["Drilling", "CNC", "Notches", "Cutouts"],
  },
  {
    id: "stage-polish",
    title: "Production stage — Polish",
    caption:
      "Edge processing station: straight-line edgers, double-edgers and CNC polishers with flat, pencil, bevel and miter edge profile tracking.",
    moduleKey: "Polish",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Straight-Line & CNC Edge Polishing Center",
    highlights: [
      "Edge profile tracking: flat polish, pencil, arrissed, beveled",
      "Linear meter calculation for edge-processing billing accuracy",
      "Line speed and polishing wheel consumption monitoring",
    ],
    tags: ["Polishing", "Edging", "Profiles", "Linear meters"],
  },
  {
    id: "stage-edge-deletion",
    title: "Production stage — Edge Deletion",
    caption:
      "Low-E coating removal: perimeter deletion width, depth and uniformity verification prior to insulated glass unit secondary sealing.",
    moduleKey: "EdgeDeletion",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Low-E Coating Edge Deletion Station",
    highlights: [
      "Deletion width validation (10-15 mm standard, custom widths)",
      "Coating conductivity probe check after grinding pass",
      "Critical quality gate: prevents seal failure in IGUs",
    ],
    tags: ["Low-E", "Edge deletion", "IGU", "Quality"],
  },
  {
    id: "stage-mitring",
    title: "Production stage — Mitring",
    caption:
      "Variable-angle edge grinding: 0° to 60° chamfer and miter cuts for structural glass balustrades, showcase corners and facade joints.",
    moduleKey: "Mitring",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Variable-Angle Edge Mitring Center",
    highlights: [
      "Angle precision tracking (45° corner joints, custom angles)",
      "Remaining glass edge thickness measurement (nose dimension)",
      "Feed speed and coolant flow monitoring for edge clarity",
    ],
    tags: ["Mitring", "Chamfer", "Balustrades", "Angles"],
  },
  {
    id: "stage-tempering",
    title: "Production stage — Tempering",
    caption:
      "Thermal toughening furnace: bed layout composition, heating zone profile, quench pressure, cycle time and fragmentation test logging.",
    moduleKey: "Tempering",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Tempering Furnace Bed & Thermal Safety Center",
    highlights: [
      "Bed layout planner: maximizes furnace load area safely",
      "Heating profile recipes by glass thickness, Low-E type and tint",
      "EN 12150 / ASTM C1048 compliance test result recording",
    ],
    tags: ["Tempering", "Furnace", "Thermal", "Safety", "EN 12150"],
  },
  {
    id: "stage-pairing",
    title: "Production stage — Pairing",
    caption:
      "IGU assembly line: automated glass washing, desiccant-filled spacer frame application, gas cavity flushing and secondary seal extrusion.",
    moduleKey: "Pairing",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "IGU Assembly, Gas Cavity & Sealant Station",
    highlights: [
      "Automated pane pairing: matches outer and inner lites from job order",
      "Gas filling control: Argon 90%+ sensor validation",
      "Secondary seal depth verification (silicone / polysulphide)",
    ],
    tags: ["IGU", "Pairing", "Assembly", "Spacer", "Argon"],
  },
  {
    id: "stage-doubling-lamination",
    title: "Production stage — Doubling / Lamination",
    caption:
      "Laminated glass assembly: cleanroom layup, interlayer positioning, vacuum de-airing ring/bagging and autoclave heat/pressure cycle.",
    moduleKey: "DoublingLamination",
    category: "production",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Lamination Cleanroom Layup & Autoclave",
    highlights: [
      "Cleanroom climate log: temperature and relative humidity tracking",
      "Autoclave recipe control: ramp-up, dwell (135°C @ 12 bar), cool-down",
      "Post-autoclave inspection: bubbles, delamination and haze checks",
    ],
    tags: ["Lamination", "Autoclave", "PVB", "Cleanroom", "Safety"],
  },
  {
    id: "report-generator",
    title: "Order Report Generator",
    caption:
      "Comprehensive job dossier: technical specifications, dimensional cut lists, processing status, quality sign-offs and delivery slips.",
    moduleKey: "OrderReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Order Specification & Fulfillment Report",
    highlights: [
      "One-click complete job packet for production supervisors",
      "Combined commercial, technical and dispatch documentation",
      "Print and PDF generation with corporate branding and signatures",
    ],
    tags: ["Report", "Dossier", "Job pack", "Documentation"],
  },
  {
    id: "statement-generator",
    title: "Statement Generator",
    caption:
      "Customer account statements: opening balances, invoices issued, credit notes, payments received and aging balance analysis (30/60/90 days).",
    moduleKey: "StatementGenerator",
    category: "finance",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Customer Balance & Periodic Statement Engine",
    highlights: [
      "Multi-period statements: monthly, quarterly, custom date range",
      "Aging analysis: Current, 30 days, 60 days, 90+ days overdue",
      "Auto-generated payment remittance slip with bank details",
    ],
    tags: ["Statements", "Accounts receivable", "Aging", "Finance"],
  },
  {
    id: "job-profitability",
    title: "Job Profitability",
    caption:
      "Job-by-job financial audit: quoted price vs actual costs (raw glass, interlayers, gas, consumables, labor hours, machine overhead).",
    moduleKey: "JobProfitability",
    category: "finance",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Job-Level Costing & Margin Analysis Engine",
    highlights: [
      "Variance analysis: estimated margin vs realized margin per job",
      "Cost breakdown: material (glass + consumables), machine time, labor",
      "Scrap cost allocation: assigns cutting waste directly to job cost",
    ],
    tags: ["Profitability", "Margin", "Costing", "Variance"],
  },
  {
    id: "report-glass-spec",
    title: "Glass Specifications & Traceability",
    caption:
      "Quality assurance record: raw material mill certificates, glass manufacturer batch numbers, interlayer lot codes and tempering test logs.",
    moduleKey: "SalesReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Glass Quality & Traceability Certification",
    highlights: [
      "End-to-end traceability: installed panel back to raw glass float pack",
      "Certification packs for facade consultants and building authorities",
      "Batch test result archive for warranty claims defense",
    ],
    tags: ["Traceability", "QA", "Certificates", "Compliance"],
  },
  {
    id: "report-executive",
    title: "Reports — Executive Summary",
    caption:
      "C-level business overview: monthly revenue, gross margin trends, top 10 customers, plant capacity utilization and scrap rate trends.",
    moduleKey: "SalesReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Executive Performance & Capacity Summary",
    highlights: [
      "High-level KPI cards with month-over-month trend indicators",
      "Revenue by glass type (monolithic, DGU, laminated, composite)",
      "Factory overall equipment effectiveness (OEE) and scrap percentage",
    ],
    tags: ["Executive", "KPIs", "Revenue", "OEE", "Management"],
  },
  {
    id: "report-customer",
    title: "Reports — Customer Directory",
    caption:
      "Client analytics: order history, total square meters purchased, payment track record, preferred glass specifications and credit limits.",
    moduleKey: "SalesReport",
    category: "reports",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Customer Account History & Volume Breakdown",
    highlights: [
      "Customer lifetime value and annual volume ranking",
      "Credit limit monitoring with automated hold warnings on new quotes",
      "Average turnaround time per customer from quote to delivery",
    ],
    tags: ["Customers", "Analytics", "Credit", "CRM"],
  },
  {
    id: "company-center",
    title: "Company Center",
    caption:
      "Enterprise profile: company legal name, trade license, TRN (Tax Registration Number), branch locations, banks and letterhead setup.",
    moduleKey: "CompanyCenter",
    category: "master-data",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Company Profile & Multi-Branch Master Data",
    highlights: [
      "Multi-branch configuration with distinct TRN and bank details",
      "Digital letterhead and official seal management for PDF reports",
      "Fiscal year calendar and currency settings (AED, USD, EUR, etc.)",
    ],
    tags: ["Company", "TRN", "Branches", "Master data"],
  },
  {
    id: "user-center",
    title: "User Center",
    caption:
      "Access control & security: user accounts, role definitions (Estimator, Production Supervisor, Storekeeper, Accountant), station assignments.",
    moduleKey: "UserCenter",
    category: "master-data",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Role-Based Access Control & Station Roles",
    highlights: [
      "Granular role-based permissions (34 distinct permission flags)",
      "Station-only logins: restricts shop floor terminals to their own stage",
      "Session audit trail: tracks user logins, changes and approvals",
    ],
    tags: ["Users", "Security", "Roles", "Permissions"],
  },
  {
    id: "system-center",
    title: "System Center",
    caption:
      "System administration: database backup/restore, local database integrity checks, archive purge routines and sync engine controls.",
    moduleKey: "SystemCenter",
    category: "settings",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "System Maintenance & Database Diagnostics",
    highlights: [
      "Automated scheduled database backups to local and network storage",
      "Database vacuum and index rebuilding for optimal query speeds",
      "Excel Sync connector status and real-time synchronization diagnostics",
    ],
    tags: ["Admin", "Database", "Backup", "Maintenance"],
  },
  {
    id: "diagnostics",
    title: "Logs & Activity",
    caption:
      "Operational event ledger: application event logs, crash reports, optimization solve times, printer errors and network diagnostic tools.",
    moduleKey: "Diagnostics",
    category: "settings",
    src: null,
    width: 1600,
    height: 1000,
    source: "pending",
    evidence: "Operational Event Logs & Activity Ledger",
    highlights: [
      "Structured log viewer with filtering by severity (Info, Warning, Error)",
      "Automatic error reporting with system environment snapshot",
      "Hardware communication diagnostics: serial COM ports, network printers",
    ],
    tags: ["Logs", "Diagnostics", "Auditing", "Troubleshooting"],
  },
];

export function screenshotsByCategory(category: ScreenshotCategory | "all"): Screenshot[] {
  if (category === "all") return screenshots;
  return screenshots.filter((s) => s.category === category);
}

export function screenshotById(id: string): Screenshot | undefined {
  return screenshots.find((s) => s.id === id);
}

