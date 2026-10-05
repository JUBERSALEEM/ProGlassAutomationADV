/**
 * The workflow the application seeds itself.
 *
 * Every value in this file is transcribed from
 * `Services/SampleData/SampleDataService.cs`, which is the seeder the desktop
 * application runs against an empty database. The parties, glass references,
 * sizes, rates, sheet stock and consumables are the application's own
 * demonstration records — they are not a customer of anyone, and nothing here
 * is a market price.
 *
 * Three things make this different from a marketing figure:
 *
 *   1. It is real. A person can install the application, press the seed
 *      button and see exactly these documents, these 22 lines and these totals.
 *   2. The arithmetic is the application's own. The line value is area × rate
 *      rounded by the same rule the application uses, the VAT is the same 5%
 *      the VAT calculator applies, and the weight uses the application's own
 *      glass formula. `derived` below performs that arithmetic rather than
 *      restating its result, so the page cannot quietly disagree with itself.
 *   3. It is checked. `assertMatchesSeed()` throws if the derived figures stop
 *      matching the seeder, and because these pages are prerendered at build
 *      time, that turns silent drift into a failed build.
 */

import { text } from "@/i18n/core";

import {
  billablePanelSqm,
  isFloored,
  moneyRate,
  physicalPanelSqm,
  SURCHARGE_THRESHOLD_SQM,
} from "@/data/pricingRules";

export const SEED_SOURCE = "Services/SampleData/SampleDataService.cs";
export const ROUNDING_SOURCE = "Services/Reporting/MoneyRounding.cs";
export const VAT_SOURCE = "Services/Reporting/VatCalculator.cs";

/* ══════════════════════════════════════════════════════════════════════════
 * Money, the way the application rounds it
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * Two decimal places, half away from zero — `MoneyRounding.Round`.
 *
 * The app keeps its books in AED, which has 100 fils, so money is stored to
 * two decimals, and a half rounds away from zero rather than to the even
 * digit. That choice is deliberate in the source: the invoice and VAT layer is
 * the part whose output is legally filed, and half-up is what a person
 * reconciling by hand expects.
 *
 * The nudge in the middle removes binary floating-point representation error
 * (0.615 stored as 0.61499999…) before the half is chosen, so this agrees with
 * .NET decimal arithmetic on the values used here.
 */
export function money(value: number, decimals = 2): number {
  const factor = 10 ** decimals;
  const scaled = value * factor;
  const nudged = Math.round(scaled * 1e9) / 1e9;
  const magnitude = Math.round(Math.abs(nudged)) / factor;
  return nudged < 0 ? -magnitude : magnitude;
}

/** One decimal, the precision the seeder uses for a piece weight. */
const weight = (value: number) => Math.round(value * 10) / 10;

/* ══════════════════════════════════════════════════════════════════════════
 * The parties and the project
 * ════════════════════════════════════════════════════════════════════════ */

export const seedParties = {
  company: {
    name: "ProGlass Automation Demo",
    trn: "100234567800003",
    location: "Sharjah, UAE",
    note: "The issuing company on the seeded documents.",
  },
  customer: {
    name: "Al Noor Glazing Contracting LLC",
    trn: "100987654300003",
    location: "Sharjah, UAE",
    note: "The customer record the workflow is written against.",
  },
  project: {
    name: "Al Zahia Tower — Podium Glazing",
    reference: "AZT-POD-2026",
    contract: "CTR-2026-042",
    lpo: "LPO-AN-2026-088",
    site: "Al Zahia, Sharjah, UAE",
    note: "Supply and installation of clear tempered, tinted and Low-E glass for the tower podium facade.",
  },
  people: {
    salesman: "Ahmed Rahman",
    preparedBy: "Demo User",
    driver: "Salim Khan",
    vehicle: "SHJ-48211",
    receiver: "Mohammed Ali (Site Storekeeper)",
  },
  /** The figure recorded when the enquiry arrived, before anything was priced. */
  inquiryValue: 87_450,
} as const;

/* ══════════════════════════════════════════════════════════════════════════
 * The five documents
 * ════════════════════════════════════════════════════════════════════════ */

export type SeedDocument = {
  key: string;
  /** The number the seeder builds, with {year} taken from the seeding machine. */
  pattern: string;
  example: string;
  title: string;
  status: string;
  /** Days relative to the seeding date — negative is before it. */
  dayOffset: number;
  summary: string;
  /** What crosses into the next document. */
  handoff: string;
  evidence: string;
};

export const seededDocuments: SeedDocument[] = [
  {
    key: "inquiry",
    pattern: "INQ-{year}-001",
    example: "INQ-2026-001",
    title: "Inquiry",
    status: "Confirmed",
    dayOffset: -14,
    summary:
      "The request arrives with its scope of supply: the project, the site, the salesman who took it and an indicative value of AED 87,450 recorded before anything was priced.",
    handoff: "The confirmed enquiry is the starting point the quotation is raised from.",
    evidence: "Models/Inquiry/InquiryRecord.cs",
  },
  {
    key: "proforma",
    pattern: "PI-{year}-001",
    example: "PI-2026-001",
    title: "Proforma invoice",
    status: "Approved",
    dayOffset: -10,
    summary:
      "The quotation, priced across three specification groups — 22 lines, 83 pieces, 160.79 m². Issued with a 30-day validity.",
    handoff: "The approved proforma is what the job order is converted from.",
    evidence: "Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs",
  },
  {
    key: "job-order",
    pattern: "JOB-{year}-001",
    example: "JOB-2026-001",
    title: "Job order",
    status: "Released",
    dayOffset: -10,
    summary:
      "Converted from the proforma, priority High, delivery date 14 days out. The floor sheet carries the specification and the quantity — not the selling price.",
    handoff: "The released job order is what the delivery note is raised against.",
    evidence: "Services/JobOrder/Mapper/JobOrderMapper.cs",
  },
  {
    key: "delivery-note",
    pattern: "DN-{year}-001-01",
    example: "DN-2026-001-01",
    title: "Delivery note",
    status: "Dispatching",
    dayOffset: 0,
    summary:
      "Batch 1 of 2: the ten Clear Tempered lines leave, 45 of the 83 pieces. The tinted and Low-E pieces stay on the job as balance.",
    handoff: "What was actually dispatched is what the tax invoice is raised from.",
    evidence: "Services/DeliveryNote/Mapper/DeliveryNoteMapper.cs",
  },
  {
    key: "tax-invoice",
    pattern: "INV-{year}-001",
    example: "INV-2026-001",
    title: "Tax invoice",
    status: "Issued",
    dayOffset: 0,
    summary:
      "Raised from the delivered lines only — the ten Clear Tempered lines, billed and VAT-applied. Payment terms run 30 days from issue.",
    handoff: "The issued invoice is what the statement and the VAT submission read.",
    evidence: "Models/TaxInvoice/SavedTaxInvoice/TaxInvoiceItem.cs",
  },
];

/* ══════════════════════════════════════════════════════════════════════════
 * The quotation: three specification groups, 22 lines
 * ════════════════════════════════════════════════════════════════════════ */

export type SeedLine = {
  serial: number;
  ref: string;
  widthMm: number;
  heightMm: number;
  qty: number;
  rate: number;
  /** Only a double-glazed line carries a second pane pair. */
  secondPane?: { widthMm: number; heightMm: number };
};

export type SeedSpecGroup = {
  number: number;
  name: string;
  glassType: "SGU" | "DGU";
  baseRate: number;
  /**
   * The specification's `Dg.SurchargePct` — the uplift a row takes once its
   * panel reaches 4 m² (`row.Sqm1 >= 4m ? SurchargeRate : BaseRate`).
   *
   * Zero on all three of the seeded groups: `SurchargePercent` is a field, its
   * initial value is 0 (`DataGridViewModel` seeds it so), and this document was
   * saved before anybody typed a figure into it. The shop's standard for
   * oversize work is 20% and `pricingRules` holds it — `proformaSpec` in
   * `liveDemos.ts` is where it is shown applied.
   */
  surchargePercent: number;
  lines: SeedLine[];
};

export const seedSpecGroups: SeedSpecGroup[] = [
  {
    number: 1,
    name: "Clear Tempered — Main Facade",
    glassType: "SGU",
    baseRate: 150,
    surchargePercent: 0,
    lines: [
      { serial: 1, ref: "CT-06-1200x900", widthMm: 1200, heightMm: 900, qty: 8, rate: 150 },
      { serial: 2, ref: "CT-06-1500x1000", widthMm: 1500, heightMm: 1000, qty: 6, rate: 150 },
      { serial: 3, ref: "CT-08-1800x1200", widthMm: 1800, heightMm: 1200, qty: 5, rate: 185 },
      { serial: 4, ref: "CT-08-2000x1500", widthMm: 2000, heightMm: 1500, qty: 4, rate: 185 },
      { serial: 5, ref: "CT-10-2200x1400", widthMm: 2200, heightMm: 1400, qty: 3, rate: 210 },
      { serial: 6, ref: "CT-10-2400x1600", widthMm: 2400, heightMm: 1600, qty: 3, rate: 210 },
      { serial: 7, ref: "CT-12-1800x1200", widthMm: 1800, heightMm: 1200, qty: 4, rate: 245 },
      { serial: 8, ref: "CT-12-2000x1500", widthMm: 2000, heightMm: 1500, qty: 2, rate: 245 },
      { serial: 9, ref: "CT-06-1000x800", widthMm: 1000, heightMm: 800, qty: 6, rate: 150 },
      { serial: 10, ref: "CT-08-1600x1100", widthMm: 1600, heightMm: 1100, qty: 4, rate: 185 },
    ],
  },
  {
    number: 2,
    name: "Tinted — Sun-Exposed Areas",
    glassType: "SGU",
    baseRate: 175,
    surchargePercent: 0,
    lines: [
      { serial: 1, ref: "TT-06-1200x900", widthMm: 1200, heightMm: 900, qty: 5, rate: 175 },
      { serial: 2, ref: "TT-06-1500x1000", widthMm: 1500, heightMm: 1000, qty: 4, rate: 175 },
      { serial: 3, ref: "TT-08-1800x1200", widthMm: 1800, heightMm: 1200, qty: 3, rate: 195 },
      { serial: 4, ref: "TT-08-2000x1500", widthMm: 2000, heightMm: 1500, qty: 2, rate: 195 },
      { serial: 5, ref: "TT-06-1000x800", widthMm: 1000, heightMm: 800, qty: 4, rate: 175 },
      { serial: 6, ref: "TT-08-1600x1100", widthMm: 1600, heightMm: 1100, qty: 3, rate: 195 },
      { serial: 7, ref: "TT-06-1400x950", widthMm: 1400, heightMm: 950, qty: 3, rate: 175 },
    ],
  },
  {
    number: 3,
    name: "Low-E — Energy Efficient Zones",
    glassType: "DGU",
    baseRate: 260,
    surchargePercent: 0,
    lines: [
      {
        serial: 1,
        ref: "LE-06-1800x1200",
        widthMm: 1800,
        heightMm: 1200,
        qty: 4,
        rate: 260,
        secondPane: { widthMm: 1800, heightMm: 1200 },
      },
      {
        serial: 2,
        ref: "LE-06-2000x1500",
        widthMm: 2000,
        heightMm: 1500,
        qty: 3,
        rate: 260,
        secondPane: { widthMm: 2000, heightMm: 1500 },
      },
      {
        serial: 3,
        ref: "LE-06-2200x1400",
        widthMm: 2200,
        heightMm: 1400,
        qty: 2,
        rate: 260,
        secondPane: { widthMm: 2200, heightMm: 1400 },
      },
      {
        serial: 4,
        ref: "LE-06-1600x1100",
        widthMm: 1600,
        heightMm: 1100,
        qty: 3,
        rate: 260,
        secondPane: { widthMm: 1600, heightMm: 1100 },
      },
      {
        serial: 5,
        ref: "LE-06-2400x1600",
        widthMm: 2400,
        heightMm: 1600,
        qty: 2,
        rate: 260,
        secondPane: { widthMm: 2400, heightMm: 1600 },
      },
    ],
  },
];

/* ══════════════════════════════════════════════════════════════════════════
 * The two rules, shown on the panels where they bite
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * The sheet cost behind each glass reference, from the seeder's own stock.
 *
 * `SampleDataService.cs` seeds eight sheets with a purchase price and a selling
 * price, and the seeded quotation's rates are the selling column to the fil:
 * Clear 6 mm 95 / 150 against the CT-06 lines at 150, Clear 12 mm 165 / 245
 * against the CT-12 lines at 245, Low-E 6 mm 180 / 260 against the LE lines at
 * 260. So the purchase column is this job's glass cost and the two columns
 * reconcile — which is the whole point of showing a profit-and-loss step: the
 * revenue is quoted, the cost is what the supplier charges, and the difference
 * is the margin.
 *
 * `purchase` is read from `GlassSheet.PurchasePricePerSqm`; `sell` is kept so
 * the reconciliation can be checked rather than asserted.
 */
export const sheetCosts = [
  { key: "06", label: "Clear 6 mm", purchase: 95, sell: 150, sheetMm: "2440 × 1830", supplier: "Guardian Glass" },
  { key: "08", label: "Clear 8 mm", purchase: 120, sell: 185, sheetMm: "2440 × 1830", supplier: "Guardian Glass" },
  { key: "10", label: "Clear 10 mm", purchase: 140, sell: 210, sheetMm: "2440 × 1830", supplier: "Guardian Glass" },
  { key: "12", label: "Clear 12 mm", purchase: 165, sell: 245, sheetMm: "2440 × 1830", supplier: "Guardian Glass" },
  { key: "TT-06", label: "Tinted 6 mm bronze", purchase: 110, sell: 175, sheetMm: "2440 × 1830", supplier: "Al Gurg Glass" },
  { key: "TT-08", label: "Tinted 8 mm grey", purchase: 125, sell: 195, sheetMm: "2440 × 1830", supplier: "Al Gurg Glass" },
  { key: "LE", label: "Low-E 6 mm clear", purchase: 180, sell: 260, sheetMm: "2440 × 1830", supplier: "Saint-Gobain" },
] as const;

/**
 * Which stock row a glass reference is cut from.
 *
 * The references carry it: `CT-08-1800x1200` is Clear, 8 mm; `TT-06-…` is
 * Tinted, 6 mm; `LE-06-…` is Low-E. The seeder names them that way and so does
 * the shop floor.
 */
export function sheetForRef(ref: string) {
  const thickness = ref.match(/^(?:CT|TT|LE)-(\d\d)/)?.[1] ?? "";
  if (ref.startsWith("LE")) return sheetCosts.find((sheet) => sheet.key === "LE");
  if (ref.startsWith("TT")) return sheetCosts.find((sheet) => sheet.key === `TT-${thickness}`);
  return sheetCosts.find((sheet) => sheet.key === thickness);
}

/**
 * The line's glass cost: billable area × quantity × the sheet's purchase price.
 *
 * Costed on the same billable area the line is sold on. A sub-0.5 m² panel
 * therefore costs half a square metre of glass too — the offcut is real, it is
 * bought and it is thrown away — which is exactly why the floor is on the area
 * rather than on the price.
 */
export function lineCost(line: SeedLine): number {
  const sheet = sheetForRef(line.ref);
  if (!sheet) return 0;
  return money(lineSqm(line) * sheet.purchase);
}

/**
 * The two edges of the two rules, priced through the same functions everything
 * else on this site prices through.
 *
 * Neither panel is on the seeded job — its smallest lite is 0.80 m² and its
 * largest is 3.84 m², so on this quotation both rules are dormant. They are not
 * hypothetical sizes though: a vent light and an atrium lite are the two panes
 * every shop argues about, and these are the figures the rules produce for
 * them, at the specification's own rate.
 */
export const ruleLights = [
  {
    id: "vent-light",
    name: "Vent light",
    ref: "CT-06-0400x0300",
    widthMm: 400,
    heightMm: 300,
    qty: 6,
    rate: 150,
    note: "A toilet vent, 400 × 300 mm. Physical area 0.12 m²; billed at the 0.5 m² floor.",
  },
  {
    id: "atrium-lite",
    name: "Atrium lite",
    ref: "CT-12-2400x1800",
    widthMm: 2400,
    heightMm: 1800,
    qty: 3,
    rate: 245,
    note: text`4.32 m² of glass, past the ${SURCHARGE_THRESHOLD_SQM} m² switch on the panel itself.`,
  },
] as const;

/**
 * A rule light with everything the grids print: physical area, billable area,
 * whether the floor engaged, the rate the row carries and what it comes to.
 *
 * `surchargePercent` is the specification's own `Dg.SurchargePct`. Both of the
 * seeded specification groups are at 0 — the field's default and the value this
 * quotation was saved with — so the argument is here so a surface can show what
 * a specification carrying the shop's standard 20% would price like, which the
 * specification preview does.
 */
export function priceLight(light: (typeof ruleLights)[number], surchargePercent = 0) {
  const physical = physicalPanelSqm(light.widthMm, light.heightMm);
  const billable = billablePanelSqm(light.widthMm, light.heightMm);
  const surcharged = billable >= SURCHARGE_THRESHOLD_SQM;
  const chargedRate = surcharged
    ? moneyRate(light.rate * (1 + surchargePercent / 100))
    : moneyRate(light.rate);

  return {
    ...light,
    physical,
    billable,
    floored: isFloored(light.widthMm, light.heightMm),
    surcharged,
    rate: chargedRate,
    baseRate: moneyRate(light.rate),
    totalSqm: billable * light.qty,
    value: money(billable * light.qty * chargedRate),
  };
}

/* ══════════════════════════════════════════════════════════════════════════
 * The arithmetic, performed rather than transcribed
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * Area of one piece in m² — millimetres to metres, multiplied.
 *
 * This is the *physical* area. What the document bills for it is
 * `billablePieceArea` below, and the difference between the two is the rule the
 * trade prices small panes by.
 */
export const pieceArea = (line: SeedLine) => physicalPanelSqm(line.widthMm, line.heightMm);

/**
 * Billable area of one piece in m² — floored at 0.5 m².
 *
 * `SqmCalculator.CalculatePanel`, which is what the application's own SQM
 * column prints: a pane too small to fill a square metre still costs half a
 * square metre to make. On this seeded job the smallest lite is 0.80 m², so the
 * floor does not engage — see `ruleLights` for the two panels that show what it
 * does when it does.
 */
export const billablePieceArea = (line: SeedLine) =>
  billablePanelSqm(line.widthMm, line.heightMm);

/** Billable area of the whole line in m² — billable piece area × quantity. */
export const lineSqm = (line: SeedLine) => billablePieceArea(line) * line.qty;

/**
 * Area of the whole line in m², as the document states it.
 *
 * The same figure the application totals a document by: `Sqm1 × Qty`, with
 * `Sqm1` the floored panel area. (`SampleDataService` totals by raw width ×
 * height when it seeds a proforma and applies the floor when it builds the tax
 * invoice from the same job — both readings are in the file. They agree on this
 * job, whose smallest panel is 0.80 m², so the seeded totals below are the
 * application's either way. The floor is what the pricing engine charges by,
 * which is the reading this file prices with.)
 */
export const lineArea = (line: SeedLine) => lineSqm(line);

/**
 * The rate a line is charged at on a saved document.
 *
 * `GlassItemRow.Rate`, as stored. The application's automatic rate switch
 * (`Sqm1 >= 4 ? SurchargeRate : BaseRate` — see `pricingRules.ts`) fires when a
 * row's size is *edited*, or when the specification's base rate or surcharge
 * changes; a document that is simply loaded keeps the rates it was saved with,
 * because `SetAllSilent` writes them without raising the change the switch
 * listens for. This quotation therefore bills its own rates, and every one of
 * them is below the 4 m² switch because no panel in it reaches 4 m².
 */
export const lineRate = (line: SeedLine) => moneyRate(line.rate);

/**
 * Line value: billable area × the line's rate, rounded the way the application
 * rounds money — `MoneyRounding.Round(Sqm1 × Qty × Rate)`.
 */
export const lineValue = (line: SeedLine, rate = lineRate(line)) =>
  money(lineSqm(line) * rate);

export type GroupTotals = {
  number: number;
  name: string;
  glassType: string;
  lines: number;
  pieces: number
  squareMetres: number;
  subtotal: number;
};

export const groupTotals: GroupTotals[] = seedSpecGroups.map((group) => ({
  number: group.number,
  name: group.name,
  glassType: group.glassType,
  lines: group.lines.length,
  pieces: group.lines.reduce((sum, line) => sum + line.qty, 0),
  squareMetres: group.lines.reduce((sum, line) => sum + lineArea(line), 0),
  subtotal: money(group.lines.reduce((sum, line) => sum + lineValue(line), 0)),
}));

const subtotal = money(groupTotals.reduce((sum, group) => sum + group.subtotal, 0));
const vat = money(subtotal * 0.05);

export const quotationTotals = {
  lines: groupTotals.reduce((sum, group) => sum + group.lines, 0),
  pieces: groupTotals.reduce((sum, group) => sum + group.pieces, 0),
  squareMetres: groupTotals.reduce((sum, group) => sum + group.squareMetres, 0),
  subtotal,
  /** 5% — `VatCalculator.VAT_RATE_DECIMAL`. */
  vat,
  /**
   * Subtotal plus VAT.
   *
   * The seeder adds these in C# `decimal`, which is exact, and does not
   * re-round. Binary floating point cannot reproduce that: 33106.85 + 1655.34
   * lands on 34762.189999999995. Passing the sum back through `money()` is how
   * the same addition is done here — it is not extra rounding, it is the same
   * arithmetic done in a representation that can hold it.
   */
  grandTotal: money(subtotal + vat),
};

/* ── Batch 1: the delivered lines, which is what the invoice bills ───────── */

const deliveredGroup = seedSpecGroups[0]!;
const deliveredLines = deliveredGroup.lines;

export const deliveredTotals = {
  lines: deliveredLines.length,
  pieces: deliveredLines.reduce((sum, line) => sum + line.qty, 0),
  squareMetres: deliveredLines.reduce((sum, line) => sum + lineArea(line), 0),
  subtotal: money(deliveredLines.reduce((sum, line) => sum + lineValue(line), 0)),
  vat: money(money(deliveredLines.reduce((sum, line) => sum + lineValue(line), 0)) * 0.05),
  /** Same reasoning as `quotationTotals.grandTotal`. */
  get total() {
    return money(this.subtotal + this.vat);
  },
};

export const balancePieces = quotationTotals.pieces - deliveredTotals.pieces;

/* ══════════════════════════════════════════════════════════════════════════
 * Weight — the application's own glass formula
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * Nominal thickness in mm, read out of the glass reference.
 *
 * The reference format is `XX-TT-WWWxHHH`, so `CT-08-1800x1200` is 8 mm clear
 * tempered. A double-glazed unit is two lites plus the 12 mm cavity, so it
 * counts as twice the glass plus the gap.
 */
export function nominalThickness(ref: string): number {
  const thickness = Number(ref.split("-")[1]);
  if (Number.isNaN(thickness)) return 6;
  return ref.startsWith("LE-") ? thickness * 2 + 12 : thickness;
}

/**
 * Piece weight in kg: area (m²) × nominal thickness (mm) × 2.5 kg/m²/mm.
 *
 * The 2.5 is the seeder's own constant — it is the density factor it uses to
 * give each delivery line a realistic weight, not a promise about any
 * particular glass make-up.
 */
export const pieceWeight = (line: SeedLine) => weight(pieceArea(line) * nominalThickness(line.ref) * 2.5);

export const batchOneWeightKg = weight(
  deliveredLines.reduce((sum, line) => sum + pieceWeight(line) * line.qty, 0),
);

/* ══════════════════════════════════════════════════════════════════════════
 * Stock and consumables behind the job
 * ════════════════════════════════════════════════════════════════════════ */

export type SeedSheet = {
  category: string;
  thicknessMm: number;
  colour: string;
  widthMm: number;
  heightMm: number;
  supplier: string;
  buyPerSqm: number;
  sellPerSqm: number;
  stock: number;
  used: number;
};

/**
 * The eight sheets seeded into the store.
 *
 * Note the coincidence that is not one: every rate on the quotation matches the
 * sell price of the sheet that glass comes from — 6 mm clear tempered at 150,
 * 8 mm at 185, 10 mm at 210, 12 mm at 245, the tinted at 175 and 195, and the
 * Low-E unit at 260. The sample dataset is internally consistent, which is what
 * makes it useful as a worked example.
 */
export const seedSheets: SeedSheet[] = [
  { category: "Clear", thicknessMm: 6, colour: "Clear", widthMm: 2440, heightMm: 1830, supplier: "Guardian Glass", buyPerSqm: 95, sellPerSqm: 150, stock: 80, used: 10 },
  { category: "Clear", thicknessMm: 8, colour: "Clear", widthMm: 2440, heightMm: 1830, supplier: "Guardian Glass", buyPerSqm: 120, sellPerSqm: 185, stock: 60, used: 8 },
  { category: "Clear", thicknessMm: 10, colour: "Clear", widthMm: 2440, heightMm: 1830, supplier: "Guardian Glass", buyPerSqm: 140, sellPerSqm: 210, stock: 50, used: 6 },
  { category: "Clear", thicknessMm: 12, colour: "Clear", widthMm: 2440, heightMm: 1830, supplier: "Guardian Glass", buyPerSqm: 165, sellPerSqm: 245, stock: 40, used: 4 },
  { category: "Tinted", thicknessMm: 6, colour: "Bronze", widthMm: 2440, heightMm: 1830, supplier: "Al Gurg Glass", buyPerSqm: 110, sellPerSqm: 175, stock: 55, used: 5 },
  { category: "Tinted", thicknessMm: 8, colour: "Grey", widthMm: 2440, heightMm: 1830, supplier: "Al Gurg Glass", buyPerSqm: 125, sellPerSqm: 195, stock: 45, used: 3 },
  { category: "Low-E", thicknessMm: 6, colour: "Clear", widthMm: 2440, heightMm: 1830, supplier: "Saint-Gobain", buyPerSqm: 180, sellPerSqm: 260, stock: 35, used: 2 },
  { category: "Frosted", thicknessMm: 5, colour: "Frosted", widthMm: 2200, heightMm: 1650, supplier: "National Glass", buyPerSqm: 130, sellPerSqm: 200, stock: 30, used: 2 },
];

export type SeedConsumable = {
  name: string;
  category: string;
  uom: string;
  specification: string;
  partNumber: string;
  supplier: string;
  buy: number;
  sell: number;
  stock: number;
  used: number;
  reorderLevel: number;
};

/** The ten non-glass items an insulated unit needs. */
export const seedConsumables: SeedConsumable[] = [
  { name: "Warm-edge spacer bar", category: "Spacer bar", uom: "Linear metre", specification: "12mm black", partNumber: "SPB-12-BLK", supplier: "Technoform Gulf", buy: 4.2, sell: 6.5, stock: 2400, used: 780, reorderLevel: 400 },
  { name: "Warm-edge spacer bar", category: "Spacer bar", uom: "Linear metre", specification: "16mm black", partNumber: "SPB-16-BLK", supplier: "Technoform Gulf", buy: 4.8, sell: 7.2, stock: 1800, used: 640, reorderLevel: 400 },
  { name: "Aluminium spacer bar", category: "Spacer bar", uom: "Linear metre", specification: "12mm mill", partNumber: "SPA-12-ML", supplier: "Al Gurg Glass", buy: 2.9, sell: 4.6, stock: 3200, used: 1150, reorderLevel: 500 },
  { name: "Molecular sieve desiccant", category: "Desiccant", uom: "Kilogram", specification: "3A beads", partNumber: "DES-3A", supplier: "Grace Davison", buy: 12.5, sell: 19, stock: 240, used: 96, reorderLevel: 60 },
  { name: "Polyisobutylene (PIB) butyl", category: "Primary seal", uom: "Kilogram", specification: "Hot-melt primary seal", partNumber: "PIB-STD", supplier: "Fenzi Gulf", buy: 22, sell: 33, stock: 180, used: 74, reorderLevel: 45 },
  { name: "Two-part silicone sealant", category: "Secondary seal", uom: "Kilogram", specification: "Structural grade", partNumber: "SIL-2K", supplier: "Dow Chemical", buy: 28.5, sell: 42, stock: 320, used: 128, reorderLevel: 80 },
  { name: "Polysulphide sealant", category: "Secondary seal", uom: "Kilogram", specification: "Secondary seal", partNumber: "PSF-STD", supplier: "Fenzi Gulf", buy: 19.75, sell: 30, stock: 150, used: 122, reorderLevel: 40 },
  { name: "Argon gas", category: "Gas", uom: "Cubic metre", specification: "99.9% purity", partNumber: "GAS-AR-999", supplier: "Air Liquide", buy: 8.4, sell: 13, stock: 500, used: 210, reorderLevel: 120 },
  { name: "Spacer corner key", category: "Hardware", uom: "Piece", specification: "12mm straight", partNumber: "CK-12", supplier: "Technoform Gulf", buy: 0.35, sell: 0.7, stock: 5000, used: 1800, reorderLevel: 1000 },
  { name: "Spacer bar connector", category: "Hardware", uom: "Piece", specification: "16mm inline joiner", partNumber: "CN-16", supplier: "Technoform Gulf", buy: 0.48, sell: 0.95, stock: 3000, used: 1120, reorderLevel: 800 },
];

/* ══════════════════════════════════════════════════════════════════════════
 * The check that keeps this file honest
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * The figures published on the site, exactly as sourced above.
 *
 * `derivedFigures()` recomputes them from the transcribed data. If a size, a
 * quantity, a rate or the rounding rule ever changes, the two stop agreeing and
 * the build fails — which is the only way a page like this stays true.
 */
const published = {
  lines: 22,
  pieces: 83,
  squareMetres: 160.79,
  subtotal: 33_106.85,
  vat: 1_655.34,
  grandTotal: 34_762.19,
  deliveredPieces: 45,
  balancePieces: 38,
  deliveredSubtotal: 16_832.8,
  deliveredVat: 841.64,
  deliveredTotal: 17_674.44,
  batchOneWeightKg: 1_891.6,
};

function derivedFigures() {
  return {
    lines: quotationTotals.lines,
    pieces: quotationTotals.pieces,
    squareMetres: Math.round(quotationTotals.squareMetres * 100) / 100,
    subtotal: quotationTotals.subtotal,
    vat: quotationTotals.vat,
    grandTotal: quotationTotals.grandTotal,
    deliveredPieces: deliveredTotals.pieces,
    balancePieces,
    deliveredSubtotal: deliveredTotals.subtotal,
    deliveredVat: deliveredTotals.vat,
    deliveredTotal: deliveredTotals.total,
    batchOneWeightKg,
  };
}

/** Round-trip check. Throws at build time, never in a visitor's browser. */
export function assertMatchesSeed(): void {
  const derived = derivedFigures();
  const mismatched = Object.entries(published).filter(
    ([key, value]) => derived[key as keyof typeof derived] !== value,
  );
  if (mismatched.length > 0) {
    throw new Error(
      `The worked example no longer matches the seeded workflow (${SEED_SOURCE}): ` +
        mismatched
          .map(([key, value]) => `${key} is ${derived[key as keyof typeof derived]}, expected ${value}`)
          .join("; "),
    );
  }
}

assertMatchesSeed();

export const reorderAlerts = seedConsumables.filter((item) => item.stock - item.used < item.reorderLevel);
