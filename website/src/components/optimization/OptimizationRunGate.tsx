"use client";

import { useEffect, useState, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { enginePipeline } from "@/data/optimization";
import { useT } from "@/i18n/provider";

/**
 * The gate a sample run stands behind.
 *
 * Every sample job on this site is drawn by the same packer, and every one of
 * them used to be on screen the moment the page loaded: a visitor arriving at
 * the optimization page or the homepage's sample strip saw a finished plate
 * plan, a yield badge and a cut schedule before they had asked for anything.
 *
 * That is not how the application behaves, and it is not how the drawing should
 * be read either. The desktop application opens its optimization tabs empty —
 * they say "Run optimization to generate 2D sheet layouts" — and fills them
 * when a run completes. This component is that behaviour, in one place, for
 * every surface that carries a sample run:
 *
 *   before the run   the cutting list, and nothing computed from it: no plate,
 *                    no schedule, no utilisation, no sheet count
 *   during the run   the engine's own pipeline, walked stage by stage
 *   after the run    whatever the surface wanted to show
 *
 * What it does **not** do is compute anything. The plan behind it is already
 * built from `src/data` at module scope and is proved cuttable at import time;
 * this is the switch that decides when a visitor sees it. The arithmetic is
 * therefore identical to what the module preview and the plate drawings
 * elsewhere on the site report — the numbers cannot change because a page
 * changed its mind about when to reveal them.
 *
 * `prefers-reduced-motion` is honoured by skipping the walk: the run is
 * instantaneous rather than a strobe of stages, which is what a reader who has
 * asked their system not to animate things has asked for.
 */
export function OptimizationRunGate({
  rows,
  children,
  className,
  note,
  runLabel,
}: {
  /** The cutting list the run is cut from — shown while nothing else can be. */
  rows: { ref: string; width: number; height: number; qty: number }[];
  /** What appears once the run has completed. */
  children: ReactNode;
  className?: string;
  /** One line of context under the list — the sheet and the profile, usually. */
  note?: ReactNode;
  runLabel?: string;
}) {
  const t = useT();
  const [phase, setPhase] = useState<"idle" | "running" | "done">("idle");
  const [step, setStep] = useState(0);

  const totalPieces = rows.reduce((total, row) => total + row.qty, 0);

  /*
   * The run walks the engine's own stage list, then reports. Every state change
   * happens inside the timer, so the effect never renders synchronously.
   */
  useEffect(() => {
    if (phase !== "running") return;
    const finished = step >= enginePipeline.length;
    const timer = window.setTimeout(
      () => {
        if (finished) setPhase("done");
        else setStep((value) => value + 1);
      },
      finished ? 420 : step === 0 ? 220 : 260,
    );
    return () => window.clearTimeout(timer);
  }, [phase, step]);

  const beginRun = () => {
    const reduced =
      typeof window !== "undefined" &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    setStep(0);
    setPhase(reduced ? "done" : "running");
  };

  if (phase === "done") return <div className={className}>{children}</div>;

  if (phase === "running") {
    const progress = Math.min(1, step / (enginePipeline.length + 1));
    return (
      <div className={cn("studio-sheet flex flex-col gap-4 p-5", className)}>
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <p className="font-mono text-sm font-semibold" style={{ color: "var(--cad-text)" }}>
            {t("Optimization running…")}
          </p>
          <span className="studio-chip">{Math.round(progress * 100)}%</span>
        </div>
        <div
          className="studio-track"
          role="progressbar"
          aria-label={t("Optimization progress")}
          aria-valuenow={Math.round(progress * 100)}
          aria-valuemin={0}
          aria-valuemax={100}
        >
          <span style={{ width: `${Math.round(progress * 100)}%`, transition: "width 260ms linear" }} />
        </div>
        <p className="text-[0.8125rem] leading-relaxed" style={{ color: "var(--cad-text)" }}>
          <span className="font-semibold">{t(enginePipeline[Math.min(step, enginePipeline.length - 1)]!.step)}</span>
          {" — "}
          {t(enginePipeline[Math.min(step, enginePipeline.length - 1)]!.detail)}
        </p>
        <p className="text-[0.6875rem] leading-relaxed font-medium" style={{ color: "var(--cad-subtle)" }}>
          {t(
            "These are the stages the application reports its search through. The plan itself is computed the moment the run starts, so what you are watching is the sequence, not a long calculation.",
          )}
        </p>
      </div>
    );
  }

  return (
    <div className={cn("studio-sheet p-4", className)}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p
          className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] font-semibold"
          style={{ color: "var(--cad-muted)" }}
        >
          {t("Sample job · the cutting list")}
        </p>
        <p className="studio-label">
          {t("{v1} sizes · {v2} pieces on the table", { v1: rows.length, v2: totalPieces })}
        </p>
      </div>

      <div className="studio-inset mt-2 max-h-[15rem] overflow-auto p-1">
        <table className="studio-table w-full min-w-[20rem] border-separate border-spacing-0 text-[0.75rem]">
          <caption className="sr-only">{t("The sizes this sample job is cut from")}</caption>
          <thead>
            <tr className="text-left">
              {["Piece", "W", "H", "Qty"].map((heading) => (
                <th
                  key={heading}
                  scope="col"
                  className="sticky top-0 px-2 py-1.5 font-mono text-3xs uppercase tracking-[0.1em] font-medium"
                  style={{ backgroundColor: "var(--cad-canvas)" }}
                >
                  {t(heading)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.ref}>
                <td className="px-2 py-1 font-semibold">{row.ref}</td>
                <td className="px-2 py-1 text-right font-mono tabular-nums">{row.width}</td>
                <td className="px-2 py-1 text-right font-mono tabular-nums">{row.height}</td>
                <td className="px-2 py-1 text-right font-mono tabular-nums">{row.qty}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-3 flex flex-wrap items-end justify-between gap-3">
        <div className="max-w-[38rem]">
          {note ? <div className="text-xs leading-relaxed font-medium" style={{ color: "var(--cad-muted)" }}>{note}</div> : null}
          <p className="mt-1 text-xs leading-relaxed font-medium" style={{ color: "var(--cad-subtle)" }}>
            {t(
              "Nothing has been computed yet — the application opens its optimization tabs the same way, empty until a run is asked for. Press Run optimization and the plan, the schedule and the yield appear together.",
            )}
          </p>
        </div>
        <button type="button" onClick={beginRun} className="studio-button studio-button-primary">
          {runLabel ?? t("Run optimization")}
        </button>
      </div>
    </div>
  );
}
