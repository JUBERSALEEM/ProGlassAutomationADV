"use client";

import { useMemo, useState, type Dispatch, type SetStateAction } from "react";
import { Pill, SampleTag } from "@/components/product/previewAtoms";
import { money, sheetForRef } from "@/data/workedExample";
import {
  billablePanelSqm,
  isFloored,
  MIN_PANEL_SQM,
  moneyRate,
  physicalPanelSqm,
  rowRate,
  STANDARD_SURCHARGE_PERCENT,
  surchargeApplies,
  SURCHARGE_THRESHOLD_SQM,
} from "@/data/pricingRules";
import { packGuillotine, plateViews } from "@/lib/guillotinePack";
import { optimizerLimits } from "@/data/liveDemos";
import { cn } from "@/lib/cn";

/**
 * Step 5 — the specification, working.
 *
 * This is the screen the whole application is priced from, and until now the
 * site showed it as a read-only table. It is not a read-only table: it is a
 * grid you type dimensions into, a specification block whose own two fields
 * re-price every row, and an action bar whose buttons do something. All of it
 * is here, and all of it does what the desktop application does:
 *
 *   W1 / H1 / QTY    editable. `GlassItemRow.RecalculateAll` — Sqm1 is
 *                    `SqmCalculator.CalculatePanel(W1, H1)`, floored at 0.5 m²;
 *                    Total Sqm is Sqm1 × Qty; Value is Sqm1 × Qty × Rate,
 *                    rounded by `MoneyRounding`.
 *   BASE             the specification's base rate. Editing it re-prices every
 *                    row through `UpdateAllRowRates`.
 *   SURCHARGE %      `Dg.SurchargePct`, seeded at the shop's standard 20%.
 *                    Editing it does the same.
 *   the rate switch  `row.Sqm1 >= 4m ? SurchargeRate : BaseRate`. A row that
 *                    reaches 4 m² prices at base + the specification's %; the
 *                    rows around it do not move. The chip in the RATE cell
 *                    says which of the two a row is on.
 *   QUICK            one fast pass. Step 6 takes the sheet quantity from it.
 *   OPTIMIZE         the search. Fewest unplaced pieces, then fewest sheets,
 *                    then highest utilisation.
 *   VIEW RESULT      opens the result — sheets, utilisation, the plate drawings.
 *   IMPORT BOQ       re-reads the inquiry's rows, discarding edits.
 *   EXPORT           Excel / CSV / PDF / share cut list, as the app offers them.
 *   + ADD SPEC       adds a specification block and pre-fills the shop's
 *                    standard surcharge on it.
 *
 * The packing is the site's own guillotine packer, shared with the homepage and
 * the optimization page, and the plate drawings come out of the same run — so
 * the sheet count here is arithmetic over the pieces actually placed, not a
 * caption. A piece no plate can hold is reported rather than dropped.
 *
 * Only the arithmetic is the application's; the rates are the seeded
 * demonstration quotation's. This is a reconstruction, and it is labelled as
 * one wherever it appears.
 */

export type SpecRowInput = {
  serial: number;
  ref: string;
  w1: number;
  h1: number;
  w2: number;
  h2: number;
  qty: number;
  /** `GlassItemRow.Rate` as saved on the document. */
  rate: number;
};

export type SpecBlockInput = {
  number: number;
  name: string;
  glassType: string;
  glassTypes: string[];
  baseRate: number;
  surchargePercent: number;
  rows: SpecRowInput[];
  /** The specification the visitor is meant to read first. */
  primary?: boolean;
};

export type SpecSurfaceInput = {
  /** The specifications the document opens with. One, in this preview. */
  blocks: SpecBlockInput[];
  /**
   * The specification `+ ADD SPEC` adds.
   *
   * The shop's oversize case — an atrium lite past 4 m² and a vent light under
   * 0.5 m², which are the two panels the two rules exist for. It is held here
   * rather than in `blocks` so the document opens on the one specification the
   * job was quoted from, and the rule is one click away rather than advertised
   * in a second block nobody asked for.
   */
  extraBlock?: SpecBlockInput;
  sheet: { label: string; w: number; h: number };
  /** `Dg.ColumnsHint`, the line the app prints above the grid. */
  columnsHint: string;
};

type Run = {
  kind: "QUICK" | "OPTIMIZE";
  sheets: number;
  utilization: number;
  placed: number;
  unplaced: number;
  partArea: number;
  stockArea: number;
};

export function SpecificationSurface({
  spec,
  blocks,
  onBlocks,
  compact = false,
}: {
  spec: SpecSurfaceInput;
  /** The specification blocks, owned by the wizard so Step 9 reads the same rows. */
  blocks: SpecBlockInput[];
  onBlocks: Dispatch<SetStateAction<SpecBlockInput[]>>;
  compact?: boolean;
}) {
  const setBlocks = onBlocks;
  const [active, setActive] = useState(0);
  const [run, setRun] = useState<Run | null>(null);
  const [showResult, setShowResult] = useState(false);
  const [exportOpen, setExportOpen] = useState(false);
  const [status, setStatus] = useState(
    `Read from the saved specification — ${spec.blocks[0]?.rows.length ?? 0} rows at the rates they were quoted at.`,
  );

  /**
   * Switch between specification blocks.
   *
   * A job with oversize work has more than one, and a block you cannot get
   * back to is a block you cannot check — so the number in the header is the
   * switcher, and it says what each block is.
   */
  function selectBlock(index: number) {
    const entry = blocks[index];
    if (!entry) return;
    setActive(index);
    setStatus(
      `Specification ${entry.number} · ${entry.name} — ${entry.rows.length} rows on a base rate of ${entry.baseRate.toFixed(2)} with a ${entry.surchargePercent}% surcharge for panels that reach ${SURCHARGE_THRESHOLD_SQM} m².`,
    );
  }
  /** Serial of each row a person has touched, so the rate switch is explained. */
  const [edited, setEdited] = useState<Record<string, boolean>>({});
  /** Rows a person typed a rate over directly — the app leaves these alone. */
  const [locked, setLocked] = useState<Record<string, boolean>>({});

  const block = blocks[active] ?? blocks[0]!;
  const rows = block.rows;

  /* ── the arithmetic ────────────────────────────────────────────────── */

  const priced = useMemo(
    () =>
      rows.map((row) => {
        const physical = physicalPanelSqm(row.w1, row.h1);
        const billable = billablePanelSqm(row.w1, row.h1);
        const rate = rowRate(block.baseRate, block.surchargePercent, billable);
        const totalSqm = billable * row.qty;
        return {
          ...row,
          physical,
          billable,
          floored: isFloored(row.w1, row.h1),
          surcharged: surchargeApplies(billable),
          target: rate,
          totalSqm,
          value: money(totalSqm * row.rate),
          cost: (sheetForRef(row.ref)?.purchase ?? 0) * totalSqm,
        };
      }),
    [rows, block.baseRate, block.surchargePercent],
  );

  const totals = useMemo(
    () =>
      priced.reduce(
        (acc, row) => ({
          qty: acc.qty + row.qty,
          sqm: acc.sqm + row.totalSqm,
          value: acc.value + row.value,
          cost: acc.cost + row.cost,
        }),
        { qty: 0, sqm: 0, value: 0, cost: 0 },
      ),
    [priced],
  );

  const flooredRows = priced.filter((row) => row.floored);
  const surchargedRows = priced.filter((row) => row.surcharged);

  /* ── the optimizer, run for real ───────────────────────────────────── */

  function optimise(kind: "QUICK" | "OPTIMIZE") {
    const parts = priced.flatMap((row) =>
      Array.from({ length: Math.max(0, Math.min(row.qty, 24)) }, (_, index) => ({
        id: `${row.ref}-${index + 1}`,
        width: row.w1,
        height: row.h1,
      })),
    );
    const result = packGuillotine(parts, { width: spec.sheet.w, height: spec.sheet.h }, {
      left: optimizerLimits.marginLeft,
      right: optimizerLimits.marginRight,
      top: optimizerLimits.marginTop,
      bottom: optimizerLimits.marginBottom,
      kerf: optimizerLimits.kerf,
    });
    setRun({
      kind,
      sheets: result.plates,
      utilization: result.utilizationPct,
      placed: result.placedPieces,
      unplaced: result.unplaced.length,
      partArea: result.partAreaSqm,
      stockArea: result.stockAreaSqm,
    });
    setShowResult(kind === "OPTIMIZE");
    setStatus(
      kind === "QUICK"
        ? `QUICK pass done — ${result.plates} sheets of ${spec.sheet.label}, ${result.utilizationPct.toFixed(2)}% used. Step 6 takes the sheet quantity from this run until you press OPTIMIZE.`
        : `OPTIMIZE search done — ${result.plates} sheets, ${result.utilizationPct.toFixed(2)}% used, ${result.unplaced.length} piece(s) no plate could hold. Open VIEW RESULT for the plate drawings.`,
    );
  }

  /* ── editing ───────────────────────────────────────────────────────── */

  /**
   * Writes a row back, applying the rate switch exactly where the application
   * applies it: on the row whose size changed, and only if a person has not
   * typed a rate over it by hand (`SuppressRateAutoAdjust`).
   */
  function editRow(serial: number, patch: Partial<Pick<SpecRowInput, "w1" | "h1" | "qty">>) {
    setBlocks((current) =>
      current.map((entry, index) => {
        if (index !== active) return entry;
        return {
          ...entry,
          rows: entry.rows.map((row) => {
            if (row.serial !== serial) return row;
            const next = { ...row, ...patch };
            if (locked[String(serial)]) return next;
            const billable = billablePanelSqm(next.w1, next.h1);
            return { ...next, rate: rowRate(entry.baseRate, entry.surchargePercent, billable) };
          }),
        };
      }),
    );
    setEdited((current) => ({ ...current, [String(serial)]: true }));
    const next = { ...(rows.find((row) => row.serial === serial) ?? rows[0]!), ...patch };
    const billable = billablePanelSqm(next.w1, next.h1);
    setStatus(
      `${next.ref} is now ${next.w1} × ${next.h1} mm — ${physicalPanelSqm(next.w1, next.h1).toFixed(3)} m² of glass, billed at ${billable.toFixed(3)} m²${
        isFloored(next.w1, next.h1) ? ` (the ${MIN_PANEL_SQM} m² floor)` : ""
      }, priced at ${rowRate(block.baseRate, block.surchargePercent, billable).toFixed(2)}${
        surchargeApplies(billable)
          ? ` — the ${SURCHARGE_THRESHOLD_SQM} m² switch took it to base + ${block.surchargePercent}%`
          : ` — under the ${SURCHARGE_THRESHOLD_SQM} m² switch, so the base rate holds`
      }.`,
    );
  }

  function editRate(serial: number, value: number) {
    setBlocks((current) =>
      current.map((entry, index) =>
        index !== active
          ? entry
          : { ...entry, rows: entry.rows.map((row) => (row.serial === serial ? { ...row, rate: value } : row)) },
      ),
    );
    setLocked((current) => ({ ...current, [String(serial)]: true }));
    setStatus(`Rate typed over on ${rows.find((row) => row.serial === serial)?.ref}. The application leaves a typed rate alone until the size changes again.`);
  }

  /** `UpdateAllRowRates` — a change to BASE or SURCHARGE % re-prices every row. */
  function editBlock(patch: Partial<Pick<SpecBlockInput, "baseRate" | "surchargePercent">>) {
    setLocked({});
    setBlocks((current) => current.map((entry, index) => (index === active ? { ...entry, ...patch } : entry)));
    const next = { ...block, ...patch };
    const moved = block.rows.filter((row) => {
      const billable = billablePanelSqm(row.w1, row.h1);
      return (
        rowRate(next.baseRate, next.surchargePercent, billable) !==
        rowRate(block.baseRate, block.surchargePercent, billable)
      );
    }).length;
    setStatus(
      `Specification ${block.number}: base ${next.baseRate.toFixed(2)}, surcharge ${next.surchargePercent}%. Every row re-priced — ${moved} of ${block.rows.length} changed. A row's rate becomes the base rate, or the surcharge rate once its panel reaches ${SURCHARGE_THRESHOLD_SQM} m².`,
    );
  }

  function importBoq() {
    setBlocks(spec.blocks);
    setActive(0);
    setEdited({});
    setLocked({});
    setRun(null);
    setShowResult(false);
    setStatus(`Inquiry BOQ re-read: ${spec.blocks[0]?.rows.length ?? 0} rows on specification 1, with the rates the document was saved with. Your edits are discarded.`);
  }

  /**
   * `Dg.AddRow` — a blank line at the foot of the grid.
   *
   * The application's grid has it beside `+ ADD SPEC`, and it is how a line
   * that is not on the BOQ gets priced. The new row starts at the
   * specification's base rate and at a size under the floor, so the visitor
   * can watch both rules work outwards as they type into it: the SQM column
   * already sitting at 0.50, and the rate switching to base + the surcharge the
   * moment a dimension passes 4 m².
   */
  function addRow() {
    const next = (block.rows.at(-1)?.serial ?? 0) + 1;
    const row: SpecRowInput = {
      serial: next,
      ref: `CT-06-0600x0400`,
      w1: 600,
      h1: 400,
      w2: 0,
      h2: 0,
      qty: 1,
      rate: block.baseRate,
    };
    setBlocks((current) =>
      current.map((entry, index) => (index === active ? { ...entry, rows: [...entry.rows, row] } : entry)),
    );
    setStatus(
      `Row ${next} added at 600 × 400 mm — 0.24 m² of glass, billed at the ${MIN_PANEL_SQM} m² floor, at the specification's base rate of ${block.baseRate.toFixed(2)}. Type a size into it: the SQM column floors, and the rate switches to ${moneyRate(block.baseRate * (1 + block.surchargePercent / 100)).toFixed(2)} once the panel reaches ${SURCHARGE_THRESHOLD_SQM} m².`,
    );
  }

  function addSpec() {
    const extra = spec.extraBlock;
    if (!extra || blocks.some((block) => block.number === extra.number)) {
      setStatus(
        `This preview carries the specification the job was quoted from and one more for the oversize case. The desktop application adds as many as the job has — press + ADD SPEC there and each new block opens with its own base rate and SURCHARGE %.`,
      );
      return;
    }
    setBlocks((current) => [...current, extra]);
    setActive(1);
    setStatus(
      `Specification ${extra.number} added — ${extra.name}, SURCHARGE % pre-filled at the shop's standard ${STANDARD_SURCHARGE_PERCENT}%. Its oversize lite prices at base + ${STANDARD_SURCHARGE_PERCENT}% the moment it is added.`,
    );
  }

  const plates = useMemo(() => {
    if (!run || !showResult) return [];
    const parts = priced.flatMap((row) =>
      Array.from({ length: Math.max(0, Math.min(row.qty, 24)) }, (_, index) => ({
        id: `${row.ref}-${index + 1}`,
        width: row.w1,
        height: row.h1,
      })),
    );
    const result = packGuillotine(parts, { width: spec.sheet.w, height: spec.sheet.h }, {
      left: optimizerLimits.marginLeft,
      right: optimizerLimits.marginRight,
      top: optimizerLimits.marginTop,
      bottom: optimizerLimits.marginBottom,
      kerf: optimizerLimits.kerf,
    });
    return plateViews(result, { width: spec.sheet.w, height: spec.sheet.h, label: spec.sheet.label });
  }, [run, showResult, priced, spec.sheet]);

  /* ── render ────────────────────────────────────────────────────────── */

  return (
    <div className="flex h-full min-h-0 flex-col gap-3">
      {/* ── block header: the specification's own fields ───────────────── */}
      <div className="rounded-xl border border-line/60 bg-ink-900/50 p-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            {blocks.length > 1 ? (
              <span className="flex items-center gap-1" role="group" aria-label="Specification blocks">
                {blocks.map((entry, index) => (
                  <button
                    key={entry.number}
                    type="button"
                    aria-pressed={index === active}
                    onClick={() => selectBlock(index)}
                    title={`Specification ${entry.number} · ${entry.name} — ${entry.rows.length} rows`}
                    className={cn(
                      "inline-flex h-7 min-w-7 items-center justify-center rounded-lg px-1.5 font-mono text-[0.8125rem] font-semibold transition-colors",
                      index === active
                        ? "bg-brand-500/25 text-brand-100 ring-1 ring-inset ring-brand-400/50"
                        : "bg-ink-800/70 text-text-muted hover:text-text-primary",
                    )}
                  >
                    {entry.number}
                  </button>
                ))}
              </span>
            ) : (
              <span className="inline-flex h-7 min-w-7 items-center justify-center rounded-lg bg-brand-500/20 px-1.5 font-mono text-[0.8125rem] font-semibold text-brand-100">
                {block.number}
              </span>
            )}
            <div>
              <p className="text-[0.8125rem] font-semibold text-text-primary">{block.name}</p>
              <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-text-faint font-medium">
                Specification {block.number}
                {block.primary ? " · from the inquiry BOQ" : " · added in this preview"}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1.5">
            <Pill title="Specification blocks on this document. `Dg.Specs`.">
              Specs
              <span className="rounded-full bg-brand-400/20 px-1.5 font-mono text-[0.6875rem] text-brand-100">
                {blocks.length}
              </span>
            </Pill>
            <Pill onClick={addSpec} title="Add a specification block. `Dg.AddSpec`.">
              + Add spec
            </Pill>
          </div>
        </div>

        <div className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          <label className="block">
            <span className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-text-faint font-medium">Glass type</span>
            <select
              value={block.glassType}
              onChange={(event) =>
                setBlocks((current) =>
                  current.map((entry, index) => (index === active ? { ...entry, glassType: event.target.value } : entry)),
                )
              }
              className="mt-1 w-full rounded-lg border border-line-strong/60 bg-ink-950/70 px-2 py-1.5 text-[0.8125rem] text-text-primary outline-none focus:border-brand-400/60"
            >
              {block.glassTypes.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-text-faint font-medium">Base rate</span>
            <input
              type="number"
              step="1"
              min="0"
              value={block.baseRate}
              onChange={(event) => editBlock({ baseRate: Number(event.target.value) })}
              className="mt-1 w-full rounded-lg border border-line-strong/60 bg-ink-950/70 px-2 py-1.5 text-[0.8125rem] tabular-nums text-text-primary outline-none focus:border-brand-400/60"
            />
          </label>
          <label className="block">
            <span className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-text-faint font-medium">Surcharge %</span>
            <input
              type="number"
              step="1"
              min="0"
              value={block.surchargePercent}
              onChange={(event) => editBlock({ surchargePercent: Number(event.target.value) })}
              className="mt-1 w-full rounded-lg border border-money-positive/45 bg-ink-950/70 px-2 py-1.5 text-[0.8125rem] tabular-nums text-text-primary outline-none focus:border-money-positive/70"
            />
          </label>
          <div className="rounded-lg border border-line/60 bg-ink-950/50 px-2 py-1.5">
            <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-text-faint font-medium">
              Rate at {SURCHARGE_THRESHOLD_SQM} m²
            </p>
            <p className="mt-1 font-mono text-[0.875rem] font-semibold tabular-nums text-money-positive">
              {moneyRate(block.baseRate * (1 + block.surchargePercent / 100)).toFixed(2)} / m²
            </p>
            <p className="text-[0.6875rem] leading-snug text-text-muted font-medium">
              {`Base ${cash(block.baseRate)} + ${block.surchargePercent}% — SurchargeRate`}
            </p>
          </div>
        </div>
      </div>

      {/* ── action bar ─────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-1.5">
        <SampleTag />
        <Pill onClick={importBoq} title="Re-read the inquiry's BOQ rows. `Dg.Import`.">
          Import BOQ
        </Pill>
        <Pill active={exportOpen} onClick={() => setExportOpen((open) => !open)} title="Export the specification. `Dg.Export`.">
          Export ▾
        </Pill>
        <Pill onClick={addRow} title="Add a blank line to this specification. `Dg.AddRow`.">
          + Add row
        </Pill>
        <Pill onClick={() => optimise("QUICK")} title="One fast pass. Step 6 reads the sheet quantity from it. `Dg.RunQuick`.">
          Quick
        </Pill>
        <Pill onClick={() => optimise("OPTIMIZE")} title="Search the plans: fewest unplaced pieces, then fewest sheets, then highest utilisation. `Dg.RunOptimize`.">
          Optimize
        </Pill>
        <Pill
          active={showResult}
          onClick={() => {
            if (!run) {
              setStatus("No run yet. QUICK gives you a fast pass; OPTIMIZE searches the plans properly.");
              return;
            }
            setShowResult((open) => !open);
          }}
          title="Open the optimization result on its SUMMARY view. `Dg.ViewResult`."
        >
          View result
        </Pill>
        {run ? (
          <Pill tone="floor" title="The run the result belongs to.">
            {run.kind} · {run.sheets} sheets · {run.utilization.toFixed(2)}%
          </Pill>
        ) : null}
      </div>

      {exportOpen ? (
        <div className="flex flex-wrap items-center gap-1.5">
          {["Excel", "CSV", "PDF", "Share cut list"].map((option) => (
            <Pill
              key={option}
              onClick={() =>
                setStatus(
                  option === "Share cut list"
                    ? "Cut list shared to the production queue — the eight stages read the same rows."
                    : `Specification exported as ${option}. The desktop application writes the file to the project folder.`,
                )
              }
            >
              {option}
            </Pill>
          ))}
        </div>
      ) : null}

      {/* ── the grid ───────────────────────────────────────────────────── */}
      <div className="min-h-0 flex-1 overflow-auto rounded-xl border border-line/60 bg-ink-950/40">
        <table className="w-full min-w-[52rem] border-collapse text-left">
          <caption className="sr-only">
            Specification {block.number}, {block.name}: one row per glass line, priced at the specification&apos;s rate, or at the
            surcharge rate once a panel reaches {SURCHARGE_THRESHOLD_SQM} square metres.
          </caption>
          <thead className="sticky top-0 z-10 bg-ink-900/95 backdrop-blur">
            <tr className="border-b border-line/60">
              {["#", "Glass ref", "W1", "H1", "W2", "H2", "Qty", "Sqm", "Total sqm", "Rate", "Value"].map((heading, index) => (
                <th
                  key={heading}
                  scope="col"
                  className={cn(
                    "whitespace-nowrap px-2.5 py-2 font-mono text-[0.6875rem] uppercase tracking-[0.1em] text-text-faint font-medium",
                    index >= 2 && index !== 4 && index !== 5 ? "text-right" : "",
                  )}
                >
                  {heading}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {priced.map((row) => (
              <tr key={row.serial} className="border-b border-line/40 last:border-0 hover:bg-ink-800/40">
                <td className="px-2.5 py-1.5 font-mono text-[0.75rem] text-text-faint">{row.serial}</td>
                <td className="whitespace-nowrap px-2.5 py-1.5 font-mono text-[0.75rem] text-text-secondary">{row.ref}</td>
                <NumCell value={row.w1} onChange={(value) => editRow(row.serial, { w1: value })} label={`Width 1 for ${row.ref}`} />
                <NumCell value={row.h1} onChange={(value) => editRow(row.serial, { h1: value })} label={`Height 1 for ${row.ref}`} />
                <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-faint/80 tabular-nums">
                  {row.w2 > 0 ? row.w2 : "—"}
                </td>
                <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-faint/80 tabular-nums">
                  {row.h2 > 0 ? row.h2 : "—"}
                </td>
                <NumCell
                  value={row.qty}
                  onChange={(value) => editRow(row.serial, { qty: value })}
                  label={`Quantity for ${row.ref}`}
                  step={1}
                />
                <td className="whitespace-nowrap px-2.5 py-1.5 text-right tabular-nums">
                  <span className={cn("font-mono text-[0.75rem]", row.floored ? "text-brand-100" : "text-text-muted")}>
                    {row.billable.toFixed(2)}
                  </span>
                  {row.floored ? (
                    <span
                      className="ml-1.5 rounded-full border border-brand-400/45 bg-brand-400/12 px-1.5 py-px font-mono text-[0.625rem] uppercase tracking-[0.08em] text-brand-100"
                      title={`${row.physical.toFixed(3)} m² of glass, billed at the ${MIN_PANEL_SQM} m² floor. The rate does not change.`}
                    >
                      floor
                    </span>
                  ) : null}
                </td>
                <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-muted tabular-nums">
                  {row.totalSqm.toFixed(2)}
                </td>
                <td className="whitespace-nowrap px-2.5 py-1.5 text-right tabular-nums">
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={row.rate}
                    onChange={(event) => editRate(row.serial, Number(event.target.value))}
                    aria-label={`Rate for ${row.ref}`}
                    className={cn(
                      "w-20 rounded-md border bg-ink-950/70 px-1.5 py-0.5 text-right font-mono text-[0.75rem] tabular-nums outline-none",
                      row.surcharged
                        ? "border-money-positive/50 text-money-positive focus:border-money-positive/70"
                        : "border-line-strong/50 text-text-secondary focus:border-brand-400/60",
                    )}
                  />
                  {row.surcharged ? (
                    <span
                      className="ml-1.5 rounded-full border border-money-positive/45 bg-money-positive/12 px-1.5 py-px font-mono text-[0.625rem] uppercase tracking-[0.08em] text-money-positive"
                      title={`Panel reaches ${SURCHARGE_THRESHOLD_SQM} m², so the row prices at SurchargeRate — base ${cash(block.baseRate)} + ${block.surchargePercent}%.`}
                    >
                      +{block.surchargePercent}%
                    </span>
                  ) : null}
                </td>
                <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] font-medium text-text-primary tabular-nums">
                  {cash(row.value)}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot className="sticky bottom-0 bg-ink-900/95 backdrop-blur">
            <tr className="border-t border-line/60">
              <td colSpan={6} className="px-2.5 py-2 text-[0.75rem] font-medium text-text-secondary">
                {block.rows.length} rows · {editedCount(edited)} edited
              </td>
              <td className="px-2.5 py-2 text-right font-mono text-[0.75rem] font-semibold text-text-secondary tabular-nums">
                {totals.qty}
              </td>
              <td aria-hidden="true" />
              <td className="px-2.5 py-2 text-right font-mono text-[0.75rem] font-semibold text-text-secondary tabular-nums">
                {totals.sqm.toFixed(2)}
              </td>
              <td className="px-2.5 py-2 text-right font-mono text-[0.6875rem] uppercase tracking-[0.1em] text-text-faint">Total</td>
              <td className="px-2.5 py-2 text-right font-mono text-[0.8125rem] font-semibold text-text-primary tabular-nums">
                {cash(totals.value)}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>

      {/* ── status line, the app's own guidance row ─────────────────────── */}
      <p aria-live="polite" className="text-[0.75rem] leading-relaxed text-text-muted font-medium">
        {status}
      </p>

      {/* ── the two rules, reporting on the rows on screen ──────────────── */}
      {!compact ? (
        <div className="grid gap-2 sm:grid-cols-2">
          <RuleNote
            label={`Minimum area · ${MIN_PANEL_SQM} m²`}
            body={
              flooredRows.length > 0
                ? `${flooredRows.length} row(s) on this specification are below the floor and bill ${MIN_PANEL_SQM} m² each: ${flooredRows
                    .map((row) => row.ref)
                    .join(", ")}. Their rate is untouched — the floor is on the area, not on the price.`
                : `No row here is under the floor: the smallest panel on this specification is ${Math.min(
                    ...priced.map((row) => row.physical),
                  ).toFixed(2)} m². Type a width or height down and the SQM column stops at ${MIN_PANEL_SQM} while the rate holds.`
            }
          />
          <RuleNote
            label={`Large-panel rate · +${block.surchargePercent}% at ${SURCHARGE_THRESHOLD_SQM} m²`}
            body={
              surchargedRows.length > 0
                ? `${surchargedRows.length} row(s) at or past ${SURCHARGE_THRESHOLD_SQM} m² price at ${cash(
                    block.baseRate * (1 + block.surchargePercent / 100),
                  )} / m² instead of ${cash(block.baseRate)}: ${surchargedRows.map((row) => row.ref).join(", ")}.`
                : `Nothing on this specification reaches ${SURCHARGE_THRESHOLD_SQM} m² yet, so every row is on the base rate of ${cash(
                    block.baseRate,
                  )}. Type a width up past the switch and that one row re-prices to ${cash(
                    block.baseRate * (1 + block.surchargePercent / 100),
                  )} / m² — the rows beside it do not move.`
            }
          />
        </div>
      ) : null}

      {/* ── the optimization result ─────────────────────────────────────── */}
      {run && showResult ? (
        <div className="rounded-xl border border-line/60 bg-ink-900/50 p-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-brand-300 font-medium">
              Optimization result · summary
            </p>
            <p className="font-mono text-[0.6875rem] text-text-faint">
              {spec.sheet.label} · {spec.sheet.w} × {spec.sheet.h} mm · kerf {optimizerLimits.kerf} mm
            </p>
          </div>
          <div className="mt-2 grid gap-2 sm:grid-cols-4">
            <ReadOutMini label="Sheets" value={String(run.sheets)} note={`${run.placed} pieces placed`} />
            <ReadOutMini label="Utilisation" value={`${run.utilization.toFixed(2)}%`} note={`${run.partArea.toFixed(2)} of ${run.stockArea.toFixed(2)} m²`} />
            <ReadOutMini label="Unplaced" value={String(run.unplaced)} note={run.unplaced > 0 ? "No plate could hold these" : "Every piece found a plate"} />
            <ReadOutMini
              label="Glass cost / m²"
              value={cash(totals.sqm > 0 ? totals.cost / totals.sqm : 0)}
              note={`on ${totals.sqm.toFixed(2)} m² billable`}
            />
          </div>
          {plates.length > 0 ? (
            <div className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
              {plates.slice(0, 4).map((plate) => (
                <figure key={plate.plate} className="rounded-lg border border-line/50 bg-ink-950/60 p-2">
                  <PlateDrawing plate={plate} />
                  <figcaption className="mt-1.5 font-mono text-[0.6875rem] text-text-faint">
                    Plate {plate.plate} · {plate.pieceCount} pieces · {plate.utilizationPct.toFixed(1)}%
                  </figcaption>
                </figure>
              ))}
            </div>
          ) : null}
          <p className="mt-2 text-[0.6875rem] leading-snug text-text-muted font-medium">
            The packing is this site&apos;s own guillotine packer, shared with the optimization page. The desktop engine searches five
            families and six plate patterns where this is one; the sheet count here is arithmetic over the pieces actually placed.
          </p>
        </div>
      ) : null}

      {!compact ? (
        <p className="text-[0.6875rem] leading-relaxed text-text-faint font-medium">{spec.columnsHint}</p>
      ) : null}
    </div>
  );
}

/* ── pieces ─────────────────────────────────────────────────────────── */

function NumCell({
  value,
  onChange,
  label,
  max = 10000,
  step = 10,
}: {
  value: number;
  onChange: (next: number) => void;
  label: string;
  /** `SpecGroupService.ValidateRow` caps a dimension at 10 000 mm. */
  max?: number;
  step?: number;
}) {
  return (
    <td className="px-2 py-1.5 text-right">
      <input
        type="number"
        min="0"
        max={max}
        step={step}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        aria-label={label}
        className="w-16 rounded-md border border-line-strong/50 bg-ink-950/70 px-1.5 py-0.5 text-right font-mono text-[0.75rem] tabular-nums text-text-secondary outline-none focus:border-brand-400/60"
      />
    </td>
  );
}

function RuleNote({ label, body }: { label: string; body: string }) {
  return (
    <div className="rounded-xl border border-line/50 bg-ink-900/40 px-3 py-2">
      <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-brand-300 font-medium">{label}</p>
      <p className="mt-1 text-[0.75rem] leading-relaxed text-text-muted font-medium">{body}</p>
    </div>
  );
}

function ReadOutMini({ label, value, note }: { label: string; value: string; note: string }) {
  return (
    <div className="rounded-lg border border-line/50 bg-ink-950/50 px-2.5 py-2">
      <p className="font-mono text-[0.625rem] uppercase tracking-[0.1em] text-text-faint font-medium">{label}</p>
      <p className="mt-0.5 text-[0.875rem] font-semibold tabular-nums text-text-primary">{value}</p>
      <p className="text-[0.625rem] leading-snug text-text-muted font-medium">{note}</p>
    </div>
  );
}

/**
 * A plate, drawn from the run.
 *
 * Placed pieces are drawn where the packer put them, in millimetres scaled to
 * the plate, so the drawing is the run rather than a picture of one.
 */
function PlateDrawing({ plate }: { plate: ReturnType<typeof plateViews>[number] }) {
  const viewBox = `0 0 ${plate.widthMm} ${plate.heightMm}`;
  return (
    <svg
      viewBox={viewBox}
      role="img"
      aria-label={`Plate ${plate.plate} with ${plate.pieceCount} pieces placed`}
      className="h-auto w-full rounded border border-line/50 bg-ink-950"
    >
      <rect x="0" y="0" width={plate.widthMm} height={plate.heightMm} fill="rgba(34,211,238,0.04)" />
      {plate.pieces.map((piece) => (
        <rect
          key={`${piece.id}-${piece.sequence}`}
          x={piece.x}
          y={piece.y}
          width={piece.width}
          height={piece.height}
          fill="rgba(139,92,246,0.22)"
          stroke="rgba(196,181,253,0.75)"
          strokeWidth={Math.max(2, plate.widthMm / 400)}
        />
      ))}
    </svg>
  );
}

/* ── small helpers ──────────────────────────────────────────────────── */

function cash(value: number) {
  return value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function editedCount(edited: Record<string, boolean>) {
  return Object.values(edited).filter(Boolean).length;
}
