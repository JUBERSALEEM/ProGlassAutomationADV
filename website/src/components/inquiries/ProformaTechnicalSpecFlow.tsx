"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import type { ClipboardEvent, KeyboardEvent } from "react";
import { calculateWpfPanelSqm, calculateWpfTotalSqm, parseCustomerCutListFile, type ImportedCutListRow } from "@/lib/customer-cutlist";
import { deriveCustomerPiProcessingSuggestions } from "@/lib/customer-pi-processing";

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

const MAX_ROWS_PER_GENERATION = 10_000;
const MAX_ROWS = MAX_ROWS_PER_GENERATION * 12;
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


function positive(value: string) {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 && number <= 100_000;
}
function positiveMillimetres(value: string) {
  const number = Number(value);
  return Number.isInteger(number) && number > 0 && number <= 100_000;
}
function rowPanelSqm(row: GridRow): number | null {
  return calculateWpfPanelSqm(row.width1, row.height1);
}
function rowTotalSqm(row: GridRow): number | null {
  return calculateWpfTotalSqm(row.width1, row.height1, row.quantity);
}
function formatSqm(value: number | null) {
  return value === null ? "—" : value.toFixed(3);
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
  return specIds.has(row.specId) && positiveMillimetres(row.width1) && positiveMillimetres(row.height1)
    && Number.isInteger(Number(row.quantity)) && Number(row.quantity) > 0 && Number(row.quantity) <= 10_000
    && (!secondPairStarted || (positiveMillimetres(row.width2) && positiveMillimetres(row.height2)));
}
function normalizeImportedKind(raw: string): GlassKind | null {
  const value = raw.toLocaleLowerCase().replace(/[\s_+-]/g, "");
  if (["sgu", "single", "singleglazed"].includes(value)) return "SGU";
  if (["dgu", "double", "doubleglazed"].includes(value)) return "DGU";
  if (["lam", "laminated"].includes(value)) return "LAM";
  if (["dgulam", "laminateddgu", "doublelaminated"].includes(value)) return "DGU + LAM";
  return null;
}

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
  const [bulkRowCounts, setBulkRowCounts] = useState<Record<string, string>>({});
  const [reviewConfirmed, setReviewConfirmed] = useState(false);
  const [previewProject, setPreviewProject] = useState<Record<string, string>>({});
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
  const activeProcessingSuggestions = deriveCustomerPiProcessingSuggestions(activeSpec);
  const specificationJson = useMemo(() => JSON.stringify({ specs, rows, specNotes }), [specs, rows, specNotes]);
  const validSpecifications = specs.length > 0 && specs.every(specIsValid);
  const validRows = rows.length > 0 && rows.every((row) => rowIsValid(row, new Set(specs.map((spec) => spec.id))));
  const completed = step === 3 && validSpecifications && validRows && reviewConfirmed;

  useEffect(() => {
    const timer = window.setTimeout(() => onCompleteChange?.(completed), 0);
    return () => window.clearTimeout(timer);
  }, [completed, onCompleteChange]);

  function invalidateReview() { setReviewConfirmed(false); }

  function updateSpec(patch: Partial<GlassSpec>) {
    setSpecs((current) => current.map((spec) => spec.id === activeSpec.id ? { ...spec, ...patch } : spec));
    invalidateReview();
  }
  function updatePane(index: number, key: keyof Pane, value: string) {
    setSpecs((current) => current.map((spec) => spec.id === activeSpec.id ? { ...spec, panes: spec.panes.map((pane, paneIndex) => paneIndex === index ? { ...pane, [key]: value } : pane) } : spec));
    invalidateReview();
  }
  function updateSpecNotes(value: string) {
    setSpecNotes(value);
    invalidateReview();
  }
  function addSpec() {
    if (specs.length >= 12) return;
    const id = `spec-${nextSpecNumber}`;
    setSpecs((current) => [...current, { ...activeSpec, id, specName: "", panes: activeSpec.panes.map((pane) => ({ ...pane })) }]);
    setNextSpecNumber((current) => current + 1);
    setActiveSpecId(id);
    setValidation("");
    invalidateReview();
  }
  function removeSpec(specId = activeSpec.id) {
    if (specs.length <= 1) return;
    const retained = specs.filter((spec) => spec.id !== specId);
    const replacement = retained[0]!;
    setSpecs(retained);
    if (activeSpecId === specId) setActiveSpecId(replacement.id);
    setRows((current) => current.map((row) => row.specId === specId ? { ...row, specId: replacement.id } : row));
    invalidateReview();
  }
  function updateRow(index: number, key: keyof GridRow, value: string) {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, [key]: value } : row));
    invalidateReview();
  }
  function addRow(specId: string) {
    if (rows.length >= MAX_ROWS) return;
    setRows((current) => [...current, blankRow(current.length + 1, specId)]);
    setValidation("");
    invalidateReview();
  }
  function generateRows(specId: string) {
    const requested = Number(bulkRowCounts[specId]);
    const remaining = MAX_ROWS - rows.length;
    if (!Number.isInteger(requested) || requested < 1) {
      setIoStatus("Enter a positive whole number of rows to generate.");
      return;
    }
    if (requested > MAX_ROWS_PER_GENERATION) {
      setIoStatus(`Generate up to ${MAX_ROWS_PER_GENERATION.toLocaleString()} rows at a time; use import for larger cutting lists.`);
      return;
    }
    if (remaining <= 0) {
      setIoStatus(`The customer grid is at its ${MAX_ROWS.toLocaleString()}-row limit.`);
      return;
    }
    const count = Math.min(requested, remaining);
    setRows((current) => [...current, ...Array.from({ length: count }, (_, offset) => blankRow(current.length + offset + 1, specId))]);
    setBulkRowCounts((current) => ({ ...current, [specId]: "" }));
    setIoStatus(count < requested ? `Added ${count.toLocaleString()} rows; the ${MAX_ROWS.toLocaleString()}-row limit was reached.` : `Generated ${count.toLocaleString()} rows in Spec Group ${specs.findIndex((spec) => spec.id === specId) + 1}.`);
    invalidateReview();
  }
  function removeRow(globalIndex: number, specId: string) {
    const groupIndices = rows.map((row, index) => row.specId === specId ? index : -1).filter((index) => index >= 0);
    if (groupIndices.length <= 1) {
      const row = rows[globalIndex];
      if (row) setRows((current) => current.map((item, index) => index === globalIndex ? { ...item, reference: "", width1: "", height1: "", width2: "", height2: "", quantity: "1" } : item));
    } else {
      setRows((current) => current.filter((_, index) => index !== globalIndex));
    }
    invalidateReview();
  }
  function pasteGridData(event: ClipboardEvent<HTMLDivElement>, specId: string, startLocalRow: number, startColumn: number) {
    const text = event.clipboardData.getData("text/plain");
    if (!text.includes("\t") && !text.includes("\n")) return;
    event.preventDefault();
    const pasted = text.replace(/\r/g, "").split("\n").filter((line, index, all) => line !== "" || index < all.length - 1).map((line) => line.split("\t"));
    const fields: Array<keyof GridRow> = ["reference", "width1", "height1", "width2", "height2", "quantity"];
    const existingGroupRows = rows.filter((row) => row.specId === specId).length;
    const neededRows = Math.max(0, startLocalRow + pasted.length - existingGroupRows);
    const clipped = neededRows > MAX_ROWS - rows.length;
    setRows((current) => {
      const groupRows = current.map((row, index) => ({ row, index })).filter(({ row }) => row.specId === specId);
      const next = [...current];
      pasted.forEach((cells, rowOffset) => {
        const localIndex = startLocalRow + rowOffset;
        let globalIndex = groupRows[localIndex]?.index;
        if (globalIndex === undefined) {
          if (next.length >= MAX_ROWS) return;
          globalIndex = next.length;
          next.push(blankRow(globalIndex + 1, specId));
          groupRows.push({ row: next[globalIndex]!, index: globalIndex });
        }
        const updated = { ...next[globalIndex]! };
        cells.forEach((cell, columnOffset) => {
          const field = fields[startColumn + columnOffset];
          if (field) updated[field] = cell.trim();
        });
        next[globalIndex] = updated;
      });
      return next;
    });
    setIoStatus(clipped ? `Pasted rows up to the ${MAX_ROWS.toLocaleString()}-row limit; remaining clipboard rows were not added.` : `Pasted ${pasted.length.toLocaleString()} spreadsheet row${pasted.length === 1 ? "" : "s"} into Spec Group ${specs.findIndex((spec) => spec.id === specId) + 1}.`);
    invalidateReview();
  }
  function navigateGrid(event: KeyboardEvent<HTMLDivElement>, specId: string) {
    const target = event.target as HTMLInputElement;
    const currentRow = Number(target.dataset.gridRow);
    const currentColumn = Number(target.dataset.gridCol);
    if (!Number.isInteger(currentRow) || !Number.isInteger(currentColumn)) return;
    const moves: Record<string, [number, number]> = { ArrowUp: [-1, 0], ArrowDown: [1, 0], ArrowLeft: [0, -1], ArrowRight: [0, 1] };
    const move = moves[event.key];
    if (!move) return;
    const totalGroupRows = rows.filter((row) => row.specId === specId).length;
    const nextRow = Math.max(0, Math.min(totalGroupRows - 1, currentRow + move[0]));
    const nextColumn = Math.max(0, Math.min(5, currentColumn + move[1]));
    const nextInput = event.currentTarget.querySelector<HTMLInputElement>(`input[data-grid-row="${nextRow}"][data-grid-col="${nextColumn}"]`);
    if (nextInput) { event.preventDefault(); nextInput.focus(); nextInput.select(); }
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
    if (step === 2) {
      const form = rootRef.current?.closest("form");
      if (form instanceof HTMLFormElement) {
        const data = new FormData(form);
        const keys = ["company_name", "contact_name", "email", "phone", "customer_reference", "project_name", "project_location", "description"];
        setPreviewProject(Object.fromEntries(keys.map((key) => [key, String(data.get(key) ?? "").trim()])));
      }
      setReviewConfirmed(false);
    }
    setStep((current) => Math.min(3, current + 1));
  }
  function previous() { setValidation(""); setReviewConfirmed(false); setStep((current) => Math.max(0, current - 1)); }

  async function importCutList(file: File) {
    setIoStatus("Reading cutting list…");
    try {
      const imported = await parseCustomerCutListFile(file);
      const replacePlaceholder = rows.length === 1 && !rows[0]!.reference.trim() && !rows[0]!.width1 && !rows[0]!.height1 && !rows[0]!.width2 && !rows[0]!.height2;
      const baseRows = replacePlaceholder ? [] : rows;
      const accepted = imported.rows.slice(0, Math.max(0, MAX_ROWS - baseRows.length));
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
      setRows((current) => {
        const stillPlaceholder = current.length === 1 && !current[0]!.reference.trim() && !current[0]!.width1 && !current[0]!.height1 && !current[0]!.width2 && !current[0]!.height2;
        return [...(replacePlaceholder && stillPlaceholder ? [] : current), ...mappedRows];
      });
      setStep(1);
      setIoStatus(`Imported ${mappedRows.length} cutting-list line${mappedRows.length === 1 ? "" : "s"}${imported.skipped ? ` · skipped ${imported.skipped} invalid row${imported.skipped === 1 ? "" : "s"}` : ""}. Spreadsheet pricing fields, if present, were ignored.`);
      setValidation("");
        invalidateReview();
    } catch (reason) {
      setIoStatus(reason instanceof Error ? reason.message : "Could not import the cutting list.");
    } finally {
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  return (
    <div ref={rootRef} data-intake-complete={completed ? "true" : "false"} data-no-intake-advance className="space-y-5" aria-label="Proforma technical specification generator">
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
        <div className="space-y-3">
          <p className="max-w-4xl text-xs leading-5 text-text-muted">WPF Step 5 · enter or import technical cutting-list rows, add specification groups, and edit lines in a spreadsheet-style grid. Dimensions are whole millimetres. SQM is calculated from W1 × H1 using WPF’s 0.5 m² minimum; Total SQM multiplies by quantity. No rates or financial figures are shown.</p>
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={() => fileInputRef.current?.click()} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-bold text-brand-200">Import CSV / Excel</button>
            <button type="button" onClick={addSpec} disabled={specs.length >= 12} className="rounded-lg border border-brand-300/30 px-3 py-2 text-xs font-bold text-brand-200 disabled:opacity-40">+ Add specification</button>
          </div>
        </div>
        {ioStatus && <p role="status" aria-live="polite" className="rounded-lg border border-line-strong bg-ink-950/70 p-3 text-xs leading-relaxed text-text-muted">{ioStatus}</p>}
        <div className="space-y-4">
          {specs.map((spec, specIndex) => {
            const groupRows = rows.map((row, index) => ({ row, index })).filter(({ row }) => row.specId === spec.id);
            return <section key={spec.id} aria-label={`Cutting list for Spec Group ${specIndex + 1}`} className="overflow-hidden rounded-2xl border border-brand-300/25 bg-ink-900/65">
              <header className="flex flex-wrap items-center justify-between gap-3 border-b border-line-strong/70 bg-ink-950/45 px-4 py-3">
                <div className="min-w-0"><p className="text-[.62rem] font-bold uppercase tracking-[.15em] text-brand-200">Specification / Step 4 type</p><h4 className="mt-1 text-base font-bold text-text-primary">Spec Group {specIndex + 1} · {spec.kind}{spec.specName.trim() ? ` · ${spec.specName.trim()}` : ""}</h4><p className="mt-1 text-xs text-text-muted">{paneNames[spec.kind].map((name, paneIndex) => `${name}: ${spec.panes[paneIndex]?.thickness ?? "—"} ${spec.panes[paneIndex]?.colour ?? ""}`).join(" · ")}</p></div>
                <div className="flex flex-wrap gap-2"><button type="button" onClick={() => { setActiveSpecId(spec.id); setStep(0); invalidateReview(); }} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-brand-200">Edit specification</button>{specs.length > 1 && <button type="button" onClick={() => removeSpec(spec.id)} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-state-danger">Remove group</button>}<button type="button" onClick={() => addRow(spec.id)} disabled={rows.length >= MAX_ROWS} className="rounded-lg bg-brand-400 px-3 py-2 text-xs font-bold text-ink-950 disabled:opacity-40">+ Add row</button></div>
              </header>
              <div className="p-4">
                {paneNames[spec.kind].map((paneName, paneIndex) => <p key={paneName} className="mb-1 text-[.68rem] text-text-faint">{paneName} stock: {spec.panes[paneIndex]?.sheetWidth || "not set"} × {spec.panes[paneIndex]?.sheetHeight || "not set"} mm</p>)}
                <div className="mt-3 overflow-x-auto rounded-xl border border-line-strong/80" onPaste={(event) => { const target = event.target as HTMLInputElement; pasteGridData(event, spec.id, Number(target.dataset.gridRow ?? 0), Number(target.dataset.gridCol ?? 0)); }} onKeyDown={(event) => navigateGrid(event, spec.id)}>
                  <table className="w-full min-w-[1120px] border-collapse text-left text-xs" aria-label={`Editable cutting list for Spec Group ${specIndex + 1}`}>
                    <thead className="bg-ink-950 text-[.65rem] font-bold uppercase tracking-wide text-text-muted"><tr><th scope="col" className="px-3 py-2.5">#</th><th scope="col" className="px-2 py-2.5">Glass ref</th><th scope="col" className="px-2 py-2.5">W1 · mm</th><th scope="col" className="px-2 py-2.5">H1 · mm</th><th scope="col" className="px-2 py-2.5">W2 · mm</th><th scope="col" className="px-2 py-2.5">H2 · mm</th><th scope="col" className="px-2 py-2.5">Quantity</th><th scope="col" title="WPF billable panel area from W1 × H1; rounded to 3 decimals with a 0.5 m² minimum for valid dimensions" className="px-2 py-2.5">SQM</th><th scope="col" title="SQM multiplied by quantity" className="px-2 py-2.5">Total SQM</th><th scope="col" className="px-2 py-2.5">Row action</th></tr></thead>
                    <tbody>{groupRows.map(({ row, index }, localIndex) => {
                      const gridInput = (field: keyof GridRow, column: number, label: string, numeric = false) => <input value={row[field]} onChange={(event) => updateRow(index, field, event.target.value)} inputMode={numeric ? "numeric" : undefined} aria-label={`Row ${index + 1}, ${label}`} data-grid-row={localIndex} data-grid-col={column} data-grid-cell="true" className="min-h-10 w-full rounded-md border border-line-strong bg-ink-950 px-2 text-xs text-text-primary outline-none focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/30" />;
                      return <tr key={`${spec.id}-${index}`} className="border-t border-line-strong/60 bg-ink-900/40"><th scope="row" className="w-12 px-3 py-2 font-mono text-text-muted">{index + 1}</th><td className="min-w-32 px-2 py-2">{gridInput("reference", 0, "Glass reference")}</td><td className="min-w-28 px-2 py-2">{gridInput("width1", 1, "Width 1 in millimetres", true)}</td><td className="min-w-28 px-2 py-2">{gridInput("height1", 2, "Height 1 in millimetres", true)}</td><td className="min-w-28 px-2 py-2">{gridInput("width2", 3, "Width 2 in millimetres (optional)", true)}</td><td className="min-w-28 px-2 py-2">{gridInput("height2", 4, "Height 2 in millimetres (optional)", true)}</td><td className="min-w-24 px-2 py-2">{gridInput("quantity", 5, "Quantity", true)}</td><td aria-label={`Row ${index + 1}, SQM`} className="whitespace-nowrap px-2 py-2 font-mono tabular-nums text-text-secondary">{formatSqm(rowPanelSqm(row))}</td><td aria-label={`Row ${index + 1}, Total SQM`} className="whitespace-nowrap px-2 py-2 font-mono tabular-nums text-text-secondary">{formatSqm(rowTotalSqm(row))}</td><td className="px-2 py-2"><button type="button" onClick={() => removeRow(index, spec.id)} aria-label={`Remove row ${index + 1}`} className="rounded-md px-2 py-2 font-semibold text-state-danger hover:bg-state-danger/10">Remove</button></td></tr>;
                    })}</tbody>
                    <tfoot className="border-t border-line-strong bg-ink-950/70 text-xs"><tr><th scope="row" colSpan={8} className="px-3 py-2 text-right font-semibold text-text-muted">Group total SQM</th><td aria-label={`Spec Group ${specIndex + 1} total SQM`} className="whitespace-nowrap px-2 py-2 font-mono font-bold tabular-nums text-brand-100">{groupRows.reduce((total, { row }) => total + (rowTotalSqm(row) ?? 0), 0).toFixed(3)}</td><td /></tr></tfoot></table>
                </div>
                <div className="mt-3 flex flex-wrap items-end justify-between gap-3"><p className="text-[.68rem] text-text-faint">Global row numbering · paste tab-separated cells from a spreadsheet · arrow keys move between cells · {rows.length.toLocaleString()} / {MAX_ROWS.toLocaleString()} total rows</p><label className="block text-xs font-semibold text-text-secondary">Generate rows<input type="number" min={1} max={MAX_ROWS_PER_GENERATION} step={1} value={bulkRowCounts[spec.id] ?? ""} onChange={(event) => setBulkRowCounts((current) => ({ ...current, [spec.id]: event.target.value }))} className="ml-2 min-h-10 w-28 rounded-lg border border-line-strong bg-ink-950 px-2 text-sm text-text-primary" placeholder="Count" /></label><button type="button" onClick={() => generateRows(spec.id)} disabled={rows.length >= MAX_ROWS} className="rounded-lg border border-line-strong px-3 py-2 text-xs font-semibold text-text-secondary disabled:opacity-40">Add generated rows</button></div>

              </div>
            </section>;
          })}
        </div>
        <p className="text-[.68rem] leading-relaxed text-text-faint">Imported spreadsheet pricing fields are ignored. Submitted customer files contain technical request information only; the generated PI document is one unpriced Inquiry PDF.</p>
      </div>}
      {step === 2 && <div className="space-y-4 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <p className="text-xs leading-5 text-text-muted">WPF Step 6 helps identify applicable fabrication and processing requirements from the specification. This customer view contains technical requirements only: no rates, costs, margins, profit, or financial calculations.</p>
        <div className="flex flex-wrap gap-2" aria-label="Choose specification group for fabrication details">{specs.map((spec, index) => <button key={spec.id} type="button" onClick={() => setActiveSpecId(spec.id)} aria-pressed={spec.id === activeSpec.id} className={`rounded-lg border px-3 py-2 text-xs font-semibold ${spec.id === activeSpec.id ? "border-brand-300/50 bg-brand-400/10 text-brand-100" : "border-line-strong text-text-muted"}`}>Spec Group {index + 1} · {spec.kind}</button>)}</div>
        <div className="grid gap-3 sm:grid-cols-2"><SelectField label="Panel extra / overlap" value={activeSpec.panelExtra} options={panelExtras} onChange={(value) => updateSpec({ panelExtra: value })} /><SelectField label="Edge finish / polish" value={activeSpec.edgeFinish} options={edgeFinishes} onChange={(value) => updateSpec({ edgeFinish: value })} />{(activeSpec.kind === "DGU" || activeSpec.kind === "DGU + LAM") && <SelectField label="Spacer extra" value={activeSpec.spacerExtra} options={["None", "With U-Insert", "With U-Insert As Per DWG"]} onChange={(value) => updateSpec({ spacerExtra: value })} />}</div>
        <section className="rounded-xl border border-brand-300/25 bg-brand-400/[.04] p-4" aria-label="Automatically suggested technical processing"><h4 className="text-sm font-bold text-text-primary">Suggested processing · {activeSpec.specName.trim() || activeSpec.kind}</h4><p className="mt-1 text-[.68rem] leading-relaxed text-text-muted">Suggestions update automatically from the selected panel extra, spacer extra, edge finish, and pane construction. The estimator confirms final requirements.</p>{activeProcessingSuggestions.length ? <ul className="mt-3 space-y-2">{activeProcessingSuggestions.map((item) => <li key={item.key} className="rounded-lg border border-line-strong/70 bg-ink-950/70 px-3 py-2"><p className="text-xs font-semibold text-text-primary">{item.label}</p><p className="mt-1 text-[.68rem] text-text-muted">{item.reason}</p></li>)}</ul> : <p className="mt-3 rounded-lg border border-dashed border-line-strong px-3 py-3 text-xs text-text-faint">No additional automatic processing suggestion for the current selections. Add details below if the drawing requires special fabrication.</p>}</section>
        <TextAreaField label="Fabrication details" value={activeSpec.fabricationNotes} onChange={(value) => updateSpec({ fabricationNotes: value })} placeholder="Examples: overlap as per drawing, fanhole location, cut-outs, holes, shutter bonding, or argon gas details." />
        <TextAreaField label="Other technical notes" value={specNotes} onChange={updateSpecNotes} placeholder="Optional: standards, tolerance, orientation, safety requirements, or delivery constraints. Do not add pricing." />
      </div>}

      {step === 3 && <div className="space-y-5 animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none">
        <div className="rounded-xl border border-emerald-300/20 bg-emerald-300/[.05] p-4"><p className="text-sm font-bold text-emerald-100">Review technical request · no prices shown</p><p className="mt-1 max-w-3xl text-xs leading-5 text-text-muted">Check the customer-facing specification below before sending. One unpriced Inquiry PDF and any supporting drawings you attach will be sent to the ProGlass inquiry inbox; an estimator confirms production suitability in the WPF workflow.</p></div>
        {!validRows && <p role="alert" className="rounded-lg border border-state-warning/40 bg-state-warning/10 p-3 text-xs text-state-warning">The cutting list needs valid dimensions and quantities before the request can be sent.</p>}
        <div className="grid items-start gap-4 xl:grid-cols-[minmax(17rem,.75fr)_minmax(0,1.25fr)]">
          <aside className="space-y-3 rounded-xl border border-line-strong/70 bg-ink-950/55 p-4" aria-label="Technical request review checklist"><h4 className="text-sm font-bold text-text-primary">Review checklist</h4>
            <div className="rounded-lg border border-line-strong/70 p-3"><p className="text-[.65rem] font-bold uppercase tracking-wide text-brand-200">Project</p><p className="mt-2 text-xs leading-5 text-text-secondary">{previewProject.project_name || "Project name not provided"}{previewProject.project_location ? ` · ${previewProject.project_location}` : ""}</p><p className="mt-1 text-xs text-text-muted">{previewProject.company_name || "Company not provided"}{previewProject.contact_name ? ` · ${previewProject.contact_name}` : ""}</p><p className="mt-1 text-[.68rem] text-text-faint">Customer reference: {previewProject.customer_reference || "Not provided"}</p></div>
            <div className="space-y-2">{specs.map((spec, specIndex) => { const count = rows.filter((row) => row.specId === spec.id).length; const requirements = deriveCustomerPiProcessingSuggestions(spec); return <div key={spec.id} className="rounded-lg border border-line-strong/70 p-3"><p className="text-xs font-semibold text-text-primary">Spec Group {specIndex + 1} · {spec.kind}{spec.specName.trim() ? ` · ${spec.specName.trim()}` : ""}</p><p className="mt-1 text-[.68rem] text-text-muted">{count} cutting-list row{count === 1 ? "" : "s"} · {paneNames[spec.kind].length} pane build-up layer{paneNames[spec.kind].length === 1 ? "" : "s"}</p><p className="mt-1 text-[.68rem] text-text-muted">{requirements.length ? `${requirements.length} suggested processing requirement${requirements.length === 1 ? "" : "s"}` : "No extra processing suggestions"}</p></div>; })}</div>
            <div className="rounded-lg border border-line-strong/70 p-3"><p className="text-[.65rem] font-bold uppercase tracking-wide text-brand-200">Cutting list</p><p className="mt-2 text-xs text-text-secondary">{rows.length.toLocaleString()} row{rows.length === 1 ? "" : "s"} · dimensions in mm · quantities included</p><p className="mt-1 text-[.68rem] text-text-muted">Rows with incomplete dimensions: {rows.filter((row) => !rowIsValid(row, new Set(specs.map((spec) => spec.id)))).length.toLocaleString()}</p></div>
            {previewProject.description && <div className="rounded-lg border border-line-strong/70 p-3"><p className="text-[.65rem] font-bold uppercase tracking-wide text-brand-200">Project requirements</p><p className="mt-2 whitespace-pre-wrap text-xs leading-5 text-text-muted">{previewProject.description}</p></div>}
          </aside>
          <article id="pi-review-paper" className="mx-auto min-h-[1123px] w-full max-w-[794px] rounded-sm border border-slate-300 bg-white p-6 text-slate-900 shadow-xl sm:p-8" aria-label="Technical specification review preview"><header className="border-b-2 border-slate-800 pb-4"><p className="text-[.62rem] font-bold uppercase tracking-[.2em] text-slate-500">ProGlass · Customer technical request</p><h4 className="mt-2 text-2xl font-bold tracking-tight">Technical specification review</h4><div className="mt-3 grid gap-x-6 gap-y-1 text-xs text-slate-700 sm:grid-cols-2"><p><strong>Company:</strong> {previewProject.company_name || "—"}</p><p><strong>Contact:</strong> {previewProject.contact_name || "—"}</p><p><strong>Project:</strong> {previewProject.project_name || "—"}</p><p><strong>Location:</strong> {previewProject.project_location || "—"}</p><p><strong>Customer reference:</strong> {previewProject.customer_reference || "—"}</p><p><strong>Email:</strong> {previewProject.email || "—"}</p><p><strong>Phone:</strong> {previewProject.phone || "—"}</p></div></header>
            <div className="mt-5 space-y-5">{specs.map((spec, specIndex) => { const specRows = rows.filter((row) => row.specId === spec.id); const suggestions = deriveCustomerPiProcessingSuggestions(spec); return <section key={spec.id} className="break-inside-avoid"><h5 className="border-b border-slate-300 pb-1 text-sm font-bold">Spec Group {specIndex + 1} · {spec.kind}{spec.specName.trim() ? ` · ${spec.specName.trim()}` : ""}</h5><div className="mt-2 grid gap-2 text-[.68rem] sm:grid-cols-2">{paneNames[spec.kind].map((paneName, paneIndex) => { const pane = spec.panes[paneIndex]!; return <p key={paneName}><strong>{paneName}:</strong> {pane.thickness} {pane.colour} · {pane.treatment} · stock {pane.sheetWidth || "—"} × {pane.sheetHeight || "—"} mm</p>; })}</div>{(spec.kind === "DGU" || spec.kind === "DGU + LAM") && <p className="mt-2 text-[.68rem]"><strong>Spacer:</strong> {spec.spacerWidth} · {spec.spacerMaterial} · {spec.spacerExtra}</p>}{(spec.kind === "LAM" || spec.kind === "DGU + LAM") && <p className="mt-1 text-[.68rem]"><strong>Interlayer:</strong> {spec.interlayerType} {spec.interlayerThickness} · {spec.interlayerColour}</p>}<p className="mt-1 text-[.68rem]"><strong>Panel extra:</strong> {spec.panelExtra} · <strong>Edge finish:</strong> {spec.edgeFinish}</p>{suggestions.length > 0 && <p className="mt-1 text-[.68rem]"><strong>Suggested processing:</strong> {suggestions.map((item) => item.label).join("; ")}</p>}{spec.fabricationNotes.trim() && <p className="mt-1 whitespace-pre-wrap text-[.68rem]"><strong>Fabrication notes:</strong> {spec.fabricationNotes}</p>}
              <div className="mt-3 overflow-x-auto"><table className="w-full border-collapse text-left text-[.65rem]"><thead><tr className="border-y border-slate-400"><th className="py-1 pr-2">#</th><th className="py-1 pr-2">Reference</th><th className="py-1 pr-2">W1 × H1 · mm</th><th className="py-1 pr-2">W2 × H2 · mm</th><th className="py-1 pr-2">SQM</th><th className="py-1 pr-2">Total SQM</th><th className="py-1 pr-2">Qty</th></tr></thead><tbody>{specRows.map((row, rowIndex) => <tr key={`${spec.id}-print-${rowIndex}`} className="border-b border-slate-200"><td className="py-1 pr-2">{rows.findIndex((item) => item === row) + 1}</td><td className="py-1 pr-2">{row.reference || `Item ${rowIndex + 1}`}</td><td className="py-1 pr-2">{row.width1} × {row.height1}</td><td className="py-1 pr-2">{row.width2 || "—"} × {row.height2 || "—"}</td><td className="py-1 pr-2 tabular-nums">{formatSqm(rowPanelSqm(row))}</td><td className="py-1 pr-2 tabular-nums">{formatSqm(rowTotalSqm(row))}</td><td className="py-1 pr-2">{row.quantity}</td></tr>)}</tbody><tfoot><tr className="border-t border-slate-400 font-bold"><td colSpan={5} className="py-1 pr-2 text-right">Group total SQM</td><td className="py-1 pr-2 tabular-nums">{specRows.reduce((sum, row) => sum + (rowTotalSqm(row) ?? 0), 0).toFixed(3)}</td><td /></tr></tfoot></table></div></section>; })}
              {specNotes.trim() && <section className="border-t border-slate-300 pt-3 text-xs"><h5 className="font-bold">Additional technical notes</h5><p className="mt-1 whitespace-pre-wrap">{specNotes}</p></section>}
              {previewProject.description && <section className="border-t border-slate-300 pt-3 text-xs"><h5 className="font-bold">Project requirements</h5><p className="mt-1 whitespace-pre-wrap">{previewProject.description}</p></section>}
              <footer className="border-t border-slate-300 pt-3 text-[.62rem] text-slate-500">Technical request only · no prices or financial calculations · production specifications subject to estimator confirmation</footer>
            </div>
          </article>
        </div>
        <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-brand-300/30 bg-brand-400/[.05] p-4 text-xs leading-5 text-text-secondary"><input type="checkbox" checked={reviewConfirmed} onChange={(event) => setReviewConfirmed(event.target.checked)} className="mt-1 size-4 accent-brand-400" /><span><strong className="text-text-primary">I have reviewed this technical request.</strong> I confirm the specification and cutting-list information above is ready to be submitted for ProGlass review. This does not approve a price or production layout.</span></label>
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
