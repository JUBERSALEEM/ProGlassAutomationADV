"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { createCustomerCutLayout, parseCustomerCutListFile, type CutLayoutResult, type ImportedCutListRow } from "@/lib/customer-cutlist";
import { createCsv, createXlsx } from "@/lib/export-formats";
import type { ExportSheet, OperationsExportBundle } from "@/lib/operations-exports";

type GlassKind = "SGU" | "DGU" | "LAM" | "DGU + LAM";
type Pane = { thickness: string; colour: string; treatment: string; sheetWidth: string; sheetHeight: string };
type GlassSpec = {
  id: string;
  specName: string;
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
type LayoutState = { specId: string; paneIndex: number; mode: "quick" | "optimize"; result: CutLayoutResult };
type ExportFormat = "csv" | "xlsx" | "pdf";

const MAX_ROWS = 250;
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
  return { id, specName: "", kind: "DGU", panes: [blankPane(), blankPane(), blankPane()], spacerWidth: "12mm", spacerMaterial: "Normal Mill Aluminum", interlayerType: "PVB", interlayerThickness: "0.76mm", interlayerColour: "Clear", panelExtra: "None", spacerExtra: "None", edgeFinish: "None", fabricationNotes: "" };
}
const blankRow = (number: number, specId: string): GridRow => ({ reference: `A${number}`, specId, width1: "", height1: "", width2: "", height2: "", quantity: "1" });

function makeSpecText(specs: GlassSpec[], rows: GridRow[], specNotes: string) {
  const details = ["CUSTOMER PI TECHNICAL SPECIFICATION — NO PRICING"];
  for (const [index, spec] of specs.entries()) {
    details.push(`Spec Group ${index + 1}: ${spec.kind}${spec.specName.trim() ? ` · ${spec.specName.trim()}` : ""}`);
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

function positive(value: string) {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 && number <= 100_000;
}
function specIsValid(spec: GlassSpec) {
  return paneNames[spec.kind].every((_, index) => {
    const pane = spec.panes[index];
    return Boolean(pane?.thickness.trim() && pane.colour.trim() && pane.treatment.trim())
      && positive(pane?.sheetWidth ?? "") && positive(pane?.sheetHeight ?? "");
  }) && ((spec.kind !== "DGU" && spec.kind !== "DGU + LAM") || Boolean(spec.spacerWidth.trim() && spec.spacerMaterial.trim()))
    && ((spec.kind !== "LAM" && spec.kind !== "DGU + LAM") || Boolean(spec.interlayerType.trim() && spec.interlayerThickness.trim() && spec.interlayerColour.trim()));
}
function rowIsValid(row: GridRow, specIds: Set<string>) {
  const secondPairStarted = Boolean(row.width2 || row.height2);
  return specIds.has(row.specId) && positive(row.width1) && positive(row.height1)
    && Number.isInteger(Number(row.quantity)) && Number(row.quantity) > 0 && Number(row.quantity) <= 10_000
    && (!secondPairStarted || (positive(row.width2) && positive(row.height2)));
}
function normalizeImportedKind(raw: string): GlassKind | null {
  const value = raw.toLocaleLowerCase().replace(/[\s_+-]/g, "");
  if (["sgu", "single", "singleglazed"].includes(value)) return "SGU";
  if (["dgu", "double", "doubleglazed"].includes(value)) return "DGU";
  if (["lam", "laminated"].includes(value)) return "LAM";
  if (["dgulam", "laminateddgu", "doublelaminated"].includes(value)) return "DGU + LAM";
  return null;
}
function inputValue(formData: FormData, name: string) { return String(formData.get(name) ?? "").trim(); }

/** WPF Steps 4–7 translated into a customer-facing, technical-only PI intake. */
export function ProformaTechnicalSpecFlow({ onCompleteChange }: { onCompleteChange?: (complete: boolean) => void }) {
  const [step, setStep] = useState(0);
  const [specs, setSpecs] = useState<GlassSpec[]>([blankSpec("spec-1")]);
  const [activeSpecId, setActiveSpecId] = useState("spec-1");
  const [nextSpecNumber, setNextSpecNumber] = useState(2);
  const [specNotes, setSpecNotes] = useState("");
  const [rows, setRows] = useState<GridRow[]>([blankRow(1, "spec-1")]);
  const [validation, setValidation] = useState("");
  const [ioStatus, setIoStatus] = useState("");
  const [layoutState, setLayoutState] = useState<LayoutState | null>(null);
  const [layoutRunning, setLayoutRunning] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);
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
  const specificationJson = useMemo(() => JSON.stringify({ specs, rows, specNotes }), [specs, rows, specNotes]);
  const validSpecifications = specs.length > 0 && specs.every(specIsValid);
  const validRows = rows.length > 0 && rows.every((row) => rowIsValid(row, new Set(specs.map((spec) => spec.id))));
  const completed = step === 3 && validSpecifications && validRows;

  useEffect(() => {
    const timer = window.setTimeout(() => onCompleteChange?.(completed), 0);
    return () => window.clearTimeout(timer);
  }, [completed, onCompleteChange]);

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
  function removeSpec(specId = activeSpec.id) {
    if (specs.length <= 1) return;
    const retained = specs.filter((spec) => spec.id !== specId);
    const replacement = retained[0]!;
    setSpecs(retained);
    if (activeSpecId === specId) setActiveSpecId(replacement.id);
    setRows((current) => current.map((row) => row.specId === specId ? { ...row, specId: replacement.id } : row));
    setLayoutState(null);
  }
  function updateRow(index: number, key: keyof GridRow, value: string) {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, [key]: value } : row));
    setLayoutState(null);
  }
  function addRow(specId: string) {
    if (rows.length >= MAX_ROWS) return;
    setRows((current) => [...current, blankRow(current.length + 1, specId)]);
    setValidation("");
  }
  function next() {
    setValidation("");
    if (step === 0) {
      const invalidSpec = specs.findIndex((spec) => !specIsValid(spec));
      if (invalidSpec >= 0) { setValidation(`Complete the pane build-up and enter valid stock sheet width and height for every pane in Spec Group ${invalidSpec + 1}.`); return; }
    }
    if (step === 1) {
      const specIds = new Set(specs.map((spec) => spec.id));
      const invalid = rows.findIndex((row) => !rowIsValid(row, specIds));
      if (invalid >= 0) { setValidation(`Enter a spec group, positive width, height, and whole-number quantity for ${rows[invalid]?.reference || `item ${invalid + 1}`}. If W2 or H2 is used, enter both dimensions.`); return; }
    }
    setStep((current) => Math.min(3, current + 1));
  }
  function previous() { setValidation(""); setStep((current) => Math.max(0, current - 1)); }

  async function importCutList(file: File) {
    setIoStatus("Reading cutting list…");
    try {
      const imported = await parseCustomerCutListFile(file);
      const accepted = imported.rows.slice(0, Math.max(0, MAX_ROWS - rows.length));
      if (!accepted.length) throw new Error(`The cutting list already has ${MAX_ROWS} rows, or the import contains no available rows.`);
      const updatedSpecs = [...specs];
      let nextNumber = nextSpecNumber;
      const mappedRows = accepted.map((source: ImportedCutListRow) => {
        const sourceName = source.specName.trim();
        const importedKind = normalizeImportedKind(source.glassType);
        const requestedGroupIndex = Number(source.specNumber) - 1;
        let target: GlassSpec | undefined;
        if (source.specNumber.trim() && Number.isInteger(requestedGroupIndex) && requestedGroupIndex >= 0 && requestedGroupIndex < 12) {
          while (updatedSpecs.length <= requestedGroupIndex && updatedSpecs.length < 12) {
            const base = updatedSpecs[0] ?? blankSpec(`spec-${nextNumber}`);
            const id = `spec-${nextNumber++}`;
            updatedSpecs.push({ ...base, id, specName: "", kind: importedKind ?? base.kind, panes: base.panes.map((pane) => ({ ...pane })) });
          }
          target = updatedSpecs[requestedGroupIndex];
        }
        if (!target && sourceName) target = updatedSpecs.find((spec, index) => {
          const groupLabel = `spec group ${index + 1}`;
          return sourceName.toLocaleLowerCase() === groupLabel || sourceName.toLocaleLowerCase() === spec.id.toLocaleLowerCase()
            || sourceName.toLocaleLowerCase() === spec.specName.toLocaleLowerCase()
            || sourceName.toLocaleLowerCase() === `${groupLabel} · ${spec.kind.toLocaleLowerCase()}`;
        });
        if (!target && sourceName && updatedSpecs.length < 12) {
          const base = updatedSpecs[0] ?? blankSpec(`spec-${nextNumber}`);
          const id = `spec-${nextNumber++}`;
          target = { ...base, id, specName: /^spec group \d+$/i.test(sourceName) ? "" : sourceName, kind: importedKind ?? base.kind, panes: base.panes.map((pane) => ({ ...pane })) };
          updatedSpecs.push(target);
        }
        if (!target) target = updatedSpecs[0]!;
        const targetIndex = updatedSpecs.findIndex((spec) => spec.id === target!.id);
        const importedName = sourceName && !/^spec group \d+$/i.test(sourceName) ? sourceName : target.specName;
        const updatedTarget = { ...target, specName: importedName, kind: importedKind ?? target.kind };
        if (targetIndex >= 0) updatedSpecs[targetIndex] = updatedTarget;
        return { reference: source.reference, specId: updatedTarget.id, width1: source.width1, height1: source.height1, width2: source.width2, height2: source.height2, quantity: source.quantity };
      });
      setSpecs(updatedSpecs);
      setNextSpecNumber(nextNumber);
      setRows((current) => [...current, ...mappedRows]);
      setStep(1);
      setIoStatus(`Imported ${mappedRows.length} cutting-list line${mappedRows.length === 1 ? "" : "s"}${imported.skipped ? ` · skipped ${imported.skipped} invalid row${imported.skipped === 1 ? "" : "s"}` : ""}. Spreadsheet pricing fields, if present, were ignored.`);
      setValidation("");
      setLayoutState(null);
    } catch (reason) {
      setIoStatus(reason instanceof Error ? reason.message : "Could not import the cutting list.");
    } finally {
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  function download(file: Blob, name: string) {
    const url = URL.createObjectURL(file);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  async function exportCutList(format: ExportFormat) {
    setIoStatus("Preparing technical export…");
    try {
      if (format === "pdf") {
        const form = rootRef.current?.closest("form");
        if (!(form instanceof HTMLFormElement)) throw new Error("Open this request inside the PI form before exporting PDF.");
        const formData = new FormData(form);
        const { generateCustomerPiCutListPdf } = await import("@/lib/customer-pi-attachments");
        const pdfFile = await generateCustomerPiCutListPdf({
          companyName: inputValue(formData, "company_name"),
          contactName: inputValue(formData, "contact_name"),
          customerReference: inputValue(formData, "customer_reference"),
          projectName: inputValue(formData, "project_name"),
          projectLocation: inputValue(formData, "project_location"),
          projectRequirements: inputValue(formData, "description"),
          technicalSpecification: technicalText,
          technicalSpecificationJson: specificationJson,
        });
        download(pdfFile, pdfFile.name);
      } else {
        const columns = [
          { key: "specNumber", header: "Spec #" }, { key: "specName", header: "Spec Name" },
          { key: "glassType", header: "Glass Type" }, { key: "reference", header: "Glass Ref" },
          { key: "width1", header: "W1" }, { key: "height1", header: "H1" },
          { key: "width2", header: "W2" }, { key: "height2", header: "H2" },
          { key: "quantity", header: "Qty" },
        ];
        const flatRows = rows.map((row) => {
          const index = specs.findIndex((spec) => spec.id === row.specId);
          const spec = specs[index] ?? specs[0]!;
          return { specNumber: Math.max(1, index + 1), specName: spec.specName.trim() || `Spec Group ${Math.max(1, index + 1)}`, glassType: spec.kind, reference: row.reference, width1: row.width1, height1: row.height1, width2: row.width2, height2: row.height2, quantity: row.quantity };
        });
        const sheet: ExportSheet = { name: "Customer Cut List", columns, rows: flatRows };
        const generatedAt = new Date().toISOString();
        const bundle: OperationsExportBundle = { schemaVersion: 1, report: "proformas", title: "Customer PI Cutting List — Technical Only", generatedAt, rowCount: flatRows.length, filters: {}, summary: { "Cut list lines": flatRows.length }, sheets: [sheet], csvSheet: sheet };
        const bytes = format === "csv" ? createCsv(sheet) : createXlsx(bundle);
        const mime = format === "csv" ? "text/csv;charset=utf-8" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        download(new Blob([bytes], { type: mime }), `ProGlass_Customer_Cut_List.${format}`);
      }
      setIoStatus(`Technical ${format.toUpperCase()} export is ready. No pricing, costs, yield, margins, or P&L are included.`);
    } catch (reason) {
      setIoStatus(reason instanceof Error ? reason.message : "Could not export the technical cut list.");
    }
  }

  async function runLayout(spec: GlassSpec, paneIndex: number, mode: "quick" | "optimize") {
    const paneName = paneNames[spec.kind][paneIndex] ?? `Pane ${paneIndex + 1}`;
    setLayoutRunning(`${spec.id}:${paneIndex}:${mode}`);
    setIoStatus("");
    try {
      await new Promise<void>((resolve) => window.setTimeout(resolve, 0));
      const pane = spec.panes[paneIndex]!;
      const selectedRows = rows.filter((row) => row.specId === spec.id);
      const layoutRows = selectedRows.map((row) => {
        const hasSecondary = positive(row.width2) && positive(row.height2);
        const useSecondary = paneIndex > 0 && hasSecondary;
        return { reference: row.reference, width: Number(useSecondary ? row.width2 : row.width1), height: Number(useSecondary ? row.height2 : row.height1), quantity: Number(row.quantity) };
      });
      const result = createCustomerCutLayout(layoutRows, { width: Number(pane.sheetWidth), height: Number(pane.sheetHeight), label: paneName }, mode);
      setLayoutState({ specId: spec.id, paneIndex, mode, result });
      setIoStatus(`${mode === "quick" ? "Quick layout" : "Optimized layout preview"} ready for ${paneName}: ${result.pieceCount} pieces across ${result.sheets.length} stock sheet${result.sheets.length === 1 ? "" : "s"}. This browser estimate is for planning only; ProGlass confirms production layouts in WPF.`);
    } catch (reason) {
      setLayoutState(null);
      setIoStatus(reason instanceof Error ? reason.message : "Could not build the layout preview.");
    } finally {
      setLayoutRunning("");
    }
  }

  return (
    <div ref={rootRef} data-intake-complete={completed ? "true" : "false"} data-no-intake-advance className="space-y-5" aria-label="Proforma technical specification generator">
      <input type="hidden" name="pi_specification" value={technicalText} />
      <input type="hidden" name="pi_specification_json" value={specificationJson} />
      <input type="hidden" name="pi_spec_complete" value={completed ? "true" : "false"} />
      <input ref={fileInputRef} type="file" accept=".csv,.txt,.xlsx" className="sr-only" aria-label="Import cutting list CSV or Excel file" onChange={(event) => { const file = event.target.files?.[0]; if (file) void importCutList(file); }} />
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div><p className="text-[.65rem] font-bold uppercase tracking-[.16em] text-brand-200">Proforma workflow · technical only</p><h3 ref={stepHeadingRef} tabIndex={-1} className="mt-1 text-lg font-bold outline-none focus-visible:ring-2 focus-visible:ring-brand-400">{["04 · Glass specification", "05 · Cutting list", "06 · Fabrication details", "07 · Review & send"][step]}</h3></div>
        <span className="rounded-full border border-line-strong px-3 py-1.5 font-mono text-xs text-text-muted">Step {step + 4} / 7</span>
      </div>
      <div className="flex gap-1.5" aria-label={`Step ${step + 4} of 7`}>{[0, 1, 2, 3].map((item) => <span key={item} className={`h-1.5 flex-1 rounded-full ${item <= step ? "bg-brand-300" : "bg-white/10"}`} />)}</div>

      {step === 0 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <div className="flex flex-wrap items-center justify-between gap-3"><p className="max-w-2xl text-xs leading-5 text-text-muted">Step 4 · build one or more glass specifications. Set the construction type, pane thickness and treatment, stock sheet size, spacer or interlayer. Customer pricing, yield, margin and cost fields are not collected.</p><div className="flex gap-2"><button type="button" onClick={addSpec} disabled={specs.length >= 12} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-bold text-brand-200 disabled:opacity-40">+ Add specification</button>{specs.length > 1 && <button type="button" onClick={() => removeSpec()} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-state-danger">Remove group</button>}</div></div>
        <div className="flex flex-wrap gap-2" aria-label="Glass specification groups">{specs.map((spec, index) => <button key={spec.id} type="button" onClick={() => { setActiveSpecId(spec.id); setValidation(""); }} aria-pressed={spec.id === activeSpec.id} className={`rounded-lg border px-3 py-2 text-xs font-semibold ${spec.id === activeSpec.id ? "border-brand-300/50 bg-brand-400/10 text-brand-100" : "border-line-strong text-text-muted"}`}>Spec Group {index + 1} · {spec.kind}</button>)}</div>
        <label className="block text-xs font-semibold text-text-secondary">Glass construction<select value={activeSpec.kind} onChange={(event) => updateSpec({ kind: event.target.value as GlassKind })} className={selectClass + " mt-2"}>{kinds.map((value) => <option key={value}>{value}</option>)}</select></label>
        <TextField label="Specification name / Step 4 type" value={activeSpec.specName} onChange={(value) => updateSpec({ specName: value })} placeholder="Optional: project or internal glass specification name" />
        <div className="grid gap-3 xl:grid-cols-2">{activePanes.map((name, index) => <fieldset key={`${activeSpec.id}-${activeSpec.kind}-${index}`} className="rounded-xl border border-line-strong/70 bg-ink-900/70 p-3"><legend className="px-1 text-sm font-bold text-text-primary">{name}</legend><div className="grid gap-3 sm:grid-cols-2"><SelectField label="Thickness" value={activeSpec.panes[index]!.thickness} options={thicknesses} onChange={(value) => updatePane(index, "thickness", value)} /><TextField label="Colour" value={activeSpec.panes[index]!.colour} onChange={(value) => updatePane(index, "colour", value)} placeholder="e.g. Clear, Grey, Bronze" /><SelectField label="Treatment" value={activeSpec.panes[index]!.treatment} options={treatments} onChange={(value) => updatePane(index, "treatment", value)} /><div className="grid grid-cols-2 gap-2"><TextField label="Stock sheet W (mm)" value={activeSpec.panes[index]!.sheetWidth} onChange={(value) => updatePane(index, "sheetWidth", value)} inputMode="numeric" placeholder="e.g. 3210" /><TextField label="Stock sheet H (mm)" value={activeSpec.panes[index]!.sheetHeight} onChange={(value) => updatePane(index, "sheetHeight", value)} inputMode="numeric" placeholder="e.g. 2250" /></div></div></fieldset>)}</div>
        {(activeSpec.kind === "DGU" || activeSpec.kind === "DGU + LAM") && <fieldset className="rounded-xl border border-line-strong/70 bg-ink-900/70 p-3"><legend className="px-1 text-sm font-bold">Air spacer</legend><div className="grid gap-3 sm:grid-cols-2"><SelectField label="Spacer width" value={activeSpec.spacerWidth} options={spacerWidths} onChange={(value) => updateSpec({ spacerWidth: value })} /><SelectField label="Spacer material" value={activeSpec.spacerMaterial} options={spacerMaterials} onChange={(value) => updateSpec({ spacerMaterial: value })} /></div></fieldset>}
        {(activeSpec.kind === "LAM" || activeSpec.kind === "DGU + LAM") && <fieldset className="rounded-xl border border-line-strong/70 bg-ink-900/70 p-3"><legend className="px-1 text-sm font-bold">Lamination interlayer</legend><div className="grid gap-3 sm:grid-cols-3"><SelectField label="Interlayer type" value={activeSpec.interlayerType} options={["PVB", "SGP", "EVA"]} onChange={(value) => updateSpec({ interlayerType: value })} /><SelectField label="Interlayer thickness" value={activeSpec.interlayerThickness} options={["0.38mm", "0.76mm", "1.14mm", "1.52mm", "2.28mm", "3.04mm"]} onChange={(value) => updateSpec({ interlayerThickness: value })} /><SelectField label="Interlayer colour" value={activeSpec.interlayerColour} options={["Clear", "Frosted", "Milky White", "Grey", "Bronze", "Blue", "Green"]} onChange={(value) => updateSpec({ interlayerColour: value })} /></div></fieldset>}
      </div>}

      {step === 1 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <div className="flex flex-wrap items-end justify-between gap-3"><div><p className="text-xs leading-5 text-text-muted">WPF Step 5 workflows: import a CSV/Excel cut list, add or remove specification groups and lines, run a quick or optimized layout preview, and export technical CSV, Excel, or PDF. Dimensions are in millimetres. Cost, yield, margin, and P&amp;L are not shown.</p><div className="mt-2 flex flex-wrap gap-2"><button type="button" onClick={() => fileInputRef.current?.click()} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-bold text-brand-200">Import CSV / Excel</button><button type="button" onClick={() => void exportCutList("xlsx")} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-secondary">Export Excel</button><button type="button" onClick={() => void exportCutList("csv")} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-secondary">Export CSV</button><button type="button" onClick={() => void exportCutList("pdf")} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-secondary">Export PDF</button><button type="button" onClick={addSpec} disabled={specs.length >= 12} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-bold text-brand-200 disabled:opacity-40">+ Add specification</button></div></div></div>
        {ioStatus && <p role="status" aria-live="polite" className="rounded-lg border border-line-strong bg-ink-950/70 p-3 text-xs leading-relaxed text-text-muted">{ioStatus}</p>}
        <div className="space-y-4">{specs.map((spec) => {
          const specIndex = specs.findIndex((item) => item.id === spec.id);
          const groupRows = rows.map((row, index) => ({ row, index })).filter(({ row }) => row.specId === spec.id);
          const currentPaneNames = paneNames[spec.kind];
          return <section key={spec.id} aria-label={`Step 4 specification group ${specIndex + 1} and Step 5 cutting list`} className="rounded-2xl border border-brand-300/25 bg-ink-900/65 p-4">
            <div className="flex flex-wrap items-start justify-between gap-3 border-b border-line-strong/70 pb-3"><div><p className="text-[.62rem] font-bold uppercase tracking-[.15em] text-brand-200">Step 4 specification generator · associated Step 5 cutting list</p><h4 className="mt-1 text-base font-bold text-text-primary">Spec Group {specIndex + 1} · {spec.kind}{spec.specName.trim() ? ` · ${spec.specName.trim()}` : ""}</h4><p className="mt-1 text-xs text-text-muted">{currentPaneNames.map((name, index) => `${name}: ${spec.panes[index]?.thickness ?? "—"} ${spec.panes[index]?.colour ?? ""}`).join(" · ")}</p></div><div className="flex flex-wrap gap-2"><button type="button" onClick={() => { setActiveSpecId(spec.id); setStep(0); }} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-brand-200">Edit Step 4 generator</button>{specs.length > 1 && <button type="button" onClick={() => removeSpec(spec.id)} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-state-danger">Remove specification</button>}<button type="button" onClick={() => addRow(spec.id)} disabled={rows.length >= MAX_ROWS} className="rounded-lg bg-brand-400 px-3 py-2 text-xs font-bold text-ink-950 disabled:opacity-40">+ Add line</button></div></div>
            {currentPaneNames.map((paneName, paneIndex) => {
              const key = `${spec.id}:${paneIndex}`;
              const currentLayout = layoutState?.specId === spec.id && layoutState.paneIndex === paneIndex ? layoutState : null;
              return <div key={paneName} className="mt-4 rounded-xl border border-line-strong/70 bg-ink-950/55 p-3"><div className="flex flex-wrap items-center justify-between gap-3"><div><h5 className="text-sm font-bold text-text-primary">{paneName}</h5><p className="mt-1 text-[.68rem] text-text-faint">Stock: {spec.panes[paneIndex]?.sheetWidth || "not set"} × {spec.panes[paneIndex]?.sheetHeight || "not set"} mm · browser layout preview</p></div><div className="flex flex-wrap gap-2"><button type="button" onClick={() => void runLayout(spec, paneIndex, "quick")} disabled={Boolean(layoutRunning) || groupRows.length === 0} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-secondary disabled:opacity-40">{layoutRunning === `${key}:quick` ? "Running…" : "Run quick"}</button><button type="button" onClick={() => void runLayout(spec, paneIndex, "optimize")} disabled={Boolean(layoutRunning) || groupRows.length === 0} className="rounded-lg border border-brand-300/35 bg-brand-400/10 px-3 py-2 text-xs font-bold text-brand-100 disabled:opacity-40">{layoutRunning === `${key}:optimize` ? "Optimizing…" : "Optimize"}</button>{currentLayout && <button type="button" onClick={() => setLayoutState(null)} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted">Hide result</button>}</div></div>
                {currentLayout && <div className="mt-3 rounded-lg border border-brand-300/20 bg-brand-400/[.05] p-3"><p className="text-xs font-semibold text-brand-100">{currentLayout.mode === "quick" ? "Quick layout" : "Optimized layout preview"}: {currentLayout.result.pieceCount} pieces · {currentLayout.result.sheets.length} stock sheet{currentLayout.result.sheets.length === 1 ? "" : "s"}</p><p className="mt-1 text-[.68rem] leading-relaxed text-text-muted">Planning estimate only; confirm dimensions, orientation, edge allowances, and production suitability in the ProGlass WPF workflow. No utilization or yield result is displayed.</p><div className="mt-3 grid gap-3 sm:grid-cols-2">{currentLayout.result.sheets.map((sheet, sheetIndex) => <div key={sheetIndex} className="rounded-lg border border-line-strong bg-ink-950 p-2"><div className="mb-2 flex justify-between gap-2 text-[.65rem] text-text-muted"><span>Stock sheet {sheetIndex + 1}</span><span>{sheet.width} × {sheet.height} mm</span></div><svg viewBox="0 0 1000 600" role="img" aria-label={`Cut layout preview for stock sheet ${sheetIndex + 1}`} className="w-full rounded border border-line-strong bg-white"><rect x="0" y="0" width="1000" height="600" fill="white" />{sheet.placements.map((placement, index) => <g key={`${placement.reference}-${index}`}><rect x={placement.x / sheet.width * 1000} y={placement.y / sheet.height * 600} width={placement.width / sheet.width * 1000} height={placement.height / sheet.height * 600} fill="rgba(37, 99, 235, .15)" stroke="#1d4ed8" strokeWidth="2" /><text x={placement.x / sheet.width * 1000 + 5} y={placement.y / sheet.height * 600 + 16} fill="#172554" fontSize="14">{placement.reference}</text></g>)}</svg></div>)}</div></div>}
              </div>;
            })}
            <div className="mt-4 space-y-3" aria-label={`Cutting list for Spec Group ${specIndex + 1}`}>
              {groupRows.length ? groupRows.map(({ row, index }) => <div key={`${spec.id}-${index}`} className="rounded-lg border border-line-strong/70 bg-ink-900/70 p-3"><div className="mb-3 flex items-center justify-between"><h5 className="text-sm font-bold text-text-primary">Line {index + 1}</h5>{rows.length > 1 && <button type="button" onClick={() => { setRows((current) => current.filter((_, rowIndex) => rowIndex !== index)); setLayoutState(null); }} className="text-xs font-semibold text-state-danger">Remove</button>}</div><div className="grid gap-3 sm:grid-cols-3"><TextField label="Glass ref" value={row.reference} onChange={(value) => updateRow(index, "reference", value)} placeholder="A1" /><TextField label="W1 (mm)" value={row.width1} onChange={(value) => updateRow(index, "width1", value)} inputMode="numeric" placeholder="e.g. 1200" /><TextField label="H1 (mm)" value={row.height1} onChange={(value) => updateRow(index, "height1", value)} inputMode="numeric" placeholder="e.g. 1800" /><TextField label="W2 (mm), optional" value={row.width2} onChange={(value) => updateRow(index, "width2", value)} inputMode="numeric" placeholder="Optional" /><TextField label="H2 (mm), optional" value={row.height2} onChange={(value) => updateRow(index, "height2", value)} inputMode="numeric" placeholder="Optional" /><TextField label="Quantity" value={row.quantity} onChange={(value) => updateRow(index, "quantity", value)} inputMode="numeric" placeholder="1" /></div></div>) : <p className="rounded-lg border border-dashed border-line-strong px-3 py-4 text-xs text-text-faint">No cutting-list rows for this specification yet. Add a line above or import a file.</p>}
            </div>
          </section>;
        })}</div>
        <p className="text-[.68rem] leading-relaxed text-text-faint">Imported Excel and CSV pricing columns are ignored. Exports and automatically attached PI documents contain technical request data only, not a priced invoice or P&amp;L.</p>
      </div>}

      {step === 2 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <p className="text-xs leading-5 text-text-muted">WPF Step 6 charge and wastage calculations are intentionally not exposed. Capture non-price fabrication instructions for each specification group here.</p>
        <div className="flex flex-wrap gap-2" aria-label="Choose specification group for fabrication details">{specs.map((spec, index) => <button key={spec.id} type="button" onClick={() => setActiveSpecId(spec.id)} aria-pressed={spec.id === activeSpec.id} className={`rounded-lg border px-3 py-2 text-xs font-semibold ${spec.id === activeSpec.id ? "border-brand-300/50 bg-brand-400/10 text-brand-100" : "border-line-strong text-text-muted"}`}>Spec Group {index + 1} · {spec.kind}</button>)}</div>
        <div className="grid gap-3 sm:grid-cols-2"><SelectField label="Panel extra / overlap" value={activeSpec.panelExtra} options={panelExtras} onChange={(value) => updateSpec({ panelExtra: value })} /><SelectField label="Edge finish / polish" value={activeSpec.edgeFinish} options={edgeFinishes} onChange={(value) => updateSpec({ edgeFinish: value })} />{(activeSpec.kind === "DGU" || activeSpec.kind === "DGU + LAM") && <SelectField label="Spacer extra" value={activeSpec.spacerExtra} options={["None", "With U-Insert", "With U-Insert As Per DWG"]} onChange={(value) => updateSpec({ spacerExtra: value })} />}</div>
        <TextAreaField label="Fabrication details" value={activeSpec.fabricationNotes} onChange={(value) => updateSpec({ fabricationNotes: value })} placeholder="Examples: overlap as per drawing, fanhole location, cut-outs, holes, shutter bonding, or argon gas details." />
        <TextAreaField label="Other technical notes" value={specNotes} onChange={setSpecNotes} placeholder="Optional: standards, tolerance, orientation, safety requirements, or delivery constraints. Do not add pricing." />
      </div>}

      {step === 3 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <div className="rounded-xl border border-emerald-300/20 bg-emerald-300/[.05] p-3"><p className="text-sm font-bold text-emerald-100">Technical review · no prices shown</p><p className="mt-1 text-xs leading-5 text-text-muted">This specification and the technical XLSX, CSV, and PDF request files will be included with the inquiry delivered to the ProGlass inbox. An estimator verifies the production layout and prepares any PI in the desktop workflow.</p></div>
        {!validRows && <p role="alert" className="rounded-lg border border-state-warning/40 bg-state-warning/10 p-3 text-xs text-state-warning">The cutting list needs valid dimensions and quantities before the request can be sent.</p>}
        <pre className="max-h-[28rem] overflow-auto whitespace-pre-wrap break-words rounded-xl border border-line-strong bg-ink-950 p-4 font-sans text-xs leading-5 text-text-secondary">{technicalText}</pre>
      </div>}

      {validation && <p role="alert" className="rounded-lg border border-state-danger/40 bg-state-danger/10 p-3 text-xs text-state-danger">{validation}</p>}
      <div className="flex items-center justify-between border-t border-line-strong/70 pt-4"><button type="button" onClick={previous} disabled={step === 0} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted disabled:opacity-40">← Back</button>{step < 3 ? <button type="button" onClick={next} className="rounded-lg bg-brand-400 px-4 py-2.5 text-xs font-bold text-ink-950">Continue →</button> : <button type="button" onClick={previous} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-semibold text-brand-200">Edit specification</button>}</div>
    </div>
  );
}

const selectClass = "min-h-11 w-full rounded-lg border border-line-strong bg-ink-950 px-3 text-sm text-text-primary outline-none focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/30";
const inputClass = "mt-1.5 min-h-11 w-full rounded-lg border border-line-strong bg-ink-950 px-3 text-sm text-text-primary outline-none placeholder:text-text-faint focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/30";
function SelectField({ label, value, options, onChange }: { label: string; value: string; options: Array<string | { value: string; label: string }>; onChange: (value: string) => void }) { return <label className="block text-xs font-semibold text-text-secondary">{label}<select value={value} onChange={(event) => onChange(event.target.value)} className={selectClass + " mt-1.5"}>{options.map((option) => typeof option === "string" ? <option key={option} value={option}>{option}</option> : <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>; }
function TextField({ label, value, onChange, placeholder, inputMode }: { label: string; value: string; onChange: (value: string) => void; placeholder?: string; inputMode?: "numeric" }) { return <label className="block text-xs font-semibold text-text-secondary">{label}<input value={value} onChange={(event) => onChange(event.target.value)} inputMode={inputMode} placeholder={placeholder} className={inputClass} /></label>; }
function TextAreaField({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (value: string) => void; placeholder: string }) { return <label className="block text-xs font-semibold text-text-secondary">{label}<textarea value={value} onChange={(event) => onChange(event.target.value)} maxLength={1000} rows={3} placeholder={placeholder} className="mt-1.5 w-full rounded-lg border border-line-strong bg-ink-950 p-3 text-sm leading-5 text-text-primary outline-none placeholder:text-text-faint focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/30" /></label>; }
