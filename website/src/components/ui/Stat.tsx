"use client";

import { useCountUp } from "@/lib/useReveal";
import { cn } from "@/lib/cn";

/**
 * A product fact, counted up once it is on screen.
 *
 * These are counts of what the application contains — navigation targets,
 * locales, databases, tests — never a performance claim. The repository makes no
 * percentage claims, so neither does this site.
 */
export function StatCounter({
  value,
  suffix,
  label,
  hint,
  className,
}: {
  value: number;
  suffix?: string;
  label: string;
  hint?: string;
  className?: string;
}) {
  const { ref, value: shown } = useCountUp(value);

  return (
    <div className={cn("stat-tile p-4", className)}>
      {/*
        The figure is the point of a stat tile, so it is the largest thing in it
        — but 30px in a tile that is one of six across a card was the "fonts too
        big in small cards" case: the number pushed the label onto a second line
        and the tile grew taller than the thing it was describing. 24px keeps the
        figure dominant and the tile square, and the label comes down to match.
      */}
      <p className="flex items-baseline gap-1 font-mono text-2xl font-semibold tabular-nums text-text-primary">
        <span ref={ref}>{shown.toLocaleString("en-US")}</span>
        {suffix ? <span className="text-base text-brand-300">{suffix}</span> : null}
      </p>
      <p className="mt-1.5 text-xs font-medium text-text-secondary">{label}</p>
      {hint ? <p className="mt-1 text-3xs leading-relaxed text-text-faint font-medium">{hint}</p> : null}
    </div>
  );
}

/** A static fact that is not a number, so no counting animation applies. */
export function StatBadge({
  value,
  label,
  hint,
  className,
}: {
  value: string;
  label: string;
  hint?: string;
  className?: string;
}) {
  return (
    <div className={cn("stat-tile p-4", className)}>
      <p className="font-mono text-lg font-semibold leading-tight text-text-primary">{value}</p>
      <p className="mt-1.5 text-xs font-medium text-text-secondary">{label}</p>
      {hint ? <p className="mt-1 text-3xs leading-relaxed text-text-faint font-medium">{hint}</p> : null}
    </div>
  );
}
