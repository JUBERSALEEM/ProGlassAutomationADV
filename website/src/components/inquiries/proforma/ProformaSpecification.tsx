"use client";

import { formatMoney, formatSqm, type DraftLine, type GroupTotal, type PricedLine } from "@/lib/proforma-draft";
import { EMPTY_RATE_SETTING, planRates, type RateSetting } from "@/lib/proforma-rates";
import { SpecificationRateBar } from "@/components/inquiries/proforma/SpecificationRateBar";

const CELL = "border-b border-line/40 px-2 py-1.5 align-middle";
const INPUT = "h-8 w-full rounded-lg border border-line-strong bg-ink-950 px-2 text-xs text-text-primary";

/**
 * Step 5: the specification, priced.
 *
 * Each group is its own document block: its own rows, its own totals, and a
 * header that says which group is receiving builder lines. Groups are
 * independent, so adding a line to group 2 never renumbers group 1, and every
 * row keeps the same key while the numbers around it change.
 */
export function ProformaSpecification({
  priced,
  groups,
  perGroup,
  activeGroup,
  currency,
  totals,
  rateSettings,
  onRateChange,
  onApplyRates,
  onEdit,
  onAddLine,
  onRemoveLine,
  onDuplicateLine,
  onMoveLine,
  onAddGroup,
  onRemoveGroup,
  onFocusGroup,
}: {
  priced: PricedLine[];
  groups: number[];
  perGroup: GroupTotal[];
  activeGroup: number;
  currency: string;
  totals: { lines: number; sqm: number; value: number };
  rateSettings: Record<number, RateSetting>;
  onRateChange: (groupNo: number, patch: Partial<RateSetting>) => void;
  onApplyRates: (groupNo: number) => void;
  onEdit: (key: string, patch: Partial<DraftLine>) => void;
  onAddLine: (groupNo: number) => void;
  onRemoveLine: (key: string) => void;
  onDuplicateLine: (key: string) => void;
  onMoveLine: (key: string, direction: -1 | 1) => void;
  onAddGroup: () => void;
  onRemoveGroup: (groupNo: number) => void;
  onFocusGroup: (groupNo: number) => void;
}) {
  return (
    <section aria-label="Step 5, priced specification" className="min-w-0 space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-3 rounded-2xl border border-line-strong/60 bg-ink-900/80 px-3 py-2.5">
        <div>
          <div className="text-3xs font-semibold uppercase tracking-wider text-brand-300">Step 5</div>
          <h2 className="text-sm font-bold text-text-primary">Priced specification</h2>
          <p className="mt-0.5 text-3xs text-text-muted">
            {totals.lines} line{totals.lines === 1 ? "" : "s"}, {formatSqm(totals.sqm)} sqm, {formatMoney(totals.value, currency)}
          </p>
        </div>
        <button
          type="button"
          onClick={onAddGroup}
          className="rounded-xl border border-brand-400/70 bg-brand-500/10 px-3 py-2 text-xs font-semibold text-brand-200 hover:bg-brand-500/20"
        >
          Add specification
        </button>
      </div>

      {groups.map((groupNo) => {
        const rows = priced.filter((line) => line.groupNo === groupNo);
        const totalsForGroup = perGroup.find((group) => group.groupNo === groupNo);
        const active = activeGroup === groupNo;
        return (
          <section
            key={groupNo}
            aria-label={`Specification ${groupNo}`}
            className={`overflow-hidden rounded-2xl border bg-ink-900/80 ${active ? "border-brand-400/70" : "border-line-strong/60"}`}
          >
            <header className="flex flex-wrap items-center justify-between gap-2 border-b border-line-strong/60 px-3 py-2">
              <div className="flex items-center gap-2">
                <span className={`grid h-6 w-6 place-items-center rounded-lg font-mono text-3xs font-bold ${active ? "bg-brand-400 text-accent-ink" : "bg-ink-850 text-text-secondary"}`}>
                  {groupNo}
                </span>
                <h3 className="text-xs font-bold text-text-primary">Specification {groupNo}</h3>
                <span className="text-3xs text-text-faint">
                  {rows.length} line{rows.length === 1 ? "" : "s"}, {formatSqm(totalsForGroup?.sqm || 0)} sqm, {formatMoney(totalsForGroup?.value || 0, currency)}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => onFocusGroup(groupNo)}
                  aria-pressed={active}
                  className={`rounded-lg border px-2 py-1 text-3xs font-semibold ${active ? "border-brand-400/70 text-brand-200" : "border-line-strong text-text-muted hover:text-text-primary"}`}
                >
                  {active ? "Receiving builder lines" : "Assign builder here"}
                </button>
                <button
                  type="button"
                  onClick={() => onAddLine(groupNo)}
                  className="rounded-lg border border-line-strong px-2 py-1 text-3xs font-semibold text-text-muted hover:text-text-primary"
                >
                  Add row
                </button>
                {groups.length > 1 ? (
                  <button
                    type="button"
                    onClick={() => onRemoveGroup(groupNo)}
                    className="rounded-lg border border-line-strong px-2 py-1 text-3xs font-semibold text-text-faint hover:text-state-danger"
                    aria-label={`Remove specification ${groupNo} and its ${rows.length} lines`}
                  >
                    Remove
                  </button>
                ) : null}
              </div>
            </header>

            <SpecificationRateBar
              groupNo={groupNo}
              setting={rateSettings[groupNo] ?? EMPTY_RATE_SETTING}
              plan={planRates(priced, groupNo, (rateSettings[groupNo] ?? EMPTY_RATE_SETTING).base, (rateSettings[groupNo] ?? EMPTY_RATE_SETTING).percent)}
              rowCount={rows.length}
              currency={currency}
              onChange={(patch) => onRateChange(groupNo, patch)}
              onApply={() => onApplyRates(groupNo)}
            />

            <div className="overflow-x-auto">
              <table className="w-full min-w-[62rem] border-collapse text-left text-xs">
                <thead>
                  <tr className="bg-ink-950/60 text-3xs uppercase tracking-wider text-text-faint">
                    <th className="px-2 py-2 font-semibold">#</th>
                    <th className="px-2 py-2 font-semibold">Glass reference</th>
                    <th className="px-2 py-2 font-semibold">W1</th>
                    <th className="px-2 py-2 font-semibold">H1</th>
                    <th className="px-2 py-2 font-semibold">W2</th>
                    <th className="px-2 py-2 font-semibold">H2</th>
                    <th className="px-2 py-2 font-semibold">Qty</th>
                    <th className="px-2 py-2 font-semibold">SQM</th>
                    <th className="px-2 py-2 font-semibold">Total SQM</th>
                    <th className="px-2 py-2 font-semibold">Approved rate</th>
                    <th className="px-2 py-2 text-right font-semibold">Value</th>
                    <th className="px-2 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((line, rowIndex) => (
                    <tr key={line.key} className="hover:bg-ink-850/40">
                      <td className={`${CELL} font-mono text-3xs text-text-faint`}>{line.lineNo}</td>
                      <td className={`${CELL} min-w-[14rem]`}>
                        <label className="sr-only" htmlFor={`${line.key}-ref`}>Glass reference for line {line.lineNo} of specification {groupNo}</label>
                        <input
                          id={`${line.key}-ref`}
                          value={line.glassRef}
                          onChange={(event) => onEdit(line.key, { glassRef: event.target.value })}
                          placeholder="Generated in Step 4, or type it"
                          maxLength={200}
                          className={INPUT}
                        />
                      </td>
                      {(["w1", "h1", "w2", "h2"] as const).map((field) => (
                        <td key={field} className={`${CELL} w-20`}>
                          <label className="sr-only" htmlFor={`${line.key}-${field}`}>
                            {field === "w1" ? "Width" : field === "h1" ? "Height" : field === "w2" ? "Inner width" : "Inner height"} for line {line.lineNo}
                          </label>
                          <input
                            id={`${line.key}-${field}`}
                            value={field === "w1" ? String(line.w1 || "") : field === "h1" ? String(line.h1 || "") : field === "w2" ? String(line.w2 || "") : String(line.h2 || "")}
                            onChange={(event) => onEdit(line.key, { [field]: event.target.value } as Partial<DraftLine>)}
                            inputMode="numeric"
                            placeholder="mm"
                            className={INPUT}
                          />
                        </td>
                      ))}
                      <td className={`${CELL} w-16`}>
                        <label className="sr-only" htmlFor={`${line.key}-qty`}>Quantity for line {line.lineNo}</label>
                        <input
                          id={`${line.key}-qty`}
                          value={line.qty ? String(line.qty) : ""}
                          onChange={(event) => onEdit(line.key, { qty: event.target.value })}
                          inputMode="numeric"
                          className={INPUT}
                        />
                      </td>
                      <td className={`${CELL} whitespace-nowrap font-mono text-3xs text-text-secondary`}>
                        {formatSqm(line.sqm)}
                        {line.floored ? <span className="ml-1 text-state-warning" title="Panels under half a square metre bill at half a square metre">min</span> : null}
                      </td>
                      <td className={`${CELL} whitespace-nowrap font-mono text-3xs text-text-secondary`}>{formatSqm(line.totalSqm)}</td>
                      <td className={`${CELL} w-24`}>
                        <label className="sr-only" htmlFor={`${line.key}-rate`}>Approved rate for line {line.lineNo}</label>
                        <input
                          id={`${line.key}-rate`}
                          value={line.rate ? String(line.rate) : ""}
                          onChange={(event) => onEdit(line.key, { rate: event.target.value })}
                          inputMode="decimal"
                          placeholder="rate"
                          className={INPUT}
                        />
                      </td>
                      <td className={`${CELL} whitespace-nowrap text-right font-mono text-xs font-bold text-text-primary`}>{formatMoney(line.value, currency)}</td>
                      <td className={`${CELL} whitespace-nowrap text-right`}>
                        <div className="flex items-center justify-end gap-1">
                          <button
                            type="button"
                            onClick={() => onMoveLine(line.key, -1)}
                            disabled={rowIndex === 0}
                            aria-label={`Move line ${line.lineNo} of specification ${groupNo} up`}
                            className="rounded-lg border border-line-strong px-1.5 py-1 text-3xs text-text-faint hover:text-text-primary disabled:opacity-40"
                          >
                            Up
                          </button>
                          <button
                            type="button"
                            onClick={() => onMoveLine(line.key, 1)}
                            disabled={rowIndex === rows.length - 1}
                            aria-label={`Move line ${line.lineNo} of specification ${groupNo} down`}
                            className="rounded-lg border border-line-strong px-1.5 py-1 text-3xs text-text-faint hover:text-text-primary disabled:opacity-40"
                          >
                            Down
                          </button>
                          <button
                            type="button"
                            onClick={() => onDuplicateLine(line.key)}
                            aria-label={`Duplicate line ${line.lineNo} of specification ${groupNo}`}
                            className="rounded-lg border border-line-strong px-1.5 py-1 text-3xs text-text-faint hover:text-brand-200"
                          >
                            Copy
                          </button>
                          <button
                            type="button"
                            onClick={() => onRemoveLine(line.key)}
                            aria-label={`Remove line ${line.lineNo} of specification ${groupNo}`}
                            className="rounded-lg border border-line-strong px-1.5 py-1 text-3xs text-text-faint hover:text-state-danger"
                          >
                            Remove
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {rows.length === 0 ? (
                    <tr>
                      <td colSpan={12} className="px-3 py-4 text-center text-3xs text-text-faint">
                        No rows yet. Build one on the right, or add a row here.
                      </td>
                    </tr>
                  ) : null}
                </tbody>
                <tfoot>
                  <tr className="bg-ink-950/40 text-3xs text-text-muted">
                    <td colSpan={8} className="px-2 py-2 text-right font-semibold uppercase tracking-wider text-text-faint">Specification {groupNo} total</td>
                    <td className="px-2 py-2 font-mono">{formatSqm(totalsForGroup?.sqm || 0)}</td>
                    <td className="px-2 py-2" />
                    <td className="px-2 py-2 text-right font-mono font-bold text-text-primary">{formatMoney(totalsForGroup?.value || 0, currency)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            </div>
          </section>
        );
      })}
    </section>
  );
}
