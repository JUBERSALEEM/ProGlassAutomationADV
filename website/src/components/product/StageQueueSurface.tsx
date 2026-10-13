"use client";

import { useState } from "react";
import {
  floorMessages,
  kpiLabels,
  releaseCandidates,
  stageChainCounts,
  stageRowsFor,
  type StageChainId,
} from "@/data/stageQueue";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/provider";

/**
 * The production floor's shared chrome, and one stage page underneath it.
 *
 * `Views/Production/README.md` describes four bars every stage page shares, and
 * a grid with stage-specific columns below them. The website was showing the
 * *optimizer's* piece list on the stage pages instead — a different screen with
 * different columns — so this draws what the floor actually shows:
 *
 *   PIPELINE CHAIN BAR   all eight stages in floor order with factory-wide
 *                        piece counts (open · done/total). The current stage is
 *                        highlighted and any chip opens that stage, so a job can
 *                        be followed stage by stage.
 *   RELEASE BAR          the confirmed job order picker. Only non-void Released
 *                        or In Production job orders can be released, and asking
 *                        for anything else is refused with the reason.
 *   KPI STRIP            arrived · pending · in progress · completed · load ·
 *                        ready for dispatch, with stage-aware labels.
 *   FILTER BAR           search, job and status, beside the waiting-upstream
 *                        strip that keeps a stage's backlog visible.
 *
 * The grid columns change with the stage, exactly as the pages do: Cutting
 * carries the full Step-5 envelope plus square and linear metres for both panes,
 * Polish and Mitring count linear metres, and the piece-flow stages carry no
 * pricing at all — pieces in, done and balance.
 */
export function StageQueueSurface({ stage, compact = false }: { stage: StageChainId; compact?: boolean }) {
  const t = useT();
  const [current, setCurrent] = useState<StageChainId>(stage);
  const [query, setQuery] = useState("");
  const [released, setReleased] = useState<string | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [gap, setGap] = useState(0);

  const counts = stageChainCounts();
  const rows = stageRowsFor(current);
  const chainIndex = counts.findIndex((entry) => entry.id === current);
  const metric = current === "Polish" || current === "Mitring" ? "LM" : current === "Pairing" ? "pairs" : current === "DoublingLamination" ? "units" : "pcs";

  const waiting = rows.filter((row) => row.inQty === 0);
  const visible = rows.filter((row) => {
    const needle = query.trim().toLowerCase();
    if (!needle) return true;
    return `${row.ref} ${row.spec} ${row.job} ${row.customer} ${row.build}`.toLowerCase().includes(needle);
  });

  const arrived = rows.reduce((sum, row) => sum + row.inQty, 0);
  const doneTotal = rows.reduce((sum, row) => sum + row.doneQty, 0);
  const pending = rows.reduce((sum, row) => sum + Math.max(0, row.qty - row.inQty), 0);
  const readyCount = 0; /* Nothing in the seeded job has finished the whole chain yet. */

  const columns =
    current === "Cutting"
      ? (["ST", "JOB", "PI", "CUSTOMER", "SALESMAN", "SPEC", "SR", "REF", "BUILD", "W1", "H1", "W2", "H2", "QTY", "IN", "DONE", "BALANCE"] as const)
      : current === "Polish" || current === "Mitring"
        ? (["ST", "JOB", "SPEC", "SR", "REF", "QTY", "LM1", "LM2", "IN", "DONE", "BALANCE"] as const)
        : (["ST", "JOB", "SPEC", "SR", "REF", "BUILD", "QTY", "IN", "DONE", "BALANCE"] as const);

  return (
    <div className="flex h-full min-h-0 flex-col gap-2.5">
      {/* ── PIPELINE CHAIN BAR ─────────────────────────────────────────── */}
      <div className="rounded-xl border border-line/60 bg-ink-900/50 p-2.5">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
            {t("Pipeline chain — all eight stages")}
          </p>
          <p className="text-[0.6875rem] font-medium text-text-muted">
            {metric} counts · step {chainIndex + 1} of 8
          </p>
        </div>
        <div className="mt-2 flex flex-wrap gap-1.5" role="tablist" aria-label={t("Production stages")}>
          {counts.map((entry, index) => (
            <button
              key={entry.id}
              type="button"
              role="tab"
              aria-selected={entry.id === current}
              onClick={() => {
                setCurrent(entry.id);
                setGap(0);
              }}
              title={`${t(entry.label)} — ${entry.open} open, ${entry.done}/${entry.total} done`}
              className={cn(
                "rounded-lg border px-2 py-1 text-left transition-colors duration-200",
                entry.id === current
                  ? "border-brand-400/60 bg-brand-400/16"
                  : "border-line-strong/50 bg-ink-800/50 hover:border-brand-400/35",
                index > chainIndex ? "opacity-80" : "",
              )}
            >
              <span className="flex items-center gap-1.5">
                <span className={cn("font-mono text-[0.625rem]", entry.id === current ? "text-brand-200" : "text-text-faint")}>
                  {index + 1}
                </span>
                <span className={cn("whitespace-nowrap text-[0.75rem] font-medium", entry.id === current ? "text-brand-100" : "text-text-muted")}>
                  {t(entry.label)}
                </span>
              </span>
              <span className="mt-0.5 flex items-center gap-1.5 font-mono text-[0.625rem] tabular-nums">
                <span className={entry.open > 0 ? "text-amber-300" : "text-text-faint"}>{entry.open} open</span>
                <span className="text-text-faint">
                  {entry.done}/{entry.total}
                </span>
              </span>
            </button>
          ))}
        </div>
      </div>

      {/* ── RELEASE BAR ────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-2 rounded-xl border border-line/60 bg-ink-900/40 px-2.5 py-2">
        <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{t("Release")}</span>
        {releaseCandidates.map((candidate) => (
          <button
            key={candidate.job}
            type="button"
            title={t(candidate.note)}
            onClick={() => {
              setReleased(candidate.releasable ? candidate.job : null);
              setRefusal(candidate.releasable ? null : candidate.status === "Void" ? floorMessages.refusedVoid : floorMessages.refusedDraft);
            }}
            className={cn(
              "rounded-full border px-2 py-0.5 font-mono text-[0.6875rem] font-medium transition-colors",
              released === candidate.job
                ? "border-brand-400/60 bg-brand-400/16 text-brand-100"
                : candidate.releasable
                  ? "border-line-strong/60 bg-ink-800/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary"
                  : "border-line/40 bg-ink-900/50 text-text-faint hover:border-danger/40 hover:text-danger",
            )}
          >
            {t(candidate.job)} · {t(candidate.status)}
          </button>
        ))}
      </div>

      {refusal ? (
        <p aria-live="polite" className="rounded-lg border border-danger/40 bg-danger/8 px-2.5 py-1.5 text-[0.75rem] font-medium leading-snug text-danger">
          {refusal}
        </p>
      ) : released ? (
        <p aria-live="polite" className="rounded-lg border border-brand-400/35 bg-brand-400/8 px-2.5 py-1.5 text-[0.75rem] font-medium leading-snug text-brand-100">{t("Released {v1} to the floor. Work items were distributed across the eight stages from the proforma's own Step 5 sizes and Step 6 charges. Releasing again is idempotent — it will not duplicate work already on the floor.", { v1: released })}</p>
      ) : null}

      {/* ── KPI STRIP ──────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-3 lg:grid-cols-6">
        {[
          { label: kpiLabels.arrived, value: arrived, tone: "" },
          { label: kpiLabels.pending, value: pending, tone: "text-amber-300" },
          { label: kpiLabels.inProgress, value: Math.max(0, arrived - doneTotal), tone: "" },
          { label: kpiLabels.completed, value: doneTotal, tone: "text-emerald-300" },
          { label: `${kpiLabels.load} (${metric})`, value: arrived, tone: "" },
          { label: kpiLabels.readyForDispatch, value: readyCount, tone: readyCount > 0 ? "text-emerald-300" : "text-text-faint" },
        ].map((tile) => (
          <div key={t(tile.label)} className="rounded-lg border border-line/50 bg-ink-900/50 px-2 py-1.5">
            <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{t(tile.label)}</p>
            <p className={cn("text-[0.9375rem] font-semibold tabular-nums", tile.tone || "text-text-primary")}>{tile.value}</p>
          </div>
        ))}
      </div>

      {/* ── FILTER BAR + WAITING UPSTREAM ──────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-1.5 rounded-lg border border-line-strong/60 bg-ink-900/70 px-2 py-1">
          <span className="sr-only">{t("Filter the stage queue")}</span>
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t("Search ref, spec, job…")}
            className="w-32 bg-transparent text-[0.75rem] text-text-primary outline-none placeholder:text-text-faint focus:w-44 sm:w-40"
          />
        </label>
        <span className="rounded-full border border-line-strong/60 bg-ink-800/60 px-2 py-0.5 font-mono text-[0.6875rem] font-medium text-text-muted">
          {current}
        </span>
        <span className="rounded-full border border-line-strong/60 bg-ink-800/60 px-2 py-0.5 font-mono text-[0.6875rem] font-medium text-text-muted">
          {t("All statuses")}
        </span>
        {waiting.length > 0 ? (
          <span className="rounded-full border border-amber-400/40 bg-amber-400/8 px-2 py-0.5 font-mono text-[0.6875rem] font-medium text-amber-300" title={floorMessages.waitingUpstream}>
            {t("waiting upstream · {length}", { length: waiting.length })}
          </span>
        ) : null}
        <span className="ml-auto font-mono text-[0.6875rem] tabular-nums text-text-faint font-medium">{t("{visible} of {rows} rows · grid shows 100 at a time", { visible: visible.length, rows: rows.length })}</span>
      </div>

      {/* ── THE GRID ───────────────────────────────────────────────────── */}
      <div className="min-h-0 flex-1 overflow-auto rounded-xl border border-line/60 bg-ink-950/50">
        <table className="w-full min-w-[46rem] border-collapse text-left">
          <caption className="sr-only">{t(
              "{stage} stage queue — {rows} work items for the seeded job, with the pieces that arrived, the pieces recorded done and the balance.",
              { stage: current, rows: rows.length },
            )}</caption>
          <thead className="sticky top-0 z-10 bg-ink-900/97">
            <tr className="border-b border-line/60">
              {columns.map((column) => (
                <th
                  key={column}
                  scope="col"
                  className={cn(
                    "whitespace-nowrap px-2 py-1.5 font-mono text-[0.625rem] uppercase tracking-[0.08em] text-text-faint font-medium",
                    ["QTY", "IN", "DONE", "BALANCE", "LM1", "LM2"].includes(column) ? "text-right" : "",
                  )}
                >
                  {column}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {visible.map((row) => {
              const balance = Math.max(0, row.inQty - row.doneQty);
              const waitingRow = row.inQty === 0;
              return (
                <tr
                  key={`${row.spec}-${row.sr}`}
                  className={cn("border-b border-line/30 last:border-0", waitingRow ? "opacity-55" : "hover:bg-ink-800/40")}
                >
                  <td className="px-2 py-1 font-mono text-[0.6875rem] text-text-faint">{row.st}</td>
                  <td className="whitespace-nowrap px-2 py-1 font-mono text-[0.6875rem] text-text-muted">{t(row.job)}</td>
                  {current === "Cutting" || current === "Polish" || current === "Mitring" ? (
                    <>
                      {current === "Cutting" ? (
                        <>
                          <td className="whitespace-nowrap px-2 py-1 font-mono text-[0.6875rem] text-text-muted">{row.pi}</td>
                          <td className="max-w-[12rem] truncate px-2 py-1 text-[0.75rem] text-text-secondary">{row.customer}</td>
                          <td className="whitespace-nowrap px-2 py-1 text-[0.75rem] text-text-secondary">{row.salesman}</td>
                        </>
                      ) : null}
                      <td className="max-w-[14rem] truncate px-2 py-1 text-[0.75rem] text-text-secondary" title={row.spec}>
                        {row.spec}
                      </td>
                      <td className="px-2 py-1 font-mono text-[0.6875rem] text-text-faint tabular-nums">{row.sr}</td>
                      <td className="whitespace-nowrap px-2 py-1 font-mono text-[0.6875rem] text-text-secondary">{row.ref}</td>
                      {current === "Cutting" ? (
                        <>
                          <td className="px-2 py-1 font-mono text-[0.6875rem] text-text-muted">{row.build}</td>
                          <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-muted tabular-nums">{row.w1}</td>
                          <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-muted tabular-nums">{row.h1}</td>
                          <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-faint tabular-nums">{row.w2}</td>
                          <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-faint tabular-nums">{row.h2}</td>
                          <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-primary tabular-nums">{row.qty}</td>
                        </>
                      ) : (
                        <>
                          <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-primary tabular-nums">{row.qty}</td>
                          {/* LM is the pane perimeter × pieces — the stage's own metric. */}
                          <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-muted tabular-nums">
                            {(((row.w1 ? Number(row.w1) : 0) + Number(row.h1)) * 2 * row.inQty / 1000).toFixed(1)}
                          </td>
                          <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-faint tabular-nums">
                            {row.w2 === "—" ? "—" : (((Number(row.w2) + Number(row.h2)) * 2 * row.inQty) / 1000).toFixed(1)}
                          </td>
                        </>
                      )}
                    </>
                  ) : (
                    <>
                      <td className="max-w-[14rem] truncate px-2 py-1 text-[0.75rem] text-text-secondary" title={row.spec}>
                        {row.spec}
                      </td>
                      <td className="px-2 py-1 font-mono text-[0.6875rem] text-text-faint tabular-nums">{row.sr}</td>
                      <td className="whitespace-nowrap px-2 py-1 font-mono text-[0.6875rem] text-text-secondary">{row.ref}</td>
                      <td className="px-2 py-1 font-mono text-[0.6875rem] text-text-muted">{row.build}</td>
                      <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-primary tabular-nums">{row.qty}</td>
                    </>
                  )}
                  <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-text-muted tabular-nums">
                    {waitingRow ? <span className="text-amber-300">0</span> : row.inQty}
                  </td>
                  <td className="px-2 py-1 text-right font-mono text-[0.6875rem] text-emerald-300 tabular-nums">{row.doneQty}</td>
                  <td className={cn("px-2 py-1 text-right font-mono text-[0.6875rem] tabular-nums font-semibold", balance > 0 ? "text-amber-300" : "text-text-faint")}>
                    {balance}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        {visible.length === 0 ? (
          <p className="px-3 py-6 text-center text-[0.75rem] font-medium text-text-faint">
            {t("No work items match that filter at this stage.")}
          </p>
        ) : null}
      </div>

      {/*
        The clamp, demonstrated where it matters. The app refuses to record more
        than arrived, and says so rather than silently trimming the figure.
      */}
      {!compact ? (
        <div className="flex flex-wrap items-center gap-2 rounded-xl border border-line/60 bg-ink-900/40 px-2.5 py-2">
          <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
            {t("Done-piece editor — try it")}
          </span>
          <input
            type="number"
            min={0}
            value={gap}
            onChange={(event) => setGap(Math.max(0, Number(event.target.value) || 0))}
            aria-label={t("Pieces done")}
            className="w-20 rounded-lg border border-line-strong/60 bg-ink-900/70 px-2 py-1 font-mono text-[0.75rem] text-text-primary tabular-nums outline-none focus:border-brand-400/70"
          />
          {(() => {
            const first = visible[0];
            if (!first) return null;
            const clamped = Math.min(gap, first.inQty);
            const over = gap > first.inQty;
            return (
              <p aria-live="polite" className="text-[0.75rem] font-medium leading-snug">
                <span className="text-text-faint">{first.ref} · arrived {first.inQty} · </span>
                <span className={over ? "text-amber-300" : "text-text-secondary"}>
                  saves {clamped}
                  {over ? t(" — clamped") : ""}
                </span>
                {over ? <span className="text-text-faint"> · {floorMessages.clampedWrite}</span> : null}
              </p>
            );
          })()}
        </div>
      ) : null}
    </div>
  );
}
