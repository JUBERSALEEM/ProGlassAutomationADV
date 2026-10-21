"use client";

import { useState } from "react";
import { cn } from "@/lib/cn";

/**
 * The moulded controls the Neumorphism family is built from.
 *
 * These are real controls, not decoration. The kit they come from is a page of
 * knobs, pill switches, and grooves, and the family only reads as that kit once
 * the bench is made of the same objects as the material:
 *
 *   NeuDial     a moulded disc whose ring fills as it turns
 *   NeuSegment  a pill that slides in a recessed track
 *
 * Both keep a native `<input type="range">` / `<button aria-pressed>` at their
 * heart, so the keyboard, the screen reader, and the product's existing slider
 * contract all keep working. The dial's range input is transparent and stretches
 * over the disc: a horizontal drag anywhere on the knob turns it, which is how
 * a physical knob behaves when you catch it.
 */

export function NeuDial({
  label,
  value,
  min,
  max,
  step,
  display,
  onChange,
  hint,
  disabled = false,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step: number;
  display: string;
  onChange: (value: number) => void;
  hint?: string;
  disabled?: boolean;
}) {
  /* The thumb of a native range reports values instantly; the studio's
     re-render is what lags. Track the pointer locally and let the parent catch
     up, exactly as the studio's slider does. Same reason, same contract. */
  const [dragValue, setDragValue] = useState<number | null>(null);
  const shown = dragValue ?? value;
  const span = max - min || 1;
  const fraction = Math.min(1, Math.max(0, (shown - min) / span));
  const arc = fraction * 270;
  const turn = -135 + arc;
  const beginLive = () => document.documentElement.setAttribute("data-studio-live", "");
  const endLive = () => {
    document.documentElement.removeAttribute("data-studio-live");
    setDragValue(null);
  };

  return (
    <div className={cn("block", disabled && "opacity-60")}>
      <span className="flex items-baseline justify-between gap-2">
        <span className="text-[0.72rem] font-bold text-text-primary">{label}</span>
        <span className="neu-dial-readout font-mono text-[0.72rem] font-semibold text-text-muted">
          {display}
        </span>
      </span>
      <span
        className="neu-dial relative mt-2 grid aspect-square w-full place-items-center rounded-full bg-ink-900 shadow-raised"
        style={{ ["--neu-dial-turn" as string]: `${turn}deg` }}
      >
        <span
          aria-hidden="true"
          className="neu-dial-ring pointer-events-none absolute inset-[7%] rounded-full"
          style={{
            background: `conic-gradient(from 225deg, rgb(var(--brand-500) / 0.95) 0deg ${arc}deg, rgb(var(--line) / 0.75) ${arc}deg 270deg, transparent 270deg)`,
            /* The annulus mask. `mask` is painted by the same trick twice for
               the two engines; the opaque stop is the ink role, not a colour
               of the design. */
            WebkitMaskImage:
              "radial-gradient(closest-side, transparent 0 76%, rgb(var(--text-1)) 77%)",
            maskImage:
              "radial-gradient(closest-side, transparent 0 76%, rgb(var(--text-1)) 77%)",
          }}
        />
        <span
          aria-hidden="true"
          className="neu-dial-face relative grid aspect-square w-[62%] place-items-center rounded-full bg-ink-850 shadow-panel"
        >
          <span
            aria-hidden="true"
            className="neu-dial-needle absolute bottom-1/2 left-1/2 h-[26%] w-[3px] -translate-x-1/2 rounded-full bg-brand-500"
            style={{ transform: `translateX(-50%) rotate(${turn}deg)`, transformOrigin: "50% 100%" }}
          />
        </span>
        <input
          type="range"
          min={min}
          max={max}
          step={step}
          value={shown}
          aria-label={label}
          disabled={disabled}
          onPointerDown={beginLive}
          onChange={(event) => {
            beginLive();
            const next = Number(event.target.value);
            setDragValue(next);
            onChange(next);
          }}
          onPointerUp={endLive}
          onBlur={endLive}
          className="neu-dial-input absolute inset-0 h-full w-full cursor-grab opacity-0"
        />
      </span>
      {hint ? (
        <span className="mt-1.5 block text-[0.6875rem] leading-snug text-text-muted">{hint}</span>
      ) : null}
    </div>
  );
}

export function NeuSegment<T extends string>({
  label,
  options,
  value,
  onChange,
  disabled = false,
  className,
}: {
  label: string;
  options: readonly { id: T; label: string }[];
  value: T;
  onChange: (id: T) => void;
  disabled?: boolean;
  className?: string;
}) {
  const match = options.findIndex((option) => option.id === value);
  const index = Math.max(0, match);
  return (
    <div
      role="group"
      aria-label={label}
      className={cn(
        "neu-segment relative grid gap-0 rounded-full bg-ink-850 p-1 shadow-well",
        disabled && "pointer-events-none opacity-60 saturate-[0.6]",
        className,
      )}
      style={{
        gridAutoFlow: "column",
        gridAutoColumns: "1fr",
        ["--neu-segment-count" as string]: options.length,
      }}
    >
      <span
        aria-hidden="true"
        className="neu-segment-thumb pointer-events-none absolute bottom-1 left-1 top-1 rounded-full bg-ink-900 shadow-panel"
        style={{
          width: `calc((100% - 8px) / ${options.length})`,
          transform: `translateX(${index * 100}%)`,
          /* No preset is active, so no plate is in the track. The dials are
             free-form above it and the switch says so by empty. */
          opacity: match < 0 ? 0 : 1,
        }}
      />
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          aria-pressed={option.id === value}
          disabled={disabled}
          onClick={() => onChange(option.id)}
          className={cn(
            "relative z-[1] min-h-[2.25rem] rounded-full px-2 text-[0.72rem] font-bold transition-colors",
            option.id === value ? "text-text-primary" : "text-text-muted hover:text-text-primary",
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
