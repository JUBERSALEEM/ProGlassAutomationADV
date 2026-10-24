"use client";

import { useMemo, useState } from "react";
import { buildOptions, glassBuilds, type GlassBuildCode } from "@/data/pricingRules";
import { emptyLine, formatMoney, formatSqm, wholeNumber, type DraftLine, type GroupTotal } from "@/lib/proforma-draft";

type Control = { key: string; label: string; options?: readonly string[]; placeholder?: string };

const LABELS: Record<string, string> = {
  thickness: "Outer thickness",
  colour: "Outer colour",
  treatment: "Outer treatment",
  innerThickness: "Inner thickness",
  innerColour: "Inner colour",
  innerTreatment: "Inner treatment",
  lamThickness: "Laminated thickness",
  lamColour: "Laminated colour",
  lamTreatment: "Laminated treatment",
  spacerWidth: "ASP spacer",
  spacerExtra: "Spacer extra",
  panelExtra: "Panel extra",
  polish: "Polish",
};

function control(key: string, options?: readonly string[], placeholder?: string): Control {
  return { key, label: LABELS[key] || key, options, placeholder };
}

/**
 * The Step 4 controls each build family asks for.
 *
 * The lists are the application's own option lists from `pricingRules`. The
 * generated reference is the build's own name function, so the line the
 * specification receives reads the way the desktop names it.
 */
const BUILD_CONTROLS: Record<GlassBuildCode, Control[]> = {
  SGU: [
    control("thickness", buildOptions.thickness),
    control("colour", buildOptions.colour),
    control("treatment", buildOptions.treatment),
    control("polish", buildOptions.polish),
    control("panelExtra", buildOptions.panelExtra),
  ],
  DGU: [
    control("thickness", buildOptions.thickness),
    control("colour", buildOptions.colour),
    control("treatment", buildOptions.treatment),
    control("spacerWidth", buildOptions.spacerWidth),
    control("spacerExtra", buildOptions.spacerExtra),
    control("innerThickness", buildOptions.thickness),
    control("innerColour", buildOptions.colour),
    control("innerTreatment", buildOptions.treatment),
    control("panelExtra", buildOptions.panelExtra),
  ],
  LAM: [
    control("thickness", buildOptions.thickness),
    control("colour", buildOptions.colour),
    control("treatment", buildOptions.treatment),
    control("pvb", undefined, "1.52mm Clear PVB"),
    control("innerThickness", buildOptions.thickness),
    control("innerColour", buildOptions.colour),
    control("innerTreatment", buildOptions.treatment),
    control("polish", buildOptions.polish),
  ],
  "DGU+LAM": [
    control("thickness", buildOptions.thickness),
    control("colour", buildOptions.colour),
    control("treatment", buildOptions.treatment),
    control("spacerWidth", buildOptions.spacerWidth),
    control("spacerExtra", buildOptions.spacerExtra),
    control("lamThickness", buildOptions.thickness),
    control("lamColour", buildOptions.colour),
    control("lamTreatment", buildOptions.treatment),
    control("interlayer", undefined, "1.52mm Clear PVB"),
    control("innerThickness", buildOptions.thickness),
    control("innerColour", buildOptions.colour),
    control("innerTreatment", buildOptions.treatment),
    control("panelExtra", buildOptions.panelExtra),
  ],
};

const DEFAULTS: Record<string, string> = {
  thickness: "6mm",
  colour: "Clear",
  treatment: "FT",
  innerThickness: "6mm",
  innerColour: "Clear",
  innerTreatment: "FT",
  lamThickness: "6mm",
  lamColour: "Clear",
  lamTreatment: "FT",
  spacerWidth: "12mm ASP",
  spacerExtra: "None",
  panelExtra: "None",
  polish: "None (Standard)",
  pvb: "1.52mm Clear PVB",
  interlayer: "1.52mm Clear PVB",
};

const FIELD = "h-8 w-full rounded-lg border border-line-strong bg-ink-950 px-2 text-xs text-text-primary";

/**
 * Step 4: the builder, and the specifications the last save recorded.
 *
 * Building a line never saves it. It joins the specification that is currently
 * assigned, and the assignment is stated rather than implied. Reusing a saved
 * specification copies rows into a new group; replacing one overwrites the
 * assigned group and says so before it does it.
 */
export function ProformaBuilder({
  activeGroup,
  groups,
  currency,
  saved,
  issues,
  onAssign,
  onAddLine,
  onReuse,
  onReplace,
}: {
  activeGroup: number;
  groups: number[];
  currency: string;
  saved: { revision: number; groups: GroupTotal[] };
  issues: string[];
  onAssign: (groupNo: number) => void;
  onAddLine: (groupNo: number, line: DraftLine) => void;
  onReuse: (savedGroup: number) => void;
  onReplace: (savedGroup: number, target: number) => void;
}) {
  const [code, setCode] = useState<GlassBuildCode>("SGU");
  const [values, setValues] = useState<Record<string, string>>(DEFAULTS);
  const [w, setW] = useState("");
  const [h, setH] = useState("");
  const [w2, setW2] = useState("");
  const [h2, setH2] = useState("");
  const [qty, setQty] = useState("1");
  const [rate, setRate] = useState("");
  const [problem, setProblem] = useState<string | null>(null);
  const [confirming, setConfirming] = useState<number | null>(null);

  const build = glassBuilds.find((entry) => entry.code === code) || glassBuilds[0];
  const reference = useMemo(() => build.name(values), [build, values]);
  const controls = BUILD_CONTROLS[code];

  const add = () => {
    const width = wholeNumber(w);
    const height = wholeNumber(h);
    const quantity = wholeNumber(qty);
    if (width < 1 || height < 1) {
      setProblem("Enter a panel width and height in millimetres.");
      return;
    }
    if (width > 20000 || height > 20000) {
      setProblem("A panel side cannot exceed 20000 mm.");
      return;
    }
    if (quantity < 1) {
      setProblem("Enter a quantity of at least one.");
      return;
    }
    setProblem(null);
    const line: DraftLine = {
      ...emptyLine(activeGroup),
      glassRef: reference,
      w1: String(width),
      h1: String(height),
      w2: w2 ? String(wholeNumber(w2)) : "",
      h2: h2 ? String(wholeNumber(h2)) : "",
      qty: String(quantity),
      rate: rate ? String(rate) : "",
    };
    onAddLine(activeGroup, line);
    setW("");
    setH("");
    setW2("");
    setH2("");
    setQty("1");
  };

  return (
    <aside aria-label="Step 4, build and saved specifications" className="min-w-0 space-y-3">
      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3">
        <div className="flex items-baseline justify-between gap-2">
          <div>
            <div className="text-3xs font-semibold uppercase tracking-wider text-brand-300">Step 4</div>
            <h2 className="text-sm font-bold text-text-primary">Build a line</h2>
          </div>
          <span className="text-3xs text-text-faint">Adds to specification {activeGroup}</span>
        </div>

        <label className="mt-3 flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
          Builder target
          <select
            value={activeGroup}
            onChange={(event) => onAssign(Number(event.target.value))}
            className={FIELD}
          >
            {groups.map((groupNo) => (
              <option key={groupNo} value={groupNo}>Specification {groupNo}</option>
            ))}
          </select>
        </label>

        <label className="mt-2 flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
          Build family
          <select value={code} onChange={(event) => setCode(event.target.value as GlassBuildCode)} className={FIELD}>
            {glassBuilds.map((entry) => (
              <option key={entry.code} value={entry.code}>{entry.label}, {entry.long}</option>
            ))}
          </select>
        </label>

        <div className="mt-2 grid grid-cols-2 gap-2">
          {controls.map((entry) => (
            <label key={entry.key} className="flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
              {entry.label}
              {entry.options ? (
                <select
                  value={values[entry.key] || ""}
                  onChange={(event) => setValues((current) => ({ ...current, [entry.key]: event.target.value }))}
                  className={FIELD}
                >
                  {entry.options.map((option) => (
                    <option key={option} value={option}>{option}</option>
                  ))}
                </select>
              ) : (
                <input
                  value={values[entry.key] || ""}
                  placeholder={entry.placeholder}
                  onChange={(event) => setValues((current) => ({ ...current, [entry.key]: event.target.value }))}
                  className={FIELD}
                />
              )}
            </label>
          ))}
        </div>

        <p className="mt-2 rounded-lg border border-line-strong/60 bg-ink-950/60 px-2 py-1.5 text-3xs leading-relaxed text-text-muted">
          <span className="font-semibold text-text-secondary">Generated reference:</span> {reference}
        </p>

        <div className="mt-2 grid grid-cols-2 gap-2">
          {([
            ["Width", w, setW],
            ["Height", h, setH],
            ["Inner width", w2, setW2],
            ["Inner height", h2, setH2],
          ] as const).map(([label, value, set]) => (
            <label key={label} className="flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
              {label}
              <input
                value={value}
                onChange={(event) => set(event.target.value)}
                inputMode="numeric"
                placeholder="mm"
                className={FIELD}
              />
            </label>
          ))}
          <label className="flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
            Quantity
            <input value={qty} onChange={(event) => setQty(event.target.value)} inputMode="numeric" className={FIELD} />
          </label>
          <label className="flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
            Approved rate
            <input value={rate} onChange={(event) => setRate(event.target.value)} inputMode="decimal" placeholder="per sqm" className={FIELD} />
          </label>
        </div>

        <button
          type="button"
          onClick={add}
          className="mt-2 h-9 w-full rounded-xl bg-brand-400 text-xs font-bold text-accent-ink hover:bg-brand-300"
        >
          Add line to specification {activeGroup}
        </button>
        {problem ? <p role="alert" className="mt-2 text-3xs text-state-danger">{problem}</p> : null}
        <p className="mt-2 text-3xs leading-relaxed text-text-faint">
          Inner dimensions describe the second panel of a double glazed unit. The billable area follows the outer panel, floored at half a square metre.
        </p>
      </section>

      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3">
        <div className="flex items-baseline justify-between gap-2">
          <h2 className="text-sm font-bold text-text-primary">Saved specifications</h2>
          <span className="text-3xs text-text-faint">{saved.revision ? `Revision ${saved.revision}` : "Not saved yet"}</span>
        </div>
        {saved.groups.length === 0 ? (
          <p className="mt-2 text-3xs leading-relaxed text-text-muted">
            Nothing is saved yet. Save the draft and the specifications you saved appear here, ready to reuse or replace.
          </p>
        ) : (
          <ul className="mt-2 space-y-2">
            {saved.groups.map((group) => (
              <li key={group.groupNo} className="rounded-xl border border-line-strong/60 bg-ink-950/50 p-2">
                <div className="flex items-baseline justify-between gap-2">
                  <span className="text-xs font-semibold text-text-secondary">Specification {group.groupNo}</span>
                  <span className="font-mono text-3xs text-text-faint">
                    {group.lines} line{group.lines === 1 ? "" : "s"}, {formatSqm(group.sqm)} sqm, {formatMoney(group.value, currency)}
                  </span>
                </div>
                <div className="mt-2 grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => onReuse(group.groupNo)}
                    className="rounded-lg border border-brand-400/60 bg-brand-500/10 px-2 py-1.5 text-3xs font-semibold text-brand-200 hover:bg-brand-500/20"
                  >
                    Reuse as new
                  </button>
                  <button
                    type="button"
                    onClick={() => setConfirming(confirming === group.groupNo ? null : group.groupNo)}
                    className="rounded-lg border border-line-strong px-2 py-1.5 text-3xs font-semibold text-text-muted hover:text-state-danger"
                  >
                    Replace specification {activeGroup}
                  </button>
                </div>
                {confirming === group.groupNo ? (
                  <div className="mt-2 rounded-lg border border-state-danger/50 bg-state-danger/10 p-2">
                    <p className="text-3xs leading-relaxed text-state-danger">
                      Replacing specification {activeGroup} discards every row it holds now, including anything not yet saved, and writes the {group.lines} saved rows of specification {group.groupNo} into it.
                    </p>
                    <div className="mt-2 flex gap-2">
                      <button
                        type="button"
                        onClick={() => {
                          onReplace(group.groupNo, activeGroup);
                          setConfirming(null);
                        }}
                        className="rounded-lg bg-state-danger px-2 py-1 text-3xs font-bold text-ink-950"
                      >
                        Replace specification {activeGroup}
                      </button>
                      <button
                        type="button"
                        onClick={() => setConfirming(null)}
                        className="rounded-lg border border-line-strong px-2 py-1 text-3xs font-semibold text-text-muted"
                      >
                        Keep it
                      </button>
                    </div>
                  </div>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </section>

      {issues.length ? (
        <section role="alert" className="rounded-2xl border border-state-warning/45 bg-state-warning/10 p-3">
          <h2 className="text-xs font-bold text-state-warning">Cannot save yet</h2>
          <ul className="mt-1 space-y-1 text-3xs leading-relaxed text-text-secondary">
            {issues.map((issue) => (
              <li key={issue}>{issue}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </aside>
  );
}
