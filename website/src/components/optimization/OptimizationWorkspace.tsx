"use client";

/**
 * Localized web walkthrough of the WPF Optimization module's eight-tab flow.
 *
 * This is an interactive browser demo, not a web port of the WPF engine. It
 * deliberately uses the site's small illustrative packer and labels that
 * limitation in the workspace; production plans, verification and machine
 * exports remain in the Windows application.
 */

import { useMemo, useState, type ReactNode } from "react";
import { demoFor, optimizerLimits, sheetSizes } from "@/data/liveDemos";
import { packGuillotine, type PackInput, type PackResult, type PackedPiece } from "@/lib/guillotinePack";
import { useLocale, useT } from "@/i18n/provider";

type CutRow = {
  id: string;
  reference: string;
  room: string;
  job: string;
  width: number;
  height: number;
  quantity: number;
  edge: string;
  holes: number;
};

type TabId = "jobs" | "sizes" | "cut-list" | "stocks" | "settings" | "summary" | "two-d-view" | "report";

const demo = demoFor("Optimization", "Optimization", []);
const seedRows = demo.views[0]?.rows ?? [];
const CUT_ROWS: CutRow[] = seedRows.map((row, index) => ({
  id: row.piece ?? `P-${String(index + 1).padStart(2, "0")}`,
  reference: row.piece ?? `P-${String(index + 1).padStart(2, "0")}`,
  room: row.room ?? "General",
  job: index < 11 ? "JO-24118" : "JO-24119",
  width: Number(row.w ?? 0),
  height: Number(row.h ?? 0),
  quantity: Number(row.qty ?? 1),
  edge: row.edge ?? "Polished",
  holes: Number(row.holes ?? 0),
}));

const TAB_ORDER: { id: TabId; label: string }[] = [
  { id: "jobs", label: "Jobs" },
  { id: "sizes", label: "Sizes" },
  { id: "cut-list", label: "Cut List" },
  { id: "stocks", label: "Stocks" },
  { id: "settings", label: "Settings" },
  { id: "summary", label: "Summary" },
  { id: "two-d-view", label: "2D View" },
  { id: "report", label: "Report" },
];

const JOB_META = [
  { ref: "JO-24118", customer: "Northstar Facades", project: "Riverside Offices", date: "07 Nov 2026", rows: CUT_ROWS.slice(0, 11) },
  { ref: "JO-24119", customer: "Maison Verre", project: "Central Hotel", date: "06 Nov 2026", rows: CUT_ROWS.slice(11) },
];

const INITIAL_MARGINS = {
  left: optimizerLimits.marginLeft,
  right: optimizerLimits.marginRight,
  top: optimizerLimits.marginTop,
  bottom: optimizerLimits.marginBottom,
  kerf: optimizerLimits.kerf,
};

function expandRows(rows: CutRow[], allowRotate: boolean): PackInput[] {
  return rows.flatMap((row) => Array.from({ length: row.quantity }, (_, index) => ({
    id: `${row.id}-${String(index + 1).padStart(2, "0")}`,
    width: row.width,
    height: row.height,
    allowRotate,
  })));
}

function makeRun(rows: CutRow[], sheet: { width: number; height: number }, margins: typeof INITIAL_MARGINS, allowRotate: boolean): PackResult {
  return packGuillotine(expandRows(rows, allowRotate), sheet, margins);
}

function Icon({ name }: { name: "grid" | "box" | "settings" | "check" | "download" | "play" | "arrow" | "alert" }) {
  const paths: Record<typeof name, ReactNode> = {
    grid: <><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></>,
    box: <><path d="m12 3 8.5 4.5v9L12 21l-8.5-4.5v-9L12 3Z"/><path d="m3.8 7.7 8.2 4.4 8.2-4.4M12 12v9"/></>,
    settings: <><path d="M12 8.3a3.7 3.7 0 1 0 0 7.4 3.7 3.7 0 0 0 0-7.4Z"/><path d="m19.4 13.5 1.1.9-1.1 1.9-1.4-.5a7.8 7.8 0 0 1-1.6.9l-.2 1.5h-2.2l-.4-1.4a7.8 7.8 0 0 1-1.8 0l-.8 1.2-1.9-1.1.5-1.4a7.8 7.8 0 0 1-.9-1.6l-1.5-.2v-2.2l1.4-.4a7.8 7.8 0 0 1 0-1.8L7.4 8.5l1.1-1.9 1.4.5a7.8 7.8 0 0 1 1.6-.9l.2-1.5h2.2l.4 1.4a7.8 7.8 0 0 1 1.8 0l.8-1.2 1.9 1.1-.5 1.4a7.8 7.8 0 0 1 .9 1.6l1.5.2v2.2l-1.4.4a7.8 7.8 0 0 1 .1 1.7Z"/></>,
    check: <path d="m5 12 4.3 4.3L19 6.8"/>,
    download: <><path d="M12 3v12m-5-5 5 5 5-5"/><path d="M5 17v3h14v-3"/></>,
    play: <path d="m8 5 11 7-11 7V5Z"/>,
    arrow: <><path d="M5 12h14"/><path d="m13 6 6 6-6 6"/></>,
    alert: <><path d="M12 3 2.7 19.2a1.2 1.2 0 0 0 1 1.8h16.6a1.2 1.2 0 0 0 1-1.8L12 3Z"/><path d="M12 9v4m0 3h.01"/></>,
  };
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4">{paths[name]}</svg>;
}

function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <section className={`rounded-2xl border border-white/10 bg-white/[0.035] p-5 shadow-[0_18px_55px_-40px_rgba(0,0,0,.9)] ${className}`}>{children}</section>;
}

function Label({ children }: { children: ReactNode }) {
  return <span className="text-[10px] font-semibold uppercase tracking-[.14em] text-slate-500">{children}</span>;
}

function Metric({ label, value, note, tone = "blue" }: { label: string; value: string; note?: string; tone?: "blue" | "green" | "amber" | "violet" }) {
  const tones = {
    blue: "border-sky-400/20 bg-sky-400/[0.07] text-sky-300",
    green: "border-emerald-400/20 bg-emerald-400/[0.07] text-emerald-300",
    amber: "border-amber-400/20 bg-amber-400/[0.07] text-amber-300",
    violet: "border-violet-400/20 bg-violet-400/[0.07] text-violet-300",
  };
  return <div className={`rounded-xl border p-4 ${tones[tone]}`}><Label>{label}</Label><div className="mt-2 font-mono text-2xl font-semibold tracking-tight text-white">{value}</div>{note && <div className="mt-1 text-xs text-slate-400">{note}</div>}</div>;
}

function Field({ label, value, suffix = "mm", onChange }: { label: string; value: number; suffix?: string; onChange: (value: number) => void }) {
  return <label className="block"><span className="mb-2 block text-xs text-slate-400">{label}</span><span className="flex items-center rounded-lg border border-white/10 bg-black/20 focus-within:border-sky-400/40"><input className="w-full bg-transparent px-3 py-2.5 font-mono text-sm text-white outline-none" type="number" min="0" value={value} onChange={(event) => onChange(Math.max(0, Number(event.target.value) || 0))}/><span className="px-3 text-xs text-slate-500">{suffix}</span></span></label>;
}

function Table({ children }: { children: ReactNode }) {
  return <div className="overflow-x-auto rounded-xl border border-white/10"><table className="w-full min-w-[650px] border-collapse text-start text-sm">{children}</table></div>;
}

function CutTable({ rows, t }: { rows: CutRow[]; t: ReturnType<typeof useT> }) {
  return <Table><thead><tr className="bg-white/[0.045] text-[10px] uppercase tracking-[.12em] text-slate-500">{["Ref", "Job Order", "Room", "Width", "Height", "Qty", "Edge", "Holes"].map((heading) => <th key={heading} className="px-4 py-3 text-start font-semibold">{t(heading)}</th>)}</tr></thead><tbody>{rows.map((row) => <tr key={row.id} className="border-t border-white/[0.07] text-slate-300"><td className="px-4 py-3 font-mono text-xs text-sky-200">{row.reference}</td><td className="px-4 py-3 font-mono text-xs">{row.job}</td><td className="px-4 py-3">{row.room}</td><td className="px-4 py-3 font-mono">{row.width.toLocaleString()}</td><td className="px-4 py-3 font-mono">{row.height.toLocaleString()}</td><td className="px-4 py-3 font-mono">{row.quantity}</td><td className="px-4 py-3">{t(row.edge)}</td><td className="px-4 py-3 font-mono">{row.holes}</td></tr>)}</tbody></Table>;
}

function SheetDrawing({ placements, sheet, activeSheet, onSheetChange, t }: { placements: PackedPiece[]; sheet: { width: number; height: number; label: string }; activeSheet: number; onSheetChange: (value: number) => void; t: ReturnType<typeof useT> }) {
  const pagePieces = placements.filter((piece) => piece.plate === activeSheet);
  const scale = Math.min(620 / sheet.width, 430 / sheet.height);
  const sw = sheet.width * scale;
  const sh = sheet.height * scale;
  const left = (700 - sw) / 2;
  const top = 28;
  const palette = ["#55c3ff", "#64d9b3", "#b49aff", "#f3b86d", "#78a9ff", "#f28ab2"];
  return <div className="rounded-xl border border-white/10 bg-[#07111c] p-4">
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3"><div><div className="font-mono text-sm text-white">{sheet.label}</div><div className="mt-1 text-xs text-slate-500">{t("Sheet")} {activeSheet} · {pagePieces.length} {t("placed pieces")}</div></div><div className="flex items-center gap-2"><button type="button" onClick={() => onSheetChange(Math.max(1, activeSheet - 1))} className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:bg-white/5">←</button><span className="font-mono text-xs text-slate-400">{activeSheet} / {Math.max(1, Math.max(...placements.map((piece) => piece.plate), 1))}</span><button type="button" onClick={() => onSheetChange(Math.min(Math.max(1, Math.max(...placements.map((piece) => piece.plate), 1)), activeSheet + 1))} className="rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:bg-white/5">→</button></div></div>
    <div className="overflow-hidden rounded-lg border border-white/5 bg-[#050b12]"><svg role="img" aria-label={`${t("Sheet")} ${activeSheet} ${t("layout preview")}`} viewBox="0 0 700 490" className="block h-auto w-full">
      <defs><pattern id="optGrid" width="18" height="18" patternUnits="userSpaceOnUse"><path d="M18 0H0V18" fill="none" stroke="#112233" strokeWidth=".7"/></pattern></defs>
      <rect width="700" height="490" fill="url(#optGrid)"/>
      <rect x={left} y={top} width={sw} height={sh} rx="3" fill="#dce7ef" stroke="#8ca2b4" strokeWidth="2"/>
      <rect x={left + sheet.width * 0.015 * scale} y={top + sheet.height * 0.015 * scale} width={Math.max(0, sw - sheet.width * 0.03 * scale)} height={Math.max(0, sh - sheet.height * 0.03 * scale)} fill="none" stroke="#f59e0b" strokeDasharray="5 4" strokeWidth="1.4"/>
      {pagePieces.map((piece, index) => <g key={piece.id}>
        <rect x={left + piece.x * scale} y={top + piece.y * scale} width={piece.width * scale} height={piece.height * scale} fill={palette[index % palette.length]} fillOpacity=".78" stroke="#f7fbff" strokeWidth="1.1" rx="1.5"/>
        {piece.width * scale > 36 && piece.height * scale > 22 && <text x={left + (piece.x + piece.width / 2) * scale} y={top + (piece.y + piece.height / 2) * scale - 2} textAnchor="middle" fontFamily="ui-monospace,monospace" fontSize="9" fill="#07111c">{piece.id.split("-").slice(0, 2).join("-")}</text>}
        {piece.width * scale > 55 && piece.height * scale > 35 && <text x={left + (piece.x + piece.width / 2) * scale} y={top + (piece.y + piece.height / 2) * scale + 11} textAnchor="middle" fontFamily="ui-monospace,monospace" fontSize="8" fill="#07111c">{piece.width}×{piece.height}{piece.rotated ? " ↻" : ""}</text>}
      </g>)}
      <text x="18" y="474" fill="#718497" fontFamily="ui-monospace,monospace" fontSize="10">{t("MILLIMETRES")} · {sheet.width} × {sheet.height}</text>
    </svg></div>
    <div className="mt-3 flex flex-wrap gap-x-5 gap-y-2 text-[11px] text-slate-500"><span><i className="me-2 inline-block h-2.5 w-2.5 rounded-sm bg-amber-400"/>{t("Edge margin")}</span><span><i className="me-2 inline-block h-2.5 w-2.5 rounded-sm bg-sky-400"/> {t("Placed glass")}</span><span>{t("Turned piece marks show 90° rotation")}</span></div>
  </div>;
}

export function OptimizationWorkspace() {
  const t = useT();
  const locale = useLocale();
  const [activeTab, setActiveTab] = useState<TabId>("jobs");
  const [selectedJobs, setSelectedJobs] = useState<string[]>(JOB_META.map((job) => job.ref));
  const [search, setSearch] = useState("");
  const [sheetIndex, setSheetIndex] = useState(1);
  const [sheetSizeIndex, setSheetSizeIndex] = useState(1);
  const [margins, setMargins] = useState(INITIAL_MARGINS);
  const [minBreakout, setMinBreakout] = useState<number>(optimizerLimits.minBreakout);
  const [rotation, setRotation] = useState(true);
  const [effort, setEffort] = useState(3);
  const [runMode, setRunMode] = useState<"idle" | "quick" | "optimize">("idle");
  const selectedRows = useMemo(() => CUT_ROWS.filter((row) => selectedJobs.includes(row.job)), [selectedJobs]);
  const stock = sheetSizes[sheetSizeIndex] ?? sheetSizes[1]!;
  const result = useMemo(() => makeRun(selectedRows, { width: stock.w, height: stock.h }, margins, rotation), [selectedRows, stock, margins, rotation]);
  const filteredRows = useMemo(() => selectedRows.filter((row) => `${row.reference} ${row.room} ${row.job}`.toLowerCase().includes(search.toLowerCase())), [selectedRows, search]);
  const number = (value: number, maximumFractionDigits = 0) => new Intl.NumberFormat(locale, { maximumFractionDigits }).format(value);
  const placedArea = result.partAreaSqm;
  const stockArea = result.stockAreaSqm;
  const areasUsed = result.placed.length;
  const piecesTotal = selectedRows.reduce((sum, row) => sum + row.quantity, 0);

  const run = (mode: "quick" | "optimize") => {
    setRunMode(mode);
    setActiveTab(mode === "quick" ? "summary" : "two-d-view");
  };

  const exportSample = () => {
    const lines = ["sheet,piece,x_mm,y_mm,width_mm,height_mm,rotated", ...result.placed.map((piece) => [piece.plate, piece.id, piece.x, piece.y, piece.width, piece.height, piece.rotated].join(","))];
    const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8" });
    const href = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = href;
    anchor.download = "proglass-optimization-demo.csv";
    anchor.click();
    URL.revokeObjectURL(href);
  };

  const updateMargin = (key: keyof typeof INITIAL_MARGINS, value: number) => setMargins((current) => ({ ...current, [key]: value }));
  const toggleJob = (jobRef: string) => setSelectedJobs((current) => current.includes(jobRef) ? current.filter((ref) => ref !== jobRef) : [...current, jobRef]);
  const placedByPlate = new Map<number, PackedPiece[]>();
  result.placed.forEach((piece) => placedByPlate.set(piece.plate, [...(placedByPlate.get(piece.plate) ?? []), piece]));

  return <main className="min-h-screen bg-ink-950 px-4 pb-20 pt-8 text-text-primary sm:px-6 lg:px-8">
    <div className="mx-auto max-w-7xl">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div><div className="mb-3 flex items-center gap-2 text-xs text-slate-500"><span>{t("Production")}</span><Icon name="arrow"/><span className="text-sky-300">{t("Optimization")}</span></div><h1 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">{t("Optimization")}</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-400">{t("Build a cutting batch, set sheet and cutting allowances, review the layout, then inspect the report surfaces.")}</p></div>
        <div className="flex flex-wrap gap-2"><button type="button" onClick={() => run("quick")} className="inline-flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.04] px-4 py-2.5 text-sm font-medium text-slate-200 transition hover:bg-white/[0.08]"><Icon name="play"/>{t("Quick")}</button><button type="button" onClick={() => run("optimize")} className="inline-flex items-center gap-2 rounded-xl bg-sky-400 px-4 py-2.5 text-sm font-semibold text-slate-950 transition hover:bg-sky-300"><Icon name="grid"/>{t("Optimize")}</button></div>
      </div>

      <div className="mb-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><Metric label={t("Job batch")} value={String(selectedJobs.length).padStart(2, "0")} note={`${number(piecesTotal)} ${t("pieces selected")}`} tone="blue"/><Metric label={t("Stock sheets")} value={number(result.plates)} note={stock.label} tone="violet"/><Metric label={t("Placed pieces")} value={`${number(areasUsed)} / ${number(piecesTotal)}`} note={result.unplaced.length ? `${number(result.unplaced.length)} ${t("unplaced pieces")}` : t("All sample pieces placed")} tone={result.unplaced.length ? "amber" : "green"}/><Metric label={t("Glass utilization")} value={`${result.utilizationPct.toFixed(1)}%`} note={`${number(placedArea, 2)} m² / ${number(stockArea, 2)} m²`} tone="green"/></div>

      <div className="mb-4 flex items-start gap-3 rounded-xl border border-amber-300/15 bg-amber-300/[0.055] px-4 py-3 text-xs leading-5 text-amber-100/80"><span className="mt-0.5 text-amber-300"><Icon name="alert"/></span><p><strong className="font-semibold text-amber-100">{t("Interactive sample preview")}</strong> · {t("This browser workspace uses a separate illustrative packing heuristic. It is not the WPF production optimizer, its validation oracle, or a machine-ready export path.")}</p></div>

      <div className="overflow-hidden rounded-2xl border border-white/10 bg-[#0a1420] shadow-2xl shadow-black/20">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-white/10 bg-white/[0.025] px-4 py-3 sm:px-5"><div className="flex items-center gap-2"><div className="grid h-8 w-8 place-items-center rounded-lg border border-sky-400/20 bg-sky-400/10 text-sky-300"><Icon name="grid"/></div><div><div className="text-sm font-semibold text-white">{t("Cutting plan workspace")}</div><div className="text-[11px] text-slate-500">{runMode === "idle" ? t("Ready for sample run") : `${t(runMode === "quick" ? "Quick" : "Optimize")} · ${t("demo run")}`}</div></div></div><div className="flex items-center gap-2 rounded-lg border border-white/10 bg-black/20 px-2.5 py-1.5 text-[11px] text-slate-400"><span className="h-1.5 w-1.5 rounded-full bg-emerald-400"/>{t("Sample data · interactive preview")}</div></div>

        <div role="tablist" aria-label={t("Optimization workflow tabs")} className="flex gap-1 overflow-x-auto border-b border-white/10 px-2 py-2 sm:px-4">{TAB_ORDER.map((tab, index) => <button key={tab.id} type="button" role="tab" aria-selected={activeTab === tab.id} onClick={() => setActiveTab(tab.id)} className={`flex shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-xs transition ${activeTab === tab.id ? "bg-sky-400/10 text-sky-200 ring-1 ring-inset ring-sky-300/20" : "text-slate-400 hover:bg-white/[0.045] hover:text-white"}`}><span className={`font-mono text-[10px] ${activeTab === tab.id ? "text-sky-300" : "text-slate-600"}`}>{String(index + 1).padStart(2, "0")}</span>{t(tab.label)}</button>)}</div>

        <div className="min-h-[520px] p-4 sm:p-6">
          {activeTab === "jobs" && <div className="space-y-5"><div className="flex flex-wrap items-end justify-between gap-3"><div><h2 className="text-lg font-semibold text-white">{t("Jobs")}</h2><p className="mt-1 text-sm text-slate-400">{t("Choose confirmed orders and build the cutting batch.")}</p></div><label className="relative block w-full sm:w-72"><span className="sr-only">{t("Search jobs")}</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("Search jobs, rooms or references…")} className="w-full rounded-xl border border-white/10 bg-black/20 px-4 py-2.5 text-sm text-white outline-none placeholder:text-slate-600 focus:border-sky-400/40"/></label></div><div className="grid gap-3 lg:grid-cols-2">{JOB_META.map((job) => { const count = job.rows.reduce((sum, row) => sum + row.quantity, 0); const checked = selectedJobs.includes(job.ref); return <button key={job.ref} type="button" onClick={() => toggleJob(job.ref)} className={`rounded-xl border p-4 text-start transition ${checked ? "border-sky-400/30 bg-sky-400/[0.055]" : "border-white/10 bg-white/[0.02] hover:border-white/20"}`}><div className="flex items-start justify-between gap-3"><div className="flex items-start gap-3"><span className={`mt-0.5 grid h-5 w-5 place-items-center rounded-md border ${checked ? "border-sky-300 bg-sky-300 text-slate-950" : "border-white/20 text-transparent"}`}><Icon name="check"/></span><div><div className="font-mono text-sm font-semibold text-white">{job.ref}</div><div className="mt-1 text-xs text-slate-400">{job.customer} · {job.project}</div></div></div><span className="rounded-full border border-white/10 px-2.5 py-1 text-[10px] text-slate-400">{job.date}</span></div><div className="mt-4 flex gap-5 border-t border-white/[0.08] pt-3 text-xs"><span className="text-slate-500">{t("Cut rows")} <b className="ms-1 font-mono text-slate-200">{job.rows.length}</b></span><span className="text-slate-500">{t("Pieces")} <b className="ms-1 font-mono text-slate-200">{count}</b></span><span className="text-slate-500">{t("Glass")}: <b className="ms-1 text-slate-200">{t("Clear Float · 6 mm")}</b></span></div></button>; })}</div><div><div className="mb-3 flex items-center justify-between"><h3 className="text-sm font-semibold text-white">{t("Included cut rows")}</h3><span className="text-xs text-slate-500">{filteredRows.length} / {selectedRows.length}</span></div><CutTable rows={filteredRows.slice(0, 9)} t={t}/></div></div>}

          {activeTab === "sizes" && <div className="space-y-5"><div><h2 className="text-lg font-semibold text-white">{t("Sizes")}</h2><p className="mt-1 text-sm text-slate-400">{t("Grouped glass specifications and the sizes released to this run.")}</p></div><Card className="border-sky-300/15"><div className="flex flex-wrap items-center justify-between gap-4"><div><Label>{t("Glass composition")}</Label><div className="mt-2 text-base font-semibold text-white">{t("Clear Float · 6 mm")}</div><div className="mt-1 text-xs text-slate-500">{t("Outer pane · single glazed sample group")}</div></div><div className="flex gap-6"><div><Label>{t("Cut rows")}</Label><div className="mt-2 font-mono text-xl text-white">{number(selectedRows.length)}</div></div><div><Label>{t("Total pieces")}</Label><div className="mt-2 font-mono text-xl text-white">{number(piecesTotal)}</div></div><div><Label>{t("Gross area")}</Label><div className="mt-2 font-mono text-xl text-white">{number(selectedRows.reduce((sum, row) => sum + row.width * row.height * row.quantity, 0) / 1_000_000, 2)} m²</div></div></div></div></Card><CutTable rows={filteredRows} t={t}/></div>}

          {activeTab === "cut-list" && <div className="space-y-5"><div className="flex flex-wrap items-end justify-between gap-3"><div><h2 className="text-lg font-semibold text-white">{t("Cut List")}</h2><p className="mt-1 text-sm text-slate-400">{t("Piece-by-piece input to the sample nesting run.")}</p></div><label className="relative block w-full sm:w-72"><span className="sr-only">{t("Search cut list")}</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("Find a reference or room…")} className="w-full rounded-xl border border-white/10 bg-black/20 px-4 py-2.5 text-sm text-white outline-none placeholder:text-slate-600 focus:border-sky-400/40"/></label></div><div className="grid gap-3 sm:grid-cols-3"><Metric label={t("Input lines")} value={number(selectedRows.length)} tone="blue"/><Metric label={t("Input pieces")} value={number(piecesTotal)} tone="violet"/><Metric label={t("Unplaced in demo run")} value={number(result.unplaced.length)} tone={result.unplaced.length ? "amber" : "green"}/></div><CutTable rows={filteredRows} t={t}/></div>}

          {activeTab === "stocks" && <div className="space-y-5"><div><h2 className="text-lg font-semibold text-white">{t("Stocks")}</h2><p className="mt-1 text-sm text-slate-400">{t("Select the sample sheet size and its available inventory.")}</p></div><div className="grid gap-3 md:grid-cols-2">{sheetSizes.map((size, index) => <button key={size.label} type="button" onClick={() => setSheetSizeIndex(index)} className={`rounded-xl border p-4 text-start transition ${index === sheetSizeIndex ? "border-sky-400/30 bg-sky-400/[0.055]" : "border-white/10 bg-white/[0.02] hover:border-white/20"}`}><div className="flex items-center justify-between"><div className="font-mono text-sm font-semibold text-white">{size.label}</div><span className={`grid h-5 w-5 place-items-center rounded-full border ${index === sheetSizeIndex ? "border-sky-300 bg-sky-300 text-slate-950" : "border-white/20 text-transparent"}`}><Icon name="check"/></span></div><div className="mt-2 text-xs text-slate-400">{size.w.toLocaleString()} × {size.h.toLocaleString()} mm</div><div className="mt-4 flex items-center justify-between border-t border-white/[0.08] pt-3 text-xs"><span className="text-slate-500">{t("Available")}</span><span className="font-mono text-emerald-300">{index === 1 ? "46" : index === 2 ? "18" : "12"} {t("sheets")}</span></div></button>)}</div><div className="flex flex-wrap items-center gap-3 rounded-xl border border-white/10 bg-black/15 p-4 text-xs text-slate-400"><Icon name="box"/><span>{t("Selected stock")}: <b className="font-mono text-slate-200">{stock.w.toLocaleString()} × {stock.h.toLocaleString()} mm</b></span><span className="text-slate-700">·</span><span>{t("Material group")}: <b className="text-slate-200">{t("Clear Float · 6 mm")}</b></span></div></div>}

          {activeTab === "settings" && <div className="space-y-5"><div><h2 className="text-lg font-semibold text-white">{t("Settings")}</h2><p className="mt-1 text-sm text-slate-400">{t("Configure the same categories of allowances and search controls as the WPF workspace.")}</p></div><div className="grid gap-4 xl:grid-cols-2"><Card><div className="mb-5 flex items-center gap-2"><span className="text-sky-300"><Icon name="box"/></span><h3 className="text-sm font-semibold text-white">{t("Plate geometry & trim margins")}</h3></div><div className="mb-4 rounded-lg border border-white/[0.08] bg-black/15 px-3 py-2 text-xs text-slate-400">{t("Thickness profile")} <b className="ms-2 font-mono text-white">6 mm · Thin</b></div><div className="grid gap-4 sm:grid-cols-2"><Field label={t("Left margin · LM")} value={margins.left} onChange={(value) => updateMargin("left", value)}/><Field label={t("Right margin · RM")} value={margins.right} onChange={(value) => updateMargin("right", value)}/><Field label={t("Top margin · TM")} value={margins.top} onChange={(value) => updateMargin("top", value)}/><Field label={t("Bottom margin · BM")} value={margins.bottom} onChange={(value) => updateMargin("bottom", value)}/></div><p className="mt-4 text-xs leading-5 text-slate-500">{t("No pane is placed inside an edge allowance. Changing these values reruns the browser preview metrics.")}</p></Card><Card><div className="mb-5 flex items-center gap-2"><span className="text-violet-300"><Icon name="settings"/></span><h3 className="text-sm font-semibold text-white">{t("Cut rules & search effort")}</h3></div><div className="grid gap-4 sm:grid-cols-2"><Field label={t("Kerf · blade gap")} value={margins.kerf} onChange={(value) => updateMargin("kerf", value)}/><Field label={t("Minimum breakout")} value={minBreakout} onChange={setMinBreakout}/></div><label className="mt-4 block"><span className="mb-2 block text-xs text-slate-400">{t("Optimize effort / strategy stage")}</span><select value={effort} onChange={(event) => setEffort(Number(event.target.value))} className="w-full rounded-lg border border-white/10 bg-[#101a26] px-3 py-2.5 text-sm text-white outline-none focus:border-sky-400/40">{[1, 2, 3, 4, 5].map((level) => <option key={level} value={level}>{level} — {t(level === 1 ? "Quick" : level < 4 ? "Balanced" : "Deep search")}</option>)}</select></label><label className="mt-4 flex cursor-pointer items-start gap-3 rounded-lg border border-white/[0.08] bg-black/15 p-3"><input type="checkbox" checked={rotation} onChange={(event) => setRotation(event.target.checked)} className="mt-0.5 accent-sky-400"/><span><b className="block text-xs font-medium text-white">{t("Allow rotation (0° / 90°)")}</b><span className="mt-1 block text-[11px] leading-5 text-slate-500">{t("Use only where the glass specification permits a 90° turn.")}</span></span></label><div className="mt-4 rounded-lg border border-white/[0.08] bg-black/15 p-3"><div className="text-xs font-medium text-slate-300">{t("Cutting complexity")}</div><div className="mt-2 flex flex-wrap gap-2">{["Unlimited guillotine", "Standard", "Reduced cut depth"].map((label, index) => <span key={label} className={`rounded-lg border px-2.5 py-1.5 text-[11px] ${index === 1 ? "border-sky-300/25 bg-sky-300/10 text-sky-200" : "border-white/10 text-slate-500"}`}>{t(label)}</span>)}</div><p className="mt-2 text-[11px] leading-5 text-slate-500">{t("The browser heuristic does not apply minimum-breakout, cutting-complexity, search-stage, or plate-pattern policies.")}</p></div><div className="mt-4 rounded-lg border border-white/[0.08] bg-black/15 p-3"><div className="text-xs font-medium text-slate-300">{t("Plate pattern")}</div><div className="mt-2 flex flex-wrap gap-2">{["Yield", "Fill first", "Waste aware"].map((label, index) => <span key={label} className={`rounded-lg border px-2.5 py-1.5 text-[11px] ${index === 0 ? "border-sky-300/25 bg-sky-300/10 text-sky-200" : "border-white/10 text-slate-500"}`}>{t(label)}</span>)}</div></div></Card></div><div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-white/10 bg-white/[0.025] px-4 py-3"><span className="text-xs text-slate-500">{t("House defaults shown for the selected sample thickness.")}</span><button type="button" onClick={() => setMargins(INITIAL_MARGINS)} className="rounded-lg border border-white/10 px-3 py-2 text-xs font-medium text-slate-300 hover:bg-white/5">{t("Load house defaults")}</button></div></div>}

          {activeTab === "summary" && <div className="space-y-5"><div className="flex flex-wrap items-end justify-between gap-3"><div><h2 className="text-lg font-semibold text-white">{t("Summary")}</h2><p className="mt-1 text-sm text-slate-400">{t("Run-level yield, stock consumption, waste and exceptions.")}</p></div><button type="button" onClick={() => run(runMode === "quick" ? "quick" : "optimize")} className="inline-flex items-center gap-2 rounded-lg border border-white/10 px-3 py-2 text-xs text-slate-300 hover:bg-white/5"><Icon name="play"/>{t("Re-run sample")}</button></div><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><Metric label={t("Sheets used")} value={number(result.plates)} note={stock.label} tone="blue"/><Metric label={t("Utilization")} value={`${result.utilizationPct.toFixed(1)}%`} note={t("Placed glass area / full stock area")} tone="green"/><Metric label={t("Placed pieces")} value={number(result.placedPieces)} note={`${number(result.unplaced.length)} ${t("unplaced")}`} tone={result.unplaced.length ? "amber" : "violet"}/><Metric label={t("Wastage area") } value={`${number(Math.max(0, stockArea - placedArea), 2)} m²`} note={t("Sample calculation; excludes remnant valuation.")} tone="amber"/></div><div className="grid gap-4 lg:grid-cols-[1.2fr_.8fr]"><Card><div className="mb-4 flex items-center justify-between"><h3 className="text-sm font-semibold text-white">{t("Per-sheet execution schedule")}</h3><span className="text-[10px] text-slate-500">{t("Demo heuristic")}</span></div><Table><thead><tr className="bg-white/[0.045] text-[10px] uppercase tracking-wider text-slate-500"><th className="px-3 py-2.5 text-start">{t("Sheet")}</th><th className="px-3 py-2.5 text-start">{t("Stock")}</th><th className="px-3 py-2.5 text-end">{t("Pieces")}</th><th className="px-3 py-2.5 text-end">{t("Yield")}</th></tr></thead><tbody>{Array.from({ length: result.plates }, (_, index) => { const pieces = placedByPlate.get(index + 1) ?? []; const area = pieces.reduce((sum, piece) => sum + piece.width * piece.height, 0); const yieldPct = stock.w * stock.h ? area / (stock.w * stock.h) * 100 : 0; return <tr key={index} className="border-t border-white/[0.07] text-xs text-slate-300"><td className="px-3 py-2.5 font-mono">{String(index + 1).padStart(2, "0")}</td><td className="px-3 py-2.5 font-mono">{stock.w}×{stock.h}</td><td className="px-3 py-2.5 text-end font-mono">{pieces.length}</td><td className="px-3 py-2.5 text-end font-mono">{yieldPct.toFixed(1)}%</td></tr>; })}</tbody></Table></Card><Card><h3 className="text-sm font-semibold text-white">{t("Run notes")}</h3><div className="mt-4 space-y-3"><div className={`flex gap-3 rounded-lg border p-3 text-xs leading-5 ${result.unplaced.length ? "border-amber-300/20 bg-amber-300/[0.06] text-amber-100/80" : "border-emerald-300/15 bg-emerald-300/[0.045] text-emerald-100/80"}`}><span className="mt-0.5"><Icon name={result.unplaced.length ? "alert" : "check"}/></span><span>{result.unplaced.length ? `${result.unplaced.length} ${t("piece(s) could not be placed on the selected stock size.")}` : t("Every sample piece was placed by the browser heuristic.")}</span></div><div className="rounded-lg border border-white/[0.08] bg-black/15 p-3 text-xs leading-5 text-slate-400">{t("This summary is illustrative. WPF verification, breakout diagnostics, stock-cap validation and cut-sequence auditing are not run in the browser.")}</div><div className="text-[11px] text-slate-500">{t("Search effort")}: <span className="font-mono text-slate-300">{effort} / 5</span> · {t("Rotation")}: <span className="text-slate-300">{rotation ? t("Allowed") : t("Locked")}</span></div></div></Card></div></div>}

          {activeTab === "two-d-view" && <div className="space-y-5"><div className="flex flex-wrap items-end justify-between gap-3"><div><h2 className="text-lg font-semibold text-white">{t("2D View")}</h2><p className="mt-1 text-sm text-slate-400">{t("Sheet-by-sheet placement preview, with turned pieces marked.")}</p></div><div className="flex items-center gap-2 rounded-lg border border-white/10 bg-black/15 px-3 py-2 text-xs text-slate-400"><Icon name="box"/>{t("Gallery view")} · {number(result.plates)} {t("sheets")}</div></div>{result.plates > 0 ? <SheetDrawing placements={result.placed} sheet={{ width: stock.w, height: stock.h, label: stock.label }} activeSheet={Math.min(sheetIndex, Math.max(1, result.plates))} onSheetChange={setSheetIndex} t={t}/> : <Card><p className="text-sm text-slate-400">{t("Choose at least one job with pieces that fit the selected stock.")}</p></Card>}<Card className="flex flex-wrap items-center justify-between gap-3"><div><h3 className="text-sm font-semibold text-white">{t("Cutting layout")}</h3><p className="mt-1 text-xs text-slate-500">{t("Illustrative drawing only · no WPF-derived cut sequence is shown.")}</p></div><div className="font-mono text-xs text-slate-400">{stock.w} × {stock.h} mm · {margins.kerf} mm {t("kerf")}</div></Card></div>}

          {activeTab === "report" && <div className="space-y-5"><div className="flex flex-wrap items-end justify-between gap-3"><div><h2 className="text-lg font-semibold text-white">{t("Report")}</h2><p className="mt-1 text-sm text-slate-400">{t("Report sections and export formats surfaced by the WPF module.")}</p></div><button type="button" onClick={exportSample} className="inline-flex items-center gap-2 rounded-xl bg-sky-400 px-4 py-2.5 text-sm font-semibold text-slate-950 hover:bg-sky-300"><Icon name="download"/>{t("Download demo CSV")}</button></div><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">{["Industrial cut schedule", "Nested sheets summary", "Merged specifications summary", "Stock plates allocation"].map((section, index) => <Card key={section}><div className="font-mono text-xs text-sky-300">0{index + 1}</div><div className="mt-3 text-sm font-semibold text-white">{t(section)}</div><p className="mt-2 text-xs leading-5 text-slate-500">{t("Report section shown in the WPF report workflow.")}</p></Card>)}</div><Card><div className="mb-4 flex items-center justify-between"><h3 className="text-sm font-semibold text-white">{t("Available WPF outputs")}</h3><span className="text-[10px] uppercase tracking-wider text-slate-500">{t("Reference only")}</span></div><div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">{["CSV", "Excel", "Print PDF ticket", "Glass stickers", "Generic DXF", "Cut-list CSV"].map((format) => <div key={format} className="flex items-center gap-3 rounded-lg border border-emerald-300/15 bg-emerald-300/[0.04] px-3 py-3 text-xs text-slate-200"><span className="text-emerald-300"><Icon name="check"/></span>{t(format)}<span className="ms-auto text-[10px] text-emerald-200/60">{t("WPF")}</span></div>)}</div><div className="mt-4 border-t border-white/[0.08] pt-4"><div className="mb-2 text-xs font-semibold text-slate-300">{t("Not implemented in the inspected WPF view")}</div><div className="flex flex-wrap gap-2">{["Bottero .OPT", "Lisec .DAT", "Intermac .ISO"].map((format) => <span key={format} className="rounded-full border border-amber-300/20 bg-amber-300/[0.05] px-3 py-1.5 text-[11px] text-amber-100/75">{format} · {t("not implemented")}</span>)}</div></div><p className="mt-4 text-xs leading-5 text-slate-500">{t("The download button exports only the browser demo's CSV. It does not generate WPF reports or CNC machine files.")}</p></Card></div>}
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-white/10 bg-black/10 px-4 py-3 sm:px-6"><div className="text-[11px] text-slate-500">{t("Workflow reference")}: <span className="text-slate-300">{t("Jobs → Sizes → Cut List → Stocks → Settings → Summary → 2D View → Report")}</span></div><button type="button" onClick={() => setActiveTab(TAB_ORDER[(TAB_ORDER.findIndex((tab) => tab.id === activeTab) + 1) % TAB_ORDER.length]!.id)} className="inline-flex items-center gap-2 rounded-lg border border-white/10 px-3 py-2 text-xs text-slate-300 hover:bg-white/5">{t("Next tab")}<Icon name="arrow"/></button></div>
      </div>

      <div className="mt-4 text-center text-[11px] leading-5 text-slate-600">{t("Source workflow: WPF Optimization module · Views/Optimization/Tabs · website simulation uses independent sample data and a separate browser heuristic.")}</div>
    </div>
  </main>;
}
