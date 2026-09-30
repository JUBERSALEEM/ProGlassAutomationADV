"use client";

import { useMemo, useState } from "react";
import { SampleTag } from "@/components/product/previewAtoms";
import {
  auditSections,
  logColumns,
  logLevels,
  logSurfaceStrings,
  seededAudit,
  seededDocuments,
  seededLog,
  type LogLevelId,
} from "@/data/activityLog";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/provider";

/**
 * Logs & Activity — the Diagnostics module, working.
 *
 * The site previously showed this module as five invented rows ("Database
 * files · Pass", "15 locales loaded") under a "Checks" heading. None of that is
 * in the application. The real screen is a rolling terminal, and this is it:
 *
 *   LOG ENTRIES     the last 500 kept in memory, newest first
 *   TIME · LEVEL · MODULE / FUNCTION · entry, with the five `LogSeverity`
 *                   levels and their own colours
 *   ERRORS          a count, with the view's "incl. critical" qualifier
 *   WARNINGS        the second count
 *   OPEN LOG FOLDER the button that opens the folder on disk
 *   an empty state, exactly as the view words it
 *
 * The second tab is the audit trail, which is a different thing from the log
 * and lives in a different service. It writes through
 * `AuditService.Log(section, action, details, entityType, entityId, reason)`,
 * and its sections are the constants in `AuditSections.cs` — so the tab filters
 * by those, and the before/after sets it can carry are the reason the Activity
 * view can draw a diff.
 *
 * Every identifier in the rows is the seeded workflow's own: INQ-2026-001,
 * PI-2026-001 with its 22 glass items, JOB-2026-001, DN-2026-001-01 and
 * INV-2026-001, on the Al Zahia Tower podium job for Al Noor Glazing
 * Contracting LLC.
 */
export function LogSurface({ compact = false }: { compact?: boolean }) {
  const t = useT();
  const [tab, setTab] = useState<"log" | "activity">("log");
  const [level, setLevel] = useState<LogLevelId | "all">("all");
  const [section, setSection] = useState<string>("all");
  const [folderOpened, setFolderOpened] = useState(false);
  const [query, setQuery] = useState("");

  const source = tab === "log" ? seededLog : seededAudit;

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return source.filter((row) => {
      if (tab === "log" && level !== "all" && row.level !== level) return false;
      if (tab === "activity" && section !== "all" && row.module !== section) return false;
      if (needle && !`${row.module} ${row.message} ${row.document ?? ""} ${row.correlation ?? ""}`.toLowerCase().includes(needle)) {
        return false;
      }
      return true;
    });
  }, [source, tab, level, section, query]);

  /** Newest first, the way a console reads. */
  const ordered = useMemo(() => [...filtered].reverse(), [filtered]);

  const errors = seededLog.filter((row) => row.level === "error").length;
  const warnings = seededLog.filter((row) => row.level === "warning").length;

  return (
    <div className="flex h-full min-h-0 flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <div className="flex gap-1" role="tablist" aria-label={t("Logs and activity views")}>
          {([
            ["log", "Application log"],
            ["activity", "Audit trail"],
          ] as const).map(([id, label]) => (
            <button
              key={id}
              type="button"
              role="tab"
              aria-selected={tab === id}
              onClick={() => {
                setTab(id);
                setLevel("all");
                setSection("all");
              }}
              className={cn(
                "rounded-full px-2.5 py-1 text-[0.75rem] font-medium transition-colors duration-200",
                tab === id
                  ? "bg-brand-400/18 text-brand-100 ring-1 ring-inset ring-brand-400/40"
                  : "text-text-muted hover:bg-ink-800/70 hover:text-text-primary",
              )}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/*
        The header line and the two counters, in the view's own words. The
        counters are counts of the rows on screen rather than a dashboard of
        invented totals.
      */}
      <div className="flex flex-wrap items-end justify-between gap-3 rounded-xl border border-line/60 bg-ink-900/50 px-3 py-2.5">
        <div>
          <p className="text-[0.8125rem] font-semibold text-text-primary">{t(logSurfaceStrings.title)}</p>
          <p className="mt-0.5 text-[0.6875rem] text-text-faint font-medium">
            {source.length} {logSurfaceStrings.entries.toLowerCase()} · {logSurfaceStrings.retention}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div>
            <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
              {logSurfaceStrings.errors}
            </p>
            <p className={cn("text-[0.9375rem] font-semibold tabular-nums", errors > 0 ? "text-danger" : "text-text-primary")}>
              {errors}
              <span className="ml-1 text-[0.625rem] font-medium text-text-faint">{logSurfaceStrings.inclCritical}</span>
            </p>
          </div>
          <div>
            <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
              {logSurfaceStrings.warnings}
            </p>
            <p className="text-[0.9375rem] font-semibold tabular-nums text-amber-300">{warnings}</p>
          </div>
          <button
            type="button"
            onClick={() => setFolderOpened(true)}
            title={logSurfaceStrings.openFolderNote}
            className="rounded-full border border-line-strong/60 bg-ink-800/60 px-2.5 py-1 font-mono text-3xs uppercase tracking-[0.08em] font-medium text-text-muted transition-colors hover:border-brand-400/40 hover:text-text-primary"
          >
            {logSurfaceStrings.openFolder}
          </button>
        </div>
      </div>

      {folderOpened ? (
        <p aria-live="polite" className="rounded-lg border border-brand-400/35 bg-brand-400/8 px-2.5 py-1.5 text-[0.75rem] leading-snug text-brand-100 font-medium">{t("The desktop application opens")}<span className="font-mono">%LOCALAPPDATA%\ProGlassAutomation\diagnostics</span> in
          Explorer — the folder holding the dated log files. A browser cannot open a folder on your machine, so this is what
          the button does there.
        </p>
      ) : null}

      {/* ── filters ──────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-1.5">
        {tab === "log" ? (
          <>
            <FilterPill active={level === "all"} onClick={() => setLevel("all")}>
              {t("All levels")}
            </FilterPill>
            {logLevels.map((entry) => (
              <FilterPill
                key={entry.id}
                active={level === entry.id}
                onClick={() => setLevel(entry.id)}
                title={`${entry.meaning} ${entry.brush}`}
                dot={entry.colour}
              >
                {t(entry.label)}
              </FilterPill>
            ))}
          </>
        ) : (
          <>
            <FilterPill active={section === "all"} onClick={() => setSection("all")}>
              {t("All sections")}
            </FilterPill>
            {/*
              The full canonical list, not only the sections that happen to
              have an entry in this sample. `AuditSections.cs` is categorical
              about why the constants exist — "so the Activity view can
              group/filter reliably and typos cannot split one section into two"
              — and a filter that only offers the sections with rows would hide
              the vocabulary the module is organised by. The count beside each
              is what is on screen.
            */}
            {auditSections.map((name) => {
              const count = seededAudit.filter((row) => row.module === name).length;
              return (
                <FilterPill
                  key={name}
                  active={section === name}
                  onClick={() => setSection(name)}
                  title={count > 0 ? `${count} entries in this sample` : "No entries in this sample"}
                  muted={count === 0}
                >
                  {name}
                  {count > 0 ? <span className="opacity-60">{count}</span> : null}
                </FilterPill>
              );
            })}
          </>
        )}
        <label className="ml-auto flex items-center gap-1.5 rounded-lg border border-line-strong/60 bg-ink-900/70 px-2 py-1">
          <span className="sr-only">{t("Filter entries")}</span>
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Filter…"
            className="w-28 bg-transparent text-[0.75rem] text-text-primary outline-none placeholder:text-text-faint focus:w-40 sm:w-36"
          />
        </label>
      </div>

      {/* ── the terminal ─────────────────────────────────────────────── */}
      <div className="min-h-0 flex-1 overflow-auto rounded-xl border border-line/60 bg-ink-950/60">
        {ordered.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center gap-1 px-4 py-8 text-center">
            <p className="text-[0.8125rem] font-medium text-text-secondary">{logSurfaceStrings.empty}</p>
            <p className="text-[0.75rem] text-text-faint font-medium">{logSurfaceStrings.emptyHint}</p>
          </div>
        ) : (
          <table className="w-full min-w-[44rem] border-collapse text-left">
            <caption className="sr-only">
              {t("The application log, newest first: time, level, module or function, and the entry itself.")}
            </caption>
            {/*
              The headings are the view's own, from the language file:
              Diagnostics.TIME · Diagnostics.Level · Diagnostics.ModuleFunction ·
              Diagnostics.LOGEntries. `logColumns` holds them with the key each
              one comes from, so the table and the application cannot drift.
            */}
            <thead className="sticky top-0 z-10 bg-ink-900/95 backdrop-blur">
              <tr className="border-b border-line/60">
                {logColumns.map((column) => (
                  <th
                    key={column.key}
                    scope="col"
                    title={t(column.note)}
                    className="whitespace-nowrap px-2.5 py-2 font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium"
                  >
                    {t(column.label)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {ordered.map((row, index) => {
                const meta = logLevels.find((entry) => entry.id === row.level);
                const isSection = row.level === "section";
                return (
                  <tr
                    key={`${row.time}-${index}`}
                    className={cn("border-b border-line/30 last:border-0", isSection ? "bg-brand-400/6" : "hover:bg-ink-800/40")}
                  >
                    <td className="whitespace-nowrap px-2.5 py-1 font-mono text-[0.6875rem] text-text-faint tabular-nums">
                      {row.time}
                    </td>
                    <td className="whitespace-nowrap px-2.5 py-1">
                      <span
                        className={cn("font-mono text-[0.6875rem] font-semibold", isSection || row.level === "error" ? "tracking-wide" : "")}
                        style={{ color: meta?.colour }}
                      >
                        {meta?.label}
                      </span>
                    </td>
                    <td className="whitespace-nowrap px-2.5 py-1 font-mono text-[0.6875rem] text-text-muted">{row.module}</td>
                    <td className="px-2.5 py-1 text-[0.75rem] leading-snug">
                      <span className={cn(isSection ? "font-semibold text-brand-200" : "text-text-secondary")}>{row.message}</span>
                      {row.correlation ? (
                        <span
                          className="ml-1.5 rounded-full border border-line-strong/50 px-1.5 py-px font-mono text-[0.625rem] text-text-faint"
                          title={t("LogContext.CorrelationId — traces one business operation across the pipeline")}
                        >
                          {row.correlation}
                        </span>
                      ) : null}
                      {row.document ? (
                        <span
                          className="ml-1.5 rounded-full border border-line-strong/50 px-1.5 py-px font-mono text-[0.625rem] text-text-faint"
                          title={t("LogContext.DocumentId — the record this entry acted on")}
                        >
                          {row.document}
                        </span>
                      ) : null}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {!compact ? (
        <p className="text-[0.6875rem] leading-relaxed text-text-faint font-medium">
          The log and the audit trail are two services, not one: `AppLogService` writes diagnostics — what the application did —
          and `AuditService` writes business events — who changed which record, with the before and after of every field. The
          seeded workflow is {seededDocuments.inquiry} → {seededDocuments.proforma} → {t(seededDocuments.job)} →{" "}
          {seededDocuments.delivery} → {seededDocuments.invoice} for {seededDocuments.customer}, and those are the identifiers in
          the rows above.
        </p>
      ) : null}
    </div>
  );
}

function FilterPill({
  children,
  active,
  onClick,
  title,
  dot,
  muted = false,
}: {
  children: React.ReactNode;
  active: boolean;
  onClick: () => void;
  title?: string;
  dot?: string;
  /** A filter with nothing behind it — still offered, shown as carrying none. */
  muted?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={title}
      aria-pressed={active}
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 font-mono text-3xs uppercase tracking-[0.06em] font-medium transition-colors duration-200",
        active
          ? "border-brand-400/60 bg-brand-400/16 text-brand-100"
          : muted
            ? "border-line/40 bg-ink-900/40 text-text-faint hover:border-brand-400/30 hover:text-text-muted"
            : "border-line-strong/60 bg-ink-800/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
      )}
    >
      {dot ? <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full" style={{ backgroundColor: dot }} /> : null}
      {children}
    </button>
  );
}
