"use client";

import { SURCHARGE_THRESHOLD_SQM } from "@/data/pricingRules";
import { formatMoney } from "@/lib/proforma-draft";
import { formatRate, type RatePlan, type RateSetting } from "@/lib/proforma-rates";

const INPUT =
  "h-8 w-24 rounded-lg border border-line-strong bg-ink-950 px-2 text-right text-xs font-mono text-text-primary placeholder:text-text-faint";

/**
 * Step 5's specification rate entry.
 *
 * The desktop prices a specification from a BASE rate and a SURCHARGE %, and each
 * row's rate follows from its billable panel area. The website stores a rate per
 * line, so this bar turns those two figures into the rate column and lets the
 * operator apply them. Both figures are stored with the revision they were saved
 * with and loaded back from it, so a reopened PI shows the rate it was priced with.
 * The money that is charged is still the rate each line carries, which
 * `save_inquiry_proforma_v2` prices.
 */
export function SpecificationRateBar({
  groupNo,
  setting,
  plan,
  rowCount,
  currency,
  onChange,
  onApply,
}: {
  groupNo: number;
  setting: RateSetting;
  plan: RatePlan | null;
  rowCount: number;
  currency: string;
  onChange: (patch: Partial<RateSetting>) => void;
  onApply: () => void;
}) {
  const ready = Boolean(plan) && rowCount > 0;

  return (
    <div className="border-b border-line-strong/60 bg-ink-950/50 px-3 py-2">
      <div className="flex flex-wrap items-end gap-3">
        <label className="text-3xs font-semibold uppercase tracking-wider text-text-faint">
          Base rate ({currency}/m²)
          <input
            value={setting.base}
            onChange={(event) => onChange({ base: event.target.value })}
            inputMode="decimal"
            placeholder="0.00"
            aria-label={`Base rate for specification ${groupNo}`}
            className={`mt-1 block ${INPUT}`}
          />
        </label>
        <label className="text-3xs font-semibold uppercase tracking-wider text-text-faint">
          Surcharge %
          <input
            value={setting.percent}
            onChange={(event) => onChange({ percent: event.target.value })}
            inputMode="decimal"
            placeholder="0"
            aria-label={`Surcharge percent for specification ${groupNo}`}
            className={`mt-1 block ${INPUT}`}
          />
        </label>

        <p className="text-3xs leading-relaxed text-text-muted">
          {plan ? (
            <>
              Oversize rate <span className="font-mono text-text-secondary">{formatMoney(plan.surchargeRate, currency)}</span> at {SURCHARGE_THRESHOLD_SQM} m²
              and above · <span className="font-mono text-text-secondary">{plan.surchargedCount}</span> of {rowCount} row
              {rowCount === 1 ? "" : "s"} reach it
            </>
          ) : (
            "Enter a base rate to price this specification"
          )}
        </p>

        <button
          type="button"
          onClick={onApply}
          disabled={!ready}
          title={ready ? `Set every row of specification ${groupNo} to its base or oversize rate` : "Enter a base rate and add a row first"}
          className="rounded-xl border border-brand-400/70 bg-brand-500/10 px-3 py-2 text-3xs font-semibold text-brand-200 hover:bg-brand-500/20 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {plan && rowCount > 0 && plan.unchangedCount === plan.rows.length
            ? "Rates already applied"
            : `Apply to specification ${groupNo}`}
        </button>
      </div>
      <p className="mt-1.5 text-3xs text-text-faint">
        The base rate and percentage are stored with the revision when you save, and come back when you reopen it. Each
        line also saves the rate shown in its RATE cell
        {plan ? ` (${formatRate(plan.baseRate)} at base, ${formatRate(plan.surchargeRate)} oversize)` : ""}, and a rate typed by
        hand is kept as typed.
      </p>
    </div>
  );
}
