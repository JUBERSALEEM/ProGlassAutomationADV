"use client";

import { useMemo, useState } from "react";
import { defaultStep4Build, dguLamInterlayerDefaults, dguSpacerSuggestedPrice, estimateStep4Rate, generatedStep4SpecName, step4Options, type Step4Build, type Step4BuildCode } from "@/lib/proforma-workflow";
import { formatMoney, formatSqm, type GroupTotal } from "@/lib/proforma-draft";

const FIELD = "h-8 w-full rounded-lg border border-line-strong bg-ink-950 px-2 text-xs text-text-primary";
type Field = { key: string; label: string; options?: readonly string[]; kind?: "text" | "money" | "number" | "toggle" };
type FieldGroup = { title: string; fields: Field[] };
const select = (key: string, label: string, options: readonly string[]): Field => ({ key, label, options });
const text = (key: string, label: string): Field => ({ key, label, kind: "text" });
const money = (key: string, label: string): Field => ({ key, label, kind: "money" });
const number = (key: string, label: string): Field => ({ key, label, kind: "number" });
const toggle: Field = { key: "useCustomPanelExtra", label: "Use custom panel extra", kind: "toggle" };
const pane = (prefix: string, title: string, thicknessOptions: readonly string[] = step4Options.thickness): FieldGroup[] => [
  { title, fields: [select(`${prefix}Thickness`, "Thickness", thicknessOptions), text(`${prefix}Colour`, "Colour (Sheet Store value)"), select(`${prefix}Treatment`, "Treatment", step4Options.treatment), money(`${prefix}Price`, "Pane price / m²"), number(`${prefix}SheetWidth`, "Sheet width (mm)"), number(`${prefix}SheetHeight`, "Sheet height (mm)")] },
];
const GROUPS: Record<Step4BuildCode, FieldGroup[]> = {
  SGU: [
    { title: "Single pane", fields: [select("thickness", "Thickness", step4Options.thickness), text("colour", "Colour (Sheet Store value)"), select("treatment", "Treatment", step4Options.sguTreatment), money("basePrice", "Pane price / m²"), number("sheetWidth", "Sheet width (mm)"), number("sheetHeight", "Sheet height (mm)")] },
    { title: "Processing and estimate", fields: [money("cutting", "Cutting / m²"), money("tempering", "Tempering / m²"), number("yield", "Yield factor"), number("margin", "Margin %"), select("polish", "Polish / detail", step4Options.sguPolish), select("panelExtra", "Panel extra", step4Options.panelExtra), toggle, text("customPanelExtra", "Custom panel extra")] },
  ],
  DGU: [
    ...pane("outer", "Outer pane"),
    { title: "Air spacer", fields: [select("spacerWidth", "Spacer width", step4Options.spacerWidth), select("spacerMaterial", "Spacer material", step4Options.spacerMaterial), money("spacerPrice", "Spacer price / m²"), select("spacerExtra", "Spacer extra", step4Options.spacerExtra), money("uInsertPrice", "U-Insert reference rate / m² (Step 6 charge)" )] },
    ...pane("inner", "Inner pane"),
    { title: "Panel extra and estimate", fields: [select("panelExtra", "Panel extra", step4Options.panelExtra), toggle, text("customPanelExtra", "Custom panel extra"), number("yield", "Yield factor"), number("margin", "Margin %")] },
  ],
  LAM: [
    ...pane("outer", "Outer glass pane"),
    { title: "PVB interlayer", fields: [select("pvbThickness", "PVB thickness", step4Options.pvbThickness), select("pvbColour", "PVB colour", step4Options.pvbColour), money("pvbPrice", "PVB rate / m²")] },
    ...pane("inner", "Inner glass pane"),
    { title: "Processing and estimate", fields: [select("polish", "Lamination polish", step4Options.polish), money("outerCutting", "Outer cutting / m²"), money("innerCutting", "Inner cutting / m²"), money("outerTempering", "Outer tempering / m²"), money("innerTempering", "Inner tempering / m²"), number("yield", "Yield factor"), number("margin", "Margin %")] },
  ],
  "DGU+LAM": [
    ...pane("outer", "Outer DGU pane", step4Options.hybridThickness),
    { title: "Air spacer", fields: [select("spacerThickness", "Spacer thickness", step4Options.hybridSpacer), money("spacerPrice", "Spacer price / m²"), select("spacerExtra", "Spacer extra", step4Options.spacerExtra)] },
    ...pane("lamOuter", "Laminated outer pane", step4Options.hybridThickness),
    { title: "Interlayer", fields: [select("interlayerType", "Interlayer type", step4Options.interlayerType), select("interlayerThickness", "Interlayer thickness", step4Options.interlayerThickness), select("interlayerColour", "Interlayer colour", step4Options.interlayerColour), money("interlayerBuyPrice", "Interlayer buy price / m²"), money("interlayerSellPrice", "Interlayer sell price / m²")] },
    ...pane("lamInner", "Laminated inner pane", step4Options.hybridThickness),
    { title: "Extras and per-pane processing", fields: [select("panelExtra", "Panel extra", step4Options.panelExtra), toggle, text("customPanelExtra", "Custom panel extra"), select("edgePolish", "Edge polish", step4Options.polish), money("outerCutting", "Outer cutting / m²"), money("outerTempering", "Outer tempering / m²"), money("lamOuterCutting", "Lam outer cutting / m²"), money("lamOuterTempering", "Lam outer tempering / m²"), money("lamInnerCutting", "Lam inner cutting / m²"), money("lamInnerTempering", "Lam inner tempering / m²"), number("yield", "Yield factor"), number("margin", "Margin %")] },
  ],
};

const BUILD_CHOICES: Array<{ code: Step4BuildCode; label: string }> = [
  { code: "SGU", label: "Single Glass Unit (SGU)" },
  { code: "DGU", label: "Double Glass Unit (DGU)" },
  { code: "LAM", label: "Laminated Assembly (LAM)" },
  { code: "DGU+LAM", label: "DGU + Laminated (hybrid)" },
];

/** Step 4 is the WPF glass builder and estimator. Dimensions and cut-list rows are entered in Step 5. */
export function ProformaBuilder({
  groupNo, build, onBuildChange, onApply, saved, currency, onReuse, onReplace,
}: {
  groupNo: number;
  build: Step4Build;
  onBuildChange: (build: Step4Build) => void;
  onApply: (groupNo: number, build: Step4Build) => void;
  saved: { revision: number; groups: GroupTotal[] };
  currency: string;
  onReuse: (savedGroup: number) => void;
  onReplace: (savedGroup: number, target: number) => void;
}) {
  const [confirming, setConfirming] = useState<number | null>(null);
  const estimate = useMemo(() => estimateStep4Rate(build), [build]);
  const generated = useMemo(() => generatedStep4SpecName(build), [build]);

  const changeCode = (code: Step4BuildCode) => {
    const defaults = defaultStep4Build(code);
    onBuildChange(defaults);
  };
  const update = (key: string, value: string) => {
    const values = { ...build.values, [key]: value };
    if (build.code === "DGU" && (key === "spacerWidth" || key === "spacerMaterial")) {
      values.spacerPrice = String(dguSpacerSuggestedPrice(values.spacerWidth ?? "12mm ASP", values.spacerMaterial ?? "Normal Mill Aluminum"));
    }
    if (build.code === "DGU+LAM" && key === "interlayerType") {
      const defaults = dguLamInterlayerDefaults(value);
      values.interlayerBuyPrice = String(defaults.buy);
      values.interlayerSellPrice = String(defaults.sell);
    }
    onBuildChange({ ...build, values });
  };

  return (
    <div className="grid gap-3 xl:grid-cols-[minmax(0,1fr)_22rem]">
      <section aria-label="Step 4, glass specification and estimate" className="space-y-3 rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4">
        <header>
          <div className="text-3xs font-semibold uppercase tracking-wider text-brand-300">Step 4 · Specification {groupNo}</div>
          <h2 className="mt-1 text-base font-bold text-text-primary">Glass build and price estimate</h2>
          <p className="mt-1 text-xs leading-relaxed text-text-muted">Enter the Step 4 pane, spacer or interlayer inputs and processing rates. The estimate follows the corresponding WPF build-family calculation. Website Step 4 does not read Sheet Store inventory, so verify the pane prices and sheet sizes here.</p>
        </header>

        <label className="flex max-w-xl flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
          Glass build family
          <select aria-label="Glass build family" value={build.code} onChange={(event) => changeCode(event.target.value as Step4BuildCode)} className={FIELD}>
            {BUILD_CHOICES.map((choice) => <option key={choice.code} value={choice.code}>{choice.label}</option>)}
          </select>
        </label>

        <div className="grid gap-3 lg:grid-cols-2">
          {GROUPS[build.code].map((group) => (
            <fieldset key={group.title} className="rounded-xl border border-line-strong/60 bg-ink-950/40 p-3">
              <legend className="px-1 text-xs font-semibold text-text-secondary">{group.title}</legend>
              <div className="grid gap-2 sm:grid-cols-2">
                {group.fields.map((field) => {
                  if (field.kind === "toggle") return <label key={field.key} className="flex min-h-8 items-center gap-2 text-xs text-text-muted"><input type="checkbox" checked={build.values[field.key] === "true"} onChange={(event) => update(field.key, String(event.target.checked))} />{field.label}</label>;
                  if ((field.key === "customPanelExtra") && build.values.useCustomPanelExtra !== "true") return null;
                  return <label key={field.key} className="flex flex-col gap-1 text-3xs font-semibold text-text-faint">{field.label}{field.options ? <select aria-label={field.label} value={build.values[field.key] ?? field.options[0]} onChange={(event) => update(field.key, event.target.value)} className={FIELD}>{field.options.map((option) => <option key={option} value={option}>{option}</option>)}</select> : <input aria-label={field.label} type={field.kind === "money" || field.kind === "number" ? "number" : "text"} step={field.kind === "money" ? "0.01" : field.kind === "number" ? "any" : undefined} value={build.values[field.key] ?? ""} onChange={(event) => update(field.key, event.target.value)} className={FIELD} />}</label>;
                })}
              </div>
            </fieldset>
          ))}
        </div>

        <div className="grid gap-3 rounded-xl border border-brand-400/30 bg-brand-500/[.05] p-3 sm:grid-cols-2">
          <div><div className="text-3xs font-semibold uppercase tracking-wider text-brand-300">Generated specification</div><p className="mt-1 break-words text-xs leading-relaxed text-text-primary">{generated}</p></div>
          <div><div className="text-3xs font-semibold uppercase tracking-wider text-brand-300">Estimated rate</div><p className="mt-1 font-mono text-lg font-bold text-text-primary">{formatMoney(estimate.rate, currency)} / m²</p>{estimate.warning ? <p role="status" className="mt-1 text-3xs text-state-warning">{estimate.warning}</p> : null}</div>
        </div>
        <button type="button" disabled={!generated.trim() || estimate.rate <= 0} onClick={() => onApply(groupNo, build)} className="h-10 w-full rounded-xl bg-brand-400 text-xs font-bold text-accent-ink hover:bg-brand-300 disabled:cursor-not-allowed disabled:opacity-50">Apply Step 4 specification and estimate to Step 5 group {groupNo}</button>
      </section>

      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3" aria-label="Saved specification groups">
        <div className="flex items-baseline justify-between gap-2"><h2 className="text-sm font-bold text-text-primary">Saved specifications</h2><span className="text-3xs text-text-faint">{saved.revision ? `Revision ${saved.revision}` : "Not saved yet"}</span></div>
        {saved.groups.length === 0 ? <p className="mt-2 text-3xs leading-relaxed text-text-muted">Saved cut-list groups will appear here after a revision is saved.</p> : <ul className="mt-2 space-y-2">{saved.groups.map((group) => <li key={group.groupNo} className="rounded-xl border border-line-strong/60 bg-ink-950/50 p-2"><div className="flex items-baseline justify-between gap-2"><span className="text-xs font-semibold text-text-secondary">Specification {group.groupNo}</span><span className="font-mono text-3xs text-text-faint">{group.lines} line{group.lines === 1 ? "" : "s"}, {formatSqm(group.sqm)} sqm, {formatMoney(group.value, currency)}</span></div><div className="mt-2 grid grid-cols-2 gap-2"><button type="button" onClick={() => onReuse(group.groupNo)} className="rounded-lg border border-brand-400/60 bg-brand-500/10 px-2 py-1.5 text-3xs font-semibold text-brand-200">Reuse as new</button><button type="button" onClick={() => setConfirming(confirming === group.groupNo ? null : group.groupNo)} className="rounded-lg border border-line-strong px-2 py-1.5 text-3xs font-semibold text-text-muted">Replace specification {groupNo}</button></div>{confirming === group.groupNo ? <div className="mt-2 rounded-lg border border-state-danger/50 bg-state-danger/10 p-2"><p className="text-3xs leading-relaxed text-state-danger">Replacing specification {groupNo} discards its working rows and copies the saved rows from specification {group.groupNo}.</p><div className="mt-2 flex gap-2"><button type="button" onClick={() => { onReplace(group.groupNo, groupNo); setConfirming(null); }} className="rounded-lg bg-state-danger px-2 py-1 text-3xs font-bold text-ink-950">Replace specification {groupNo}</button><button type="button" onClick={() => setConfirming(null)} className="rounded-lg border border-line-strong px-2 py-1 text-3xs">Keep it</button></div></div> : null}</li>)}</ul>}
      </section>
    </div>
  );
}
