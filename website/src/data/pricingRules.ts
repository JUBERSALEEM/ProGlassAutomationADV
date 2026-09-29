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
    value: `+${STANDARD_SURCHARGE_PERCENT}% at ${SURCHARGE_THRESHOLD_SQM} m² and above`,
    headline: `A panel that reaches ${SURCHARGE_THRESHOLD_SQM} m² prices at base + ${STANDARD_SURCHARGE_PERCENT}%`,
    body: `Step 5 re-prices a row the moment its billable area reaches ${SURCHARGE_THRESHOLD_SQM} m²: \`Sqm1 >= ${SURCHARGE_THRESHOLD_SQM} ? SurchargeRate : BaseRate\`. Oversize lites are the ones that break, that need two people and a crane, and that cut badly from a standard sheet, so the specification carries a SURCHARGE % beside its base rate and ${
      STANDARD_SURCHARGE_PERCENT
    }% is the shop's standard for it. Only the rows that reach the size change — the rest of the specification stays at base — and if somebody types a rate over by hand the application leaves it alone.`,
    evidence: "SpecGroup.Part2.cs / Part3.cs · row.Sqm1 >= 4m ? SurchargeRate : BaseRate · Dg.SurchargePct",
  },
] as const;
