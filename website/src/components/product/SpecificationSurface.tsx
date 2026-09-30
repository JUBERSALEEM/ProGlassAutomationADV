"use client";

import { useCallback, useMemo, useState, type Dispatch, type SetStateAction } from "react";
import { Pill, SampleTag } from "@/components/product/previewAtoms";
import { money, sheetForRef } from "@/data/workedExample";
import {
  billablePanelSqm,
  buildOptions,
  glassBuilds,
  step4RatePerSqm,
  unsupportedBuilds,
  type GlassBuildCode,
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
import { useT } from "@/i18n/provider";

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
  /**
   * `GlassItemRow.Rate` as saved on the document.
   *
   * The grid opens on the specification's base rate — one rate for the whole
   * specification, which is what the screen actually shows. `UpdateAllRowRates`
   * writes that figure down every row every time BASE or SURCHARGE % moves, so
   * a specification does not hold a different price per line: it holds one
   * price, and the rows that reach 4 m² take the surcharge rate instead.
   */
  rate: number;
  /**
   * `GlassItemRow.SuppressRateAutoAdjust` — set the moment somebody types a rate
   * into the RATE cell by hand. From then on the row keeps that figure whatever
   * BASE, SURCHARGE % or its own dimensions do, which is why a saved quotation's
   * manually-priced lines survive a reload. `null` means "follow the
   * specification", and the two rules apply again.
   */
  rateOverride?: number | null;
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
  /** The biggest piece the run had to place — what decides the plan. */
  largest: string;
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
  const t = useT();
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
  /**
   * `Dg.GenerateRows` — how many rows `GENERATE` adds.
   *
   * The application's own control is a number box with the same tooltip, in
   * the grid's footer beside `+ ADD ROW`, and it caps at 10,000 rows. This cap
   * is lower on purpose: ten thousand rows of a table is not a preview of
   * anything, and the count is printed so the visitor can see the limit rather
   * than be silently clipped by it.
   */
  const [generateCount, setGenerateCount] = useState(5);
  /** `Ctrl+F` — the find box, matching against glass reference and size. */
  const [find, setFind] = useState("");
  /** `Ctrl+H` — find and replace over the GLASS REF column. */
  const [replaceOpen, setReplaceOpen] = useState(false);
  const [replaceWith, setReplaceWith] = useState("CT-06");
  /**
   * `Ctrl+V` — the paste pad.
   *
   * The application takes a paste straight from the clipboard with the cell's
   * own context menu (`PasteExcelCommand`, "Paste — click a cell first, then
   * Ctrl+V"). A web page can read the clipboard only after the visitor grants
   * it, so the button tries that first and falls back to the visible pad, which
   * is the same paste with one more click.
   */
  const [pasteOpen, setPasteOpen] = useState(false);
  const [pasteText, setPasteText] = useState("");
  const [clipboardNote, setClipboardNote] = useState<string | null>(null);
  /** The optimizer's narration, while a run is being prepared. */
  const [phase, setPhase] = useState<{ step: number; label: string } | null>(null);

  const block = blocks[active] ?? blocks[0]!;
  const rows = block.rows;
  /** DGU, LAM and DGU + LAM have an inner pane; SGU does not. */
  const hasInnerPane = block.glassType !== "SGU";

  /**
   * The Step 4 selection behind the specification — the panel above the grid.
   *
   * A specification is not just a rate: it is a *build*. Switching SGU → DGU →
   * LAM → DGU + LAM changes which panes the rows carry (a DGU has an outer and
   * an inner, so W2/H2 come alive), what the generated specification name says
   * (the application composes it from the panel fields — `DguParametersViewModel
   * .GeneratedSpecName` is `"{outer} + {spacer} + {inner}"`), and what the base
   * rate opens at, because the estimator prices the inner pane and the spacer on
   * top of the outer sheet's own rate.
   *
   * That is the piece that was missing: the glass type used to be a dropdown
   * that changed a word and nothing else.
   */
  const [build, setBuild] = useState<Record<string, string>>({
    thickness: "6mm",
    colour: "Clear",
    treatment: "FT",
    innerThickness: "6mm",
    innerColour: "Clear",
    innerTreatment: "FT",
    spacerWidth: "12mm ASP",
    spacerMaterial: "Normal Mill Aluminum",
    spacerExtra: "None",
    panelExtra: "None",
    pvb: "1.52mm Clear PVB",
    lamThickness: "6mm",
    lamColour: "Clear",
    lamTreatment: "FT",
    interlayer: "1.52mm Clear PVB",
    polish: "None (Standard)",
  });

  /* ── the arithmetic ────────────────────────────────────────────────── */

  /*
   * The one place a row's price is decided.
   *
   * `effective` is `SuppressRateAutoAdjust ? typed rate : Sqm1 >= 4 ? SurchargeRate : BaseRate`
   * — the application's own expression. Every figure on the screen is read from
   * this map, so there is no second copy of the arithmetic to fall out of step
   * with it: type a panel past 4 m² and the RATE, the VALUE, the footer and the
   * banner all move in the same frame, because they are all the same number.
   *
   * Before this, the RATE column showed the live `rowRate` while the VALUE
   * column multiplied by the row's stored rate. The two only agreed until
   * somebody typed into the grid — which is exactly the state a visitor reaches
   * in about four seconds, and the reason the base rate and the surcharge looked
   * like they were not applying.
   */
  const priced = useMemo(
    () =>
      rows.map((row) => {
        const physical = physicalPanelSqm(row.w1, row.h1);
        const billable = billablePanelSqm(row.w1, row.h1);
        const specRate = rowRate(block.baseRate, block.surchargePercent, billable);
        const overridden = typeof row.rateOverride === "number";
        const effective = overridden ? moneyRate(row.rateOverride as number) : specRate;
        const totalSqm = billable * row.qty;
        return {
          ...row,
          physical,
          billable,
          floored: isFloored(row.w1, row.h1),
          surcharged: surchargeApplies(billable),
          overridden,
          /** What the two rules say this row should cost, override or not. */
          specRate,
          /** What it is actually charged at — what the RATE cell prints. */
          effective,
          totalSqm,
          value: money(totalSqm * effective),
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

  /**
   * The rollup across every specification on the document.
   *
   * Summed from each row's own VALUE cell — `Sqm1 × Qty × Rate`, which is the
   * figure the grid prints and its footer totals. Not from the specification's
   * base rate: one specification spans several glasses at several rates (the
   * seeded job's main facade runs 6, 8, 10 and 12 mm at 150 / 185 / 210 / 245),
   * so a rollup that re-priced every row at the base rate would print a total
   * that no footer on the screen agrees with. The desktop view aggregates the
   * grid's own footers for exactly this reason.
   */
  const specRollup = useCallback(
    (entry: SpecBlockInput) =>
      entry.rows.reduce(
        (sum, row) => {
          const billable = billablePanelSqm(row.w1, row.h1);
          const effective =
            typeof row.rateOverride === "number"
              ? moneyRate(row.rateOverride)
              : rowRate(entry.baseRate, entry.surchargePercent, billable);
          return {
            qty: sum.qty + row.qty,
            sqm: sum.sqm + billable * row.qty,
            value: sum.value + money(billable * row.qty * effective),
          };
        },
        { qty: 0, sqm: 0, value: 0 },
      ),
    [],
  );

  const grand = useMemo(
    () =>
      blocks.reduce(
        (acc, entry) => {
          const own = specRollup(entry);
          return { qty: acc.qty + own.qty, sqm: acc.sqm + own.sqm, value: acc.value + own.value };
        },
        { qty: 0, sqm: 0, value: 0 },
      ),
    [blocks, specRollup],
  );

  const flooredRows = priced.filter((row) => row.floored);
  const surchargedRows = priced.filter((row) => row.surcharged);

  /* ── the grid's own editing commands ──────────────────────────────── */

  /**
   * `Dg.Generate` — add `generateCount` rows at once.
   *
   * The application's tooltip says "Number of rows to generate (max 10,000)",
   * and the rows come out blank-but-human: a reference, a size under the floor
   * and a quantity of one, ready to be typed over. That is what this does, so
   * the visitor sees the SQM column sitting at the floor on every new line and
   * can watch the rate switch as soon as a size passes 4 m².
   */
  function generateRows() {
    const count = Math.max(1, Math.min(50, Math.floor(generateCount) || 1));
    const first = (block.rows.at(-1)?.serial ?? 0) + 1;
    const added: SpecRowInput[] = Array.from({ length: count }, (_, index) => ({
      serial: first + index,
      ref: `CT-06-0600x0400`,
      w1: 600,
      h1: 400,
      w2: 0,
      h2: 0,
      qty: 1,
      rate: block.baseRate,
      rateOverride: null,
    }));
    setBlocks((current) =>
      current.map((entry, index) => (index === active ? { ...entry, rows: [...entry.rows, ...added] } : entry)),
    );
    setStatus(
      `Generated ${count} row${count === 1 ? "" : "s"} — serials ${first} to ${first + count - 1}, each 600 × 400 mm at the specification's base rate. The application's box takes up to 10,000 rows; this preview stops at 50 so the grid stays readable.`,
    );
  }

  /** `Dg.DeleteRow` — the bin at the end of a row. */
  function deleteRow(serial: number) {
    const gone = block.rows.find((row) => row.serial === serial);
    setBlocks((current) =>
      current.map((entry, index) =>
        index !== active
          ? entry
          : {
              ...entry,
              // Serials are renumbered the way the grid renumbers them, so the
              // # column stays 1..n instead of growing holes.
              rows: entry.rows.filter((row) => row.serial !== serial).map((row, position) => ({ ...row, serial: position + 1 })),
            },
      ),
    );
    setStatus(`Row deleted — ${gone?.ref ?? `serial ${serial}`} is off the specification. The grid renumbered the rows behind it.`);
  }

  /** `Dg.FindReplace` — replace text across the GLASS REF column. */
  function replaceAll() {
    const needle = find.trim();
    if (!needle) {
      setStatus("Type what to find first, then press REPLACE ALL — the application's Find and Replace works the same way.");
      return;
    }
    let changed = 0;
    setBlocks((current) =>
      current.map((entry, index) =>
        index !== active
          ? entry
          : {
              ...entry,
              rows: entry.rows.map((row) => {
                if (!row.ref.includes(needle)) return row;
                changed += 1;
                return { ...row, ref: row.ref.split(needle).join(replaceWith) };
              }),
            },
      ),
    );
    setStatus(
      changed > 0
        ? `Find and replace: ${changed} glass reference${changed === 1 ? "" : "s"} changed from “${needle}” to “${replaceWith}”. The reference is what the sheet a line is cut from is read off, so a wrong one prices to the wrong stock row.`
        : `No glass reference on specification ${block.number} contains “${needle}”.`,
    );
  }

  /**
   * `Dg.PasteHint` — paste a block of rows.
   *
   * The application's `PasteExcelCommand` reads REF / W1 / H1 / W2 / H2 / QTY
   * straight from the clipboard. This parses the same six tab-separated fields
   * out of whatever is pasted, and takes either route: the clipboard, if the
   * browser will give it up, or the visible pad if it will not.
   */
  function applyPaste(text: string) {
    const rows = text
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter(Boolean)
      .map((line) => line.split(/\t|,|;|\s{2,}/).map((cell) => cell.trim()))
      .filter((cells) => cells.length >= 3);

    if (rows.length === 0) {
      setStatus("Nothing to paste yet. Copy rows from a BOQ or a spreadsheet as REF, W1, H1, W2, H2, QTY and paste them here — tabs, commas or runs of spaces all separate the fields.");
      return;
    }

    const first = (block.rows.at(-1)?.serial ?? 0) + 1;
    let skipped = 0;
    const added: SpecRowInput[] = [];
    rows.forEach((cells, index) => {
      const [ref = "", w1 = "", h1 = "", w2 = "", h2 = "", qty = "1"] = cells;
      const width = Number(w1.replace(/[^0-9.]/g, ""));
      const height = Number(h1.replace(/[^0-9.]/g, ""));
      if (!ref || !width || !height) {
        skipped += 1;
        return;
      }
      added.push({
        serial: first + added.length,
        ref,
        w1: width,
        h1: height,
        w2: Number(w2.replace(/[^0-9.]/g, "")) || 0,
        h2: Number(h2.replace(/[^0-9.]/g, "")) || 0,
        qty: Math.max(1, Number(qty.replace(/[^0-9.]/g, "")) || 1),
        rate: block.baseRate,
      });
      void index;
    });

    if (added.length === 0) {
      setStatus("No row in that paste had a reference, a width and a height. The application imports REF / W1 / H1 / W2 / H2 / QTY in that order.");
      return;
    }

    setBlocks((current) =>
      current.map((entry, index) => (index === active ? { ...entry, rows: [...entry.rows, ...added] } : entry)),
    );
    setPasteOpen(false);
    setPasteText("");
    setStatus(
      `Pasted ${added.length} row${added.length === 1 ? "" : "s"} onto specification ${block.number}${skipped > 0 ? `, and skipped ${skipped} that had no reference, width or height` : ""}. Every pasted row starts at the specification's base rate of ${block.baseRate.toFixed(2)} and takes the surcharge if its panel reaches ${SURCHARGE_THRESHOLD_SQM} m².`,
    );
  }

  async function pasteFromClipboard() {
    try {
      const text = await navigator.clipboard.readText();
      if (!text.trim()) {
        setPasteOpen(true);
        setClipboardNote("Your clipboard is empty — copy the rows, or type them into the pad below.");
        return;
      }
      setClipboardNote(null);
      applyPaste(text);
    } catch {
      // Firefox, a denied prompt, or a page without clipboard permission.
      setPasteOpen(true);
      setClipboardNote("This browser did not hand over the clipboard, so the pad below is the same paste with one more click.");
    }
  }

  /* ── the optimizer, run for real ───────────────────────────────────── */

  function optimise(kind: "QUICK" | "OPTIMIZE") {
    const parts = priced.flatMap((row) =>
      Array.from({ length: Math.max(0, Math.min(row.qty, 24)) }, (_, index) => ({
        id: `${row.ref}-${index + 1}`,
        width: row.w1,
        height: row.h1,
      })),
    );

    /*
     * The run, narrated the way the application narrates it.
     *
     * The desktop grid shows a real progress bar while the engine searches —
     * "Optimization running…", a detail line, a percentage, and the sentence
     * "Please wait — cutting sheets… You can keep editing, but do not close
     * this invoice until the bar reaches 100%." The packing here finishes in
     * one tick, so the steps are shown for their duration rather than padded:
     * each label names something the run genuinely does, and the bar is gone
     * the moment the plates are ready. It is a state readout, not a delay.
     */
    const stages =
      kind === "QUICK"
        ? ["Reading the specification", "Selecting the sheet", "Packing the pieces"]
        : ["Reading the specification", "Selecting the sheet", "Packing the pieces", "Choosing the best plan"];

    setPhase({ step: 1, label: stages[0] ?? "" });
    setStatus(
      kind === "QUICK"
        ? "QUICK pass running — one fast cutting pass over this specification."
        : "OPTIMIZE running — deepest search over this specification: fewest unplaced, then fewest sheets, then highest utilisation. Please wait — cutting sheets… You can keep editing, but do not close this invoice until the bar reaches 100%.",
    );

    stages.slice(1).forEach((label, index) => {
      window.setTimeout(() => setPhase({ step: index + 2, label }), 220 * (index + 1));
    });

    window.setTimeout(() => {
      const result = packGuillotine(parts, { width: spec.sheet.w, height: spec.sheet.h }, {
        left: optimizerLimits.marginLeft,
        right: optimizerLimits.marginRight,
        top: optimizerLimits.marginTop,
        bottom: optimizerLimits.marginBottom,
        kerf: optimizerLimits.kerf,
      });
      const cheapest = [...parts].sort((a, b) => b.width * b.height - a.width * a.height)[0];
      setPhase(null);
      setRun({
        kind,
        sheets: result.plates,
        utilization: result.utilizationPct,
        placed: result.placedPieces,
        unplaced: result.unplaced.length,
        partArea: result.partAreaSqm,
        stockArea: result.stockAreaSqm,
        largest: cheapest ? `${cheapest.width} × ${cheapest.height}` : "—",
      });
      setShowResult(kind === "OPTIMIZE");
      setStatus(
        kind === "QUICK"
          ? `QUICK pass done — ${result.plates} sheets of ${spec.sheet.label}, ${result.utilizationPct.toFixed(2)}% used. Step 6 takes the sheet quantity and utilisation from this run until you press OPTIMIZE.`
          : `OPTIMIZE search done — ${result.plates} sheets, ${result.utilizationPct.toFixed(2)}% used, ${result.unplaced.length} piece(s) no plate could hold. Open VIEW RESULT for the plate drawings.`,
      );
    }, 220 * stages.length + 120);
  }

  /* ── editing ───────────────────────────────────────────────────────── */

  /**
   * Writes a row back, applying the rate switch exactly where the application
   * applies it: on the row whose size changed, and only if a person has not
   * typed a rate over it by hand (`SuppressRateAutoAdjust`).
   */
  function editRow(serial: number, patch: Partial<Pick<SpecRowInput, "w1" | "h1" | "w2" | "h2" | "qty">>) {
    setBlocks((current) =>
      current.map((entry, index) => {
        if (index !== active) return entry;
        return {
          ...entry,
          rows: entry.rows.map((row) => {
            if (row.serial !== serial) return row;
            const next = { ...row, ...patch };
            /*
             * A row that was typed over by hand keeps its price only while its
             * dimensions are left alone. Resizing it puts it back under the two
             * rules — the same thing the application does when a size edit
             * arrives, and the reason the rate visibly switches on the row you
             * are editing rather than silently staying put.
             */
            const billable = billablePanelSqm(next.w1, next.h1);
            return { ...next, rate: rowRate(entry.baseRate, entry.surchargePercent, billable), rateOverride: null };
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

  /**
   * The specification's generated name, composed the way the panel composes it.
   *
   * Read from `glassBuilds`, whose `name` functions are the four view-models'
   * own `GeneratedSpecName` expressions — so the string on screen is built by
   * the same rule as the string in the application, field order included.
   */
  const generatedName = useMemo(() => {
    const entry = glassBuilds.find((build2) => build2.code === block.glassType);
    return entry ? entry.name(build) : block.name;
  }, [block.glassType, block.name, build]);

  /**
   * `SelectGlassTypeCard` + `ApplyStep4PricingSnapshot` — switch the build.
   *
   * Three things move together, which is the whole point of the handoff:
   *
   *   the type      the specification is now an SGU / DGU / LAM / DGU+LAM
   *   the rate      re-derived from the sheet the outer pane is cut from, plus
   *                 the inner pane and spacer the build adds
   *   the rows      a build that has no second pane clears W2/H2 rather than
   *                 leaving stale figures in columns the grid is no longer
   *                 showing, and every row re-prices from the new base
   *
   * A row typed over by hand is cleared here too, because the application clears
   * the manual markers on a Step 4 handoff: the specification was re-specified,
   * so its prices are the specification's again.
   */
  function selectBuild(code: GlassBuildCode) {
    const rate = step4RatePerSqm(
      code,
      build.thickness ?? "6mm",
      build.colour ?? "Clear",
      code === "DGU" || code === "DGU+LAM",
    );
    const carriesInner = code === "DGU" || code === "LAM" || code === "DGU+LAM";
    const sealed = code === "DGU" || code === "DGU+LAM";
    setBlocks((current) =>
      current.map((entry, index) => {
        if (index !== active) return entry;
        return {
          ...entry,
          glassType: code,
          baseRate: rate,
          rows: entry.rows.map((row) => ({
            ...row,
            w2: carriesInner ? row.w2 : 0,
            h2: carriesInner ? row.h2 : 0,
            rateOverride: null,
            rate: rowRate(rate, entry.surchargePercent, billablePanelSqm(row.w1, row.h1)),
          })),
        };
      }),
    );
    setStatus(
      `Glass type set to ${code}. Base rate re-derived to ${cash(rate)} /m² from the ${build.thickness} ${build.colour} outer sheet${
        sealed ? `, the inner pane and the ${build.spacerWidth}` : code === "LAM" ? ", the PVB interlayer and the inner pane" : ""
      }. ${
        carriesInner ? "W2 / H2 are live on every row" : "W2 / H2 cleared — a single pane has no second pane to cut"
      }, all ${block.rows.length} rows re-priced at the new base, and the generated specification name is now “${glassBuilds.find((entry) => entry.code === code)?.name(build) ?? ""}”.`,
    );
  }

  function editBuild(patch: Record<string, string>) {
    const next = { ...build, ...patch };
    setBuild(next);
    const code = block.glassType as GlassBuildCode;
    const rate = step4RatePerSqm(code, next.thickness ?? "6mm", next.colour ?? "Clear", code === "DGU" || code === "DGU+LAM");
    setBlocks((current) =>
      current.map((entry, index) =>
        index !== active
          ? entry
          : {
              ...entry,
              baseRate: rate,
              rows: entry.rows.map((row) => ({
                ...row,
                rateOverride: null,
                rate: rowRate(rate, entry.surchargePercent, billablePanelSqm(row.w1, row.h1)),
              })),
            },
      ),
    );
    setStatus(
      `Panel updated — ${Object.keys(patch)[0]} set to ${Object.values(patch)[0]}. The base rate is now ${cash(rate)} /m² and every row has re-priced. The generated specification name follows the panel, so the document and the panel say the same thing.`,
    );
  }

  function editRate(serial: number, value: number) {
    setBlocks((current) =>
      current.map((entry, index) =>
        index !== active
          ? entry
          : {
              ...entry,
              rows: entry.rows.map((row) => (row.serial === serial ? { ...row, rate: value, rateOverride: value } : row)),
            },
      ),
    );
    const row = rows.find((entry) => entry.serial === serial);
    const specRate = row ? rowRate(block.baseRate, block.surchargePercent, billablePanelSqm(row.w1, row.h1)) : 0;
    setStatus(
      `${row?.ref ?? "Row"} typed over to ${cash(value)} — ${
        Math.abs(value - specRate) < 0.005 ? "the same as" : value > specRate ? "above" : "below"
      } the ${cash(specRate)} the specification's own rules would charge. The application leaves a typed rate alone until the size changes again, and moving BASE or SURCHARGE % clears it back to the rules.`,
    );
  }

  /**
   * `UpdateAllRowRates` — a change to BASE or SURCHARGE % re-prices every row.
   *
   * This is the behaviour that was missing: the two fields at the top of the
   * specification are the specification's price, and moving either one writes
   * the recalculated rate down every row that has not been typed over. So the
   * RATE column, the VALUE column, the footer totals and the profit-and-loss
   * screen all move on the keystroke — and only the rows that have actually
   * reached 4 m² take the surcharge rate, because the switch is evaluated per
   * row against that row's own billable area.
   */
  function editBlock(patch: Partial<Pick<SpecBlockInput, "baseRate" | "surchargePercent">>) {
    const next = { ...block, ...patch };
    setBlocks((current) =>
      current.map((entry, index) => {
        if (index !== active) return entry;
        return {
          ...entry,
          ...patch,
          rows: entry.rows.map((row) => ({
            ...row,
            rateOverride: null,
            rate: rowRate(next.baseRate, next.surchargePercent, billablePanelSqm(row.w1, row.h1)),
          })),
        };
      }),
    );
    const onSurcharge = block.rows.filter((row) => surchargeApplies(billablePanelSqm(row.w1, row.h1))).length;
    setStatus(
      `Specification ${block.number} re-priced — base ${cash(next.baseRate)}, surcharge ${next.surchargePercent}%. One rate for the whole specification; the ${
        onSurcharge === 1 ? "1 row" : `${onSurcharge} rows`
      } at or above ${SURCHARGE_THRESHOLD_SQM} m² now price at ${cash(next.baseRate * (1 + next.surchargePercent / 100))}. Every row, the totals and the profit-and-loss figure above moved with it.`,
    );
  }

  function importBoq() {
    setBlocks(spec.blocks);
    setActive(0);
    setEdited({});
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
      rateOverride: null,
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
              <span className="flex items-center gap-1" role="group" aria-label={t("Specification blocks")}>
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
              <p className="text-[0.8125rem] font-semibold text-text-primary">{t(block.name)}</p>
              <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
                Specification {block.number}
                {block.primary ? " · from the inquiry BOQ" : " · added in this preview"}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1.5">
            <Pill title="Specification blocks on this document. `Dg.Specs`.">
              {t("Specs")}
              <span className="rounded-full bg-brand-400/20 px-1.5 font-mono text-[0.6875rem] text-brand-100">
                {blocks.length}
              </span>
            </Pill>
            <Pill onClick={addSpec} title="Add a specification block. `Dg.AddSpec`.">
              + Add spec
            </Pill>
          </div>
        </div>

        {/*
          ── Step 4 — the build, and the panes it is made of ─────────────
          The four build families are buttons, because switching between them is
          the thing worth doing: SGU has one pane, DGU and DGU + LAM add an inner
          pane and a spacer, LAM bonds two panes across PVB. Pick one and the
          base rate, the pane columns and the generated specification name all
          follow. Underneath it, the panel the application shows for that build —
          the outer pane, the inner pane, and the ASP spacer with its material
          and its U-insert variant.
        */}
        <div className="mt-3 rounded-xl border border-line/60 bg-ink-950/40 p-2.5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
              {t("Step 4 · glass type and panes")}
            </p>
            <p className="text-[0.6875rem] font-medium text-text-faint">
              {glassBuilds.find((entry) => entry.code === block.glassType)?.panes ?? ""} ·{" "}
              {glassBuilds.find((entry) => entry.code === block.glassType)?.long ?? ""}
            </p>
          </div>

          <div className="mt-2 flex flex-wrap gap-1.5" role="group" aria-label={t("Glass type")}>
            {glassBuilds.map((entry) => (
              <button
                key={entry.code}
                type="button"
                aria-pressed={block.glassType === entry.code}
                onClick={() => selectBuild(entry.code)}
                title={`${entry.long} — ${entry.panes}`}
                className={cn(
                  "rounded-lg border px-2.5 py-1 text-[0.75rem] font-semibold transition-colors duration-200",
                  block.glassType === entry.code
                    ? "border-brand-400/60 bg-brand-400/16 text-brand-100"
                    : "border-line-strong/60 bg-ink-900/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                )}
              >
                {t(entry.label)}
              </button>
            ))}
            {unsupportedBuilds.map((name) => (
              <span
                key={name}
                title={`${name} is not one of the four build families the application supports. An out-of-family name degrades with a warning rather than being priced silently.`}
                className="cursor-not-allowed rounded-lg border border-line/40 bg-ink-900/30 px-2.5 py-1 text-[0.75rem] font-medium text-text-faint line-through"
              >
                {name.split(" (")[0]}
              </span>
            ))}
          </div>

          {/*
            What the *other* three families bring, said on screen.
            The inner pane, the ASP spacer and the PVB interlayer only have a
            control when the build under them has one — that is how the desktop
            Step 4 panel works — so with SGU selected a visitor sees the outer
            pane and nothing telling them where the inner one went. One line
            under the buttons answers that without pretending an SGU has a
            spacer it does not have.
          */}
          <p className="mt-1.5 text-[0.6875rem] font-medium text-text-faint">
            {block.glassType === "SGU"
              ? "SGU is a single pane. Choose DGU or DGU + LAM for the inner pane and the ASP spacer, or LAM for the PVB interlayer — the panel below grows those controls with the build."
              : block.glassType === "LAM"
                ? "LAM bonds the outer pane to the inner one across PVB. There is no cavity, so no spacer: the interlayer and the inner pane are the controls below."
                : "The inner pane and the ASP spacer belong to this build — their thickness, colour, width, material and U-insert variant are all selectable below."}
          </p>

          {/* The generated name, exactly as the panel composes it. */}
          <p className="mt-2 flex flex-wrap items-baseline gap-2 rounded-lg border border-line/50 bg-ink-900/50 px-2.5 py-1.5">
            <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">Spec</span>
            <span className="font-mono text-[0.8125rem] font-semibold text-brand-100">{generatedName}</span>
          </p>

          <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            <BuildSelect
              label={t("Outer thickness")}
              value={build.thickness ?? "6mm"}
              options={buildOptions.thickness}
              onChange={(value) => editBuild({ thickness: value })}
            />
            <BuildSelect label={t("Outer colour")} value={build.colour ?? "Clear"} options={buildOptions.colour} onChange={(value) => editBuild({ colour: value })} />
            <BuildSelect
              label={t("Outer treatment")}
              value={build.treatment ?? "FT"}
              options={buildOptions.treatment}
              onChange={(value) => editBuild({ treatment: value })}
            />

            {block.glassType === "SGU" ? (
              <BuildSelect label={t("Polish")} value={build.polish ?? "None (Standard)"} options={buildOptions.polish} onChange={(value) => editBuild({ polish: value })} />
            ) : (
              <BuildSelect
                label={t("Inner thickness")}
                value={build.innerThickness ?? "6mm"}
                options={buildOptions.thickness}
                onChange={(value) => editBuild({ innerThickness: value })}
              />
            )}

            {block.glassType === "DGU" || block.glassType === "DGU+LAM" ? (
              <>
                <BuildSelect
                  label={t("Inner colour")}
                  value={build.innerColour ?? "Clear"}
                  options={buildOptions.colour}
                  onChange={(value) => editBuild({ innerColour: value })}
                />
                <BuildSelect
                  label={t("Inner treatment")}
                  value={build.innerTreatment ?? "FT"}
                  options={buildOptions.treatment}
                  onChange={(value) => editBuild({ innerTreatment: value })}
                />
                <BuildSelect
                  label={t("Spacer width")}
                  value={build.spacerWidth ?? "12mm ASP"}
                  options={buildOptions.spacerWidth}
                  onChange={(value) => editBuild({ spacerWidth: value })}
                />
                <BuildSelect
                  label={t("Spacer material")}
                  value={build.spacerMaterial ?? "Normal Mill Aluminum"}
                  options={buildOptions.spacerMaterial}
                  onChange={(value) => editBuild({ spacerMaterial: value })}
                />
                <BuildSelect
                  label={t("Spacer extra")}
                  value={build.spacerExtra ?? "None"}
                  options={buildOptions.spacerExtra}
                  onChange={(value) => editBuild({ spacerExtra: value })}
                />
              </>
            ) : null}

            {block.glassType === "LAM" || block.glassType === "DGU+LAM" ? (
              <>
                <BuildSelect
                  label={block.glassType === "LAM" ? "PVB interlayer" : "Lam interlayer"}
                  value={block.glassType === "LAM" ? build.pvb ?? "1.52mm Clear PVB" : build.interlayer ?? "1.52mm Clear PVB"}
                  options={["0.38mm Clear PVB", "0.76mm Clear PVB", "1.52mm Clear PVB", "1.52mm Grey PVB", "1.52mm White PVB"]}
                  onChange={(value) => editBuild(block.glassType === "LAM" ? { pvb: value } : { interlayer: value })}
                />
                <BuildSelect
                  label={t("Lamination polish")}
                  value={build.polish ?? "None (Standard)"}
                  options={buildOptions.polish}
                  onChange={(value) => editBuild({ polish: value })}
                />
              </>
            ) : null}

            <BuildSelect
              label={t("Panel extra")}
              value={build.panelExtra ?? "None"}
              options={buildOptions.panelExtra}
              onChange={(value) => editBuild({ panelExtra: value })}
            />
          </div>

          <p className="mt-2 text-[0.6875rem] leading-snug text-text-muted font-medium">
            The outer sheet rate comes from the sheet store — {cash(step4RatePerSqm(block.glassType as GlassBuildCode, build.thickness ?? "6mm", build.colour ?? "Clear", block.glassType === "DGU" || block.glassType === "DGU+LAM"))} / m² on
            this build. Changing any field above re-derives the specification&apos;s base rate and re-prices every row, the way
            <span className="mx-1 font-mono">ApplyStep4PricingSnapshot</span>
            {t("does on the Continue button.")}
          </p>
        </div>

        <div className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          <label className="block">
            <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">Base rate</span>
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
            <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">Surcharge %</span>
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
            <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
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
          {t("Import BOQ")}
        </Pill>
        <Pill active={exportOpen} onClick={() => setExportOpen((open) => !open)} title="Export the specification. `Dg.Export`.">
          Export ▾
        </Pill>
        <Pill onClick={addRow} title="Add a blank line to this specification. `Dg.AddRowShort`.">
          + Add row
        </Pill>
        <Pill onClick={pasteFromClipboard} title="Paste rows from Excel or a BOQ: REF / W1 / H1 / W2 / H2 / QTY. `Dg.PasteHint`.">
          {t("Paste")}
        </Pill>
        <Pill active={replaceOpen} onClick={() => setReplaceOpen((open) => !open)} title="Find and Replace across the grid. `Dg.FindReplace`.">
          Find &amp; replace
        </Pill>
        <Pill onClick={() => optimise("QUICK")} title="One fast pass. Step 6 reads the sheet quantity from it. `Dg.RunQuick`.">
          {t("Quick")}
        </Pill>
        <Pill onClick={() => optimise("OPTIMIZE")} title="Search the plans: fewest unplaced pieces, then fewest sheets, then highest utilisation. `Dg.RunOptimize`.">
          {t("Optimize")}
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
          {t("View result")}
        </Pill>
        {run ? (
          <Pill tone="floor" title={t("The run the result belongs to.")}>
            {run.kind} · {run.sheets} sheets · {run.utilization.toFixed(2)}%
          </Pill>
        ) : null}
      </div>

      {replaceOpen ? (
        <div className="flex flex-wrap items-end gap-2 rounded-xl border border-line/60 bg-ink-900/50 p-2.5">
          <label className="block">
            <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">Find</span>
            <input
              value={find}
              onChange={(event) => setFind(event.target.value)}
              placeholder="CT-06"
              className="mt-1 w-32 rounded-lg border border-line-strong/60 bg-ink-950/70 px-2 py-1 font-mono text-[0.75rem] text-text-primary outline-none focus:border-brand-400/60"
            />
          </label>
          <label className="block">
            <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">Replace with</span>
            <input
              value={replaceWith}
              onChange={(event) => setReplaceWith(event.target.value)}
              className="mt-1 w-32 rounded-lg border border-line-strong/60 bg-ink-950/70 px-2 py-1 font-mono text-[0.75rem] text-text-primary outline-none focus:border-brand-400/60"
            />
          </label>
          <Pill onClick={replaceAll} active={false}>
            {t("Replace all")}
          </Pill>
          <p className="text-[0.6875rem] leading-snug text-text-muted font-medium">
            {find.trim()
              ? `${block.rows.filter((row) => row.ref.includes(find.trim())).length} of ${block.rows.length} rows match “${find.trim()}” in GLASS REF.`
              : "Type a reference to see how many rows match, then replace them all."}
          </p>
        </div>
      ) : null}

      {pasteOpen ? (
        <div className="rounded-xl border border-line/60 bg-ink-900/50 p-2.5">
          {clipboardNote ? (
            <p className="mb-1.5 text-[0.6875rem] leading-snug text-text-muted font-medium">{clipboardNote}</p>
          ) : null}
          <label className="block">
            <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
              {t("Paste REF · W1 · H1 · W2 · H2 · QTY — one row per line, fields separated by tabs, commas or two spaces")}
            </span>
            <textarea
              value={pasteText}
              onChange={(event) => setPasteText(event.target.value)}
              rows={4}
              placeholder={"CT-06-1200x900\t1200\t900\t0\t0\t8\nCT-08-2000x1500\t2000\t1500\t0\t0\t4"}
              className="mt-1 w-full rounded-lg border border-line-strong/60 bg-ink-950/70 px-2 py-1.5 font-mono text-[0.75rem] text-text-primary outline-none focus:border-brand-400/60"
            />
          </label>
          <div className="mt-1.5 flex flex-wrap gap-1.5">
            <Pill onClick={() => applyPaste(pasteText)}>Apply paste</Pill>
            <Pill onClick={() => { setPasteOpen(false); setClipboardNote(null); }}>Close</Pill>
          </div>
        </div>
      ) : null}

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
            Specification {block.number}, {t(block.name)}: one row per glass line, priced at the specification&apos;s rate, or at the
            surcharge rate once a panel reaches {SURCHARGE_THRESHOLD_SQM} square metres.
          </caption>
          <thead className="sticky top-0 z-10 bg-ink-900/95 backdrop-blur">
            <tr className="border-b border-line/60">
              {["#", "Glass ref", "W1", "H1", "W2", "H2", "Qty", "Sqm", "Total sqm", "Rate", "Value", ""].map((heading, index) => (
                <th
                  key={heading}
                  scope="col"
                  className={cn(
                    "whitespace-nowrap px-2.5 py-2 font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium",
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
              <tr
                key={row.serial}
                className={cn(
                  "border-b border-line/40 last:border-0 hover:bg-ink-800/40",
                  find.trim() && row.ref.includes(find.trim()) ? "bg-brand-400/8" : "",
                )}
              >
                <td className="px-2.5 py-1.5 font-mono text-[0.75rem] text-text-faint">{row.serial}</td>
                <td className="whitespace-nowrap px-2.5 py-1.5 font-mono text-[0.75rem] text-text-secondary">{row.ref}</td>
                <NumCell value={row.w1} onChange={(value) => editRow(row.serial, { w1: value })} label={`Width 1 for ${row.ref}`} />
                <NumCell value={row.h1} onChange={(value) => editRow(row.serial, { h1: value })} label={`Height 1 for ${row.ref}`} />
                {/*
                  W2 / H2 are the inner pane. On a single-glazed specification
                  there is no inner pane to type into, so the cells print the dash
                  the grid prints — and on a DGU, LAM or DGU+LAM they are live,
                  because the build has a second pane and the floor has to cut it.
                */}
                {hasInnerPane ? (
                  <>
                    <NumCell value={row.w2} onChange={(value) => editRow(row.serial, { w2: value, h2: row.h2 })} label={`Inner width for ${row.ref}`} />
                    <NumCell value={row.h2} onChange={(value) => editRow(row.serial, { h2: value, w2: row.w2 })} label={`Inner height for ${row.ref}`} />
                  </>
                ) : (
                  <>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-faint tabular-nums">—</td>
                    <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] text-text-faint tabular-nums">—</td>
                  </>
                )}
                <NumCell
                  value={row.qty}
                  onChange={(value) => editRow(row.serial, { qty: value })}
                  label={`Quantity for ${row.ref}`}
                  step={1}
                />
                <td className="whitespace-nowrap px-2.5 py-1.5 text-right tabular-nums">
                  <span className={cn("font-mono text-[0.75rem]", row.floored ? "text-brand-100" : "text-text-muted")}>
                    {row.billable.toFixed(3)}
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
                  {row.totalSqm.toFixed(3)}
                </td>
                <td className="whitespace-nowrap px-2.5 py-1.5 text-right tabular-nums">
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    /*
                      The cell shows the rate the row is actually charged at, and
                      shows it immediately: a size typed past 4 m² swaps this
                      figure for the surcharge rate in the same frame, because it
                      is read from `effective` rather than from the stored
                      `rate`. Typing here is a manual override, and the chip
                      beside it says so — the application keeps a typed rate
                      until the dimensions move again.
                    */
                    value={row.effective}
                    onChange={(event) => editRate(row.serial, Number(event.target.value))}
                    aria-label={`Rate for ${row.ref}`}
                    className={cn(
                      "w-20 rounded-md border bg-ink-950/70 px-1.5 py-0.5 text-right font-mono text-[0.75rem] tabular-nums outline-none",
                      row.overridden
                        ? "border-brand-400/60 text-brand-100 focus:border-brand-400/80"
                        : row.surcharged
                          ? "border-money-positive/50 text-money-positive focus:border-money-positive/70"
                          : "border-line-strong/50 text-text-secondary focus:border-brand-400/60",
                    )}
                  />
                  {row.overridden ? (
                    <span
                      className="ml-1.5 rounded-full border border-brand-400/50 bg-brand-400/12 px-1.5 py-px font-mono text-[0.625rem] uppercase tracking-[0.08em] text-brand-100"
                      title={`Typed over by hand — SuppressRateAutoAdjust. The specification's own rules would charge ${cash(row.specRate)} on this panel.`}
                    >
                      manual
                    </span>
                  ) : row.surcharged ? (
                    <span
                      className="ml-1.5 rounded-full border border-money-positive/45 bg-money-positive/12 px-1.5 py-px font-mono text-[0.625rem] uppercase tracking-[0.08em] text-money-positive"
                      title={`Panel reaches ${SURCHARGE_THRESHOLD_SQM} m², so the row prices at SurchargeRate — base ${cash(block.baseRate)} + ${block.surchargePercent}%.`}
                    >
                      +{block.surchargePercent}%
                    </span>
                  ) : null}
                </td>
                <td className="px-2.5 py-1.5 text-right font-mono text-[0.75rem] font-medium text-text-primary tabular-nums">
                  {aed(row.value)}
                </td>
                <td className="px-1 py-1.5 text-right">
                  <button
                    type="button"
                    onClick={() => deleteRow(row.serial)}
                    title={`Delete row — Dg.DeleteRow (Ctrl+Delete). ${row.ref}`}
                    aria-label={`Delete row ${row.serial}, ${row.ref}`}
                    className="rounded-md border border-transparent px-1.5 py-0.5 font-mono text-[0.75rem] text-text-faint transition-colors hover:border-danger/45 hover:text-danger"
                  >
                    ✕
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot className="sticky bottom-0 bg-ink-900/95 backdrop-blur">
            <tr className="border-t border-line/60">
              <td colSpan={2} className="px-2.5 py-2">
                {/*
                  The grid's own footer controls, `Dg.GenerateRows` and
                  `Dg.AddRowShort`, with the row count beside them.
                */}
                <span className="flex items-center gap-1.5">
                  <input
                    type="number"
                    min="1"
                    max="50"
                    value={generateCount}
                    onChange={(event) => setGenerateCount(Number(event.target.value))}
                    aria-label={t("Number of rows to generate")}
                    title={t("Number of rows to generate — Dg.GenerateRows. The application allows 10,000; this preview stops at 50.")}
                    className="w-14 rounded-md border border-line-strong/50 bg-ink-950/70 px-1.5 py-0.5 text-right font-mono text-[0.75rem] tabular-nums text-text-primary outline-none focus:border-brand-400/60"
                  />
                  <button
                    type="button"
                    onClick={generateRows}
                    title="Generate that many rows. `Dg.Generate`."
                    className="rounded-full border border-line-strong/60 bg-ink-800/60 px-2 py-0.5 font-mono text-3xs uppercase tracking-[0.08em] font-medium text-text-muted transition-colors hover:border-brand-400/40 hover:text-text-primary"
                  >
                    {t("Generate")}
                  </button>
                  <button
                    type="button"
                    onClick={addRow}
                    className="rounded-full border border-line-strong/60 bg-ink-800/60 px-2 py-0.5 font-mono text-3xs uppercase tracking-[0.08em] font-medium text-text-muted transition-colors hover:border-brand-400/40 hover:text-text-primary"
                  >
                    + Add row
                  </button>
                </span>
              </td>
              <td colSpan={4} className="px-2.5 py-2 text-[0.6875rem] text-text-faint font-medium">
                {block.rows.length} rows · {editedCount(edited)} edited
              </td>
              <td className="px-2.5 py-2 text-center font-mono text-[0.75rem] font-semibold text-text-primary tabular-nums">
                {totals.qty}
              </td>
              <td aria-hidden="true" />
              <td className="px-2.5 py-2 text-right font-mono text-[0.75rem] font-semibold text-text-primary tabular-nums">
                {totals.sqm.toFixed(2)}
              </td>
              <td className="px-2.5 py-2 text-right font-mono text-3xs uppercase tracking-[0.08em] text-text-faint">Total</td>
              <td className="px-2.5 py-2 text-right font-mono text-[0.8125rem] font-semibold text-text-primary tabular-nums">
                {aed(totals.value)}
              </td>
              <td />
            </tr>
          </tfoot>
        </table>
      </div>

      {/*
        ── Optimization running ────────────────────────────────────────────
        The application's own progress block, moved here from its overlay:
        the title, the detail line naming what the run is doing, the
        percentage, and the warning that stands in the desktop grid.
      */}
      {phase ? (
        <div className="rounded-xl border border-brand-400/40 bg-brand-400/8 px-3 py-2.5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="font-mono text-3xs uppercase tracking-[0.08em] font-medium text-brand-200">
              {t("Optimization running…")}
            </p>
            <p className="font-mono text-[0.75rem] font-semibold tabular-nums text-brand-100">
              {Math.min(100, Math.round((phase.step / 4) * 100))}%
            </p>
          </div>
          <div
            className="mt-1.5 h-1.5 w-full overflow-hidden rounded-full bg-ink-950/70"
            role="progressbar"
            aria-label={t("Optimization progress")}
            aria-valuenow={Math.min(100, Math.round((phase.step / 4) * 100))}
            aria-valuemin={0}
            aria-valuemax={100}
          >
            <span
              className="block h-full rounded-full bg-brand-400 transition-[width] duration-200"
              style={{ width: `${Math.min(100, (phase.step / 4) * 100)}%` }}
            />
          </div>
          <p className="mt-1.5 text-[0.75rem] font-medium text-text-secondary">{t(phase.label)}…</p>
          <p className="text-[0.6875rem] leading-snug text-text-muted font-medium">
            {t("Please wait — cutting sheets… You can keep editing, but do not close this invoice until the bar reaches 100%.")}
          </p>
        </div>
      ) : null}

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
            <p className="font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">
              {t("Optimization result · summary")}
            </p>
            <p className="font-mono text-[0.6875rem] text-text-faint">
              {t(spec.sheet.label)} · {spec.sheet.w} × {spec.sheet.h} mm · kerf {optimizerLimits.kerf} mm
            </p>
          </div>
          <div className="mt-2 grid gap-2 sm:grid-cols-3 lg:grid-cols-5">
            <ReadOutMini label={t("Sheets")} value={String(run.sheets)} note={`${run.placed} pieces placed`} />
            <ReadOutMini label={t("Utilisation")} value={`${run.utilization.toFixed(2)}%`} note={`${run.partArea.toFixed(2)} of ${run.stockArea.toFixed(2)} m²`} />
            <ReadOutMini label={t("Unplaced")} value={String(run.unplaced)} note={run.unplaced > 0 ? "No plate could hold these" : "Every piece found a plate"} />
            <ReadOutMini label={t("Largest piece")} value={run.largest} note="the piece the plan has to fit" />
            <ReadOutMini
              label={t("Glass cost / m²")}
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

      {/*
        ── GRAND TOTAL SUMMARY ─────────────────────────────────────────────
        The read-only rollup that sits at the bottom of the desktop grid —
        `Views/ProformaInvoice/Step5DataGrid/GrandTotalSummary/`, whose README
        is explicit about what it is and is not: one row per specification
        with that specification's QTY, TOTAL SQM and VALUE, then a highlighted
        GRAND TOTAL row across all of them. It deliberately excludes other
        charges, discount and VAT — those belong to the invoice summary built
        after Step 6 — so this rollup is raw grid data and nothing else.
      */}
      <div className="rounded-xl border border-line/60 bg-ink-900/50 p-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <p className="font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">
            {t("Grand total summary")}
          </p>
          <p className="text-[0.6875rem] text-text-faint font-medium">
            {t("Read-only · every specification · no charges, discount or VAT")}
          </p>
        </div>
        <table className="mt-2 w-full border-collapse text-left">
          <caption className="sr-only">
            {t("One row per specification with its quantity, total square metres and value, then the grand total across the")}
            specification the grid is showing.
          </caption>
          <thead>
            <tr className="border-b border-line/60">
              <th scope="col" className="px-2 py-1.5 font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
                {t("Spec group")}
              </th>
              <th scope="col" className="px-2 py-1.5 text-right font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
                {t("Qty")}
              </th>
              <th scope="col" className="px-2 py-1.5 text-right font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
                {t("Total sqm")}
              </th>
              <th scope="col" className="px-2 py-1.5 text-right font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
                {t("Value")}
              </th>
            </tr>
          </thead>
          <tbody>
            {blocks.map((entry, index) => {
              const own = specRollup(entry);
              return (
                <tr
                  key={entry.number}
                  className={cn("border-b border-line/40", index === active ? "bg-brand-400/8" : "")}
                >
                  <td className="px-2 py-1.5">
                    <span className="font-mono text-[0.75rem] text-text-secondary">{entry.number}</span>
                    <span className="ml-2 text-[0.75rem] text-text-muted">{t(entry.name)}</span>
                    <span className="ml-2 font-mono text-[0.6875rem] text-text-faint">{entry.glassType}</span>
                  </td>
                  <td className="px-2 py-1.5 text-right font-mono text-[0.75rem] text-text-secondary tabular-nums">{own.qty}</td>
                  <td className="px-2 py-1.5 text-right font-mono text-[0.75rem] text-text-secondary tabular-nums">
                    {own.sqm.toFixed(2)}
                  </td>
                  <td className="px-2 py-1.5 text-right font-mono text-[0.75rem] text-text-secondary tabular-nums">
                    {aed(own.value)}
                  </td>
                </tr>
              );
            })}
          </tbody>
          <tfoot>
            {/*
              The highlighted grand row. It aggregates the specifications on
              the document — which in this preview is the one the job was
              quoted from, plus any block `+ ADD SPEC` has added.
            */}
            <tr className="border-t border-money-positive/40">
              <td className="px-2 py-2 text-[0.8125rem] font-semibold text-money-positive">Grand total</td>
              <td className="px-2 py-2 text-right font-mono text-[0.8125rem] font-semibold text-money-positive tabular-nums">
                {grand.qty}
              </td>
              <td className="px-2 py-2 text-right font-mono text-[0.8125rem] font-semibold text-money-positive tabular-nums">
                {grand.sqm.toFixed(2)}
              </td>
              <td className="px-2 py-2 text-right font-mono text-[0.8125rem] font-semibold text-money-positive tabular-nums">
                {aed(grand.value)}
              </td>
            </tr>
          </tfoot>
        </table>
        <p className="mt-1.5 text-[0.6875rem] leading-snug text-text-faint font-medium">
          Grand total of the specification data only. Other charges, discount and VAT are added on the steps after this one —
          the document&apos;s own grand total is the one the invoice summary prints, not this rollup.
        </p>
      </div>

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
      <p className="font-mono text-3xs uppercase tracking-[0.08em] text-brand-300 font-medium">{label}</p>
      <p className="mt-1 text-[0.75rem] leading-relaxed text-text-muted font-medium">{body}</p>
    </div>
  );
}

function ReadOutMini({ label, value, note }: { label: string; value: string; note: string }) {
  return (
    <div className="rounded-lg border border-line/50 bg-ink-950/50 px-2.5 py-2">
      <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{label}</p>
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

/** `AED {0:N2}` — the format the grid's value cell uses. */
function aed(value: number) {
  return `AED ${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function cash(value: number) {
  return value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function editedCount(edited: Record<string, boolean>) {
  return Object.values(edited).filter(Boolean).length;
}

/** One labelled dropdown in the Step 4 panel. */
function BuildSelect({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: readonly string[];
  onChange: (value: string) => void;
}) {
  return (
    <label className="block">
      <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{label}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded-lg border border-line-strong/60 bg-ink-950/70 px-2 py-1.5 text-[0.8125rem] text-text-primary outline-none focus:border-brand-400/60"
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </label>
  );
}
