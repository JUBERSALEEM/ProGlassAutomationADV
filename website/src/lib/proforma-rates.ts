import { rowRate, surchargedRate } from "@/data/pricingRules";
import type { DraftLine, PricedLine } from "@/lib/proforma-draft";
import type { ProformaGroupSetting } from "@/types/proforma";

/**
 * Step 5 rate handling, as the desktop does it.
 *
 * The desktop prices a specification, not a line: a group carries a BASE rate and
 * a SURCHARGE %, and each row's rate is `Sqm1 >= 4 ? SurchargeRate : BaseRate`
 * (`SpecGroup.Part2.cs`). The website's saved record is still one rate per line,
 * which is what `save_inquiry_proforma_v1` accepts and prices from, so this module
 * turns the specification's figures into the rate each row should carry and hands
 * back the changed rows. The database remains the only authority for the money
 * that is stored; this is the authoring step that produces the rate it receives.
 *
 * The base rate and the percentage are stored per revision in
 * `inquiry_proforma_groups`, so reopening a PI shows the figures it was saved with
 * and an older revision keeps its own. They are not a second price: the money that
 * is charged is still the rate each line carries, which the write function prices.
 */

export type RateSetting = { base: string; percent: string };

export const EMPTY_RATE_SETTING: RateSetting = { base: "", percent: "" };

/** A rate as the operator typed it, or null when it cannot price anything. */
export function parseRateInput(value: string): number | null {
  const trimmed = String(value ?? "").trim().replace(/,/g, "");
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  if (!Number.isFinite(parsed) || parsed < 0) return null;
  // The write function refuses a rate above 1,000,000, so do not offer one.
  if (parsed > 1_000_000) return null;
  return parsed;
}

/**
 * A percentage as typed, or null when it is negative or unreadable. A trailing
 * percent sign and thousands commas are accepted, because operators type "20%"
 * and the database stores the number.
 */
export function parsePercentInput(value: string): number | null {
  const trimmed = String(value ?? "").trim().replace(/%/g, "").replace(/,/g, "").trim();
  if (!trimmed) return 0;
  const parsed = Number(trimmed);
  if (!Number.isFinite(parsed) || parsed < 0 || parsed > 1000) return null;
  return parsed;
}

/** Two decimals, the precision the rate column stores. */
export function formatRate(rate: number): string {
  return (Math.round(rate * 100) / 100).toFixed(2);
}

export type PlannedRow = {
  key: string;
  sqm: number;
  rate: number;
  surcharged: boolean;
};

export type RatePlan = {
  groupNo: number;
  baseRate: number;
  percent: number;
  /** `BaseRate * (1 + percent/100)`, rounded the way the desktop rounds it. */
  surchargeRate: number;
  rows: PlannedRow[];
  /** Rows whose billable area reaches the switch, so they take the surcharge rate. */
  surchargedCount: number;
  /** Rows that already carry the rate this plan would give them. */
  unchangedCount: number;
};

/**
 * What a specification's base rate and percentage would do to its rows.
 *
 * Returns null when the specification cannot be priced, so a button can stay
 * disabled rather than applying zeroes. A row whose panel area is zero (an
 * incomplete row) is left at its current rate instead of being given a rate for
 * an area that does not exist yet.
 */
export function planRates(
  priced: PricedLine[],
  groupNo: number,
  baseInput: string,
  percentInput: string,
): RatePlan | null {
  const baseRate = parseRateInput(baseInput);
  const percent = parsePercentInput(percentInput);
  if (baseRate === null || percent === null) return null;

  const rows: PlannedRow[] = priced
    .filter((line) => line.groupNo === groupNo)
    .map((line) => {
      const surcharged = line.sqm >= 4;
      const rate = line.sqm > 0 ? rowRate(baseRate, percent, line.sqm) : line.rate;
      return { key: line.key, sqm: line.sqm, rate, surcharged };
    });

  return {
    groupNo,
    baseRate,
    percent,
    surchargeRate: surchargedRate(baseRate, percent),
    rows,
    surchargedCount: rows.filter((row) => row.surcharged && row.sqm > 0).length,
    unchangedCount: rows.filter((row) => Math.abs(row.rate - (priced.find((line) => line.key === row.key)?.rate ?? 0)) < 0.005).length,
  };
}

/** The draft with the planned rates written into the rows the plan covers. */
export function applyRatePlan(lines: DraftLine[], plan: RatePlan): DraftLine[] {
  const byKey = new Map(plan.rows.map((row) => [row.key, row]));
  return lines.map((line) => {
    const planned = byKey.get(line.key);
    if (!planned) return line;
    return { ...line, rate: formatRate(planned.rate) };
  });
}

/**
 * How many rows of a specification carry a rate other than the expected one.
 *
 * After a save and reload the base rate and percentage are gone, so the honest
 * question is not "what was the base" but "do these rows agree with each other".
 * This counts the rows that would change if the current figures were applied.
 */
export function rowsThatWouldChange(plan: RatePlan): number {
  return plan.rows.length - plan.unchangedCount;
}

/**
 * The stored settings as the rate bar holds them.
 *
 * A specification with no stored row keeps an empty setting, so the operator sees
 * an empty box rather than a zero that looks like a saved price.
 */
export function settingsFromServer(groups: ProformaGroupSetting[]): Record<number, RateSetting> {
  const settings: Record<number, RateSetting> = {};
  for (const group of groups) {
    settings[group.group_no] = {
      base: group.base_rate === null || group.base_rate === undefined ? "" : String(group.base_rate),
      percent: group.surcharge_percent === null || group.surcharge_percent === undefined ? "" : String(group.surcharge_percent),
    };
  }
  return settings;
}

/**
 * The rate bar's state as the write function expects it.
 *
 * Only a specification with a usable base rate is sent. A blank box is not a zero
 * price and is not sent as one; the database keeps whatever that revision had.
 */
export function settingsPayload(settings: Record<number, RateSetting>): Array<{ group_no: number; base_rate: number; surcharge_percent: number }> {
  const payload: Array<{ group_no: number; base_rate: number; surcharge_percent: number }> = [];
  for (const [key, setting] of Object.entries(settings)) {
    const groupNo = Number(key);
    const base = parseRateInput(setting.base);
    const percent = parsePercentInput(setting.percent);
    if (!Number.isInteger(groupNo) || groupNo < 1 || groupNo > 50) continue;
    if (base === null || percent === null) continue;
    payload.push({ group_no: groupNo, base_rate: base, surcharge_percent: percent });
  }
  return payload.sort((left, right) => left.group_no - right.group_no);
}
