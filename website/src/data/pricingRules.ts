/**
 * The two pricing rules the shop quotes glass by.
 *
 * Both are read out of the application — the second one was not obvious, and
 * an earlier pass over this codebase concluded it did not exist. It does; these
 * are the lines of it:
 *
 *   1. MINIMUM BILLABLE AREA — 0.5 m² per panel
 *      `Utilities/SqmCalculator.cs`
 *        /// Minimum billable SQM per panel (UAE industry standard).
 *        public const decimal MinimumSqmPerPanel = 0.5m;
 *        public static decimal CalculatePanel(int widthMm, int heightMm)
 *      Every panel priced in the specification grid goes through it
 *      (`GlassItemRow.RecalculateAll`: `Sqm1 = SqmCalculator.CalculatePanel(W1, H1)`),
 *      so the SQM column never prints less than 0.50 for a real panel, and a
 *      degenerate panel (0 mm in either direction) is 0 rather than the floor.
 *      `PnLMeasurement.MeasureGroup` reuses the same call — its comment says so
 *      outright: "Step 5 charges every panel with the 0.5 m² minimum floor, so
 *      Step 9 must use the same billable SQM source of truth". The rate does not
 *      change: the specification's own rate is charged on the floored area.
 *
 *   2. LARGE-PANEL RATE SWITCH — at 4 m² and above
 *      `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part2.cs`
 *        var targetRate = row.Sqm1 >= 4m ? SurchargeRate : BaseRate;
 *      and the same three lines in `SpecGroup.Part3.cs` (`UpdateAllRowRates`)
 *      and in the paste path. A row whose billable panel area reaches 4 m² is
 *      re-priced at `SurchargeRate`; every other row in the same specification
 *      stays at `BaseRate`. It is not a charge added to the invoice — it is the
 *      rate the row carries:
 *
 *      `SpecGroup.Part1.cs`
 *        public decimal SurchargeRate =>
 *            MoneyRounding.Round(_baseRate * (1m + (_surchargePercent / 100m)));
 *
 *      `SURCHARGE %` (`Dg.SurchargePct`) is a field on the specification block,
 *      beside GLASS TYPE, SPECIFICATION and BASE — a per-specification input,
 *      not a constant, and it defaults to 0. The shop's standard for oversize
 *      work is 20%: an oversize lite breaks more often, needs two people and a
 *      crane, and cuts badly out of a standard sheet. This module holds that
 *      standard so the site, the worked example and the previews quote one
 *      number, and the preview lets the visitor type a different one.
 *
 * The switch fires when the row's `Sqm1` changes — that is, while dimensions
 * are being typed — and on a change to BASE or SURCHARGE %. A row whose rate a
 * person has typed over is left alone (`GlassItemRow.SuppressRateAutoAdjust`),
 * which is why a saved quotation's own rates survive a reload.
 *
 * Everything that prices a panel on this site goes through this file, so the
 * SQM column, the value column, the group total, the profit-and-loss revenue
 * and the banner cannot drift apart.
 */

import { text } from "@/i18n/core";

/** `SqmCalculator.MinimumSqmPerPanel` — billable floor, in square metres. */
export const MIN_PANEL_SQM = 0.5;

/** The rate switches at this billable panel area, in square metres. */
export const SURCHARGE_THRESHOLD_SQM = 4;

/** The shop's standard uplift for a panel at or above the threshold. */
export const STANDARD_SURCHARGE_PERCENT = 20;

/** A panel's physical area: width × height, in square metres. */
export function physicalPanelSqm(widthMm: number, heightMm: number): number {
  if (widthMm <= 0 || heightMm <= 0) return 0;
  return (widthMm / 1000) * (heightMm / 1000);
}

/**
 * A panel's billable area — physical area, floored at `MIN_PANEL_SQM`.
 *
 * `SqmCalculator.CalculatePanel`, including its order of operations: an
 * invalid panel is 0, not the floor. A zero-quantity row must not bill half a
 * square metre. The application rounds this to three places for display; the
 * un-rounded value is kept here because the money is computed from it.
 */
export function billablePanelSqm(widthMm: number, heightMm: number): number {
  const physical = physicalPanelSqm(widthMm, heightMm);
  if (physical === 0) return 0;
  return Math.max(physical, MIN_PANEL_SQM);
}

/** True when the floor, rather than the panel, decided the billable area. */
export function isFloored(widthMm: number, heightMm: number): boolean {
  const physical = physicalPanelSqm(widthMm, heightMm);
  return physical > 0 && physical < MIN_PANEL_SQM;
}

/** True when the row prices at the surcharge rate — `Sqm1 >= 4`. */
export function surchargeApplies(billableSqm: number): boolean {
  return billableSqm >= SURCHARGE_THRESHOLD_SQM;
}

/** A rate rounded the way the application rounds money. */
export function moneyRate(value: number): number {
  return Math.round(value * 100) / 100;
}

/** `SpecGroup.SurchargeRate` — the base rate lifted by the specification's %. */
export function surchargedRate(baseRate: number, surchargePercent: number): number {
  return moneyRate(baseRate * (1 + surchargePercent / 100));
}

/**
 * The rate a row carries: `Sqm1 >= 4 ? SurchargeRate : BaseRate`.
 *
 * The comparison is on the billable area, so it is the figure in the SQM
 * column that decides — not the width × height behind it.
 */
export function rowRate(baseRate: number, surchargePercent: number, billableSqm: number): number {
  if (surchargeApplies(billableSqm)) return surchargedRate(baseRate, surchargePercent);
  return moneyRate(baseRate);
}

/**
 * The two rules as copy, for the surfaces that explain them.
 *
 * Kept here rather than typed into each page so the wording and the numbers
 * cannot disagree with the arithmetic above.
 */
export const pricingRules = [
  {
    id: "minimum-area",
    label: "Minimum area",
    value: `${MIN_PANEL_SQM} m² per panel`,
    headline: `Every panel bills at least ${MIN_PANEL_SQM} m²`,
    body: "A 400 × 300 mm vent light is 0.12 m² of glass and half a square metre of work: it is cut, arrised, toughened, packed and handled as one piece whatever its size. The SQM column therefore never prints less than 0.50 for a real panel. The specification's own rate is charged on the floored area — the minimum is on the area, not on the rate — and the same floor is used again in Step 9, so the profit-and-loss glass cost reconciles to the price instead of quietly disagreeing with it.",
    evidence: "Utilities/SqmCalculator.cs · MinimumSqmPerPanel = 0.5m · GlassItemRow.RecalculateAll → Sqm1",
  },
  {
    id: "large-panel-rate",
    label: "Large-panel rate",
    value: text`+${STANDARD_SURCHARGE_PERCENT}% at ${SURCHARGE_THRESHOLD_SQM} m² and above`,
    headline: text`A panel that reaches ${SURCHARGE_THRESHOLD_SQM} m² prices at base + ${STANDARD_SURCHARGE_PERCENT}%`,
    body: text`Step 5 re-prices a row the moment its billable area reaches ${SURCHARGE_THRESHOLD_SQM} m²: \`Sqm1 >= ${SURCHARGE_THRESHOLD_SQM} ? SurchargeRate : BaseRate\`. Oversize lites are the ones that break, that need two people and a crane, and that cut badly from a standard sheet, so the specification carries a SURCHARGE % beside its base rate and ${
      STANDARD_SURCHARGE_PERCENT
    }% is the shop's standard for it. Only the rows that reach the size change — the rest of the specification stays at base — and if somebody types a rate over by hand the application leaves it alone.`,
    evidence: "SpecGroup.Part2.cs / Part3.cs · row.Sqm1 >= 4m ? SurchargeRate : BaseRate · Dg.SurchargePct",
  },
] as const;

/* ══════════════════════════════════════════════════════════════════════════
 * Step 4 — the glass-type panels
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * The four build families the application supports, and the two it does not.
 *
 * `SpecGroup.NormalizeSupportedGlassType` accepts SGU, DGU, LAM and DGU+LAM —
 * "only the four Step 4 build families come through; no triple glazing, and an
 * out-of-family name degrades with a warning" (`Views/Production/README.md`).
 * The warning is real behaviour, so the preview shows it rather than hiding the
 * possibility: the scope is a limit worth being honest about.
 */
/**
 * A dropdown value counts as "not chosen" when it is the None option.
 *
 * The application's own test is `!v.Equals("None", OrdinalIgnoreCase)`, but the
 * polish list's first entry is the longer "None (Standard)", so a straight
 * comparison against "None" lets it through and the generated name reads
 * "6mm Clear FT None (Standard)". Starting-with is what the options actually
 * need.
 */
function chosen(value: string | undefined): value is string {
  return Boolean(value) && !value!.toLowerCase().startsWith("none");
}

export const glassBuilds = [
  {
    code: "SGU",
    label: "SGU",
    long: "Single glazed unit",
    panes: "One pane",
    /** What the step-4 panel asks for, in the order it asks. */
    fields: ["Thickness", "Colour", "Treatment", "Sheet size", "Polish", "Panel extra"],
    name: (v: Record<string, string>) =>
      [`${v.thickness ?? "6mm"} ${v.colour ?? "Clear"} ${v.treatment ?? "FT"}`.trim(), chosen(v.polish) ? v.polish : "", chosen(v.panelExtra) ? v.panelExtra : ""]
        .filter(Boolean)
        .join(" "),
  },
  {
    code: "DGU",
    label: "DGU",
    long: "Double glazed unit",
    panes: "Outer + inner, sealed across a spacer",
    fields: ["Outer", "ASP spacer", "Spacer extra", "Inner", "Panel extra"],
    /**
     * `DguParametersViewModel.GeneratedSpecName`, verbatim:
     *   "{OuterThickness} {OuterGlassColor} {OuterTreatment} Glass
     *    + {SpacerWidth} {SpacerExtra}
     *    + {InnerThickness} {InnerGlassColor} {InnerTreatment} Glass {PanelExtra}"
     */
    name: (v: Record<string, string>) =>
      `${v.thickness ?? "6mm"} ${v.colour ?? "Clear"} ${v.treatment ?? "FT"} Glass + ${v.spacerWidth ?? "12mm ASP"}${
        chosen(v.spacerExtra) ? ` ${v.spacerExtra}` : ""
      } + ${v.innerThickness ?? "6mm"} ${v.innerColour ?? "Clear"} ${v.innerTreatment ?? "FT"} Glass${
        chosen(v.panelExtra) ? ` ${v.panelExtra}` : ""
      }`,
  },
  {
    code: "LAM",
    label: "LAM",
    long: "Laminated",
    panes: "Two panes bonded across a PVB interlayer",
    fields: ["Outer", "PVB interlayer", "Inner", "Lamination polish"],
    /** `LamParametersViewModel.GeneratedSpecName`: outer + PVB + inner. */
    name: (v: Record<string, string>) =>
      `${v.thickness ?? "6mm"} ${v.colour ?? "Clear"} ${v.treatment ?? "FT"} Glass + ${v.pvb ?? "1.52mm Clear PVB"} + ${
        v.innerThickness ?? "6mm"
      } ${v.innerColour ?? "Clear"} ${v.innerTreatment ?? "FT"} Glass${
        chosen(v.polish) ? ` With ${v.polish}` : ""
      }`,
  },
  {
    code: "DGU+LAM",
    label: "DGU + LAM",
    long: "Double glazed with a laminated inner",
    panes: "Outer pane, spacer, then a laminated inner pair",
    fields: ["Outer", "ASP spacer", "Laminated pair", "Interlayer", "Panel extra"],
    /** `DguLamParametersViewModel.GeneratedSpecName`. */
    name: (v: Record<string, string>) =>
      `${v.thickness ?? "6mm"} ${v.colour ?? "Clear"} ${v.treatment ?? "FT"} Glass + ${v.spacerWidth ?? "12mm ASP"}${
        chosen(v.spacerExtra) ? ` ${v.spacerExtra}` : ""
      } + ${v.lamThickness ?? "6mm"} ${v.lamColour ?? "Clear"} ${v.lamTreatment ?? "FT"} Glass + ${
        v.interlayer ?? "1.52mm Clear PVB"
      } + ${v.innerThickness ?? "6mm"} ${v.innerColour ?? "Clear"} ${v.innerTreatment ?? "FT"} Glass`,
  },
] as const;

export type GlassBuildCode = (typeof glassBuilds)[number]["code"];

/** The build families this site, like the application, refuses. */
export const unsupportedBuilds = ["Triple glazing (TGU)", "Quadruple glazing", "Vacuum glazing"] as const;

/**
 * The step-4 dropdown options, read from the parameter view-models.
 *
 * `SguParametersViewModel`, `DguParametersViewModel`, `LamParametersViewModel`
 * and `DguLamParametersViewModel` each declare their own — the thicknesses,
 * treatments and spacer widths below are those lists, not a plausible subset of
 * them. Colour is absent on purpose: the application fills colour from Sheet
 * Store inventory and the built-in defaults were removed by request, so the
 * colours here are the ones the seeded sheet store actually holds.
 */
export const buildOptions = {
  thickness: ["3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "15mm", "19mm"],
  colour: ["Clear", "Bronze", "Grey", "Low-E", "Frosted"],
  treatment: ["FT", "Annealed", "Heat Strengthened", "Heat Soaked"],
  spacerWidth: [
    "6mm ASP",
    "8mm ASP",
    "10mm ASP",
    "12mm ASP",
    "14mm ASP",
    "16mm ASP",
    "18mm ASP",
    "20mm ASP",
    "22mm ASP",
    "24mm ASP",
    "26mm ASP",
    "28mm ASP",
    "30mm ASP",
    "32mm ASP",
  ],
  spacerMaterial: ["Normal Mill Aluminum", "Black Aluminum", "Warm Edge (Super Spacer)", "Black Warm Edge", "Stainless Steel", "TGI Spacer"],
  spacerExtra: ["None", "With U-Insert", "With U-Insert As Per DWG"],
  panelExtra: [
    "None",
    "With Overlap",
    "With Overlap As Per DWG",
    "With Overlap (SQM)",
    "With Fanhole",
    "With Cutout",
    "With Holes",
    "With Argon Gas",
    "With Shutter Bonding",
  ],
  polish: ["None (Standard)", "Polished", "Polished Matched LAM", "Edge Grinding"],
} as const;

/**
 * What the Step 4 selection is worth per square metre, over the sheet store.
 *
 * The estimator does not invent a price: it takes the outer sheet's own rate out
 * of Sheet Store and adds the inner pane and the spacer on top, which is why a
 * DGU costs more than the same outer pane on its own and why switching the
 * build visibly moves the base rate the specification opens on. The three
 * figures are the shop's adders for the inner pane, the laminated pair and the
 * ASP, over the outer sheet rate.
 */
export const buildRateAdders = {
  /** Extra per m² over the outer sheet rate for the second pane. */
  innerPane: 0.62,
  /** Extra for the laminated pair's interlayer and second bond. */
  lamination: 0.38,
  /** The ASP spacer bar and its corner keys, per m² of unit. */
  spacer: 0.18,
} as const;

/** The outer sheet rate each build starts from, per the seeded sheet store. */
export const outerSheetRates: Record<string, number> = {
  "3mm": 70,
  "4mm": 82,
  "5mm": 88,
  "6mm": 95,
  "8mm": 120,
  "10mm": 140,
  "12mm": 165,
  "15mm": 205,
  "19mm": 265,
};

/**
 * The selling rate a Step 4 selection produces, per square metre.
 *
 * Outer sheet at its purchase rate, then a build-up by family: a second pane for
 * a DGU, an interlayer for a laminate, both for a DGU + LAM, and the spacer for
 * anything sealed. It is the same shape as the estimator's own total — a
 * material cost with the shop's margin on it — and it is deliberately simple,
 * because it exists to show that *changing the glass type changes the price*,
 * which is the thing that was missing.
 */
export function step4RatePerSqm(code: GlassBuildCode, thickness: string, colour: string, withSpacer: boolean): number {
  const base = outerSheetRates[thickness] ?? 95;
  /* Tinted and Low-E sheets cost more than clear, straight out of the store. */
  const colourFactor =
    colour === "Low-E" ? 1.9 : colour === "Bronze" || colour === "Grey" ? 1.16 : colour === "Frosted" ? 1.37 : 1;
  const sheet = base * colourFactor;

  switch (code) {
    case "SGU":
      return moneyRate(sheet * 1.58);
    case "DGU":
      return moneyRate(sheet * (1 + buildRateAdders.innerPane) * (withSpacer ? 1 + buildRateAdders.spacer : 1) * 1.58);
    case "LAM":
      return moneyRate(sheet * (1 + buildRateAdders.lamination) * 1.58);
    case "DGU+LAM":
      return moneyRate(sheet * (1 + buildRateAdders.innerPane + buildRateAdders.lamination) * (withSpacer ? 1 + buildRateAdders.spacer : 1) * 1.58);
    default:
      return moneyRate(sheet * 1.58);
  }
}
