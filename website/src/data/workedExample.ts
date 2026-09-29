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
  lines: SeedLine[];
};

export const seedSpecGroups: SeedSpecGroup[] = [
  {
    number: 1,
    name: "Clear Tempered — Main Facade",
    glassType: "SGU",
    baseRate: 150,
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
 * The arithmetic, performed rather than transcribed
 * ════════════════════════════════════════════════════════════════════════ */

/** Area of one piece in m² — millimetres to metres, multiplied. */
export const pieceArea = (line: SeedLine) => (line.widthMm / 1000) * (line.heightMm / 1000);

/** Area of the whole line in m². */
export const lineArea = (line: SeedLine) => pieceArea(line) * line.qty;

/** Line value: area × quantity × rate, rounded the way the application rounds money. */
export const lineValue = (line: SeedLine) => money(pieceArea(line) * line.qty * line.rate);

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
