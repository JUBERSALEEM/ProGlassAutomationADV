"use client";

import { useEffect, useMemo, useRef, useState } from "react";

type GlassKind = "SGU" | "DGU" | "LAM" | "DGU + LAM";
type Pane = { thickness: string; colour: string; treatment: string; sheetWidth: string; sheetHeight: string };
type GlassSpec = {
  id: string;
  kind: GlassKind;
  panes: Pane[];
  spacerWidth: string;
  spacerMaterial: string;
  interlayerType: string;
  interlayerThickness: string;
  interlayerColour: string;
  panelExtra: string;
  spacerExtra: string;
  edgeFinish: string;
  fabricationNotes: string;
};
type GridRow = { reference: string; specId: string; width1: string; height1: string; width2: string; height2: string; quantity: string };

const kinds: GlassKind[] = ["SGU", "DGU", "LAM", "DGU + LAM"];
const thicknesses = ["3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "15mm", "16mm", "19mm"];
const treatments = ["FT", "Annealed", "Heat Strengthened", "Heat Soaked"];
const spacerWidths = ["6mm", "8mm", "9mm", "10mm", "12mm", "14mm", "16mm", "18mm", "20mm", "22mm", "24mm", "26mm", "28mm", "30mm", "32mm"];
const spacerMaterials = ["Normal Mill Aluminum", "Black Aluminum", "Warm Edge (Super Spacer)", "Black Warm Edge", "Stainless Steel", "TGI Spacer"];
const panelExtras = ["None", "With Overlap", "With Overlap As Per DWG", "With Overlap As Per Details", "With Overlap (SQM)", "With Overlap (SQM) As Per DWG", "With Fanhole", "With Fanhole As Per DWG", "With Cutout", "With Cutout As Per DWG", "With Holes", "With Holes As Per DWG", "With Cutout And Holes", "With Cutout And Holes As Per DWG", "With Shutter Bonding", "With Shutter Bonding As Per DWG", "With Argon Gas", "With Argon Gas As Per DWG"];
const edgeFinishes = ["None", "Polished Edges", "All Side Polish", "All Side Polish As Per DWG", "Both Height Side Polish", "Both Height Side Polish As Per DWG", "Both Width Side Polish", "Both Width Side Polish As Per DWG", "Beveled Edges", "Mitring (45° Bevel)"];
const paneNames: Record<GlassKind, string[]> = {
  SGU: ["Glass pane"],
  DGU: ["Outer pane", "Inner pane"],
  LAM: ["Outer glass", "Inner glass"],
  "DGU + LAM": ["Outer DGU pane", "Laminate outer glass", "Laminate inner glass"],
};
const blankPane = (): Pane => ({ thickness: "6mm", colour: "Clear", treatment: "FT", sheetWidth: "", sheetHeight: "" });
function blankSpec(id: string): GlassSpec {
  return { id, kind: "DGU", panes: [blankPane(), blankPane(), blankPane()], spacerWidth: "12mm", spacerMaterial: "Normal Mill Aluminum", interlayerType: "PVB", interlayerThickness: "0.76mm", interlayerColour: "Clear", panelExtra: "None", spacerExtra: "None", edgeFinish: "None", fabricationNotes: "" };
}
const blankRow = (number: number, specId: string): GridRow => ({ reference: `A${number}`, specId, width1: "", height1: "", width2: "", height2: "", quantity: "1" });

function makeSpecText(specs: GlassSpec[], rows: GridRow[], specNotes: string) {
  const details = ["CUSTOMER PI TECHNICAL SPECIFICATION — NO PRICING"];
  for (const [index, spec] of specs.entries()) {
    details.push(`Spec Group ${index + 1}: ${spec.kind}`);
    paneNames[spec.kind].forEach((name, paneIndex) => {
      const pane = spec.panes[paneIndex]!;
      const sheet = pane.sheetWidth && pane.sheetHeight ? `${pane.sheetWidth} × ${pane.sheetHeight} mm` : "not specified";
      details.push(`- ${name}: ${pane.thickness}, ${pane.colour}, ${pane.treatment}; stock sheet ${sheet}`);
    });
    if (spec.kind === "DGU" || spec.kind === "DGU + LAM") details.push(`- Spacer: ${spec.spacerWidth || "not specified"} / ${spec.spacerMaterial || "not specified"}`);
    if (spec.kind === "LAM" || spec.kind === "DGU + LAM") details.push(`- Interlayer: ${spec.interlayerType} ${spec.interlayerThickness}, ${spec.interlayerColour}`);
    details.push(`- Panel extra / overlap: ${spec.panelExtra}`, ...(spec.kind === "DGU" || spec.kind === "DGU + LAM" ? [`- Spacer extra: ${spec.spacerExtra}`] : []), `- Edge finish: ${spec.edgeFinish}`);
    if (spec.fabricationNotes.trim()) details.push(`- Fabrication details: ${spec.fabricationNotes.trim()}`);
  }
  details.push("Cutting list (dimensions in mm):");
  for (const [index, row] of rows.entries()) {
    const groupNumber = Math.max(1, specs.findIndex((spec) => spec.id === row.specId) + 1);
    details.push(`- ${row.reference.trim() || `Item ${index + 1}`} [Spec Group ${groupNumber}]: ${row.width1 || "?"} × ${row.height1 || "?"}${row.width2 || row.height2 ? `; secondary ${row.width2 || "?"} × ${row.height2 || "?"}` : ""} mm; quantity ${row.quantity || "?"}`);
  }
  if (specNotes.trim()) details.push(`Additional technical notes: ${specNotes.trim()}`);
  return details.join("\n");
}

/** WPF Steps 4–7 translated into a customer-facing, technical-only PI intake. */
export function ProformaTechnicalSpecFlow() {
  const [step, setStep] = useState(0);
  const [specs, setSpecs] = useState<GlassSpec[]>([blankSpec("spec-1")]);
  const [activeSpecId, setActiveSpecId] = useState("spec-1");
  const [nextSpecNumber, setNextSpecNumber] = useState(2);
  const [specNotes, setSpecNotes] = useState("");
  const [rows, setRows] = useState<GridRow[]>([blankRow(1, "spec-1")]);
  const [validation, setValidation] = useState("");
  const rootRef = useRef<HTMLDivElement>(null);
  const stepHeadingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    stepHeadingRef.current?.focus({ preventScroll: true });
  }, [step]);

  useEffect(() => {
    const root = rootRef.current;
    if (!root) return;
    const blocked = () => setValidation("Finish the glass specifications, cutting list, fabrication details, and review before continuing.");
    root.addEventListener("intake-advance-blocked", blocked);
    return () => root.removeEventListener("intake-advance-blocked", blocked);
  }, []);

  const activeSpec = specs.find((spec) => spec.id === activeSpecId) ?? specs[0]!;
  const activePanes = paneNames[activeSpec.kind];
  const technicalText = useMemo(() => makeSpecText(specs, rows, specNotes), [specs, rows, specNotes]);
  const completed = step === 3;

  function updateSpec(patch: Partial<GlassSpec>) {
    setSpecs((current) => current.map((spec) => spec.id === activeSpec.id ? { ...spec, ...patch } : spec));
  }
  function updatePane(index: number, key: keyof Pane, value: string) {
    setSpecs((current) => current.map((spec) => spec.id === activeSpec.id ? { ...spec, panes: spec.panes.map((pane, paneIndex) => paneIndex === index ? { ...pane, [key]: value } : pane) } : spec));
  }
  function addSpec() {
    if (specs.length >= 12) return;
    const id = `spec-${nextSpecNumber}`;
    setSpecs((current) => [...current, { ...activeSpec, id, panes: activeSpec.panes.map((pane) => ({ ...pane })) }]);
    setNextSpecNumber((current) => current + 1);
    setActiveSpecId(id);
    setValidation("");
  }
  function removeSpec() {
    if (specs.length <= 1) return;
    const retained = specs.filter((spec) => spec.id !== activeSpec.id);
    const replacement = retained[0]!;
    setSpecs(retained);
    setActiveSpecId(replacement.id);
    setRows((current) => current.map((row) => row.specId === activeSpec.id ? { ...row, specId: replacement.id } : row));
  }
  function updateRow(index: number, key: keyof GridRow, value: string) {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, [key]: value } : row));
  }
  function next() {
    setValidation("");
    if (step === 1) {
      const invalid = rows.findIndex((row) => !positive(row.width1) || !positive(row.height1) || !positive(row.quantity) || !specs.some((spec) => spec.id === row.specId));
      if (invalid >= 0) { setValidation(`Enter a valid spec group, positive width, height and quantity for ${rows[invalid]?.reference || `item ${invalid + 1}`}.`); return; }
    }
    setStep((current) => Math.min(3, current + 1));
  }
  function previous() { setValidation(""); setStep((current) => Math.max(0, current - 1)); }

  return (
    <div ref={rootRef} data-intake-complete={completed ? "true" : "false"} data-no-intake-advance className="space-y-5" aria-label="Proforma technical specification generator">
      <input type="hidden" name="pi_specification" value={technicalText} />
      <input type="hidden" name="pi_spec_complete" value={completed ? "true" : "false"} />
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div><p className="text-[.65rem] font-bold uppercase tracking-[.16em] text-brand-200">Proforma workflow · technical only</p><h3 ref={stepHeadingRef} tabIndex={-1} className="mt-1 text-lg font-bold outline-none focus-visible:ring-2 focus-visible:ring-brand-400">{["04 · Glass specification", "05 · Cutting list", "06 · Fabrication details", "07 · Review & send"][step]}</h3></div>
        <span className="rounded-full border border-line-strong px-3 py-1.5 font-mono text-xs text-text-muted">Step {step + 4} / 7</span>
      </div>
      <div className="flex gap-1.5" aria-label={`Step ${step + 4} of 7`}>
        {[0, 1, 2, 3].map((item) => <span key={item} className={`h-1.5 flex-1 rounded-full ${item <= step ? "bg-brand-300" : "bg-white/10"}`} />)}
      </div>

      {step === 0 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <div className="flex flex-wrap items-center justify-between gap-3"><p className="max-w-2xl text-xs leading-5 text-text-muted">Build one or more glass spec groups as in WPF Step 4. Configure pane thickness, colour, FT or annealed treatment, stock sheet sizes, spacer or interlayer. No rates, yield, margins, or cost fields are collected.</p><div className="flex gap-2"><button type="button" onClick={addSpec} disabled={specs.length >= 12} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-bold text-brand-200 disabled:opacity-40">+ Add glass specification</button>{specs.length > 1 && <button type="button" onClick={removeSpec} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-state-danger">Remove group</button>}</div></div>
        <div className="flex flex-wrap gap-2" aria-label="Glass specification groups">{specs.map((spec, index) => <button key={spec.id} type="button" onClick={() => { setActiveSpecId(spec.id); setValidation(""); }} aria-pressed={spec.id === activeSpec.id} className={`rounded-lg border px-3 py-2 text-xs font-semibold ${spec.id === activeSpec.id ? "border-brand-300/50 bg-brand-400/10 text-brand-100" : "border-line-strong text-text-muted"}`}>Spec Group {index + 1} · {spec.kind}</button>)}</div>
        <label className="block text-xs font-semibold text-text-secondary">Glass construction<select value={activeSpec.kind} onChange={(event) => updateSpec({ kind: event.target.value as GlassKind })} className={selectClass + " mt-2"}>{kinds.map((value) => <option key={value}>{value}</option>)}</select></label>
        <div className="grid gap-3 xl:grid-cols-2">
          {activePanes.map((name, index) => <fieldset key={`${activeSpec.id}-${activeSpec.kind}-${index}`} className="rounded-xl border border-line-strong/70 bg-ink-900/70 p-3"><legend className="px-1 text-sm font-bold text-text-primary">{name}</legend><div className="grid gap-3 sm:grid-cols-2">
            <SelectField label="Thickness" value={activeSpec.panes[index]!.thickness} options={thicknesses} onChange={(value) => updatePane(index, "thickness", value)} />
            <TextField label="Colour" value={activeSpec.panes[index]!.colour} onChange={(value) => updatePane(index, "colour", value)} placeholder="e.g. Clear, Grey, Bronze" />
            <SelectField label="Treatment" value={activeSpec.panes[index]!.treatment} options={treatments} onChange={(value) => updatePane(index, "treatment", value)} />
            <div className="grid grid-cols-2 gap-2"><TextField label="Stock sheet W (mm)" value={activeSpec.panes[index]!.sheetWidth} onChange={(value) => updatePane(index, "sheetWidth", value)} inputMode="numeric" placeholder="e.g. 3210" /><TextField label="Stock sheet H (mm)" value={activeSpec.panes[index]!.sheetHeight} onChange={(value) => updatePane(index, "sheetHeight", value)} inputMode="numeric" placeholder="e.g. 2250" /></div>
          </div></fieldset>)}
        </div>
        {(activeSpec.kind === "DGU" || activeSpec.kind === "DGU + LAM") && <fieldset className="rounded-xl border border-line-strong/70 bg-ink-900/70 p-3"><legend className="px-1 text-sm font-bold">Air spacer</legend><div className="grid gap-3 sm:grid-cols-2"><SelectField label="Spacer width" value={activeSpec.spacerWidth} options={spacerWidths} onChange={(value) => updateSpec({ spacerWidth: value })} /><SelectField label="Spacer material" value={activeSpec.spacerMaterial} options={spacerMaterials} onChange={(value) => updateSpec({ spacerMaterial: value })} /></div></fieldset>}
        {(activeSpec.kind === "LAM" || activeSpec.kind === "DGU + LAM") && <fieldset className="rounded-xl border border-line-strong/70 bg-ink-900/70 p-3"><legend className="px-1 text-sm font-bold">Lamination interlayer</legend><div className="grid gap-3 sm:grid-cols-3"><SelectField label="Interlayer type" value={activeSpec.interlayerType} options={["PVB", "SGP", "EVA"]} onChange={(value) => updateSpec({ interlayerType: value })} /><SelectField label="Interlayer thickness" value={activeSpec.interlayerThickness} options={["0.38mm", "0.76mm", "1.14mm", "1.52mm", "2.28mm", "3.04mm"]} onChange={(value) => updateSpec({ interlayerThickness: value })} /><SelectField label="Interlayer colour" value={activeSpec.interlayerColour} options={["Clear", "Frosted", "Milky White", "Grey", "Bronze", "Blue", "Green"]} onChange={(value) => updateSpec({ interlayerColour: value })} /></div></fieldset>}
      </div>}

      {step === 1 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <div className="flex flex-wrap items-end justify-between gap-3"><p className="text-xs leading-5 text-text-muted">WPF Step 5 cutting-list fields: reference, spec group, W1/H1, optional W2/H2, and quantity. Dimensions are in millimetres; there are no rates or amounts.</p><button type="button" onClick={() => setRows((current) => current.length >= 40 ? current : [...current, blankRow(current.length + 1, activeSpec.id)])} disabled={rows.length >= 40} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-bold text-brand-200 disabled:opacity-40">+ Add line</button></div>
        <div className="space-y-3">{rows.map((row, index) => <div key={index} className="rounded-xl border border-line-strong/70 bg-ink-900/70 p-3"><div className="mb-3 flex items-center justify-between"><h4 className="text-sm font-bold">Line {index + 1}</h4>{rows.length > 1 && <button type="button" onClick={() => setRows((current) => current.filter((_, rowIndex) => rowIndex !== index))} className="text-xs font-semibold text-state-danger">Remove</button>}</div><div className="grid gap-3 sm:grid-cols-3"><TextField label="Glass ref" value={row.reference} onChange={(value) => updateRow(index, "reference", value)} placeholder="A1" /><SelectField label="Spec group" value={row.specId} options={specs.map((spec, specIndex) => ({ value: spec.id, label: `Spec Group ${specIndex + 1} · ${spec.kind}` }))} onChange={(value) => updateRow(index, "specId", value)} /><TextField label="W1 (mm)" value={row.width1} onChange={(value) => updateRow(index, "width1", value)} inputMode="numeric" placeholder="e.g. 1200" /><TextField label="H1 (mm)" value={row.height1} onChange={(value) => updateRow(index, "height1", value)} inputMode="numeric" placeholder="e.g. 1800" /><TextField label="W2 (mm), optional" value={row.width2} onChange={(value) => updateRow(index, "width2", value)} inputMode="numeric" placeholder="Optional" /><TextField label="H2 (mm), optional" value={row.height2} onChange={(value) => updateRow(index, "height2", value)} inputMode="numeric" placeholder="Optional" /><TextField label="Quantity" value={row.quantity} onChange={(value) => updateRow(index, "quantity", value)} inputMode="numeric" placeholder="1" /></div><p className="mt-2 text-[.68rem] text-text-faint">Indicative panel area: {area(row).toFixed(3)} m² per piece · quantity {Number(row.quantity) || 0}</p></div>)}</div>
      </div>}

      {step === 2 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <p className="text-xs leading-5 text-text-muted">WPF Step 6 contains charge and wastage calculations, which are intentionally not exposed to customers. This step captures non-price fabrication instructions for the selected specification group.</p>
        <div className="flex flex-wrap gap-2" aria-label="Choose specification group for fabrication details">{specs.map((spec, index) => <button key={spec.id} type="button" onClick={() => setActiveSpecId(spec.id)} aria-pressed={spec.id === activeSpec.id} className={`rounded-lg border px-3 py-2 text-xs font-semibold ${spec.id === activeSpec.id ? "border-brand-300/50 bg-brand-400/10 text-brand-100" : "border-line-strong text-text-muted"}`}>Spec Group {index + 1} · {spec.kind}</button>)}</div>
        <div className="grid gap-3 sm:grid-cols-2"><SelectField label="Panel extra / overlap" value={activeSpec.panelExtra} options={panelExtras} onChange={(value) => updateSpec({ panelExtra: value })} /><SelectField label="Edge finish / polish" value={activeSpec.edgeFinish} options={edgeFinishes} onChange={(value) => updateSpec({ edgeFinish: value })} />{(activeSpec.kind === "DGU" || activeSpec.kind === "DGU + LAM") && <SelectField label="Spacer extra" value={activeSpec.spacerExtra} options={["None", "With U-Insert", "With U-Insert As Per DWG"]} onChange={(value) => updateSpec({ spacerExtra: value })} />}</div>
        <TextAreaField label="Fabrication details" value={activeSpec.fabricationNotes} onChange={(value) => updateSpec({ fabricationNotes: value })} placeholder="Examples: overlap as per drawing, fanhole location, cut-outs, holes, shutter bonding, or argon gas details." />
        <TextAreaField label="Other technical notes" value={specNotes} onChange={setSpecNotes} placeholder="Optional: standards, tolerance, orientation, safety requirements, or delivery constraints. Do not add pricing." />
      </div>}

      {step === 3 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <div className="rounded-xl border border-emerald-300/20 bg-emerald-300/[.05] p-3"><p className="text-sm font-bold text-emerald-100">Technical review · no prices shown</p><p className="mt-1 text-xs leading-5 text-text-muted">This specification will be included in the PI request delivered to the ProGlass inquiry inbox. The estimating team will prepare the PI in the desktop workflow.</p></div>
        <pre className="max-h-[28rem] overflow-auto whitespace-pre-wrap break-words rounded-xl border border-line-strong bg-ink-950 p-4 font-sans text-xs leading-5 text-text-secondary">{technicalText}</pre>
      </div>}

      {validation && <p role="alert" className="rounded-lg border border-state-danger/40 bg-state-danger/10 p-3 text-xs text-state-danger">{validation}</p>}
      <div className="flex items-center justify-between border-t border-line-strong/70 pt-4">
        <button type="button" onClick={previous} disabled={step === 0} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted disabled:opacity-40">← Back</button>
        {step < 3 ? <button type="button" onClick={next} className="rounded-lg bg-brand-400 px-4 py-2.5 text-xs font-bold text-ink-950">Continue →</button> : <button type="button" onClick={previous} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-semibold text-brand-200">Edit specification</button>}
      </div>
    </div>
  );
}

function positive(value: string) { const number = Number(value); return Number.isFinite(number) && number > 0; }
function area(row: GridRow) { const w = Number(row.width1) || 0; const h = Number(row.height1) || 0; return (w * h) / 1_000_000; }
const selectClass = "min-h-11 w-full rounded-lg border border-line-strong bg-ink-950 px-3 text-sm text-text-primary outline-none focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/30";
const inputClass = "mt-1.5 min-h-11 w-full rounded-lg border border-line-strong bg-ink-950 px-3 text-sm text-text-primary outline-none placeholder:text-text-faint focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/30";
function SelectField({ label, value, options, onChange }: { label: string; value: string; options: Array<string | { value: string; label: string }>; onChange: (value: string) => void }) { return <label className="block text-xs font-semibold text-text-secondary">{label}<select value={value} onChange={(event) => onChange(event.target.value)} className={selectClass + " mt-1.5"}>{options.map((option) => typeof option === "string" ? <option key={option} value={option}>{option}</option> : <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>; }
function TextField({ label, value, onChange, placeholder, inputMode }: { label: string; value: string; onChange: (value: string) => void; placeholder?: string; inputMode?: "numeric" }) { return <label className="block text-xs font-semibold text-text-secondary">{label}<input value={value} onChange={(event) => onChange(event.target.value)} inputMode={inputMode} placeholder={placeholder} className={inputClass} /></label>; }
function TextAreaField({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (value: string) => void; placeholder: string }) { return <label className="block text-xs font-semibold text-text-secondary">{label}<textarea value={value} onChange={(event) => onChange(event.target.value)} maxLength={1000} rows={3} placeholder={placeholder} className="mt-1.5 w-full rounded-lg border border-line-strong bg-ink-950 p-3 text-sm leading-5 text-text-primary outline-none placeholder:text-text-faint focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/30" /></label>; }
