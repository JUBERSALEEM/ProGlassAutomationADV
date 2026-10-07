"use client";

import { useCallback, useEffect, useState } from "react";
import { cn } from "@/lib/cn";
import { Button } from "@/components/ui/Button";
import { PlateCad } from "@/components/optimization/PlateCad";
import { PlateSchedule } from "@/components/optimization/PlateSchedule";
import { CadLegend } from "@/components/optimization/CadLegend";
import { SheetGallery } from "@/components/optimization/SheetGallery";
import type { PackMargins, PlateView } from "@/lib/guillotinePack";
import { usePrefersReducedMotion } from "@/lib/useReveal";
import { useT } from "@/i18n/provider";

/**
 * The 2D view: one plate at a time, large, with its schedule.
 *
 * This is the website's reading of the application's Single Sheet CAD mode
 * (`Views/Optimization/Tabs/TwoDViewTabView.xaml`), and it is the piece the site
 * was missing: everywhere a plate had been drawn up to now, it was rectangles in
 * a box — no trims, no breakout band, no sizes, no sequence, no remnant, and
 * nothing to tell the reader what they were looking at. What the application
 * gives an operator, and what this now gives a visitor:
 *
 *   · the sheet counter, stock size and yield, in the toolbar, as the
 *     application puts them
 *   · the plate drawn to scale with its callouts, its edge trims, its breakout
 *     band, each piece labelled with its reference, cut size and area, its
 *     sequence tag, and the remnant that goes back on the rack
 *   · the piece cutting schedule for that sheet, beside it
 *   · the CAD key underneath, and the margins the plan was cut to
 *
 * One difference from the desktop view, and it is only about where things sit:
 * the application switches between the single-sheet canvas and the gallery, and
 * this shows the canvas with the gallery under it as the selector. On a web page
 * both fit at once, and making the reader toggle to see a sheet they could
 * simply click would be fidelity to a window size the reader does not have.
 *
 * "Play the cut sequence" is the site's own: the tags come on in the order the
 * packer placed the pieces, which is the order the schedule lists and the
 * operator works through. It teaches in five seconds what six columns of
 * figures take a minute to say — and with reduced motion it is simply the
 * finished sheet.
 */
export function PlateInspector({
  plates,
  margins,
  minBreakoutMm = 15,
  thicknessMm,
  /** What the run is cut from. One thickness on this job, so it is said once. */
  composition,
  showSchedule = false,
  showLegend = true,
  playable = false,
  galleryColumns = "grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6",
  className,
}: {
  plates: PlateView[];
  margins: PackMargins;
  minBreakoutMm?: number;
  thicknessMm?: number;
  composition: string;
  showSchedule?: boolean;
  showLegend?: boolean;
  playable?: boolean;
  galleryColumns?: string;
  className?: string;
}) {
  const t = useT();
  const reduced = usePrefersReducedMotion();
  const [plateNumber, setPlateNumber] = useState(1);
  const [reveal, setReveal] = useState<number | undefined>(undefined);
  const [playing, setPlaying] = useState(false);

  const safeNumber = Math.min(Math.max(plateNumber, 1), Math.max(plates.length, 1));
  const plate = plates.find((entry) => entry.plate === safeNumber) ?? plates[0];
  const total = plate?.pieces.length ?? 0;

  /* A new sheet starts finished, so switching is never a half-drawn plate. */
  const focusSheet = useCallback((next: number) => {
    setPlateNumber(next);
    setReveal(undefined);
    setPlaying(false);
  }, []);

  const start = useCallback(() => {
    if (reduced) {
      setReveal(undefined);
      return;
    }
    setReveal(0);
    setPlaying(true);
  }, [reduced]);

  useEffect(() => {
    if (!playing) return;
    if (reveal === undefined) return;
    if (reveal >= total) {
      const done = setTimeout(() => setPlaying(false), 0);
      return () => clearTimeout(done);
    }
    const timer = setTimeout(() => setReveal((value) => (value ?? 0) + 1), reveal === 0 ? 500 : 300);
    return () => clearTimeout(timer);
  }, [playing, reveal, total]);

  if (!plate) {
    return (
      <p className="surface p-5 text-sm leading-relaxed text-text-muted">
        {t("No plates to show — nothing was placed.")}
      </p>
    );
  }

  return (
    <div className={cn("flex flex-col gap-6", className)}>
      {/* ── Toolbar: which sheet, off what stock, and how full ─────────── */}
      <div className="flex flex-wrap items-center gap-x-5 gap-y-3">
        {plates.length > 1 ? (
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => focusSheet(Math.max(1, safeNumber - 1))}
              disabled={safeNumber <= 1}
              aria-label={t("Previous sheet")}
              className="grid h-9 w-9 place-items-center rounded-lg border-2 border-line-strong/80 text-text-secondary transition-colors hover:border-brand-400/60 hover:text-text-primary disabled:opacity-40 disabled:hover:border-line-strong/80"
            >
              <span aria-hidden="true">←</span>
            </button>
            <span className="rounded-lg border border-brand-400/45 bg-brand-500/12 px-3 py-1.5 font-mono text-xs font-bold text-brand-200">
              {t("Sheet {v1} of {v2}", { v1: plate.plate, v2: plates.length })}
            </span>
            <button
              type="button"
              onClick={() => focusSheet(Math.min(plates.length, safeNumber + 1))}
              disabled={safeNumber >= plates.length}
              aria-label={t("Next sheet")}
              className="grid h-9 w-9 place-items-center rounded-lg border-2 border-line-strong/80 text-text-secondary transition-colors hover:border-brand-400/60 hover:text-text-primary disabled:opacity-40 disabled:hover:border-line-strong/80"
            >
              <span aria-hidden="true">→</span>
            </button>
          </div>
        ) : null}

        <span className="rounded-lg border border-line/70 bg-ink-900/70 px-3 py-1.5 font-mono text-xs font-semibold text-text-secondary">
          {plate.stockLabel}
          {thicknessMm ? ` · ${thicknessMm} mm` : ""}
        </span>

        <span className="rounded-lg border border-brand-400/45 bg-brand-500/12 px-3 py-1.5 font-mono text-xs font-bold text-brand-200">
          {t("Yield: {v1}", { v1: `${plate.utilizationPct.toFixed(1)}%` })}
        </span>

        <span className="text-xs font-medium text-text-faint">{composition}</span>
      </div>

      {/* ── The canvas, and the schedule for the sheet on it ───────────── */}
      <div
        className={cn(
          "grid gap-6",
          showSchedule && "lg:grid-cols-[minmax(0,1.55fr)_minmax(0,1fr)] lg:items-start",
        )}
      >
        <div className="plate p-3 sm:p-5">
          {/*
            On a phone the plate would be drawn about 320 px wide, where a
            260 mm piece carries a label four pixels tall. Rather than shrink the
            drawing until its own annotations disappear, the stage scrolls
            sideways and keeps a floor width — the same answer the application
            gives with its own scroller, for the same reason.
          */}
          <div className="-mx-1 overflow-x-auto px-1">
            <PlateCad
              t={t}
              plate={plate}
              margins={margins}
              minBreakoutMm={minBreakoutMm}
              density="inspect"
              reveal={reveal}
              revealStepMs={playing ? 300 : 0}
              className="min-w-[30rem] rounded-[0.6rem] border border-line/70 bg-ink-950"
            />
          </div>

          {playable ? (
            <div className="mt-4 flex flex-wrap items-center gap-3">
              <Button size="sm" onClick={start} loading={playing}>
                {playing
                  ? t("Running…")
                  : reveal !== undefined
                    ? t("Replay the sequence")
                    : t("Play the cut sequence")}
              </Button>
              <Button
                size="sm"
                variant="ghost"
                onClick={() => {
                  setPlaying(false);
                  setReveal(undefined);
                }}
              >
                {t("Show the finished sheet")}
              </Button>
              <p className="font-mono text-2xs uppercase tracking-[0.12em] text-text-faint font-medium">
                {reveal === undefined
                  ? t("All {v1} pieces placed", { v1: total })
                  : t("{v1} of {v2} pieces placed", {
                      v1: Math.min(reveal, total),
                      v2: total,
                    })}
              </p>
            </div>
          ) : null}

          <p className="mt-4 text-xs font-medium leading-relaxed text-text-faint">
            {t("Drawn to scale from this job's own plate, margins and packing result — every rectangle is a piece the table can cut, and the figures on them are computed, not placed by hand. It is a plan from one heuristic, not a result from the engine, which searches five families and six plate patterns.")}
          </p>
        </div>

        {showSchedule ? <PlateSchedule t={t} plate={plate} /> : null}
      </div>

      {showLegend ? <CadLegend t={t} margins={margins} /> : null}

      {/* ── Every plate of the run ─────────────────────────────────────── */}
      {plates.length > 1 ? (
        <div>
          <p className="mono-label">{t("Multi-sheet gallery")}</p>
          <SheetGallery
            plates={plates}
            margins={margins}
            minBreakoutMm={minBreakoutMm}
            thicknessMm={thicknessMm}
            selectedPlate={plate.plate}
            onSelectPlate={focusSheet}
            columns={galleryColumns}
            className="mt-3"
          />
        </div>
      ) : null}
    </div>
  );
}
