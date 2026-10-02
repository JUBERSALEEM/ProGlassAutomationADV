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
import { STANDARD_SURCHARGE_PERCENT } from "@/data/pricingRules";
import type { SpecSurfaceInput } from "@/components/product/SpecificationSurface";
import type { PnLInput } from "@/components/product/ProfitLossSurface";

export type DemoKind =
  | "dashboard"
  | "table"
  | "wizard"
  | "calculator"
  | "optimizer"
  | "queue"
  | "stage"
  | "log"
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
  /**
   * The record's own header fields, printed above the grid as label/value
   * pairs — the specification block on Step 5 (GLASS TYPE · SPECIFICATION ·
   * BASE · SURCHARGE %), the way `DataGridView.xaml` lays it out per group.
   */
  header?: { label: string; value: string; note?: string }[];
  /**
   * Action pills from the record's own toolbar, in the order the view lays
   * them: `Dg.Import` · `Dg.Export` · `Dg.RunQuick` · `Dg.RunOptimize` ·
   * `Dg.ViewResult`. A pill whose id the engine understands runs for real —
   * `optimize` packs the visible rows with the site's own guillotine packer and
   * reports the result, which is what the button does to Step 6 in the app.
   */
  actions?: { id: string; label: string; primary?: boolean }[];
  /** A line of the view's own guidance, printed under the pills. */
  actionNote?: string;
  /**
   * Set when this view *is* a wizard step's own surface rather than a data
   * view — Step 5's specification and Step 9's profit-and-loss. The wizard rail
   * navigates to it, and the tab row labels it with its step.
   */
  step?: number;
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
  /**
   * The step a wizard opens on.
   *
   * Step 1 of a wizard is a company card, which is the right place for the
   * application and the wrong place for a preview: a visitor who opens the
   * Proforma Invoice card and lands on a paragraph has not seen the pricing
   * engine. The proforma opens on Step 5, because the specification grid is
   * where a shop decides whether this software is worth its time.
   */
  defaultStep?: number;
  /**
   * What each step of a wizard holds, in the application's own terms.
   *
   * A wizard step is a screen, not a tab: pressing 06 in the rail should show
   * step 6 and not whichever table happened to be open. Steps 4, 5 and 9 carry
   * a real surface (`step` on the view); the rest get their own line, taken
   * from what the application puts on them.
   */
  stepNotes?: string[];
  /**
   * The specification Step 5 works on, and the profit-and-loss settings Step 9
   * analyses it with. Both surfaces read these, so editing a size on Step 5
   * moves the margin on Step 9 — the two screens share one engine, as they do
   * in the application.
   */
  spec?: SpecSurfaceInput;
  pnl?: PnLInput;
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

const sampleParties = [
  "Commercial Facades Ltd",
  "Glazing Contractor A",
  "Architectural Glass LLC",
  "Fenestration Project B",
];

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

/* ────────────────────────────────────────────────────────────────────────────
 * Step 5 and Step 9 — the two screens the pricing engine lives on
 * ──────────────────────────────────────────────────────────────────────────── */

/**
 * The specification Step 5 opens on, and the one `+ ADD SPEC` adds.
 *
 * The first block is the seeded quotation's own: `seedSpecGroups[0]`, "Clear
 * Tempered — Main Facade", ten rows at the rates the document was saved with.
 * Its glass type, base rate and rows are the seeder's; the SURCHARGE % is the
 * shop's standard 20, which is what a specification carrying oversize work is
 * quoted at (the field's own default is 0, and the app's is a field, not a
 * constant — the preview lets the visitor set either).
 *
 * The second block is the one the *shop* would have added: an atrium lite past
 * 4 m² and a vent light under 0.5 m², which are the two panels the two rules
 * exist for. It ships in the bundle and appears when `+ ADD SPEC` is pressed,
 * the way the application adds a specification block — so the surcharge is
 * visible in the invoice rather than described in a footnote.
 */
const mainSpec = seedSpecGroups[0]!;
const mainLines = mainSpec.lines;

export const proformaSpec: SpecSurfaceInput = {
  sheet: { label: sheetSizes[1]!.label, w: sheetSizes[1]!.w, h: sheetSizes[1]!.h },
  columnsHint:
    "Dg.ColumnsHint — # · GLASS REF · W1 · H1 · W2 · H2 · QTY · SQM · TOTAL SQM · RATE · VALUE. W2/H2 hold 0 on a single-glazed line, which the grid prints as a dash. Type a dimension: SQM floors at 0.5 m², and a panel that reaches 4 m² takes the specification's surcharge rate while the rows beside it stay on base.",
  blocks: [
    {
      number: mainSpec.number,
      name: mainSpec.name,
      glassType: mainSpec.glassType,
      glassTypes: ["SGU", "DGU", "LAM", "DGU + LAM"],
      baseRate: mainSpec.baseRate,
      surchargePercent: STANDARD_SURCHARGE_PERCENT,
      primary: true,
      /*
       * One rate for the whole specification — the base rate.
       *
       * The seeded document stores a rate per line, because it was priced
       * thickness by thickness and saved that way. The *grid* does not work like
       * that: BASE is one field and `UpdateAllRowRates` writes it down every row
       * the moment it moves, so a specification holds one price and the only
       * rows that differ are the ones that reach 4 m² and take the surcharge
       * rate. Opening the preview on the per-line stored rates showed a
       * specification that could never exist on screen — four prices in one
       * block — and made the surcharge look like it was doing nothing, because a
       * lifted base could still land under a stored 245.
       *
       * The saved document is still on `/worked-example`, parity-locked to
       * `seedSpecGroups`; this is the working grid.
       */
      rows: mainLines.map((line) => ({
        serial: line.serial,
        ref: line.ref,
        w1: line.widthMm,
        h1: line.heightMm,
        w2: line.secondPane?.widthMm ?? 0,
        h2: line.secondPane?.heightMm ?? 0,
        qty: line.qty,
        rate: mainSpec.baseRate,
        rateOverride: null,
      })),
    },
  ],
  /*
   * What `+ ADD SPEC` adds: the specification the shop would open for oversize
   * work, at the same standard 20%. Its atrium lite is past 4 m² and its vent
   * light is under 0.5 m², so pressing the button puts both rules on screen at
   * once — in the grid, priced, with the chips that name them.
   */
  extraBlock: {
    number: 2,
    name: "Oversize — Atrium Skylight",
    /*
     * A double-glazed specification, because that is what the seeded document's
     * third group is (LE-* at 260 / m², second pane equal to the first). It also
     * means pressing `+ ADD SPEC` shows the half of Step 4 the single-pane
     * specification has no reason to show: the inner pane and the ASP spacer,
     * with their width, material and U-insert variant.
     */
    glassType: "DGU",
    glassTypes: ["SGU", "DGU", "LAM", "DGU + LAM"],
    baseRate: 245,
    surchargePercent: STANDARD_SURCHARGE_PERCENT,
    rows: [
      /*
       * 2400 × 1800 is 4.32 m², so this row prices at the surcharge rate the
       * moment it is opened: base 245 + 20% = 294. Nothing has to be typed for
       * the rule to be visible.
       */
      {
        serial: 1,
        ref: "CT-12-2400x1800",
        w1: 2400,
        h1: 1800,
        w2: 2400,
        h2: 1800,
        qty: 3,
        rate: 245,
        rateOverride: null,
      },
      /*
       * 400 × 300 is 0.12 m² of glass and half a square metre of work, so it
       * bills at the floor on the specification's base rate.
       */
      {
        serial: 2,
        ref: "CT-06-0400x0300",
        w1: 400,
        h1: 300,
        w2: 400,
        h2: 300,
        qty: 6,
        rate: 245,
        rateOverride: null,
      },
    ],
  },
};

/**
 * Step 9's settings.
 *
 * The question and the section titles are the view's own strings. The expected
 * profit margin is the application's fresh-install fallback, 15% --
 * `Step9PricingDefaults.FallbackExpectedProfitMarginPercent`, whose comment
 * says it "keeps the approved ×1.15 formula byte-identical on fresh installs".
 * The charges are the estimator's own rows, in its order.
 */
export const proformaPnL: PnLInput = {
  question: "Is the given price above target?",
  expectedProfitMarginPercent: 15,
  givenPriceSource: "Given price source · Step 5 specification total",
  costBasis: "cost glass at purchase price, from the Sheet Store",
  sheet: { label: sheetSizes[1]!.label, w: sheetSizes[1]!.w, h: sheetSizes[1]!.h },
  services: [
    { key: "TEMPERING", label: "Tempering", basis: "per m²", rate: 38, costed: false },
    { key: "HOLES", label: "Holes", basis: "each", rate: 6, costed: false },
    { key: "CUTOUT", label: "Cut-outs", basis: "each", rate: 9, costed: false },
    { key: "ARGON_GAS", label: "Argon gas", basis: "per m²", rate: 12, costed: false, dguOnly: true },
    { key: "SILICON_BED", label: "Silicon bed", basis: "per m²", rate: 14, costed: false, dguOnly: true },
  ],
};

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
    /*
     * The dashboard is not four counters. It is `DashboardViewModel.Pages` —
     * seven pages, in the application's own order, Overview first:
     *
     *   Overview · Sheet Store · Proforma Invoice · Job Orders ·
     *   Deliveries · Tax Invoices · Statements
     *
     * Those are the pages' own `TabTitle`s, verbatim, and each carries its
     * `TabSubtitle` — business at a glance; stock and inventory value; quotes
     * and conversion; production workload; dispatch and balance; revenue and
     * ageing; customer balances. `SelectTabCommand` sets `ActivePage` and that
     * setter calls `LoadAsync()` on the page it switched to, so a tab is a
     * different screen rather than the same screen with different numbers.
     * Under the titles, the pulses:
     *
     *   Quote pulse        count of quotes · pipeline volume and confirmation
     *                      · status distribution · top customers
     *   Production pulse   IN PRODUCTION · workload, priority mix, recent job
     *                      activity
     *   Dispatch pulse     delivery note count · dispatch flow, delivery
     *                      balance, recent movements
     *   Stock pulse        available SQM by glass category · category mix ·
     *                      dummy / split / master
     *   Collections pulse  customer balances, outstanding value and collection
     *                      risk · ageing brackets · collected vs outstanding
     *                      vs overdue
     *   Statement pulse    statements, and "Generate a statement to see
     *                      customer balances here" when there are none
     *
     * The figures are this repository's seeded workflow — one inquiry, one
     * proforma of 22 items in 3 groups, one job, one partial delivery of 15
     * items and one invoice — so nothing here is a fabricated trade volume.
     */
    kind: "dashboard",
    blurb: "Seven dashboard pages on one design grammar — Overview, Sheet Store, Proforma Invoice, Job Orders, Deliveries, Tax Invoices, Statements.",
    views: [
      {
        label: "Overview",
        columns: [],
        rows: [],
        summary: [
          { label: "Overview — business at a glance", value: "1 inquiry · 1 proforma · 1 job · 1 invoice" },
          { label: "Order-to-cash pipeline", value: "Quote 1 → Production 1 → Delivery 15 of 22 → Invoice AED 34,762.19 → Payment due in 30 days" },
          { label: "Quote pulse", value: "INQ-2026-001 confirmed — PI-2026-001, 22 items, 3 spec groups" },
          { label: "Production pulse", value: "JOB-2026-001 in production · priority High" },
          { label: "Collections pulse", value: "INV-2026-001 issued · VAT at 5% · due in 30 days" },
          { label: "Only Overview carries the pipeline rail", value: "stages 4 and 5 read live from the tax-invoice metrics; an unreadable stage shows an em dash, not a zero" },
        ],
      },
      {
        label: "Sheet Store",
        columns: [],
        rows: [],
        summary: [
          { label: "Sheet Store — stock and inventory value", value: "Available SQM by glass category" },
          { label: "Stock pulse", value: "Clear 6 mm 46 · Clear 8 mm 18 · Clear 10 mm 9 · Clear 12 mm 4" },
          { label: "Category mix", value: "Clear 6 / 8 / 10 / 12 · Tinted 6 / 8 · Low-E 6 · Frosted 5" },
          { label: "Dummy · split · master sheets", value: "Remnant and part sheets counted with the sheets they came from" },
          { label: "Reserved", value: "12 sheets committed to released work" },
        ],
      },
      {
        label: "Proforma Invoice",
        columns: [],
        rows: [],
        summary: [
          { label: "Proforma Invoice — quotes and conversion", value: "PI-2026-001 approved · 22 items · 160.79 m²" },
          { label: "Quote pulse", value: "1 quote · INQ-2026-001 · confirmed" },
          { label: "Pipeline volume", value: "AED 33,106.85 ex-VAT across 3 specification groups" },
          { label: "Status distribution", value: "Draft → Issued → Approved → Converted to job order" },
          { label: "Top customers", value: "Al Noor Glazing Contracting LLC · AZT-POD-2026" },
        ],
      },
      {
        label: "Job Orders",
        columns: [],
        rows: [],
        summary: [
          { label: "Job Orders — production workload", value: "JOB-2026-001 · priority High" },
          { label: "Production pulse — in production", value: "83 pieces across 8 stages" },
          { label: "Workload and priority mix", value: "High · Normal · Low by station" },
          { label: "Recent job activity", value: "Cutting 22 rows open 11 · Tempering 10 rows" },
          { label: "Delivery balance", value: "7 of 22 items still on the floor" },
        ],
      },
      {
        label: "Deliveries",
        columns: [],
        rows: [],
        summary: [
          { label: "Deliveries — dispatch and balance", value: "DN-2026-001-01 · Dispatching" },
          { label: "Dispatch pulse — delivery notes", value: "1 note · 15 of 22 items leaving · 7 on balance" },
          { label: "Dispatch flow", value: "Job order released → note raised → vehicle loading → delivered" },
          { label: "Delivery type", value: "Site delivery · Al Noor Glazing Contracting LLC" },
          { label: "Recent movements", value: "SHJ-48211 · Salim Khan" },
        ],
      },
      {
        label: "Tax Invoices",
        columns: [],
        rows: [],
        summary: [
          { label: "Tax Invoices — revenue and ageing", value: "INV-2026-001 · AED 34,762.19 incl. 5% VAT" },
          { label: "Revenue · VAT · paid · pending", value: "Subtotal 33,106.85 · VAT 1,655.34" },
          { label: "Ageing brackets", value: "Current · 30 · 60 · 90+ days" },
          { label: "Collections pulse", value: "Outstanding, overdue and collection risk by customer" },
          { label: "Past terms", value: "Overdue amounts grouped by age" },
        ],
      },
      {
        label: "Statements",
        columns: [],
        rows: [],
        summary: [
          { label: "Statements — customer balances", value: "Al Noor Glazing Contracting LLC" },
          { label: "Statement pulse", value: "Generate a statement to see customer balances here." },
          { label: "Open items", value: "INV-2026-001 · issued · due in 30 days" },
          { label: "Ageing", value: "Current · 30 · 60 · 90+ once statements exist" },
          { label: "Empty state", value: "Every page can say there is nothing yet instead of showing a blank card" },
        ],
      },
    ],
    footnote:
      "Seven pages on one design grammar: Overview, Sheet Store, Proforma Invoice, Job Orders, Deliveries, Tax Invoices and Statements, in that order. Selecting a tab loads that page — its own KPI band, its own chart and its own feed — so the tab strip switches the screen, not just the numbers. Each page has its own folder under Views/Dashboard/ beside the Overview.",
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
    /*
     * Opens on the grid rather than on step 1. Steps 4, 5 and 9 are the three
     * screens with an engine behind them, and they are the three the pills
     * above the rail go straight to.
     */
    defaultStep: 5,
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
        label: "Step 5 · Specification",
        step: 5,
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
        label: "Step 4 · Pricing estimator",
        step: 4,
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
      {
        /*
         * Step 9 — profit and loss.
         *
         * This view carries the step's own surface (`ProfitLossSurface`): the
         * decision cards, the four tabs, and the arithmetic over Step 5's rows.
         * The columns and rows here are empty on purpose — the surface is not a
         * table, and a table's worth of invented numbers beside it would be a
         * second, disagreeing answer.
         */
        label: "Step 9 · Profit & loss",
        step: 9,
        columns: [],
        rows: [],
      },
    ],
    stepNotes: [
      "Company details, TRN, bank lines and the approval block that prints on the document. Company setup is held once and reused by every proforma, tax invoice and statement.",
      "Bill-to party, TRN, attention name, site location and the assigned salesman. The client list auto-fills the block; a new party can be typed straight in.",
      "Project name, reference, contract number, LPO number and prepared-by — the identifiers the job is filed and searched under.",
      "The pane and sheet source, then the charges the estimator adds: glass rate, tempering, holes, cut-outs, argon gas, silicon bed and wastage. Its figures stamp onto the specification in step 5, and the difference between the two is what the review step exists to show.",
      "The grid the job is priced in: one block per specification, GLASS TYPE · SPECIFICATION · BASE and SURCHARGE % on the block, and one row per glass line below it. Every panel bills at least 0.5 m², and a panel that reaches 4 m² takes the specification's surcharge rate.",
      "Transport, installation, crane, packing, discount and VAT — everything that is not glass, added to the glass subtotal before the document is printed.",
      "The document as the client receives it, with the letterhead, the specification table, the charges, the terms and the signature block. Print, PDF and the approval stamp live here.",
      "The client's LPO, the drawings and the approval scans, attached to the job and carried with it into the tax invoice and the statement.",
      "Profit and loss on the specification the job was priced from: the required target price, the expected margin, and the balance between the two — then the money, the services, the sheets and the spec-by-spec check.",
    ],
    spec: proformaSpec,
    pnl: proformaPnL,
    footnote:
      "Step 5 holds the specification: # · GLASS REF · W1 · H1 · W2 · H2 · QTY · SQM · TOTAL SQM · RATE · VALUE, on a block carrying GLASS TYPE · SPECIFICATION · BASE · SURCHARGE %. A dash in W2 or H2 is a single-glazed line — the application stores 0 in those cells, as it does in the seeded job on the worked example. Two rules do the pricing and both are live here: every panel bills at least 0.5 m² (the rate is unchanged — the floor is on the area), and a panel that reaches 4 m² takes the specification's surcharge rate instead of its base rate. Step 9 then costs the same rows at the Sheet Store's purchase price and answers the only question that matters: is the given price above target?",
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

  Cutting: { kind: "stage", blurb: "Cutting — a stage page with the shared production chrome.", views: [] },
  HolesCutouts: { kind: "stage", blurb: "Holes, fan holes, cut-outs and countersunk holes — a stage page with the shared production chrome.", views: [] },
  Polish: { kind: "stage", blurb: "Polish — a stage page with the shared production chrome.", views: [] },
  EdgeDeletion: { kind: "stage", blurb: "EdgeDeletion — a stage page with the shared production chrome.", views: [] },
  Mitring: { kind: "stage", blurb: "Mitring — a stage page with the shared production chrome.", views: [] },
  Tempering: { kind: "stage", blurb: "Tempering — a stage page with the shared production chrome.", views: [] },
  Pairing: { kind: "stage", blurb: "Pairing — a stage page with the shared production chrome.", views: [] },
  DoublingLamination: { kind: "stage", blurb: "DoublingLamination — a stage page with the shared production chrome.", views: [] },

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
    /*
     * Settings is a rail of twelve sections, not two: About, Agent, AI,
     * Appearance, Data, Demo, Features, License, Sharing, Themes, Updates and
     * Voice — one XAML each under `Views/Settings/Sections/`.
     *
     * DATABASE HEALTH and BACKUP & RESTORE live in the Data section, which is
     * worth saying out loud because a health surface is easy to go looking for
     * in System Center, where it is not.
     */
    kind: "form",
    blurb: "Twelve sections of application settings — appearance, data, agent, sharing, licence, updates.",
    views: [
      {
        label: "Sections",
        columns: [
          { key: "section", label: "Section" },
          { key: "covers", label: "What it covers", hideBelow: "md" },
        ],
        rows: [
          { section: "Appearance", covers: "Theme family, accents, mode, wallpaper, motion, per-module backgrounds" },
          { section: "Themes", covers: "Visual theme family and Crystal Fluid material tuning" },
          { section: "Data", covers: "DATABASE HEALTH · BACKUP & RESTORE · Backup Sets · rebuild" },
          { section: "Demo", covers: "Seed the demo dataset on an empty database, reset the demo folder" },
          { section: "Features", covers: "Feature switches for modules you do not use" },
          { section: "Sharing", covers: "Sharing Center — what leaves the application and how" },
          { section: "License", covers: "LICENSE STATUS and the Security section" },
          { section: "Updates", covers: "RUNNING VERSION against the latest published release, UPDATE HISTORY" },
          { section: "AI", covers: "About the AI Assistant — models, API key, privacy" },
          { section: "Voice", covers: "Offline speech models for dictation and voice notes" },
          { section: "Agent", covers: "Agent settings and the agent's own database" },
          { section: "About", covers: "Built for UAE glass fabricators — version and credits" },
        ],
      },
      {
        label: "Database health",
        columns: [
          { key: "item", label: "Item" },
          { key: "detail", label: "Detail", hideBelow: "md" },
        ],
        rows: [
          { item: "Probe databases", detail: "Probe every database and refresh the row-count table" },
          { item: "Row counts", detail: "One row per database file, so drift is visible at a glance" },
          { item: "Auto-migration", detail: "Handles most drift on the next app start" },
          { item: "Rebuild all databases", detail: "The emergency option for a file too broken to open" },
          { item: "Backup set", detail: "Every database plus settings into one timestamped folder" },
          { item: "Automatic sets", detail: "Created when the app closes" },
          { item: "Restore", detail: "Always takes a safety snapshot first" },
        ],
      },
    ],
    footnote:
      "Sixteen databases sit behind the application, one EF Core context each, with additive-only migrations. The backup set copies all of them plus settings into one timestamped folder; restore never overwrites without taking a safety snapshot first. The four family buttons below are the real control — pressing one re-themes this website as well as describing the application.",
  },

  About: {
    /*
     * About carries the licence state, and the editions are the five in
     * `LicenseEdition` (`Services/Licensing/LicensePayload.cs`) — Trial, Basic,
     * Business, Factory, Enterprise. There is no "Standard" or "Professional";
     * the contract carries a licence *type* and the edition is derived from it
     * plus the granted feature set.
     *
     * The numbers are the code's own: the auto trial is 30 days, the verifier
     * warns when fewer than 14 days remain, an expired licence runs read-only
     * for the grace days written into the signed offline policy, and the
     * auto-issued 7-day trial refuses to start once it has expired.
     */
    kind: "form",
    blurb: "Licence state, the editions, and what each one unlocks.",
    views: [
      {
        label: "Licence",
        columns: [
          { key: "field", label: "Field" },
          { key: "value", label: "Value" },
        ],
        rows: [
          { field: "Edition", value: "Derived from the licence type and the granted features" },
          { field: "Trial", value: "30 days, single activation, all features" },
          { field: "Expiry warning", value: "Surfaced at 14 days remaining" },
          { field: "After expiry", value: "Read-only, for the grace days in the signed offline policy" },
          { field: "Devices", value: "Counted against the activation limit, checked by machine fingerprint" },
        ],
      },
      {
        label: "Editions",
        columns: [
          { key: "edition", label: "Edition" },
          { key: "state", label: "Scope" },
        ],
        rows: [
          { edition: "Trial", state: "30 days, single activation, all features" },
          { edition: "Basic", state: "Single user, inquiry and proforma only" },
          { edition: "Business", state: "Up to 5 users, full ERP without production optimisation" },
          { edition: "Factory", state: "Unlimited users, includes the cutting-optimisation engine" },
          { edition: "Enterprise", state: "Unlimited, multi-site, custom integrations" },
        ],
        summary: [{ label: "Price list", value: "Not published — ask the developer" }],
      },
    ],
  },

  SystemCenter: {
    /*
     * System Center is not a storage browser. Its own strings say what it is:
     * "User management & audit trail — signed in as", an ACTIVITY LOG with the
     * note "Audit trail of every recorded action", an ACTIVE NOW pane whose
     * empty state reads "Colleagues running ProGlass on the same LAN appear
     * here automatically", and five scope buttons over the same trail.
     *
     * The scopes are not arbitrary groupings — they map onto the canonical
     * section constants in `Services/Audit/AuditSections.cs`, which is why the
     * membership is listed here rather than paraphrased.
     *
     * The database-health surface sits in Settings, not here, so it is shown
     * there.
     */
    kind: "dashboard",
    blurb: "User management and the audit trail — every recorded action, filtered by scope.",
    views: [
      {
        label: "Activity log",
        columns: [
          { key: "time", label: "Time" },
          { key: "scope", label: "Scope", kind: "status" },
          { key: "section", label: "Section" },
          { key: "action", label: "Action", hideBelow: "md" },
          { key: "details", label: "Details", hideBelow: "md" },
          { key: "user", label: "User" },
        ],
        rows: [
          {
            time: "08:04",
            scope: "System",
            section: "Settings",
            action: "Sample data loaded",
            details: "1 complete workflow seeded: Inquiry → PI (22 items) → JO → DN → TI",
            user: "Demo User",
          },
          {
            time: "09:02",
            scope: "Transactions",
            section: "Proforma Invoice",
            action: "Inquiry status changed",
            details: "INQ-2026-001 Draft → Confirmed — reason \"LPO LPO-AN-2026-088 received\"",
            user: "Ahmed Rahman",
          },
          {
            time: "09:18",
            scope: "Transactions",
            section: "Proforma Invoice",
            action: "Proforma approved",
            details: "PI-2026-001 — 22 glass items, 3 specification groups",
            user: "Ahmed Rahman",
          },
          {
            time: "09:20",
            scope: "Transactions",
            section: "Job Order",
            action: "Job order created",
            details: "JOB-2026-001 from PI-2026-001 — priority High",
            user: "Demo User",
          },
          {
            time: "11:44",
            scope: "Inventory",
            section: "Specifications",
            action: "Counts reconciled",
            details: "3 usage rows healed, 0 orphan rows purged",
            user: "Demo User",
          },
          {
            time: "14:12",
            scope: "Transactions",
            section: "Delivery Note",
            action: "Status changed",
            details: "DN-2026-001-01 Draft → Dispatching — driver Salim Khan",
            user: "Ahmed Rahman",
          },
          {
            time: "15:02",
            scope: "Transactions",
            section: "Tax Invoice",
            action: "Tax invoice issued",
            details: "INV-2026-001 — VAT 5% from subtotal, due in 30 days",
            user: "Demo User",
          },
          {
            time: "16:48",
            scope: "System",
            section: "Reports",
            action: "Generation failed",
            details: "Order report — no job order matched the date range",
            user: "Demo User",
          },
        ],
      },
      {
        label: "Scopes",
        columns: [
          { key: "scope", label: "Scope" },
          { key: "sections", label: "Sections behind it", hideBelow: "md" },
        ],
        rows: [
          { scope: "All activity", sections: "Full audit trail — every recorded event (compliance view)" },
          { scope: "Transactions", sections: "Proforma · Job Order · Delivery · Tax Invoice · Credit Note · Statement · Inquiry" },
          { scope: "Inventory", sections: "Sheet Store · Specifications · Master Data" },
          { scope: "System", sections: "Application · Dashboard · Reports · AI Assistant" },
          { scope: "Users & security", sections: "Users · Settings · Sharing · Updates" },
        ],
      },
      {
        label: "Active now",
        columns: [
          { key: "user", label: "User" },
          { key: "role", label: "Role" },
          { key: "since", label: "Signed in", align: "right" },
        ],
        rows: [
          { user: "Demo User", role: "Administrator", since: "08:02" },
          { user: "Ahmed Rahman", role: "Salesman", since: "09:01" },
        ],
      },
    ],
    footnote:
      "Colleagues running ProGlass on the same LAN appear in ACTIVE NOW automatically. IMPORTANT ONLY hides low-signal events — viewed, opened, session — for reading; the tooltip is explicit that recording is never affected. Sections are the constants in AuditSections.cs and the five scopes are the groupings System Center filters them by.",
  },

  Diagnostics: {
    /*
     * `Nav.LogsActivity` — "Logs & Activity" in the rail, navigating with
     * `CommandParameter="Diagnostics"`. The surface is the application's own
     * terminal: TIME · LEVEL · MODULE / FUNCTION · entry, the last 500 kept in
     * memory, counters for errors and warnings, and a button that opens the log
     * folder. The rows are pattern rows built from the seeded workflow — see
     * `activityLog.ts`, where the identifiers come from `SampleDataService` and
     * the section names from `AuditSections.cs`.
     *
     * The five invented rows this replaced ("Database files · Pass", "15
     * locales loaded") were not in the application at all.
     */
    kind: "log",
    blurb: "The application log and the audit trail — what it did, and who changed what.",
    views: [],
    footnote:
      "Two services, not one. AppLogService writes diagnostics — startup, licence, seeding, production, errors — with a CorrelationId (CORR-XXXXXX) and a DocumentId so one business operation can be traced from the inquiry to the statement across files. AuditService writes business events, with the section names held as constants in AuditSections.cs so a typo cannot split one section into two, and before/after change sets that the Activity view draws as a diff.",
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
