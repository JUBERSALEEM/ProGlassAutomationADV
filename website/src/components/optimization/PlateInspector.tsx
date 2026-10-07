"use client";

import { useCallback, useEffect, useState } from "react";
import { cn } from "@/lib/cn";
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
 * The website's reading of the application's Single Sheet CAD mode
 * (`Views/Optimization/Tabs/TwoDViewTabView.xaml`) — and it is drawn on the
 * application's light studio panel in both readings of the site, because it is a
 * drawing *of* that screen rather than a card on this page. The toolbar chips
 * are the application's (`OptIndigoSoft` on `OptIndigo`), the sheet stage is its
 * `OptCutBed` with an `OptIndigo` rim, the yield badge is its success triad, and
 * the key underneath is its footer legend.
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
 * "Play the cut sequence" is the site's own: the pieces come on in the order the
 * packer placed them, which is the order the schedule lists and the operator
 * works through. It teaches in five seconds what six columns of figures take a
 * minute to say — and with reduced motion it is simply the finished sheet.
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
  autoPlay = false,
  galleryColumns = "grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6",
  galleryPageSize = 6,
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
  /** Start the cut sequence as soon as the sheet is on the canvas. */
  autoPlay?: boolean;
  galleryColumns?: string;
  galleryPageSize?: number;
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
  const focusSheet = useCallback(
    (next: number) => {
      setPlateNumber(next);
      setReveal(autoPlay && !reduced ? 0 : undefined);
      setPlaying(autoPlay && !reduced);
    },
    [autoPlay, reduced],
  );

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
      <p className="studio-sheet p-5 text-sm leading-relaxed" style={{ color: "var(--cad-muted)" }}>
        {t("No plates to show — nothing was placed.")}
      </p>
    );
  }

  return (
    <div className={cn("flex flex-col gap-5", className)}>
      {/* ── Toolbar: which sheet, off what stock, and how full ─────────── */}
      <div className="studio-sheet flex flex-wrap items-center gap-x-4 gap-y-3 px-4 py-3">
        {plates.length > 1 ? (
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => focusSheet(Math.max(1, safeNumber - 1))}
              disabled={safeNumber <= 1}
              aria-label={t("Previous sheet")}
              className="studio-button px-2.5 py-1"
            >
              <span aria-hidden="true">←</span>
            </button>
            <span className="studio-chip">
              {t("Sheet {v1} of {v2}", { v1: plate.plate, v2: plates.length })}
            </span>
            <button
              type="button"
              onClick={() => focusSheet(Math.min(plates.length, safeNumber + 1))}
              disabled={safeNumber >= plates.length}
              aria-label={t("Next sheet")}
              className="studio-button px-2.5 py-1"
            >
              <span aria-hidden="true">→</span>
            </button>
          </div>
        ) : null}

        <span className="studio-chip">
          {plate.stockLabel}
          {thicknessMm ? ` · ${thicknessMm} mm` : ""}
        </span>

        <span className="studio-chip-yield">{t("Yield: {v1}", { v1: `${plate.utilizationPct.toFixed(1)}%` })}</span>

        <span className="font-mono text-xs font-medium" style={{ color: "var(--cad-muted)" }}>
          {composition}
        </span>
      </div>

      {/* ── The canvas, and the schedule for the sheet on it ───────────── */}
      <div
        className={cn(
          "grid gap-5",
          showSchedule && "lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)] lg:items-start",
        )}
      >
        <div className="studio-sheet p-3 sm:p-4">
          {/*
            On a phone the plate would be drawn about 320 px wide, where a
            260 mm piece carries a label four pixels tall. Rather than shrink the
            drawing until its own annotations disappear, the stage scrolls
            sideways and keeps a floor width — the same answer the application
            gives with its own scroller, for the same reason.
          */}
          <div className="-mx-1 overflow-x-auto px-1">
            <div className="studio-stage p-1">
              <PlateCad
                t={t}
                plate={plate}
                margins={margins}
                minBreakoutMm={minBreakoutMm}
                density="inspect"
                reveal={reveal}
                revealStepMs={playing ? 300 : 0}
                revealDurationMs={playing ? 200 : 500}
                className="block min-w-[30rem] rounded-[0.35rem]"
              />
            </div>
          </div>

          {playable ? (
            <div className="mt-4 flex flex-wrap items-center gap-3">
              <button type="button" onClick={start} className="studio-button studio-button-primary" disabled={playing}>
                {playing
                  ? t("Running…")
                  : reveal !== undefined
                    ? t("Replay the sequence")
                    : t("Play the cut sequence")}
              </button>
              <button
                type="button"
                className="studio-button"
                onClick={() => {
                  setPlaying(false);
                  setReveal(undefined);
                }}
              >
                {t("Show the finished sheet")}
              </button>
              <p className="studio-label">
                {reveal === undefined
                  ? t("All {v1} pieces placed", { v1: total })
                  : t("{v1} of {v2} pieces placed", {
                      v1: Math.min(reveal, total),
                      v2: total,
                    })}
              </p>
            </div>
          ) : null}

          {showLegend ? (
            <div className="mt-4 pt-4" style={{ borderTop: "1px solid var(--cad-keyline)" }}>
              <CadLegend t={t} margins={margins} />
            </div>
          ) : null}

          <p className="mt-4 text-xs font-medium leading-relaxed" style={{ color: "var(--cad-subtle)" }}>
            {t("Drawn to scale from this job's own plate, margins and packing result — every rectangle is a piece the table can cut, and the figures on them are computed, not placed by hand. It is a plan from one heuristic, not a result from the engine, which searches five families and six plate patterns.")}
          </p>
        </div>

        {showSchedule ? <PlateSchedule t={t} plate={plate} /> : null}
      </div>

      {/* ── Every plate of the run ─────────────────────────────────────── */}
      {plates.length > 1 ? (
        <div>
          <p className="studio-label">{t("Multi-sheet gallery")}</p>
          <SheetGallery
            plates={plates}
            margins={margins}
            minBreakoutMm={minBreakoutMm}
            thicknessMm={thicknessMm}
            selectedPlate={plate.plate}
            onSelectPlate={focusSheet}
            pageSize={galleryPageSize}
            columns={galleryColumns}
            className="mt-3"
          />
        </div>
      ) : null}
    </div>
  );
}
