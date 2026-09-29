"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { usePrefersReducedMotion, useReveal } from "@/lib/useReveal";
import { Button } from "@/components/ui/Button";
import {
  exampleCuts,
  exampleMargins,
  exampleMetrics,
  examplePieces,
  exampleSheet,
  exampleUsable,
} from "@/components/optimization/nestingExample";

/**
 * Animated nesting demonstration.
 *
 * This is an illustration of the guillotine model the engine implements —
 * edge trims first, then a cut sequence that splits the remaining rectangle,
 * with the blade gap consumed between pieces. It is NOT a recording of a run
 * and it is labelled as such on screen.
 *
 * Everything visible is computed from the worked example in
 * `nestingExample.ts`, so the utilization figure is arithmetic over the sheet
 * and piece sizes on screen rather than an invented result.
 */

const STEPS = ["Sheet", "Safety edges", "Kerf", "Placement", "Cut sequence", "Result"] as const;

export function NestingDemo() {
  const reduced = usePrefersReducedMotion();
  const [rawStep, setRawStep] = useState(0);
  const [running, setRunning] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const { ref, style } = useReveal<HTMLDivElement>();

  /**
   * Reduced motion: the demonstration is shown at its final state instead of
   * being animated, and every per-piece transition delay collapses to zero.
   * Deriving this rather than setting it from an effect keeps the first paint
   * correct for those visitors.
   */
  const step = reduced ? STEPS.length - 1 : rawStep;

  const clear = () => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
  };

  const start = useCallback(() => {
    clear();
    setRawStep(0);
    setRunning(true);
  }, []);

  useEffect(() => {
    if (!running || reduced) return;
    if (rawStep >= STEPS.length - 1) {
      const done = setTimeout(() => setRunning(false), 0);
      return () => clearTimeout(done);
    }
    timer.current = setTimeout(() => setRawStep((s) => s + 1), rawStep === 0 ? 700 : 1150);
    return clear;
  }, [running, rawStep, reduced]);

  useEffect(() => clear, []);

  const showEdges = step >= 1;
  const showKerf = step >= 2;
  const showPieces = step >= 3;
  const showCuts = step >= 4;
  const showResult = step >= 5;

  // SVG viewBox is the sheet itself, so coordinates are millimetres.
  const pad = 130;

  return (
    <div ref={ref} style={style} className="reveal">
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1.55fr)_minmax(0,1fr)]">
        {/* ── The plate ─────────────────────────────────────────── */}
        <div className="plate p-3 sm:p-5">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
            <p className="mono-label">
              Plate 1 · {exampleSheet.width} × {exampleSheet.height} × {exampleSheet.thickness} mm
            </p>
            <p className="mono-label">
              LM/RM/TM/BM {exampleMargins.left} mm · kerf {exampleMargins.kerf} mm
            </p>
          </div>

          <svg
            viewBox={`${-pad} ${-pad} ${exampleSheet.width + pad * 2} ${exampleSheet.height + pad * 2}`}
            className="w-full rounded-[0.6rem] border border-line/70 bg-ink-950"
            role="img"
            aria-label={`Illustration: ${examplePieces.length} glass pieces nested on one ${exampleSheet.width} by ${exampleSheet.height} millimetre plate. Utilization ${exampleMetrics.utilizationPct} percent.`}
          >
            <defs>
              <linearGradient id="glass-a" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0%" stopColor="#0EA5E9" stopOpacity="0.36" />
                <stop offset="100%" stopColor="#22D3EE" stopOpacity="0.2" />
              </linearGradient>
              <linearGradient id="glass-b" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0%" stopColor="#2DD4BF" stopOpacity="0.32" />
                <stop offset="100%" stopColor="#38BDF8" stopOpacity="0.18" />
              </linearGradient>
              <linearGradient id="glass-c" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0%" stopColor="#7DD3FC" stopOpacity="0.3" />
                <stop offset="100%" stopColor="#0EA5E9" stopOpacity="0.16" />
              </linearGradient>
            </defs>

            {/* Raw sheet */}
            <rect
              x="0"
              y="0"
              width={exampleSheet.width}
              height={exampleSheet.height}
              rx="10"
              className="fill-ink-850 stroke-line-strong"
              strokeWidth="6"
            />

            {/* Usable rectangle inside the safety edges */}
            <rect
              x={exampleUsable.x}
              y={exampleUsable.y}
              width={exampleUsable.width}
              height={exampleUsable.height}
              fill="none"
              className={cn(
                "stroke-brand-300 transition-opacity duration-700",
                showEdges ? "opacity-90" : "opacity-0",
              )}
              strokeWidth="5"
              strokeDasharray="34 22"
            />

            {/* Edge trim hatch */}
            {showEdges ? (
              <g className="animate-fade-in">
                <TrimHatch x={0} y={0} w={exampleSheet.width} h={exampleMargins.top} />
                <TrimHatch
                  x={0}
                  y={exampleSheet.height - exampleMargins.bottom}
                  w={exampleSheet.width}
                  h={exampleMargins.bottom}
                />
                <TrimHatch x={0} y={exampleMargins.top} w={exampleMargins.left} h={exampleUsable.height} />
                <TrimHatch
                  x={exampleSheet.width - exampleMargins.right}
                  y={exampleMargins.top}
                  w={exampleMargins.right}
                  h={exampleUsable.height}
                />
              </g>
            ) : null}

            {/* Kerf gaps — the blade consumption between pieces */}
            {showKerf ? (
              <g className="animate-fade-in" fill="#F59E0B" fillOpacity="0.34">
                <rect x={exampleUsable.x + 1600} y={exampleUsable.y} width={exampleMargins.kerf} height={exampleUsable.height} />
                <rect x={exampleUsable.x} y={exampleUsable.y + 1100} width={1600} height={exampleMargins.kerf} />
                <rect
                  x={exampleUsable.x + 1600 + exampleMargins.kerf}
                  y={exampleUsable.y + 700}
                  width={exampleUsable.width - 1600 - exampleMargins.kerf}
                  height={exampleMargins.kerf}
                />
                <rect
                  x={exampleUsable.x + 1600 + exampleMargins.kerf}
                  y={exampleUsable.y + 1403}
                  width={exampleUsable.width - 1600 - exampleMargins.kerf}
                  height={exampleMargins.kerf}
                />
              </g>
            ) : null}

            {/* Placed pieces */}
            {examplePieces.map((piece, index) => (
              <g
                key={piece.id}
                className={cn("transition-all duration-700 ease-reveal", showPieces ? "opacity-100" : "opacity-0")}
                style={{ transitionDelay: reduced ? "0ms" : `${index * 140}ms` }}
              >
                <rect
                  x={piece.x}
                  y={piece.y}
                  width={piece.width}
                  height={piece.height}
                  fill={`url(#glass-${piece.tone})`}
                  stroke="#7DD3FC"
                  strokeOpacity="0.55"
                  strokeWidth="3"
                />
                <text
                  x={piece.x + piece.width / 2}
                  y={piece.y + piece.height / 2 - 12}
                  textAnchor="middle"
                  className="fill-text-primary"
                  style={{ fontSize: 62, fontWeight: 600 }}
                >
                  {piece.label}
                </text>
                <text
                  x={piece.x + piece.width / 2}
                  y={piece.y + piece.height / 2 + 62}
                  textAnchor="middle"
                  className="fill-brand-200"
                  style={{ fontSize: 48, fontFamily: "var(--font-mono)" }}
                >
                  {piece.width} × {piece.height}
                </text>
              </g>
            ))}

            {/* Cut sequence */}
            {showCuts ? (
              <g className="transition-opacity duration-500">
                {exampleCuts.map((cut, index) => {
                  const common = {
                    stroke: "#FB7185",
                    strokeWidth: 4,
                    strokeDasharray: "26 16",
                    className: "animate-fade-in",
                    style: { animationDelay: reduced ? "0ms" : `${index * 110}ms` },
                  } as const;
                  return cut.axis === "vertical" ? (
                    <line
                      key={cut.id}
                      x1={cut.atMm}
                      y1={exampleUsable.y}
                      x2={cut.atMm}
                      y2={exampleUsable.y + exampleUsable.height}
                      {...common}
                    />
                  ) : (
                    <line
                      key={cut.id}
                      x1={cut.span === 0 ? exampleUsable.x : exampleUsable.x + 1600 + exampleMargins.kerf}
                      y1={cut.atMm}
                      x2={
                        cut.span === 0
                          ? exampleUsable.x + 1600
                          : exampleUsable.x + exampleUsable.width
                      }
                      y2={cut.atMm}
                      {...common}
                    />
                  );
                })}
              </g>
            ) : null}
          </svg>

          <p className="mt-3 text-xs leading-relaxed text-text-faint">
            Illustration of the guillotine model, drawn to scale from the worked example below. It is not a recording of
            a live run.
          </p>
        </div>

        {/* ── Step readout + metrics ────────────────────────────── */}
        <div className="flex flex-col gap-4">
          <div className="surface-raised p-5">
            <div className="flex items-center justify-between gap-3">
              <p className="mono-label">Sequence</p>
              <span className="font-mono text-2xs text-brand-300">
                {String(step + 1).padStart(2, "0")} / {String(STEPS.length).padStart(2, "0")}
              </span>
            </div>

            <ol className="mt-3 space-y-1">
              {STEPS.map((label, index) => (
                <li
                  key={label}
                  className={cn(
                    "flex items-center gap-2.5 rounded-lg px-2 py-1.5 text-sm transition-colors duration-300",
                    index === step
                      ? "bg-brand-400/10 text-brand-100"
                      : index < step
                        ? "text-text-secondary"
                        : "text-text-faint",
                  )}
                >
                  <span
                    aria-hidden="true"
                    className={cn(
                      "h-1.5 w-1.5 shrink-0 rounded-full transition-colors duration-300",
                      index <= step ? "bg-brand-400" : "bg-line-strong",
                    )}
                  />
                  {label}
                </li>
              ))}
            </ol>

            <div className="mt-4 flex gap-2">
              <Button size="sm" onClick={start} loading={running}>
                {step === STEPS.length - 1 ? "Replay" : running ? "Running" : "Play"}
              </Button>
              <Button
                size="sm"
                variant="ghost"
                onClick={() => {
                  clear();
                  setRunning(false);
                  setRawStep(STEPS.length - 1);
                }}
              >
                Skip to result
              </Button>
            </div>
          </div>

          <div
            className={cn(
              "surface-raised p-5 transition-opacity duration-500",
              showResult ? "opacity-100" : "opacity-55",
            )}
          >
            <p className="mono-label">Result · this example only</p>
            <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-3">
              <Metric label="Sheets" value={String(exampleMetrics.sheets)} />
              <Metric label="Pieces" value={String(exampleMetrics.pieces)} />
              <Metric label="Utilization" value={`${exampleMetrics.utilizationPct}%`} tone="accent" />
              <Metric label="Offcut" value={`${exampleMetrics.offcutSqm} m²`} />
              <Metric label="Used area" value={`${exampleMetrics.usedSqm} m²`} />
              <Metric label="Stock area" value={`${exampleMetrics.stockSqm} m²`} />
            </dl>
            <p className="mt-4 text-xs leading-relaxed text-text-faint">
              These six numbers come from the plate and piece sizes drawn above. They are not a benchmark, and the
              engine makes no savings claim — it reports what it produced for the cutting list you gave it.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

function TrimHatch({ x, y, w, h }: { x: number; y: number; w: number; h: number }) {
  return <rect x={x} y={y} width={w} height={h} fill="#FB7185" fillOpacity="0.2" />;
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: "accent" }) {
  return (
    <div>
      <dt className="text-2xs uppercase tracking-[0.12em] text-text-faint">{label}</dt>
      <dd
        className={cn(
          "mt-0.5 font-mono text-lg font-semibold tabular-nums",
          tone === "accent" ? "text-brand-300" : "text-text-primary",
        )}
      >
        {value}
      </dd>
    </div>
  );
}
