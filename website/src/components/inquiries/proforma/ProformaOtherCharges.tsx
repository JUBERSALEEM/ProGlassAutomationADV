"use client";

import { applyAdvancedStep6Suggestions, chargeTemplates, emptyStep6, polishKinds, totalStep6, type Step4Build, type Step6Charge, type Step6Draft, type Step6Wastage } from "@/lib/proforma-workflow";
import { formatMoney, newLineKey, type DraftLine } from "@/lib/proforma-draft";

const FIELD = "h-8 rounded-lg border border-line-strong bg-ink-950 px-2 text-xs text-text-primary";
const METRICS: Record<string, Array<[string, string]>> = {
  Sqm: [["SQM1", "Glass 1 area (SQM1)"], ["SQM2", "Glass 2 area (SQM2)"], ["SQM_COMBINED", "Combined glass area"], ["DGU_CAVITY", "DGU cavity area"]],
  Lm: [["LM1", "Glass 1 perimeter (LM1)"], ["LM2", "Glass 2 perimeter (LM2)"], ["LM_COMBINED", "Combined perimeter"]],
  Qty: [["QTY", "Piece count"], ["DOUBLE_GLASS", "Physical panes"]],
};

function amountFor(charge: Step6Charge, step6: Step6Draft, lines: DraftLine[], builds: Record<number, Step4Build>) {
  const test = { ...step6, charges: step6.charges.map((row) => ({ ...row, isActive: row.key === charge.key })), wastages: [], customCharges: [] };
  return totalStep6(test, lines, builds);
}
function metricQtyFor(charge: Step6Charge, amount: number) {
  const rate = Number(charge.rate) || 0;
  return charge.key === "DELIVERY" ? 1 : charge.key.startsWith("POLISH") && charge.polishType === "Custom" ? Number(charge.customQty) || 0 : rate > 0 ? amount / rate : 0;
}
function paneColour(build: Step4Build | undefined, side: Step6Wastage["side"]): string {
  if (!build) return "";
  const keys = build.code === "SGU" ? ["colour"] : build.code === "DGU" ? ["outerColour", "innerColour"] : build.code === "LAM" ? ["outerColour", "innerColour"] : ["outerColour", "lamOuterColour", "lamInnerColour"];
  const index = side === "outer" ? 0 : side === "inner" ? Math.min(1, keys.length - 1) : Math.min(2, keys.length - 1);
  return build.values[keys[index] ?? ""] ?? "";
}

/** Step 6: source-driven supplementary, fabrication, delivery, wastage and custom charges. */
export function ProformaOtherCharges({
  value, lines, builds, groupNos, currency, onChange,
}: {
  value: Step6Draft;
  lines: DraftLine[];
  builds: Record<number, Step4Build>;
  groupNos: number[];
  currency: string;
  onChange: (next: Step6Draft) => void;
}) {
  const total = totalStep6(value, lines, builds);
  const updateCharge = (key: string, patch: Partial<Step6Charge>, manualActive = false, manualTarget = false) => {
    const next = { ...value, charges: value.charges.map((row) => row.key === key ? { ...row, ...patch } : row) };
    if (manualActive && !next.manualActiveKeys.includes(key)) next.manualActiveKeys = [...next.manualActiveKeys, key];
    if (manualTarget && !next.manualTargetKeys.includes(key)) next.manualTargetKeys = [...next.manualTargetKeys, key];
    onChange(next);
  };
  const toggleGroup = (charge: Step6Charge, groupNo: number) => {
    const groupNos = charge.groupNos.includes(groupNo) ? charge.groupNos.filter((n) => n !== groupNo) : [...charge.groupNos, groupNo].sort((a, b) => a - b);
    updateCharge(charge.key, { groupNos }, false, true);
  };
  const addWastage = (groupNo: number, side: Step6Wastage["side"]) => {
    const row: Step6Wastage = { id: `w-${newLineKey()}`, isActive: true, color: paneColour(builds[groupNo], side), groupNos: [groupNo], side, rate: "0", sheetQuantity: "1" };
    onChange({ ...value, wastages: [...value.wastages, row] });
  };
  const updateWastage = (id: string, patch: Partial<Step6Wastage>) => onChange({ ...value, wastages: value.wastages.map((row) => row.id === id ? { ...row, ...patch } : row), manualWastageIds: id.startsWith("auto-w-") && !(value.manualWastageIds ?? []).includes(id) ? [...(value.manualWastageIds ?? []), id] : (value.manualWastageIds ?? []) });
  const toggleWastageGroup = (row: Step6Wastage, groupNo: number) => updateWastage(row.id, { groupNos: row.groupNos.includes(groupNo) ? row.groupNos.filter((n) => n !== groupNo) : [...row.groupNos, groupNo].sort((a, b) => a - b) });
  const addCustom = () => onChange({ ...value, customCharges: [...value.customCharges, { id: `c-${newLineKey()}`, isActive: true, name: "", qty: "1", unit: "Qty", rate: "0" }] });

  return (
    <section aria-label="Step 6, Other Charges" className="space-y-3">
      <header className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-line-strong/60 bg-ink-900/80 px-4 py-3">
        <div><div className="text-3xs font-semibold uppercase tracking-wider text-brand-300">Step 6</div><h2 className="text-sm font-bold text-text-primary">Other Charges</h2><p className="mt-1 text-3xs text-text-muted">Charges are separate from Step 5 glass-line pricing and remain staff-only.</p></div>
        <div className="text-right"><div className="text-3xs uppercase tracking-wider text-text-faint">Total Other Charges</div><div className="font-mono text-lg font-bold text-text-primary">{formatMoney(total, currency)}</div></div>
      </header>

      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3">
        <div className="flex flex-wrap items-center justify-between gap-3"><h3 className="text-xs font-bold text-text-primary">Predefined charges</h3><div role="group" aria-label="Step 6 automation mode" className="flex gap-2"><button type="button" aria-pressed={value.mode === "advanced"} onClick={() => onChange(applyAdvancedStep6Suggestions({ ...value, mode: "advanced" }, builds))} className={`rounded-lg border px-3 py-1.5 text-3xs font-semibold ${value.mode === "advanced" ? "border-brand-400 text-brand-200" : "border-line-strong text-text-muted"}`}>Advanced suggestions</button><button type="button" aria-pressed={value.mode === "manual"} onClick={() => onChange({ ...value, mode: "manual" })} className={`rounded-lg border px-3 py-1.5 text-3xs font-semibold ${value.mode === "manual" ? "border-brand-400 text-brand-200" : "border-line-strong text-text-muted"}`}>Manual mode</button></div></div>
        <p className="mt-1 text-3xs text-text-faint">Advanced mode preselects charges from the applied Step 4 specification. Operator active/target edits are retained.</p>
        <div className="mt-3 space-y-2">
          {value.charges.map((charge) => {
            const template = chargeTemplates.find(([key]) => key === charge.key);
            if (!template) return null;
            const [, label, unit] = template;
            const amount = amountFor(charge, value, lines, builds);
            const qty = metricQtyFor(charge, amount);
            return <article key={charge.key} className={`rounded-xl border p-3 ${charge.isActive ? "border-brand-400/40 bg-ink-950/45" : "border-line-strong/50 bg-ink-950/25 opacity-75"}`}>
              <div className="grid gap-2 lg:grid-cols-[minmax(10rem,1.4fr)_7rem_8rem_minmax(12rem,1.7fr)] lg:items-center">
                <label className="flex items-center gap-2 text-xs font-semibold text-text-primary"><input type="checkbox" checked={charge.isActive} onChange={(event) => updateCharge(charge.key, { isActive: event.target.checked }, true)} />{label}</label>
                <label className="flex flex-col gap-1 text-3xs text-text-faint">Rate / {unit}<input aria-label={`${label} rate`} type="number" step="0.01" value={charge.rate} onChange={(event) => updateCharge(charge.key, { rate: event.target.value })} className={FIELD} /></label>
                {charge.key.startsWith("POLISH") ? <label className="flex flex-col gap-1 text-3xs text-text-faint">Polish subtype<select aria-label={`${label} subtype`} value={charge.polishType ?? "AllSidePolish"} onChange={(event) => updateCharge(charge.key, { polishType: event.target.value })} className={FIELD}>{polishKinds.map(([key, display]) => <option key={key} value={key}>{display}</option>)}</select></label> : charge.key === "DELIVERY" ? <div className="text-3xs text-text-muted">1 trip per invoice</div> : <label className="flex flex-col gap-1 text-3xs text-text-faint">Metric<select aria-label={`${label} metric`} value={charge.metricKey} onChange={(event) => updateCharge(charge.key, { metricKey: event.target.value }, false, true)} className={FIELD}>{(METRICS[unit] ?? []).map(([key, name]) => <option key={key} value={key}>{name}</option>)}</select></label>}
                <div className="text-right"><div className="text-3xs text-text-faint">{charge.isActive ? `${qty.toFixed(3)} ${unit} × ${Number(charge.rate || 0).toFixed(2)}` : "Disabled"}</div><div className="font-mono text-xs font-bold text-text-primary">{formatMoney(amount, currency)}</div></div>
              </div>
              {charge.key.startsWith("POLISH") && charge.polishType === "Custom" ? <div className="mt-2 grid gap-2 sm:grid-cols-2"><label className="text-3xs text-text-faint">Custom polish description<input aria-label="Custom polish description" value={charge.customDescription ?? ""} onChange={(event) => updateCharge(charge.key, { customDescription: event.target.value })} className={`${FIELD} mt-1 w-full`} /></label><label className="text-3xs text-text-faint">Manual LM quantity<input aria-label="Custom polish quantity" type="number" step="0.001" value={charge.customQty ?? ""} onChange={(event) => updateCharge(charge.key, { customQty: event.target.value })} className={`${FIELD} mt-1 w-full`} /></label></div> : null}
              {charge.key !== "DELIVERY" && !charge.key.startsWith("POLISH") ? <fieldset className="mt-2"><legend className="text-3xs font-semibold text-text-faint">Target specifications</legend><div className="mt-1 flex flex-wrap gap-2">{groupNos.map((groupNo) => <label key={groupNo} className="flex items-center gap-1 rounded-lg border border-line-strong/60 px-2 py-1 text-3xs text-text-muted"><input type="checkbox" checked={charge.groupNos.includes(groupNo)} onChange={() => toggleGroup(charge, groupNo)} />Spec {groupNo}</label>)}</div></fieldset> : charge.key.startsWith("POLISH") ? <fieldset className="mt-2"><legend className="text-3xs font-semibold text-text-faint">Polish target specifications</legend><div className="mt-1 flex flex-wrap gap-2">{groupNos.map((groupNo) => <label key={groupNo} className="flex items-center gap-1 rounded-lg border border-line-strong/60 px-2 py-1 text-3xs text-text-muted"><input type="checkbox" checked={charge.groupNos.includes(groupNo)} onChange={() => toggleGroup(charge, groupNo)} />Spec {groupNo}</label>)}</div></fieldset> : null}
            </article>;
          })}
        </div>
      </section>

      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3"><div className="flex items-center justify-between gap-2"><h3 className="text-xs font-bold text-text-primary">Wastage allowances</h3><span className="text-3xs text-text-faint">Manual sheet usage entry</span></div><p className="mt-1 text-3xs text-text-muted">WPF’s manual charge is calculated from the selected pane area and rate. Live optimizer feed and automatic Sheet Store consumption are not available in this website workspace.</p>
        <div className="mt-2 flex flex-wrap gap-2">{groupNos.map((groupNo) => <span key={groupNo} className="flex gap-1"><button type="button" onClick={() => addWastage(groupNo, "outer")} className="rounded-lg border border-line-strong px-2 py-1 text-3xs text-text-muted">Add Spec {groupNo} outer wastage</button><button type="button" onClick={() => addWastage(groupNo, "inner")} className="rounded-lg border border-line-strong px-2 py-1 text-3xs text-text-muted">Add inner pane</button></span>)}</div>
        {value.wastages.map((row) => <div key={row.id} className="mt-2 grid gap-2 rounded-xl border border-line-strong/60 p-2 sm:grid-cols-[auto_1fr_7rem_7rem_auto] sm:items-center"><label className="flex items-center gap-2 text-3xs text-text-muted"><input type="checkbox" checked={row.isActive} onChange={(event) => updateWastage(row.id, { isActive: event.target.checked })} />Active</label><div className="flex flex-wrap items-center gap-2"><input aria-label="Wastage colour" value={row.color} onChange={(event) => updateWastage(row.id, { color: event.target.value })} placeholder="Glass colour" className={FIELD} /><select aria-label="Wastage pane" value={row.side} onChange={(event) => updateWastage(row.id, { side: event.target.value as Step6Wastage["side"] })} className={FIELD}><option value="outer">Outer</option><option value="inner">Inner</option><option value="third">Third pane</option></select><span className="flex flex-wrap gap-1">{groupNos.map((groupNo) => <label key={groupNo} className="flex items-center gap-1 rounded border border-line-strong/60 px-1.5 py-1 text-3xs text-text-muted"><input aria-label={`Wastage target Spec ${groupNo}`} type="checkbox" checked={row.groupNos.includes(groupNo)} onChange={() => toggleWastageGroup(row, groupNo)} />Spec {groupNo}</label>)}</span></div><label className="text-3xs text-text-faint">Rate / sqm<input aria-label="Wastage rate" type="number" step="0.01" value={row.rate} onChange={(event) => updateWastage(row.id, { rate: event.target.value })} className={`${FIELD} mt-1 w-full`} /></label><label className="text-3xs text-text-faint">Sheet qty<input aria-label="Wastage sheet quantity" type="number" step="1" value={row.sheetQuantity} onChange={(event) => updateWastage(row.id, { sheetQuantity: event.target.value })} className={`${FIELD} mt-1 w-full`} /></label><button type="button" onClick={() => onChange({ ...value, wastages: value.wastages.filter((item) => item.id !== row.id), manualWastageIds: row.id.startsWith("auto-w-") ? [...(value.manualWastageIds ?? []), row.id] : (value.manualWastageIds ?? []) })} className="text-3xs text-state-danger">Remove</button></div>)}
      </section>

      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3"><div className="flex items-center justify-between gap-2"><h3 className="text-xs font-bold text-text-primary">Custom charges</h3><button type="button" onClick={addCustom} className="rounded-lg border border-brand-400/60 px-2 py-1 text-3xs font-semibold text-brand-200">Add custom charge</button></div>{value.customCharges.map((row) => <div key={row.id} className="mt-2 grid gap-2 rounded-xl border border-line-strong/60 p-2 sm:grid-cols-[auto_1fr_6rem_6rem_7rem_auto] sm:items-center"><label className="text-3xs text-text-muted"><input type="checkbox" checked={row.isActive} onChange={(event) => onChange({ ...value, customCharges: value.customCharges.map((item) => item.id === row.id ? { ...item, isActive: event.target.checked } : item) })} /> Active</label><input aria-label="Custom charge name" value={row.name} onChange={(event) => onChange({ ...value, customCharges: value.customCharges.map((item) => item.id === row.id ? { ...item, name: event.target.value } : item) })} placeholder="Charge name" className={FIELD} /><input aria-label="Custom charge quantity" type="number" step="0.001" value={row.qty} onChange={(event) => onChange({ ...value, customCharges: value.customCharges.map((item) => item.id === row.id ? { ...item, qty: event.target.value } : item) })} className={FIELD} /><select aria-label="Custom charge unit" value={row.unit} onChange={(event) => onChange({ ...value, customCharges: value.customCharges.map((item) => item.id === row.id ? { ...item, unit: event.target.value as typeof row.unit } : item) })} className={FIELD}><option>Sqm</option><option>Lm</option><option>Qty</option></select><input aria-label="Custom charge rate" type="number" step="0.01" value={row.rate} onChange={(event) => onChange({ ...value, customCharges: value.customCharges.map((item) => item.id === row.id ? { ...item, rate: event.target.value } : item) })} className={FIELD} /><button type="button" onClick={() => onChange({ ...value, customCharges: value.customCharges.filter((item) => item.id !== row.id) })} className="text-3xs text-state-danger">Remove</button></div>)}</section>

      <footer className="flex flex-wrap items-center justify-between gap-2 rounded-2xl border border-brand-400/30 bg-brand-500/[.05] px-4 py-3"><span className="text-xs font-semibold text-text-secondary">Total of active predefined, wastage, and custom charges</span><strong className="font-mono text-base text-text-primary">{formatMoney(total, currency)}</strong></footer>
    </section>
  );
}

export function initialOtherCharges(): Step6Draft { return emptyStep6(); }
