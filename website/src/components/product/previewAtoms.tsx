"use client";

import type { ReactNode } from "react";
/**
 * The small parts every live preview is built from.
 *
 * They live here rather than inside `LiveSurface` because the Step 5
 * specification surface and the Step 9 profit-and-loss surface are their own
 * components — the two screens that carry the pricing engine — and three
 * copies of a pill is where a house style starts drifting.
 */

/** The honesty tag. Every preview carries it, without exception. */
export function SampleTag() {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-line-strong/60 bg-ink-800/70 px-2 py-0.5 font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-text-faint font-medium">
      <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-brand-400" />
      Sample data · live
    </span>
  );
}

export function SearchBox({
  value,
  onChange,
  label,
}: {
  value: string;
  onChange: (next: string) => void;
  label: string;
}) {
  return (
    <label className="flex items-center gap-1.5 rounded-lg border border-line-strong/60 bg-ink-900/70 px-2 py-1">
      <svg
        aria-hidden="true"
        viewBox="0 0 16 16"
        className="h-3 w-3 text-text-faint"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
      >
        <circle cx="7" cy="7" r="4.5" />
        <path d="m10.5 10.5 3 3" strokeLinecap="round" />
      </svg>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={label}
        className="w-24 bg-transparent text-[0.75rem] text-text-primary outline-none placeholder:text-text-faint focus:w-36 sm:w-32"
      />
    </label>
  );
}

/**
 * A toolbar pill.
 *
 * `active` marks a toggle that is on, `tone` the two states worth colouring:
 * a figure that came off the specification's own rate, and one that came off
 * the surcharge rate.
 */
export function Pill({
  children,
  onClick,
  active = false,
  tone,
  title,
  disabled = false,
}: {
  children: ReactNode;
  onClick?: () => void;
  active?: boolean;
  tone?: "surcharge" | "floor";
  title?: string;
  disabled?: boolean;
}) {
  if (!onClick) {
    return (
      <span
        title={title}
        className={cnPill({ tone })}
      >
        {children}
      </span>
    );
  }
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={title}
      aria-pressed={active || undefined}
      className={cnPill({ active, disabled, tone })}
    >
      {children}
    </button>
  );
}

/**
 * One class string, so a pill is the same pill wherever it is pressed.
 *
 * The pressed state is a ring rather than a fill: a row of eight filled pills
 * reads as a wall, and these toolbars carry eight.
 */
function cnPill({ active = false, disabled = false, tone }: { active?: boolean; disabled?: boolean; tone?: "surcharge" | "floor" }) {
  const base =
    "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[0.6875rem] font-medium uppercase tracking-[0.1em] transition-colors duration-200";
  const toneClass =
    tone === "surcharge"
      ? "border-money-positive/45 bg-money-positive/12 text-money-positive"
      : tone === "floor"
        ? "border-brand-400/45 bg-brand-400/12 text-brand-100"
        : "";
  const stateClass = active
    ? "border-brand-400/60 bg-brand-400/16 text-brand-100 ring-1 ring-inset ring-brand-400/40"
    : disabled
      ? "border-line/50 text-text-faint/60"
      : "border-line-strong/60 bg-ink-800/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary";
  return [base, stateClass, toneClass].filter(Boolean).join(" ");
}

/** A labelled read-out: the label above, the figure under it. */
export function ReadOut({
  label,
  value,
  note,
  tone,
  money = false,
}: {
  label: string;
  value: string;
  note?: string;
  tone?: "brand" | "good" | "watch";
  money?: boolean;
}) {
  const toneClass =
    tone === "good"
      ? "text-money-positive"
      : tone === "watch"
        ? "text-amber-300"
        : tone === "brand"
          ? "text-brand-100"
          : "text-text-primary";
  return (
    <div className="rounded-xl border border-line/60 bg-ink-900/50 px-3 py-2.5">
      <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-text-faint font-medium">{label}</p>
      <p className={["mt-1 text-[0.9375rem] font-semibold tabular-nums", toneClass, money ? "" : ""].join(" ")}>{value}</p>
      {note ? <p className="mt-1 text-[0.6875rem] leading-snug text-text-muted font-medium">{note}</p> : null}
    </div>
  );
}
