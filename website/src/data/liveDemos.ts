/**
 * Live module previews.
 *
 * Every screenshot slot on the site used to fall back to a "capture pending"
 * plate. It was honest, but it left the visitor with nothing to look at. These
 * demos replace it: each module now renders an interactive reconstruction of
 * its own layout, driven by sample data, and the visitor can work it — sort the
 * grid, run the optimizer, step a cut list through the eight stages, change the
 * site's own theme from inside the Settings screen.
 *
 * Two rules keep this honest:
 *
 *   1. It is labelled. Every preview carries a "Sample data · interactive
 *      preview" tag and the frame caption says it is not a screen capture.
 *   2. The fields, tabs, stage names, charge rows and thickness profiles are
 *      the application's own. They are read from the WPF source, not invented:
 *      see the `evidence` line on each module in `modules.ts`.
 *
 * The numbers are sample data and are computed live in the browser — the
 * calculator and the optimizer really do their own arithmetic.
 */

import { lineArea, lineValue, pieceArea, seedSpecGroups } from "@/data/workedExample";

export type DemoKind =
  | "dashboard"
  | "table"
  | "wizard"
  | "calculator"
  | "optimizer"
  | "queue"
  | "form";

export type DemoColumn = {
  key: string;
  label: string;
  /** Right-align numbers so a column of figures reads as a column. */
  align?: "left" | "right";
  kind?: "text" | "num" | "money" | "status" | "progress";
  /** Hiding a column on small screens keeps the grid usable at 360px. */
  hideBelow?: "sm" | "md";
};

export type DemoRow = Record<string, string>;

export type DemoView = {
  /** Tab label — taken from the module's own views where they are named. */
  label: string;
  columns: DemoColumn[];
  rows: DemoRow[];
  /** Keys the search box matches against. Defaults to every text column. */
  searchKeys?: string[];
  summary?: { label: string; value: string; money?: boolean }[];
};

export type Demo = {
  kind: DemoKind;
  /** One line describing what the visitor is looking at. */
  blurb: string;
  views: DemoView[];
  /** Ordered rail labels for the wizard engine. */
  steps?: string[];
  /** Extra note shown under the preview. */
  footnote?: string;
};

/* ────────────────────────────────────────────────────────────────────────────
 * Real domain vocabulary, read out of the application
 * ──────────────────────────────────────────────────────────────────────────── */

/** Build-ups the cut engine accepts. Triple glazing is deliberately absent. */
export const buildUps = [
  { id: "sgu", label: "SGU — Single Glazed", outer: 6, inner: 0, interlayer: 0 },
  { id: "dgu", label: "DGU — Double Glazed", outer: 6, inner: 6, interlayer: 0 },
  { id: "lam", label: "Lamination", outer: 6, inner: 6, interlayer: 1.52 },
  { id: "dgulam", label: "DGU + Lamination", outer: 6, inner: 6, interlayer: 1.52 },
] as const;

/** The eight production queues, in the order the engine feeds them. */
export const stageNames = [
  "Cutting",
  "Holes & Cutouts",
  "Polish",
  "Edge Deletion",
  "Mitring",
  "Tempering",
  "Pairing",
  "Doubling / Lamination",
] as const;

/** Charge keys the piece flow raises. Verbatim from the production services. */
export const holeChargeKeys = ["HOLES", "FAN_HOLES", "CUTOUT", "COUNTERSUNK_HOLES"];
export const assemblyChargeKeys = ["ARGON_GAS", "SILICON_BED", "U_INSERT", "OVERLAP", "SHUTTER_BONDING"];

/** Sheet sizes the optimizer plans against. */
export const sheetSizes = [
  { label: "4 × 8 ft", w: 2440, h: 1830 },
  { label: "Standard 3210 × 2250", w: 3210, h: 2250 },
  { label: "Jumbo 3660 × 2440", w: 3660, h: 2440 },
  { label: "Oversize 6000 × 3210", w: 6000, h: 3210 },
];

/** Margins and kerf the nesting respect. Millimetres. */
export const optimizerLimits = { kerf: 4, marginLeft: 15, marginRight: 15, marginTop: 3, marginBottom: 3 };

/* ────────────────────────────────────────────────────────────────────────────
 * Sample data
 * ──────────────────────────────────────────────────────────────────────────── */

const sampleParties = ["Northgate Contracting", "Harbour View Tower", "Cedar Interiors", "Metro Facades"];

const sampleJobs: DemoRow[] = [
  { job: "JO-24101", party: sampleParties[0]!, glass: "DGU 6-12-6", qty: "24", cut: "18", balance: "6", status: "In production" },
  { job: "JO-24102", party: sampleParties[1]!, glass: "Tempered 8 mm", qty: "40", cut: "40", balance: "0", status: "Ready to dispatch" },
  { job: "JO-24103", party: sampleParties[2]!, glass: "Laminated 6.38", qty: "12", cut: "5", balance: "7", status: "In production" },
  { job: "JO-24104", party: sampleParties[3]!, glass: "SGU 10 mm", qty: "18", cut: "0", balance: "18", status: "Released" },
  { job: "JO-24105", party: sampleParties[0]!, glass: "DGU + Lam", qty: "9", cut: "9", balance: "0", status: "Ready to dispatch" },
];

/**
 * The sample cutting list the whole production chain is previewed against.
 *
 * The sizes are a mixed shopfront job and the quantities are chosen so the job
 * suits the plate it is drawn on. That second point is not decoration — it is
 * the difference between a preview that reads as a working optimizer and one
 * that reads as broken.
 *
 * This list used to be dominated by 1180 × 2340 door panels. A piece that tall
 * cannot be stacked with anything else on a 3210 × 2250 plate: rotated it is
 * 2340 wide, so the next piece cannot sit beside it, and it is 2340 of the
 * 2244 usable height the other way. The packer therefore gave six plates of two
 * pieces each at 61.8%, with most of the plate bare — and because those were
 * the first sheets in the run, the first thing anyone saw was a row of
 * half-empty boxes.
 *
 * These sizes suit the sheet. On a 3210 × 2250 plate the list comes out as four
 * plates at 94% — 97 / 94 / 96 / 91 — which is what a shop sees when the sizes
 * have been chosen sensibly, and is the honest illustration of what the engine
 * is for. The quantities were also chosen to hold up if the preview is switched
 * to a smaller sheet: the same list on 4 × 8 ft gives nine plates with nothing
 * below 59%, where the old list left a plate 39% full.
 */
const cutList: DemoRow[] = [
  { piece: "P-01", room: "Lobby", w: "1580", h: "1110", qty: "4", edge: "Polished", holes: "0" },
  { piece: "P-02", room: "Stair", w: "1580", h: "730", qty: "10", edge: "Polished", holes: "2" },
  { piece: "P-03", room: "Office 1", w: "1180", h: "1110", qty: "4", edge: "Polished", holes: "0" },
  { piece: "P-04", room: "Office 2", w: "1040", h: "740", qty: "2", edge: "Flat", holes: "4" },
  { piece: "P-05", room: "Washroom", w: "780", h: "620", qty: "4", edge: "Flat", holes: "1" },
];

const sheetStock: DemoRow[] = [
  { code: "SHT-6001", spec: "Clear 6 mm", size: "3210 × 2250", plates: "46", reserved: "12", location: "Rack A1" },
  { code: "SHT-6002", spec: "Clear 8 mm", size: "3210 × 2250", plates: "18", reserved: "4", location: "Rack A2" },
  { code: "SHT-6003", spec: "Tempered 10 mm", size: "3660 × 2440", plates: "9", reserved: "9", location: "Rack B1" },
  { code: "SHT-6004", spec: "Laminated 6.38", size: "3210 × 2250", plates: "27", reserved: "6", location: "Rack B2" },
  { code: "SHT-6005", spec: "DGU unit 6-12-6", size: "Assembled", plates: "14", reserved: "2", location: "Bay 3" },
  { code: "SHT-6006", spec: "Ceramic frit 6 mm", size: "2440 × 1830", plates: "3", reserved: "0", location: "Rack C1" },
];

/**
 * Step 5's rows, built from the seeded specification groups.
 *
 * Derived rather than typed in: `pieceArea` and `lineArea` are the worked
 * example's own area functions, and the value is area × qty × rate rounded by
 * the application's money rule, so this preview cannot disagree with
 * `/worked-example`. The first group is shown standing in its own specification
 * block, the way step 5 lists one block per specification.
 */
const step5Group = seedSpecGroups[0]!;
const step5Rows: DemoRow[] = step5Group.lines.map((line) => ({
  line: String(line.serial),
  ref: line.ref,
  w1: String(line.widthMm),
  h1: String(line.heightMm),
  // A single-glazed line has no second pane; the application stores 0 there.
  w2: line.secondPane ? String(line.secondPane.widthMm) : "—",
  h2: line.secondPane ? String(line.secondPane.heightMm) : "—",
  qty: String(line.qty),
  sqm: pieceArea(line).toFixed(2),
  tSqm: lineArea(line).toFixed(2),
  rate: line.rate.toFixed(2),
  value: lineValue(line).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
}));

const deliveryRows: DemoRow[] = [
  { note: "DN-1841", job: "JO-24102", party: sampleParties[1]!, pieces: "40", vehicle: "Truck 3", status: "Delivered" },
  { note: "DN-1842", job: "JO-24101", party: sampleParties[0]!, pieces: "18", vehicle: "Truck 1", status: "In transit" },
  { note: "DN-1843", job: "JO-24105", party: sampleParties[0]!, pieces: "9", vehicle: "Pickup", status: "Loaded" },
  { note: "DN-1844", job: "JO-24103", party: sampleParties[2]!, pieces: "5", vehicle: "Truck 2", status: "Pending" },
];

const vatRows: DemoRow[] = [
  { period: "Q1", sales: "412,800.00", output: "20,640.00", purchases: "188,400.00", input: "9,420.00", net: "11,220.00" },
  { period: "Q2", sales: "508,240.00", output: "25,412.00", purchases: "224,150.00", input: "11,207.50", net: "14,204.50" },
  { period: "Q3", sales: "465,900.00", output: "23,295.00", purchases: "201,300.00", input: "10,065.00", net: "13,230.00" },
];

const profitRows: DemoRow[] = [
  { job: "JO-24101", party: sampleParties[0]!, revenue: "84,200", cost: "61,480", margin: "22,720", pct: "27.0%" },
  { job: "JO-24102", party: sampleParties[1]!, revenue: "126,400", cost: "98,300", margin: "28,100", pct: "22.2%" },
  { job: "JO-24103", party: sampleParties[2]!, revenue: "41,900", cost: "35,120", margin: "6,780", pct: "16.2%" },
  { job: "JO-24105", party: sampleParties[0]!, revenue: "63,750", cost: "44,900", margin: "18,850", pct: "29.6%" },
];

const userRows: DemoRow[] = [
  { user: "Estimator", role: "Sales", scope: "Quotes, proforma, job orders", status: "Active" },
  { user: "Store Keeper", role: "Inventory", scope: "Sheet store, specifications", status: "Active" },
  { user: "Floor Supervisor", role: "Production", scope: "Stages, work centres, dispatch", status: "Active" },
  { user: "Accounts", role: "Finance", scope: "Statements, VAT, profitability", status: "Active" },
  { user: "Owner", role: "Administrator", scope: "All 34 permissions", status: "Active" },
];

/* ────────────────────────────────────────────────────────────────────────────
 * Bespoke demos
 * ──────────────────────────────────────────────────────────────────────────── */

const bespoke: Record<string, Demo> = {
  Dashboard: {
    kind: "dashboard",
    blurb: "Today's position: what was quoted, what is on the floor and what is owed.",
    views: [
      {
        label: "Overview",
        columns: [],
        rows: [],
        summary: [
          { label: "Open inquiries", value: "7" },
          { label: "Proforma raised", value: "12" },
          { label: "Jobs in production", value: "9" },
          { label: "Ready to dispatch", value: "3" },
        ],
      },
    ],
    footnote: "Figures change with the range you select, the way the dashboard recomputes on load.",
  },

  Inquiry: {
    kind: "form",
    blurb: "A new inquiry before it becomes a quotation.",
    views: [
      {
        label: "Inquiry",
        columns: [],
        rows: [],
        summary: [
          { label: "Party", value: "Selected from Master Data" },
          { label: "Build-up", value: "DGU 6-12-6" },
          { label: "Area", value: "Calculated from the two dimensions" },
        ],
      },
    ],
  },

  ProformaInvoice: {
    /*
     * The wizard is nine steps, and they are the application's steps, named the
     * way the application names them: the folders under
     * `Views/ProformaInvoice/` are Step1CompanySetup … Step9ProfitLoss, and step
     * five is the specification grid (`Dg.Specification`). This earlier read
     * "Party / Build-up / Dimensions / Area / Charges / Discount / Tax / Terms /
     * Review", which was a plausible wizard and not this one.
     */
    kind: "wizard",
    blurb: "The commercial document: line build-up, charges, then a stamped price basis.",
    steps: [
      "Company Setup",
      "Client Profile",
      "Project Specs",
      "Pricing Estimator",
      "Specification",
      "Other Charges",
      "Print Preview",
      "Attachments",
      "Profit & Loss",
    ],
    views: [
      {
        /*
         * Step 5 — the specification grid.
         *
         * The column set is `Dg.*` from Themes/Languages/en-US.xaml, in the
         * order `Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml` lays
         * them out: # · GLASS REF · W1 · H1 · W2 · H2 · QTY · SQM · TOTAL SQM ·
         * RATE · VALUE, under a SPECIFICATION block that also carries the glass
         * type and the base rate for the group. The rows are the seeded
         * specification groups from `workedExample.ts`, so the preview and the
         * worked example show the same job rather than two different ones.
         */
        label: "Specification",
        columns: [
          { key: "line", label: "#" },
          { key: "ref", label: "GLASS REF" },
          { key: "w1", label: "W1", align: "right", kind: "num" },
          { key: "h1", label: "H1", align: "right", kind: "num" },
          { key: "w2", label: "W2", align: "right", kind: "num", hideBelow: "sm" },
          { key: "h2", label: "H2", align: "right", kind: "num", hideBelow: "sm" },
          { key: "qty", label: "QTY", align: "right", kind: "num" },
          { key: "sqm", label: "SQM", align: "right", kind: "num", hideBelow: "md" },
          { key: "tSqm", label: "TOTAL SQM", align: "right", kind: "num" },
          { key: "rate", label: "RATE", align: "right", kind: "num", hideBelow: "md" },
          { key: "value", label: "VALUE", align: "right", kind: "money" },
        ],
        rows: step5Rows,
        summary: [
          { label: "Specification 1", value: "Clear Tempered — Main Facade" },
          { label: "Glass type", value: "SGU" },
          { label: "Base rate", value: "150.00 / m²" },
          { label: "Lines", value: "10" },
        ],
      },
      {
        label: "Price basis",
        columns: [
          { key: "line", label: "#" },
          { key: "desc", label: "Input" },
          { key: "w", label: "Value", align: "right", kind: "num" },
        ],
        rows: [
          { line: "01", desc: "Glass rate — DGU 6-12-6", w: "412.00 / m²" },
          { line: "02", desc: "Tempering", w: "38.00 / m²" },
          { line: "03", desc: "Holes", w: "6.00 each" },
          { line: "04", desc: "Cut-outs", w: "9.00 each" },
          { line: "05", desc: "Argon gas", w: "12.00 / m²" },
          { line: "06", desc: "Silicon bed", w: "14.00 / m²" },
          { line: "07", desc: "Wastage", w: "6.0 %" },
        ],
        summary: [{ label: "Basis", value: "Stamped onto the document" }],
      },
    ],
    footnote:
      "Step 5 holds the specification: # · GLASS REF · W1 · H1 · W2 · H2 · QTY · SQM · TOTAL SQM · RATE · VALUE, with the glass type and base rate on the block. A dash in W2 or H2 is a single-glazed line — the application stores 0 in those cells, as it does in the seeded job on the worked example. Change any input on the price basis and the stamped basis changes with it; the difference between the two is what the review step exists to show.",
  },

  JobOrder: {
    kind: "table",
    blurb: "Released jobs, what has been cut and what is still owed.",
    views: [
      { label: "Jobs", columns: jobColumns(), rows: sampleJobs },
      {
        label: "Cut list",
        columns: cutColumns(),
        rows: cutList,
      },
    ],
  },

  TaxInvoice: {
    kind: "table",
    blurb: "Invoices raised against delivered movements.",
    views: [
      {
        label: "Invoices",
        columns: [
          { key: "note", label: "Invoice" },
          { key: "party", label: "Party" },
          { key: "job", label: "Job", hideBelow: "md" },
          { key: "net", label: "Net", align: "right", kind: "money" },
          { key: "vat", label: "VAT", align: "right", kind: "money" },
          { key: "total", label: "Total", align: "right", kind: "money" },
          { key: "status", label: "Status", kind: "status" },
        ],
        rows: [
          { note: "TI-9001", party: sampleParties[1]!, job: "JO-24102", net: "126,400.00", vat: "6,320.00", total: "132,720.00", status: "Settled" },
          { note: "TI-9002", party: sampleParties[0]!, job: "JO-24101", net: "84,200.00", vat: "4,210.00", total: "88,410.00", status: "Part paid" },
          { note: "TI-9003", party: sampleParties[2]!, job: "JO-24103", net: "41,900.00", vat: "2,095.00", total: "43,995.00", status: "Raised" },
        ],
        summary: [
          { label: "Raised", value: "3 documents" },
          { label: "Net", value: "252,500.00", money: true },
          { label: "VAT", value: "12,625.00", money: true },
        ],
      },
    ],
  },

  SheetStore: {
    kind: "table",
    blurb: "Plate stock by specification, with what the open jobs have already reserved.",
    views: [
      { label: "Stock", columns: sheetColumns(), rows: sheetStock },
      {
        label: "Movements",
        columns: [
          { key: "date", label: "Date" },
          { key: "code", label: "Sheet" },
          { key: "type", label: "Movement" },
          { key: "qty", label: "Qty", align: "right", kind: "num" },
          { key: "ref", label: "Reference", hideBelow: "md" },
        ],
        rows: [
          { date: "Today", code: "SHT-6001", type: "Inward", qty: "+12", ref: "GRN-4471" },
          { date: "Today", code: "SHT-6003", type: "Reserved", qty: "−9", ref: "JO-24105" },
          { date: "Yesterday", code: "SHT-6004", type: "Consumed", qty: "−4", ref: "JO-24103" },
          { date: "Yesterday", code: "SHT-6002", type: "Inward", qty: "+6", ref: "GRN-4469" },
        ],
      },
    ],
  },

  Specifications: {
    kind: "table",
    blurb: "The glass specifications the shop is allowed to quote from.",
    views: [
      {
        label: "Specifications",
        columns: [
          { key: "code", label: "Code" },
          { key: "spec", label: "Specification" },
          { key: "thk", label: "Thickness", align: "right", kind: "num" },
          { key: "process", label: "Process", hideBelow: "md" },
          { key: "use", label: "Typical use", hideBelow: "sm" },
        ],
        rows: [
          { code: "SP-001", spec: "Clear Float", thk: "6 mm", process: "Tempered", use: "Windows, doors" },
          { code: "SP-002", spec: "Clear Float", thk: "10 mm", process: "Tempered", use: "Structural glazing" },
          { code: "SP-003", spec: "Laminated 6.38", thk: "6.38 mm", process: "Cut + polish", use: "Overhead, balustrade" },
          { code: "SP-004", spec: "DGU 6-12-6 Argon", thk: "24 mm", process: "Assembled", use: "Facade, shopfront" },
          { code: "SP-005", spec: "Ceramic frit", thk: "6 mm", process: "Printed + tempered", use: "Spandrel, screen" },
        ],
      },
    ],
  },

  Optimization: {
    kind: "optimizer",
    blurb: "Run the nesting against a real sheet size and watch the metrics move.",
    views: [{ label: "Plan", columns: cutColumns(), rows: cutList }],
    footnote: "Utilization, wastage, part area, stock area and scrap are computed from the parts actually placed — the kerf and the edge margins are respected, so the numbers move when you change the sheet.",
  },

  ProcessWorkCenter: {
    kind: "queue",
    blurb: "The eight queues, and how much of each job is sitting in them.",
    views: [{ label: "Queues", columns: jobColumns(), rows: sampleJobs }],
  },

  ProductionControlCenter: {
    kind: "queue",
    blurb: "The same flow, seen as one board from Cutting to Dispatch-ready.",
    views: [{ label: "Board", columns: jobColumns(), rows: sampleJobs }],
  },

  Cutting: { kind: "queue", blurb: "Cutting: the plan goes in, cut pieces come out.", views: [{ label: "Cutting", columns: cutColumns(), rows: cutList }] },
  HolesCutouts: {
    kind: "queue",
    blurb: "Holes, fan holes, cut-outs and countersunk holes are charged per piece.",
    views: [
      {
        label: "Pieces",
        columns: [
          { key: "piece", label: "Piece" },
          { key: "job", label: "Job" },
          { key: "w", label: "W", align: "right", kind: "num", hideBelow: "sm" },
          { key: "h", label: "H", align: "right", kind: "num", hideBelow: "sm" },
          { key: "holes", label: "Holes", align: "right", kind: "num" },
          { key: "charge", label: "Charged as", hideBelow: "md" },
          { key: "done", label: "Done", align: "right", kind: "num" },
        ],
        rows: [
          { piece: "P-01", job: "JO-24101", w: "1180", h: "2340", holes: "2", charge: "HOLES", done: "4" },
          { piece: "P-03", job: "JO-24101", w: "860", h: "1980", holes: "4", charge: "FAN_HOLES", done: "6" },
          { piece: "P-06", job: "JO-24103", w: "640", h: "1180", holes: "1", charge: "COUNTERSUNK_HOLES", done: "10" },
          { piece: "P-04", job: "JO-24102", w: "1220", h: "1520", holes: "1", charge: "CUTOUT", done: "8" },
        ],
      },
    ],
  },
  Polish: { kind: "queue", blurb: "Edge polish by piece, per job.", views: [{ label: "Polish", columns: cutColumns(), rows: cutList }] },
  EdgeDeletion: { kind: "queue", blurb: "Edge deletion takes the coating back where the seal will sit.", views: [{ label: "Deletion", columns: cutColumns(), rows: cutList }] },
  Mitring: { kind: "queue", blurb: "Mitred edges, by angle and by piece.", views: [{ label: "Mitring", columns: cutColumns(), rows: cutList }] },
  Tempering: { kind: "queue", blurb: "The furnace queue. Toughened glass cannot be cut after this point.", views: [{ label: "Tempering", columns: cutColumns(), rows: cutList }] },
  Pairing: { kind: "queue", blurb: "Outer and inner lites matched before they are sealed together.", views: [{ label: "Pairing", columns: cutColumns(), rows: cutList }] },
  DoublingLamination: { kind: "queue", blurb: "Doubling and lamination, with the overlap position recorded.", views: [{ label: "Doubling", columns: cutColumns(), rows: cutList }] },

  DeliveryNote: {
    kind: "table",
    blurb: "What left the yard, on which vehicle, and what it belongs to.",
    views: [
      { label: "Notes", columns: deliveryColumns(), rows: deliveryRows },
      {
        label: "Reconciliation",
        columns: [
          { key: "job", label: "Job" },
          { key: "ordered", label: "Ordered", align: "right", kind: "num" },
          { key: "delivered", label: "Delivered", align: "right", kind: "num" },
          { key: "balance", label: "Balance", align: "right", kind: "num" },
          { key: "state", label: "State", kind: "status" },
        ],
        rows: [
          { job: "JO-24101", ordered: "24", delivered: "18", balance: "6", state: "Part delivered" },
          { job: "JO-24102", ordered: "40", delivered: "40", balance: "0", state: "Complete" },
          { job: "JO-24103", ordered: "12", delivered: "5", balance: "7", state: "Part delivered" },
          { job: "JO-24105", ordered: "9", delivered: "9", balance: "0", state: "Complete" },
        ],
      },
    ],
  },

  StatementGenerator: {
    kind: "table",
    blurb: "A running statement per party, from opening balance to closing.",
    views: [
      {
        label: "Statement",
        columns: [
          { key: "date", label: "Date" },
          { key: "doc", label: "Document" },
          { key: "debit", label: "Debit", align: "right", kind: "money" },
          { key: "credit", label: "Credit", align: "right", kind: "money" },
          { key: "balance", label: "Balance", align: "right", kind: "money" },
        ],
        rows: [
          { date: "01 Sep", doc: "Opening balance", debit: "", credit: "", balance: "18,400.00" },
          { date: "04 Sep", doc: "TI-9002", debit: "88,410.00", credit: "", balance: "106,810.00" },
          { date: "11 Sep", doc: "Receipt R-2210", debit: "", credit: "60,000.00", balance: "46,810.00" },
          { date: "19 Sep", doc: "TI-9004", debit: "31,240.00", credit: "", balance: "78,050.00" },
          { date: "26 Sep", doc: "Receipt R-2231", debit: "", credit: "78,050.00", balance: "0.00" },
        ],
        summary: [{ label: "Closing", value: "0.00", money: true }],
      },
    ],
  },

  OrderReport: {
    kind: "wizard",
    blurb: "Four steps: filter, preview, pipeline, review — then it is saved.",
    steps: ["Filter", "Preview", "Pipeline", "Review"],
    views: [
      { label: "Jobs", columns: jobColumns(), rows: sampleJobs },
      {
        label: "Preview",
        columns: [
          { key: "k", label: "Section" },
          { key: "v", label: "Rows" },
        ],
        rows: [
          { k: "Document header", v: "Party, job no., dates" },
          { k: "Build-up summary", v: "One row per specification" },
          { k: "Cut list", v: "Piece, size, edge, holes" },
          { k: "Stage progress", v: "Done against balance, per stage" },
          { k: "Totals", v: "Area, weight, amount" },
        ],
      },
    ],
    footnote: "Saved reports live in orderreports.db, so a report can be reopened years later with the data it was built from.",
  },

  JobProfitability: {
    kind: "table",
    blurb: "Revenue against real cost, per job.",
    views: [
      { label: "By job", columns: profitColumns(), rows: profitRows },
      {
        label: "Cost build-up",
        columns: [
          { key: "row", label: "Cost row" },
          { key: "basis", label: "Basis" },
          { key: "amount", label: "Amount", align: "right", kind: "money" },
        ],
        rows: [
          { row: "Glass", basis: "Area × rate", amount: "38,420.00" },
          { row: "Tempering", basis: "Area × rate", amount: "3,180.00" },
          { row: "Holes & cut-outs", basis: "Per piece", amount: "640.00" },
          { row: "Argon + spacer", basis: "Per m²", amount: "1,260.00" },
          { row: "Wastage", basis: "Applied to glass", amount: "2,305.00" },
          { row: "Delivery", basis: "Per movement", amount: "890.00" },
        ],
      },
    ],
  },

  VatSubmission: {
    kind: "table",
    blurb: "Output tax, input tax and the net position for the period.",
    views: [
      { label: "Periods", columns: vatColumns(), rows: vatRows },
      {
        label: "Documents",
        columns: [
          { key: "doc", label: "Document" },
          { key: "party", label: "Party" },
          { key: "net", label: "Net", align: "right", kind: "money" },
          { key: "vat", label: "VAT", align: "right", kind: "money" },
          { key: "dir", label: "Direction", kind: "status" },
        ],
        rows: [
          { doc: "TI-9001", party: sampleParties[1]!, net: "126,400.00", vat: "6,320.00", dir: "Output" },
          { doc: "TI-9002", party: sampleParties[0]!, net: "84,200.00", vat: "4,210.00", dir: "Output" },
          { doc: "PI-3310", party: "Glass supply", net: "188,400.00", vat: "9,420.00", dir: "Input" },
        ],
      },
    ],
  },

  SalesReport: {
    kind: "dashboard",
    blurb: "The report hub: relationships, documents and financial surfaces over one data layer.",
    views: [
      {
        label: "Overview",
        columns: [],
        rows: [],
        summary: [
          { label: "Report surfaces", value: "14" },
          { label: "Hub groups", value: "Relationships · Documents · Financial" },
          { label: "Export", value: "PDF, Excel, CSV" },
        ],
      },
    ],
    footnote: "Executive, Customer, Salesman, Company, Project, PI, JO, Delivery, Tax Invoice, Glass Spec, Profit, Statement and VAT each open from the same hub.",
  },

  CompanyCenter: {
    kind: "form",
    blurb: "The company record every document header is built from.",
    views: [
      {
        label: "Company",
        columns: [],
        rows: [],
        summary: [
          { label: "Legal name", value: "Used on every invoice" },
          { label: "Tax registration", value: "Printed on the VAT document" },
          { label: "Bank details", value: "Carried onto the statement" },
          { label: "Logo", value: "Shown on the document header" },
        ],
      },
    ],
  },

  UserCenter: {
    kind: "table",
    blurb: "Users, their role, and the scope that role actually grants.",
    views: [{ label: "Users", columns: userColumns(), rows: userRows }],
  },

  Settings: {
    kind: "form",
    blurb: "Application settings — including the appearance controls, live in this preview.",
    views: [
      {
        label: "Appearance",
        columns: [],
        rows: [],
        summary: [
          { label: "Visual family", value: "Normal (Industrial) · Monet · Crystal Fluid · iOS 26" },
          { label: "Accents", value: "17 presets plus a custom colour" },
          { label: "Mode", value: "Light, Dark or follow the system" },
          { label: "Wallpaper", value: "Scene per family" },
        ],
      },
      {
        label: "General",
        columns: [
          { key: "setting", label: "Setting" },
          { key: "value", label: "Value" },
        ],
        rows: [
          { setting: "Language", value: "One of 15 locales" },
          { setting: "Currency and rounding", value: "Per company record" },
          { setting: "Document numbering", value: "Prefix, series, next number" },
          { setting: "Backup", value: "Folder and interval" },
          { setting: "Print", value: "Page size and margins" },
        ],
      },
    ],
    footnote: "The four family buttons below are the real control: pressing one re-themes this website as well as describing the application.",
  },

  About: {
    kind: "form",
    blurb: "Licence state, the free tier, and the modules a licence unlocks.",
    views: [
      {
        label: "Licence",
        columns: [
          { key: "field", label: "Field" },
          { key: "value", label: "Value" },
        ],
        rows: [
          { field: "Edition", value: "Set by the licence key" },
          { field: "Term", value: "Trial 30 days · 7 day grace after expiry" },
          { field: "Devices", value: "Recorded in activations.json" },
          { field: "Free modules", value: "22 available without a key" },
        ],
      },
      {
        label: "Editions",
        columns: [
          { key: "edition", label: "Edition" },
          { key: "modules", label: "Modules", align: "right", kind: "num" },
          { key: "state", label: "Notes" },
        ],
        rows: [
          { edition: "Trial", modules: "22", state: "30 days, everything free-tier" },
          { edition: "Standard", modules: "—", state: "Core workflow" },
          { edition: "Professional", modules: "—", state: "Adds production and reports" },
          { edition: "Enterprise", modules: "—", state: "Everything, including licensing surfaces" },
        ],
        summary: [{ label: "Price list", value: "Not published — ask the developer" }],
      },
    ],
  },

  SystemCenter: {
    kind: "dashboard",
    blurb: "Which databases are present, and what they hold.",
    views: [
      {
        label: "Databases",
        columns: [
          { key: "db", label: "Database" },
          { key: "scope", label: "Holds" },
          { key: "size", label: "Size", align: "right", kind: "num" },
          { key: "state", label: "State", kind: "status" },
        ],
        rows: [
          { db: "company.db", scope: "Company, users, permissions", size: "1.4 MB", state: "OK" },
          { db: "proforma.db", scope: "Inquiries and proforma invoices", size: "12.8 MB", state: "OK" },
          { db: "joborders.db", scope: "Job orders and cut lists", size: "8.2 MB", state: "OK" },
          { db: "stock.db", scope: "Sheet store and movements", size: "6.1 MB", state: "OK" },
          { db: "invoices.db", scope: "Tax invoices and receipts", size: "4.7 MB", state: "OK" },
          { db: "orderreports.db", scope: "Saved order reports", size: "2.3 MB", state: "OK" },
        ],
      },
    ],
    footnote: "16 databases, one EF Core context each, additive-only migrations.",
  },

  Diagnostics: {
    kind: "dashboard",
    blurb: "What the application checks about itself.",
    views: [
      {
        label: "Checks",
        columns: [
          { key: "check", label: "Check" },
          { key: "result", label: "Result", kind: "status" },
          { key: "detail", label: "Detail", hideBelow: "md" },
        ],
        rows: [
          { check: "Database files", result: "Pass", detail: "All present and writable" },
          { check: "Locale resources", result: "Pass", detail: "15 locales loaded" },
          { check: "Print pipeline", result: "Pass", detail: "PDF writer available" },
          { check: "Licence state", result: "Pass", detail: "Within term" },
          { check: "Error log", result: "Warn", detail: "1 handled exception this week" },
        ],
      },
    ],
  },
};

/* ────────────────────────────────────────────────────────────────────────────
 * Column helpers
 * ──────────────────────────────────────────────────────────────────────────── */

function jobColumns(): DemoColumn[] {
  return [
    { key: "job", label: "Job no." },
    { key: "party", label: "Customer" },
    { key: "glass", label: "Glass", hideBelow: "sm" },
    { key: "qty", label: "Total", align: "right", kind: "num" },
    { key: "cut", label: "Done", align: "right", kind: "num" },
    { key: "balance", label: "Balance", align: "right", kind: "num" },
    { key: "status", label: "Status", kind: "status" },
  ];
}

function cutColumns(): DemoColumn[] {
  return [
    { key: "piece", label: "Piece" },
    { key: "room", label: "Room", hideBelow: "md" },
    { key: "w", label: "W", align: "right", kind: "num" },
    { key: "h", label: "H", align: "right", kind: "num" },
    { key: "qty", label: "Qty", align: "right", kind: "num" },
    { key: "edge", label: "Edge", hideBelow: "sm" },
    { key: "holes", label: "Holes", align: "right", kind: "num" },
  ];
}

function sheetColumns(): DemoColumn[] {
  return [
    { key: "code", label: "Sheet" },
    { key: "spec", label: "Specification" },
    { key: "size", label: "Size", hideBelow: "sm" },
    { key: "plates", label: "Plates", align: "right", kind: "num" },
    { key: "reserved", label: "Reserved", align: "right", kind: "num" },
    { key: "location", label: "Location", hideBelow: "md" },
  ];
}

function deliveryColumns(): DemoColumn[] {
  return [
    { key: "note", label: "Note" },
    { key: "job", label: "Job" },
    { key: "party", label: "Party", hideBelow: "sm" },
    { key: "pieces", label: "Pieces", align: "right", kind: "num" },
    { key: "vehicle", label: "Vehicle", hideBelow: "md" },
    { key: "status", label: "Status", kind: "status" },
  ];
}

function profitColumns(): DemoColumn[] {
  return [
    { key: "job", label: "Job" },
    { key: "party", label: "Customer", hideBelow: "sm" },
    { key: "revenue", label: "Revenue", align: "right", kind: "money" },
    { key: "cost", label: "Cost", align: "right", kind: "money" },
    { key: "margin", label: "Margin", align: "right", kind: "money" },
    { key: "pct", label: "Margin %", align: "right", kind: "num" },
  ];
}

function vatColumns(): DemoColumn[] {
  return [
    { key: "period", label: "Period" },
    { key: "sales", label: "Sales", align: "right", kind: "money" },
    { key: "output", label: "Output VAT", align: "right", kind: "money" },
    { key: "purchases", label: "Purchases", align: "right", kind: "money", hideBelow: "md" },
    { key: "input", label: "Input VAT", align: "right", kind: "money", hideBelow: "sm" },
    { key: "net", label: "Net due", align: "right", kind: "money" },
  ];
}

function userColumns(): DemoColumn[] {
  return [
    { key: "user", label: "User" },
    { key: "role", label: "Role", hideBelow: "sm" },
    { key: "scope", label: "Scope" },
    { key: "status", label: "Status", kind: "status" },
  ];
}

/* ────────────────────────────────────────────────────────────────────────────
 * Generated demos for everything else
 * ──────────────────────────────────────────────────────────────────────────── */

/**
 * A module with no bespoke preview still gets a working one: its capability
 * lines become the tabs, and the grid under each tab is filled from the
 * application's own vocabulary for the group the module sits in.
 */
function generatedDemo(name: string, capabilities: string[]): Demo {
  const tabs = capabilities.length > 0 ? capabilities.slice(0, 3) : ["Overview"];
  const views: DemoView[] = tabs.map((tab, index) => ({
    label: shortLabel(tab, index),
    columns:
      index === 0
        ? [
            { key: "ref", label: "Reference" },
            { key: "detail", label: "Detail" },
            { key: "value", label: "Value", align: "right", kind: "num" },
            { key: "state", label: "State", kind: "status" },
          ]
        : [
            { key: "ref", label: "Reference" },
            { key: "detail", label: "Detail" },
            { key: "value", label: "Value", align: "right", kind: "num" },
          ],
    rows: [
      { ref: "Row 01", detail: tab, value: "—", state: "Ready" },
      { ref: "Row 02", detail: "Checked against the job order", value: "—", state: "Ready" },
      { ref: "Row 03", detail: "Written on save", value: "—", state: "Ready" },
    ],
    summary: [{ label: "Screen", value: name }],
  }));

  return { kind: "table", blurb: "The layout of this module, with its own tabs.", views };
}

function shortLabel(text: string, index: number): string {
  const first = text.split(/[—·,.]/)[0]?.trim() ?? "";
  const words = first.split(/\s+/).slice(0, 3).join(" ");
  return words.length > 2 ? words : `View ${index + 1}`;
}

/**
 * Resolves the preview for a module. `moduleKey` matches the application's own
 * navigation key; `name` and `capabilities` come from the module registry and
 * are used only by the generated fallback.
 */
export function demoFor(moduleKey: string, name: string, capabilities: string[]): Demo {
  return bespoke[moduleKey] ?? generatedDemo(name, capabilities);
}
