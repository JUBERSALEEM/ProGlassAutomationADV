"use client";

import { useMemo, useState } from "react";
import { SampleTag } from "@/components/product/previewAtoms";
import type { SpecBlockInput } from "@/components/product/SpecificationSurface";
import { money, sheetForRef } from "@/data/workedExample";
import { billablePanelSqm, physicalPanelSqm, SURCHARGE_THRESHOLD_SQM } from "@/data/pricingRules";
import { optimizerLimits, sheetSizes } from "@/data/liveDemos";
import { packGuillotine } from "@/lib/guillotinePack";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/provider";

/**
 * Step 9 — Profit & Loss.
 *
 * The step the site did not have. It was named in the wizard rail and its
 * numbers appeared inside other views; the analysis itself — the four tabs, the
 * decision cards, the profit-and-loss arithmetic — was missing, which is the
 * part a shop owner actually opens this screen for.
 *
 * What it does, in the application's own terms:
 *
 *   REQUIRED TARGET PRICE   base price × (1 + the expected profit margin).
 *                           `Step9PricingDefaults.FallbackExpectedProfitMarginPercent`
 *                           is 15% ("15% keeps the approved ×1.15 formula
 *                           byte-identical on fresh installs").
 *   EXPECTED PROFIT MARGIN  the uplift applied to the cost basis; a field, not
 *                           a constant, which is why it is editable here.
 *   PRICE BALANCE           the given price against the required target, and
 *                           the verdict — is the given price above target?
 *   NET REVENUE (ex-VAT)    what the specification totals, VAT excluded.
 *   GLASS COST ONLY         the same billable square metres costed at the
 *                           Sheet Store *purchase* price for each glass.
 *   GLASS MARGIN            revenue less that cost — "reference", because the
 *                           app's own margin is measured against the target.
 *
 * The four tabs are `1 · OVERVIEW` (the decision), `2 · SERVICES` (the charge
 * rows, and the application's own empty state where a charge is not costed
 * yet), `3 · SHEETS` (a real packing run: bought area, buy price, waste and the
 * Step 6 cross-check) and `4 · SPECS` ("MAIN SPEC CHECK — ONE ROW PER STEP 5
 * SPEC").
 *
 * It reads Step 5's rows, not a copy of them. Edit a size on the specification
 * and the revenue, the cost, the margin and the verdict all move with it —
 * which is what the real screen does, because it is the same engine.
 */

export type PnLInput = {
  /** The question the step answers, verbatim from the view's own header. */
  question: string;
  /** `ExpectedProfitMarginPercent` — the app's fallback is 15%. */
  expectedProfitMarginPercent: number;
  /** Where the given price came from: `GIVEN PRICE SOURCE`. */
  givenPriceSource: string;
  /** `Cost basis` — selling reconciles with the quote, purchase with the invoices. */
  costBasis: string;
  /**
   * The charge rows Step 4/Step 6 raise, as the estimator lists them.
   *
   * `costed` is false for a charge whose service cost is not in the seeded
   * data: the app then prints its own "No costed lines yet" line rather than a
   * profit figure, and so does this.
   */
  services: { key: string; label: string; basis: string; rate: number; costed: boolean; dguOnly?: boolean }[];
  sheet: { label: string; w: number; h: number };
};

const tabs = ["1 · OVERVIEW", "2 · SERVICES", "3 · SHEETS", "4 · SPECS"] as const;

export function ProfitLossSurface({
  pnl,
  blocks,
  compact = false,
}: {
  pnl: PnLInput;
  blocks: SpecBlockInput[];
  compact?: boolean;
}) {
  const t = useT();
  const [tab, setTab] = useState<(typeof tabs)[number]>("1 · OVERVIEW");
  const [target, setTarget] = useState(pnl.expectedProfitMarginPercent);
  /**
   * `RECALCULATE`.
   *
   * On the desktop screen this button re-runs the analysis over Step 5's rows.
   * Here the analysis *is* live — it recomputes on every keystroke on the
   * specification — so the button confirms what it found rather than asking for
   * it, and says how many rows it read. Pressing it is never a no-op: the line
   * under it reports the count it just measured.
   */
  const [recalc, setRecalc] = useState<string | null>(null);

  /* ── the measurement, over the rows Step 5 is showing ──────────────── */

  const measured = useMemo(
    () =>
      blocks.map((block) => {
        let area = 0;
        let revenue = 0;
        let cost = 0;
        let baseValue = 0;
        let surcharged = 0;
        let floored = 0;

        for (const row of block.rows) {
          const billable = billablePanelSqm(row.w1, row.h1);
          const totalSqm = billable * row.qty;
          area += totalSqm;
          revenue += money(totalSqm * row.rate);
          cost += (sheetForRef(row.ref)?.purchase ?? 0) * totalSqm;
          baseValue += money(totalSqm * block.baseRate);
          if (billable >= SURCHARGE_THRESHOLD_SQM) surcharged += 1;
          if (physicalPanelSqm(row.w1, row.h1) < 0.5) floored += 1;
        }

        return {
          number: block.number,
          name: block.name,
          glassType: block.glassType,
          baseRate: block.baseRate,
          surchargePercent: block.surchargePercent,
          lines: block.rows.length,
          area,
          revenue,
          cost,
          baseValue,
          surcharged,
          floored,
          required: money(area * block.baseRate * (1 + target / 100)),
        };
      }),
    [blocks, target],
  );

  /** A double-glazed build-up raises the spacer and gas charges; this job may not have one. */
  const hasDgu = blocks.some((block) => block.glassType === "DGU");

  const totals = useMemo(() => {
    const sum = measured.reduce(
      (acc, spec) => ({
        area: acc.area + spec.area,
        revenue: acc.revenue + spec.revenue,
        cost: acc.cost + spec.cost,
        baseValue: acc.baseValue + spec.baseValue,
        required: acc.required + spec.required,
      }),
      { area: 0, revenue: 0, cost: 0, baseValue: 0, required: 0 },
    );
    const difference = money(sum.revenue - sum.required);
    return {
      ...sum,
      /** The area-weighted base price per m², the app's "Base price (no uplift)". */
      basePricePerSqm: sum.area > 0 ? sum.baseValue / sum.area : 0,
      requiredPerSqm: sum.area > 0 ? sum.required / sum.area : 0,
      givenPerSqm: sum.area > 0 ? sum.revenue / sum.area : 0,
      difference,
      aboveTarget: difference >= 0,
      glassMargin: money(sum.revenue - sum.cost),
      glassMarginPercent: sum.revenue > 0 ? ((sum.revenue - sum.cost) / sum.revenue) * 100 : 0,
      marginVsBase: sum.revenue > 0 ? ((sum.revenue - sum.baseValue) / sum.revenue) * 100 : 0,
      marginVsTarget: sum.revenue > 0 ? ((sum.revenue - sum.required) / sum.revenue) * 100 : 0,
    };
  }, [measured]);

  /* ── the sheet report, from a run of the site's own packer ─────────── */

  const sheets = useMemo(() => {
    const parts = blocks.flatMap((block) =>
      block.rows.flatMap((row) =>
        Array.from({ length: Math.max(0, Math.min(row.qty, 24)) }, (_, index) => ({
          id: `${row.ref}-${index + 1}`,
          width: row.w1,
          height: row.h1,
        })),
      ),
    );
    if (parts.length === 0) return null;
    const result = packGuillotine(parts, { width: pnl.sheet.w, height: pnl.sheet.h }, {
      left: optimizerLimits.marginLeft,
      right: optimizerLimits.marginRight,
      top: optimizerLimits.marginTop,
      bottom: optimizerLimits.marginBottom,
      kerf: optimizerLimits.kerf,
    });
    const stockSqm = (pnl.sheet.w / 1000) * (pnl.sheet.h / 1000);
    const boughtSqm = result.plates * stockSqm;
    /** The heavy end of the job decides the glass price; cost at the dearest sheet. */
    const buyPrice = Math.max(...blocks.flatMap((block) => block.rows.map((row) => sheetForRef(row.ref)?.purchase ?? 0)));
    return {
      result,
      stockSqm,
      boughtSqm,
      wasteSqm: boughtSqm - result.partAreaSqm,
      sheetCost: boughtSqm * buyPrice,
      buyPrice,
      wasteValue: (boughtSqm - result.partAreaSqm) * buyPrice,
    };
  }, [blocks, pnl.sheet]);

  /* ── the decision ─────────────────────────────────────────────────── */

  return (
    <div className="flex h-full min-h-0 flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <button
          type="button"
          onClick={() =>
            setRecalc(
              t.template`RECALCULATE — ${blocks.reduce((sum, block) => sum + block.rows.length, 0)} rows read from ${blocks.length} Step 5 specification${blocks.length === 1 ? "" : "s"}, ${totals.area.toFixed(2)} m² billable, AED ${cash(totals.revenue)} given against AED ${cash(totals.required)} required.`,
            )
          }
          title="Re-run the analysis over Step 5's rows. `ProformaInvoice.NoCostedLinesYetPressRecalculateOnceStep5`."
          className="rounded-full border border-line-strong/60 bg-ink-800/60 px-2.5 py-1 font-mono text-3xs uppercase tracking-[0.08em] font-medium text-text-muted transition-colors duration-200 hover:border-brand-400/40 hover:text-text-primary"
        >
          {t("Recalculate")}
        </button>
        <div className="flex flex-wrap gap-1" role="tablist" aria-label={t("Profit and loss views")}>
          {tabs.map((entry) => (
            <button
              key={entry}
              type="button"
              role="tab"
              aria-selected={tab === entry}
              onClick={() => setTab(entry)}
              className={cn(
                "rounded-full px-2.5 py-1 font-mono text-3xs uppercase tracking-[0.08em] font-medium transition-colors duration-200",
                tab === entry
                  ? "bg-brand-400/18 text-brand-100 ring-1 ring-inset ring-brand-400/40"
                  : "text-text-muted hover:bg-ink-800/70 hover:text-text-primary",
              )}
            >
              {entry}
            </button>
          ))}
        </div>
      </div>

      {recalc ? (
        <p aria-live="polite" className="rounded-lg border border-brand-400/35 bg-brand-400/8 px-2.5 py-1.5 text-[0.75rem] leading-snug text-brand-100 font-medium">
          {recalc}
        </p>
      ) : null}

      {/* ── 1 · OVERVIEW ──────────────────────────────────────────────── */}
      {tab === "1 · OVERVIEW" ? (
        <div className="min-h-0 flex-1 overflow-y-auto pr-0.5">
          <p className="text-[0.8125rem] font-semibold text-text-primary">{t(pnl.question)}</p>
          <p className="mt-0.5 text-[0.75rem] text-text-muted font-medium">
            {totals.area.toFixed(2)} m² billable · {blocks.reduce((sum, block) => sum + block.rows.length, 0)} rows ·{" "}
            {blocks.length} specification{blocks.length === 1 ? "" : "s"} · base paid by the customer, VAT on top at the invoice
          </p>

          <p className="mt-3 font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">
            {t("Main decision — target, expected margin, result")}
          </p>

          <div className="mt-2 grid gap-2 lg:grid-cols-3">
            <div className="rounded-xl border border-line/60 bg-ink-900/50 px-3 py-2.5">
              <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{t("Required target price")}</p>
              <p className="mt-1 text-[1.0625rem] font-semibold tabular-nums text-text-primary">
                AED {cash(totals.requiredPerSqm)} <span className="text-[0.75rem] font-medium text-text-muted">/ m²</span>
              </p>
              <p className="mt-1 text-[0.6875rem] leading-snug text-text-muted font-medium">{t("Base price (no uplift) AED {v1} / m² → AED {v2} across the job.", { v1: cash(totals.basePricePerSqm), v2: cash(totals.required) })}</p>
              <p className="mt-1 text-[0.6875rem] leading-snug text-text-faint font-medium">{t("Basis · area-weighted across specifications")}</p>
            </div>

            <div className="rounded-xl border border-line/60 bg-ink-900/50 px-3 py-2.5">
              <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{t("Expected profit margin")}</p>
              <div className="mt-1 flex items-baseline gap-2">
                <input
                  type="number"
                  min="0"
                  step="1"
                  value={target}
                  onChange={(event) => setTarget(Math.max(0, Number(event.target.value)))}
                  aria-label={t("Expected profit margin percent")}
                  className="w-16 rounded-md border border-line-strong/50 bg-ink-950/70 px-1.5 py-0.5 text-right font-mono text-[0.875rem] tabular-nums text-text-primary outline-none focus:border-brand-400/60"
                />
                <span className="text-[0.875rem] font-semibold text-text-primary">%</span>
                <span className="font-mono text-[0.75rem] text-text-muted">×{(1 + target / 100).toFixed(2)}</span>
              </div>
              <p className="mt-1 text-[0.6875rem] leading-snug text-text-muted font-medium">{t("Uplift applied to the cost basis.")}</p>
              <p className="mt-1 text-[0.6875rem] leading-snug text-text-faint font-medium">
                Source · Step 9 pricing defaults{target === 15 ? " (the application's fresh-install value)" : " (changed in this preview)"}
              </p>
            </div>

            <div
              className={cn(
                "rounded-xl border px-3 py-2.5",
                totals.aboveTarget ? "border-money-positive/45 bg-money-positive/8" : "border-amber-400/45 bg-amber-400/8",
              )}
            >
              <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{t("Price balance — base / target")}</p>
              <p
                className={cn(
                  "mt-1 text-[1.0625rem] font-semibold",
                  totals.aboveTarget ? "text-money-positive" : "text-amber-300",
                )}
              >
                {totals.aboveTarget ? t("Given price above target") : t("Given price below target")}
              </p>
              <p className="mt-1 text-[0.6875rem] leading-snug text-text-muted font-medium">
                Given AED {cash(totals.revenue)} against a required AED {cash(totals.required)} — {totals.aboveTarget ? "surplus" : "shortfall"} AED{" "}
                {cash(Math.abs(totals.difference))}.
              </p>
              <p className="mt-1 text-[0.6875rem] leading-snug text-text-faint font-medium">
                {pnl.givenPriceSource} · margin against target {fmtPct(totals.marginVsTarget)}
              </p>
            </div>
          </div>

          <p className="mt-3 font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">{t("2 · Money — revenue, glass cost and margin")}</p>
          <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            <MoneyRow label={t("Net revenue (ex-VAT)")} value={`AED ${cash(totals.revenue)}`} note={`${cash(totals.givenPerSqm)} / m² across ${totals.area.toFixed(2)} m²`} />
            <MoneyRow label={t("Glass cost only")} value={`AED ${cash(totals.cost)}`} note={t.template`purchase column of the Sheet Store · ${pnl.costBasis}`} />
            <MoneyRow
              label={t("Glass margin (reference)")}
              value={`AED ${cash(totals.glassMargin)}`}
              note={t.template`${fmtPct(totals.glassMarginPercent)} of net revenue`}
              tone={totals.glassMargin >= 0 ? "good" : "watch"}
            />
            <MoneyRow label={t("Margin against base price")} value={fmtPct(totals.marginVsBase)} note={`base AED ${cash(totals.baseValue)}`} />
            <MoneyRow label={t("Margin against target")} value={fmtPct(totals.marginVsTarget)} note={`target AED ${cash(totals.required)}`} tone={totals.marginVsTarget >= 0 ? "good" : "watch"} />
            <MoneyRow
              label={t("Surcharged rows")}
              value={String(measured.reduce((sum, spec) => sum + spec.surcharged, 0))}
              note={t.template`at ${SURCHARGE_THRESHOLD_SQM} m² and above · +${blocks[0]?.surchargePercent ?? 0}% on those rows`}
            />
          </div>

          {/*
            ── 5 · RESULT — BASE AND TARGET BALANCE ─────────────────────────
            The section the desktop view puts under the money: the same
            answer asked twice, once against the base price and once against
            the target, with a balance in per-m² and in value for each. The
            status chip flips on sign, and the U-insert line says whether the
            treatment is inside the given price — the app's own toggle.
          */}
          <p className="mt-3 font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">{t("5 · Result — base and target balance")}</p>
          <div className="mt-2 grid gap-2 sm:grid-cols-3">
            <div className="rounded-xl border border-line/60 bg-ink-900/50 px-3 py-2.5">
              <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{t("Status")}</p>
              <p
                className={cn(
                  "mt-1 inline-flex rounded-full border px-2 py-0.5 font-mono text-3xs uppercase tracking-[0.08em] font-semibold",
                  totals.aboveTarget
                    ? "border-money-positive/45 bg-money-positive/12 text-money-positive"
                    : "border-amber-400/45 bg-amber-400/12 text-amber-300",
                )}
              >
                {totals.aboveTarget ? t("Above target") : t("Below target")}
              </p>
              <p className="mt-1.5 text-[0.6875rem] leading-snug text-text-muted font-medium">
                {totals.aboveTarget
                  ? "Final required target met and exceeded on the given price."
                  : "The given price does not reach the required target."}
              </p>
            </div>
            <BalanceCard
              title={t("Base")}
              percent={t.template`${fmtPct(totals.marginVsBase)} margin against base price`}
              perSqm={`AED ${cash(totals.givenPerSqm - totals.basePricePerSqm)}`}
              value={`AED ${cash(totals.revenue - totals.baseValue)}`}
              positive={totals.revenue - totals.baseValue >= 0}
            />
            <BalanceCard
              title={t("Target")}
              percent={t.template`${fmtPct(totals.marginVsTarget)} margin against target`}
              perSqm={`AED ${cash(totals.givenPerSqm - totals.requiredPerSqm)}`}
              value={`AED ${cash(totals.difference)}`}
              positive={totals.aboveTarget}
            />
          </div>

          <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            <MoneyRow label={t("Given price (Step 5 base price)")} value={`AED ${cash(totals.basePricePerSqm)} / m²`} note={t("base rate paid by the customer, area-weighted")} />
            <MoneyRow label={t("Cost-based profit margin")} value={fmtPct(totals.glassMarginPercent)} note={t("net revenue less the glass cost only")} tone={totals.glassMarginPercent >= 0 ? "good" : "watch"} />
            <MoneyRow label={t("Given judged for the margin")} value={`AED ${cash(totals.givenPerSqm)} / m²`} note={`against a required AED ${cash(totals.requiredPerSqm)} / m²`} />
            <MoneyRow label={t("U-insert inside given")} value="Toggle off" note={t("the treatment is not counted inside the given price on this specification")} />
          </div>

          <p className="mt-3 text-[0.6875rem] leading-relaxed text-text-faint font-medium">{t("Cost is the Sheet Store purchase price of each glass, over the same billable square metres Step 5 charged — the sub-0.5 m² floor included, because the offcut is bought and thrown away. Change anything on Step 5 and every figure here moves with it; the specification is the only input.")}</p>
        </div>
      ) : null}

      {/* ── 2 · SERVICES ──────────────────────────────────────────────── */}
      {tab === "2 · SERVICES" ? (
        <div className="min-h-0 flex-1 overflow-auto">
          <p className="font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">
            {t("Service profitability analysis")}
          </p>
          <table className="mt-2 w-full min-w-[36rem] border-collapse text-left">
            <caption className="sr-only">{t("Charge rows raised by the pricing estimator, with the area they apply to.")}</caption>
            <thead>
              <tr className="border-b border-line/60">
                {["Service", "Spec", "Charge", "Area", "Cost", "Profit", "Margin %", "Total"].map((heading, index) => (
                  <th
                    key={heading}
                    scope="col"
                    className={cn(
                      "whitespace-nowrap px-2.5 py-2 font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium",
                      index >= 2 ? "text-right" : "",
                    )}
                  >
                    {heading}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {pnl.services.map((service) => {
                const perSqm = service.basis === "per m²" && !(service.dguOnly && !hasDgu);
                const total = perSqm ? money(totals.area * service.rate) : null;
                return (
                  <tr key={service.key} className={cn("border-b border-line/40 last:border-0 hover:bg-ink-800/40", service.dguOnly && !hasDgu ? "opacity-55" : "")}>
                    <td className="px-2.5 py-1.5 text-[0.75rem] text-text-secondary">{t(service.label)}</td>
                    <td className="px-2.5 py-1.5 font-mono text-[0.6875rem] text-text-faint">
                      {service.basis}
                      {service.dguOnly && !hasDgu ? t(" · SGU") : ""}
                    </td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-secondary tabular-nums">{cash(service.rate)}</td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-muted tabular-nums">
                      {total === null ? "—" : `${totals.area.toFixed(2)} m²`}
                    </td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-faint tabular-nums">
                      {service.costed ? "—" : "not costed"}
                    </td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-faint tabular-nums">—</td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-faint tabular-nums">—</td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-secondary tabular-nums">
                      {total === null ? "—" : cash(total)}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <div className="mt-2 flex flex-wrap items-center justify-between gap-2 rounded-lg border border-line/50 bg-ink-900/40 px-2.5 py-2">
            <p className="text-[0.75rem] leading-relaxed text-text-muted font-medium">
              {pnl.services.some((service) => !service.costed)
                ? "No costed lines yet. Press RECALCULATE once Step 5 has glass items and the estimator's costs are entered — the charge rows above are the estimator's own, and their service cost comes from Step 4's pane and sheet source."
                : "Every charge on this job carries a cost."}
            </p>
            <p className="font-mono text-[0.75rem] font-semibold text-text-secondary">
              {t("Overall service margin — not costed yet")}
            </p>
          </div>
        </div>
      ) : null}

      {/* ── 3 · SHEETS ────────────────────────────────────────────────── */}
      {tab === "3 · SHEETS" ? (
        <div className="min-h-0 flex-1 overflow-y-auto pr-0.5">
          {sheets ? (
            <>
              <p className="font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">{t("1 · Sheet usage")}</p>
              <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                <MoneyRow label={t("Buy sheets")} value={`${sheets.result.plates}`} note={`${pnl.sheet.label} · ${pnl.sheet.w} × ${pnl.sheet.h} mm`} />
                <MoneyRow label={t("Bought area")} value={`${sheets.boughtSqm.toFixed(2)} m²`} note={t.template`${sheets.stockSqm.toFixed(2)} m² per sheet`} />
                <MoneyRow
                  label={t("Part area")}
                  value={`${sheets.result.partAreaSqm.toFixed(2)} m²`}
                  note={t.template`${sheets.result.placedPieces} of ${sheets.result.totalPieces} pieces placed`}
                />
                <MoneyRow
                  label={t("Utilisation")}
                  value={`${sheets.result.utilizationPct.toFixed(2)}%`}
                  note={`offcut ${sheets.wasteSqm.toFixed(2)} m²`}
                  tone={sheets.result.utilizationPct >= 80 ? "good" : "watch"}
                />
              </div>

              <p className="mt-3 font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">{t("2 · Sheet price reconciliation")}</p>
              <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                <MoneyRow label={t("Buy sheet price")} value={`AED ${cash(sheets.buyPrice)} / m²`} note={t("dearest glass on the job, from the Sheet Store purchase column")} />
                <MoneyRow label={t("Sheet cost")} value={`AED ${cash(sheets.sheetCost)}`} note={`${sheets.result.plates} × ${sheets.stockSqm.toFixed(2)} m²`} />
                <MoneyRow label={t("Wastage value")} value={`AED ${cash(sheets.wasteValue)}`} note={t.template`${sheets.wasteSqm.toFixed(2)} m² not sold`} tone="watch" />
                <MoneyRow
                  label={t("Glass recovered in the price")}
                  value={`AED ${cash(totals.cost)}`}
                  note={t.template`the Step 5 billable area costed at purchase — ${(totals.cost / sheets.sheetCost * 100).toFixed(1)}% of the sheet spend`}
                  tone={totals.cost >= sheets.sheetCost ? "good" : "watch"}
                />
              </div>

              <p className="mt-3 font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">{t("3 · Step 6 wastage cross-check")}</p>
              <p className="mt-1.5 text-[0.75rem] leading-relaxed text-text-muted font-medium">
                Wastage the sheets bought beyond the pieces cut is {(sheets.wasteSqm / sheets.boughtSqm * 100).toFixed(1)}% of the bought
                area ({sheets.wasteSqm.toFixed(2)} m²). The glass cost carried into the price is AED {cash(totals.cost)}
                {totals.cost >= sheets.sheetCost
                  ? t.template`, which covers the sheet spend with AED ${cash(totals.cost - sheets.sheetCost)} left against offcuts and handling.`
                  : t.template`, so AED ${cash(sheets.sheetCost - totals.cost)} of the sheet spend is absorbed by the margin rather than billed.`}{" "}
                {t("The gap is net wastage absorbed after billing recovery — it is why the 0.5 m² floor exists: a 0.4 m² pane still buys half")}
                a square metre of glass.
              </p>

              <p className="mt-3 font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">{t("4 · Overall report — all sheets")}</p>
              <div className="mt-2 grid gap-2 sm:grid-cols-3">
                <MoneyRow label={t("Placed")} value={String(sheets.result.placedPieces)} note={t("every piece found a plate")} />
                <MoneyRow label={t("Unplaced")} value={String(sheets.result.unplaced.length)} note={t("no plate could hold these")} tone={sheets.result.unplaced.length === 0 ? "good" : "watch"} />
                <MoneyRow label={t("Stock area")} value={`${sheets.result.stockAreaSqm.toFixed(2)} m²`} note={`kerf ${optimizerLimits.kerf} mm · margins ${optimizerLimits.marginLeft}/${optimizerLimits.marginRight}/${optimizerLimits.marginTop}/${optimizerLimits.marginBottom} mm`} />
              </div>
              <p className="mt-2 text-[0.6875rem] leading-snug text-text-faint font-medium">
                Sheets planned against {t(pnl.sheet.label)}. The other sizes the optimizer offers are{" "}
                {sheetSizes.filter((size) => size.label !== pnl.sheet.label).map((size) => size.label).join(", ")}.
              </p>
            </>
          ) : (
            <p className="text-[0.75rem] text-text-muted font-medium">{t("No glass on this specification yet, so there is nothing to cost.")}</p>
          )}
        </div>
      ) : null}

      {/* ── 4 · SPECS ─────────────────────────────────────────────────── */}
      {tab === "4 · SPECS" ? (
        <div className="min-h-0 flex-1 overflow-auto">
          <p className="font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">
            {t("Main spec check — one row per Step 5 spec")}
          </p>
          <table className="mt-2 w-full min-w-[40rem] border-collapse text-left">
            <caption className="sr-only">{t("Each Step 5 specification: its area, what it is given at, what the target requires and the difference.")}</caption>
            <thead>
              <tr className="border-b border-line/60">
                {[
                  "Glass / specification",
                  "Area",
                  "Given value",
                  "Required target value",
                  "Difference",
                  "Margin %",
                  "Base",
                  "Surcharge %",
                  "Target uplift",
                  "Validation",
                ].map((heading, index) => (
                  <th
                    key={heading}
                    scope="col"
                    className={cn(
                      "whitespace-nowrap px-2.5 py-2 font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium",
                      index >= 1 ? "text-right" : "",
                    )}
                  >
                    {heading}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {measured.map((spec) => {
                const difference = money(spec.revenue - spec.required);
                return (
                  <tr key={spec.number} className="border-b border-line/40 last:border-0 hover:bg-ink-800/40">
                    <td className="px-2.5 py-1.5">
                      <span className="font-mono text-[0.75rem] text-text-secondary">
                        {spec.number} · {spec.glassType}
                      </span>
                      <span className="ml-2 text-[0.75rem] text-text-muted">{t(spec.name)}</span>
                    </td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-muted tabular-nums">{spec.area.toFixed(2)} m²</td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-secondary tabular-nums">{cash(spec.revenue)}</td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-secondary tabular-nums">{cash(spec.required)}</td>
                    <td
                      className={cn(
                        "px-2.5 py-1.5 text-right font-mono text-[0.75rem] tabular-nums",
                        difference >= 0 ? "text-money-positive" : "text-amber-300",
                      )}
                    >
                      {difference >= 0 ? "+" : "−"}
                      {cash(Math.abs(difference))}
                    </td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-secondary tabular-nums">
                      {fmtPct(spec.revenue > 0 ? ((spec.revenue - spec.required) / spec.revenue) * 100 : 0)}
                    </td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-muted tabular-nums">{cash(spec.baseRate)}</td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-muted tabular-nums">
                      {spec.surchargePercent}%
                      {spec.surcharged > 0 ? (
                        <span className="ml-1.5 rounded-full border border-money-positive/45 bg-money-positive/12 px-1.5 py-px font-mono text-[0.625rem] text-money-positive">
                          {spec.surcharged} row{spec.surcharged === 1 ? "" : "s"}
                        </span>
                      ) : null}
                    </td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-muted tabular-nums">
                      / m² {cash(spec.area > 0 ? spec.required / spec.area - spec.baseRate : 0)}
                    </td>
                    <td className="px-2.5 py-1.5 text-right">
                      <span
                        className={cn(
                          "rounded-full border px-1.5 py-px font-mono text-[0.625rem] uppercase tracking-[0.08em] font-semibold",
                          difference >= 0
                            ? "border-money-positive/45 bg-money-positive/12 text-money-positive"
                            : "border-amber-400/45 bg-amber-400/12 text-amber-300",
                        )}
                      >
                        {difference >= 0 ? t("Above") : t("Below")}
                      </span>
                      {spec.floored > 0 ? (
                        <span className="ml-1.5 font-mono text-[0.625rem] text-text-faint">
                          {spec.floored} floored
                        </span>
                      ) : null}
                    </td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr className="border-t border-line/60">
                <td className="px-2.5 py-2 text-[0.75rem] font-medium text-text-secondary">Total · {measured.length} specification{measured.length === 1 ? "" : "s"}</td>
                <td className="px-2.5 py-2 text-right font-mono text-[0.75rem] font-semibold text-text-secondary tabular-nums">{totals.area.toFixed(2)} m²</td>
                <td className="px-2.5 py-2 text-right font-mono text-[0.75rem] font-semibold text-text-primary tabular-nums">{cash(totals.revenue)}</td>
                <td className="px-2.5 py-2 text-right font-mono text-[0.75rem] font-semibold text-text-secondary tabular-nums">{cash(totals.required)}</td>
                <td
                  className={cn(
                    "px-2.5 py-2 text-right font-mono text-[0.75rem] font-semibold tabular-nums",
                    totals.difference >= 0 ? "text-money-positive" : "text-amber-300",
                  )}
                >
                  {totals.difference >= 0 ? "+" : "−"}
                  {cash(Math.abs(totals.difference))}
                </td>
                <td colSpan={5} />
              </tr>
            </tfoot>
          </table>
          <p className="mt-2 text-[0.6875rem] leading-snug text-text-faint font-medium">{t("{v1}above. The required value is that specification's area at its base rate plus {v2}%.", { v1: t("Each Step 5 specification stays separate — its own base rate, its own surcharge, its own rows at {SURCHARGE_THRESHOLD_SQM} m² and", { SURCHARGE_THRESHOLD_SQM: SURCHARGE_THRESHOLD_SQM }), v2: target })}</p>
        </div>
      ) : null}

      {!compact ? (
        <p className="text-[0.6875rem] leading-relaxed text-text-faint font-medium">{t("Sample data · the charges are the estimator's own rows and the arithmetic is the application's, but this is a reconstruction of the screen, not a capture of it.")}</p>
      ) : null}
    </div>
  );
}

/* ── pieces ─────────────────────────────────────────────────────────── */

function MoneyRow({
  label,
  value,
  note,
  tone,
}: {
  label: string;
  value: string;
  note: string;
  tone?: "good" | "watch";
}) {
  return (
    <div className="rounded-xl border border-line/60 bg-ink-900/50 px-3 py-2.5">
      <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{label}</p>
      <p
        className={cn(
          "mt-1 text-[0.9375rem] font-semibold tabular-nums",
          tone === "good" ? "text-money-positive" : tone === "watch" ? "text-amber-300" : "text-text-primary",
        )}
      >
        {value}
      </p>
      <p className="mt-1 text-[0.6875rem] leading-snug text-text-muted font-medium">{note}</p>
    </div>
  );
}

/**
 * BASE or TARGET, asked the same way.
 *
 * The desktop view prints both halves of the balance in the same shape — a
 * margin against that price, the difference per square metre, and the
 * difference in money — so the two are comparable line for line rather than
 * one being a figure and the other a paragraph.
 */
function BalanceCard({
  title,
  percent,
  perSqm,
  value,
  positive,
}: {
  title: string;
  percent: string;
  perSqm: string;
  value: string;
  positive: boolean;
}) {
  return (
    <div className="rounded-xl border border-line/60 bg-ink-900/50 px-3 py-2.5">
      <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{title}</p>
      <p className="mt-1 text-[0.875rem] font-semibold text-text-primary">{percent}</p>
      <dl className="mt-1.5 space-y-1">
        <div className="flex items-baseline justify-between gap-2">
          <dt className="text-[0.6875rem] text-text-muted font-medium">{title === "Base" ? "Base balance" : "Target balance"}</dt>
          <dd className={cn("font-mono text-[0.75rem] tabular-nums", positive ? "text-money-positive" : "text-amber-300")}>{perSqm} / m²</dd>
        </div>
        <div className="flex items-baseline justify-between gap-2">
          <dt className="text-[0.6875rem] text-text-muted font-medium">{title === "Base" ? "Base balance value" : "Target balance value"}</dt>
          <dd className={cn("font-mono text-[0.75rem] tabular-nums", positive ? "text-money-positive" : "text-amber-300")}>{value}</dd>
        </div>
      </dl>
    </div>
  );
}

function cash(value: number) {
  return value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function fmtPct(value: number) {
  return `${value.toFixed(2)}%`;
}
