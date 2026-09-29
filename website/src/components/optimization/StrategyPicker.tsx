"use client";

import { useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { Badge } from "@/components/ui/Badge";
import { platePatterns, searchFamilies } from "@/data/optimization";

/**
 * The two dials the operator actually turns: which search stage the engine
 * runs, and which tie-break decides between plans that are otherwise equal.
 *
 * Nothing here is invented: every string comes from `data/optimization.ts`,
 * which was transcribed from the engine source. The component exists so the
 * two lists can be explored side by side instead of read as static text.
 */
export function StrategyPicker() {
  const [familyIndex, setFamilyIndex] = useState(0);
  const [patternIndex, setPatternIndex] = useState(0);
  const familyTabs = useRef<(HTMLButtonElement | null)[]>([]);

  const family = searchFamilies[familyIndex] ?? searchFamilies[0];
  const pattern = platePatterns[patternIndex] ?? platePatterns[0];

  const moveFamily = (delta: number) => {
    const next = (familyIndex + delta + searchFamilies.length) % searchFamilies.length;
    setFamilyIndex(next);
    familyTabs.current[next]?.focus();
  };

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      {/* ── Search stage ─────────────────────────────────────────── */}
      <div className="surface-raised flex flex-col p-6">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h3 className="text-base font-semibold text-text-primary">Search stage</h3>
          <Badge tone="accent">Optimize effort 1–5</Badge>
        </div>

        <div
          role="tablist"
          aria-label="Search stage"
          aria-orientation="horizontal"
          className="mt-5 flex flex-wrap gap-2"
          onKeyDown={(event) => {
            if (event.key === "ArrowRight") {
              event.preventDefault();
              moveFamily(1);
            }
            if (event.key === "ArrowLeft") {
              event.preventDefault();
              moveFamily(-1);
            }
          }}
        >
          {searchFamilies.map((item, index) => {
            const selected = index === familyIndex;
            return (
              <button
                key={`${item.name}-${index}`}
                ref={(node) => {
                  familyTabs.current[index] = node;
                }}
                type="button"
                role="tab"
                id={`stage-tab-${index}`}
                aria-selected={selected}
                aria-controls="stage-panel"
                tabIndex={selected ? 0 : -1}
                onClick={() => setFamilyIndex(index)}
                className={cn(
                  "rounded-full border px-3.5 py-1.5 text-xs font-medium transition-colors duration-200",
                  selected
                    ? "border-brand-400/60 bg-brand-400/12 text-brand-100"
                    : "border-line-strong/70 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                )}
              >
                {item.name.replace(" — default", "").replace(" — Quick", "")}
                {item.name.includes("default") ? (
                  <span className="ml-1.5 font-mono text-[0.625rem] uppercase tracking-[0.1em] text-brand-300">
                    default
                  </span>
                ) : null}
                {item.name.includes("Quick") ? (
                  <span className="ml-1.5 font-mono text-[0.625rem] uppercase tracking-[0.1em] text-text-faint font-medium">
                    quick run
                  </span>
                ) : null}
              </button>
            );
          })}
        </div>

        <div
          id="stage-panel"
          role="tabpanel"
          aria-labelledby={`stage-tab-${familyIndex}`}
          className="mt-5 rounded-card border border-line/60 bg-ink-950/50 p-4"
        >
          <p className="text-sm font-semibold text-text-primary">{family.name}</p>
          <p className="mt-2 text-sm leading-relaxed text-text-muted">{family.detail}</p>
        </div>

        <p className="mt-4 text-xs leading-relaxed text-text-faint font-medium">
          Stage 5 at effort 5 evaluates every ordering against every generator — minutes on a large job rather than
          seconds. The application says so on the control itself and suggests a quick run first.
        </p>
      </div>

      {/* ── Plate pattern ────────────────────────────────────────── */}
      <div className="surface-raised flex flex-col p-6">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h3 className="text-base font-semibold text-text-primary">Plate pattern</h3>
          <Badge>Six tie-breaks</Badge>
        </div>

        <div className="mt-5 flex flex-wrap gap-2" role="group" aria-label="Plate pattern">
          {platePatterns.map((item, index) => {
            const selected = index === patternIndex;
            return (
              <button
                key={`${item.name}-${index}`}
                type="button"
                aria-pressed={selected}
                onClick={() => setPatternIndex(index)}
                className={cn(
                  "rounded-full border px-3.5 py-1.5 text-xs font-medium transition-colors duration-200",
                  selected
                    ? "border-brand-400/60 bg-brand-400/12 text-brand-100"
                    : "border-line-strong/70 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                )}
              >
                {item.name}
              </button>
            );
          })}
        </div>

        <div aria-live="polite" className="mt-5 rounded-card border border-line/60 bg-ink-950/50 p-4">
          <p className="text-sm font-semibold text-text-primary">{pattern.name}</p>
          <p className="mt-1 font-mono text-2xs uppercase tracking-[0.12em] text-brand-300">{pattern.tagline}</p>
          <p className="mt-2 text-sm leading-relaxed text-text-muted">{pattern.detail}</p>
        </div>

        <p className="mt-4 text-xs leading-relaxed text-text-faint font-medium">
          Every pattern is a tie-break between plans that already use the same number of sheets and leave nothing out.
          None of them may buy its preference by opening another plate.
        </p>
      </div>
    </div>
  );
}
