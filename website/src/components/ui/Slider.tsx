"use client";

/**
 * The studio's slider.
 *
 * One control, used by the docked studio and by the appearance section on the
 * home page — the same dial writes the same CSS variable whichever panel it is
 * turned from, so there is no second implementation to drift.
 *
 * A native `<input type="range">` on purpose: it is keyboard-operable, it
 * announces itself correctly, and it inherits the appearance styling from
 * `globals.css`.
 */
export function Slider({
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
  /** One line under the dial saying what the range does at its ends. */
  hint?: string;
  /**
   * Family gating (senior audit): the glass bench wraps itself in
   * `pointer-events-none` when a dial's family is not active — but
   * pointer-events never stops a KEYBOARD. Without this flag a visitor
   * tabbing the drawer could still drag a veiled dial with the arrow keys,
   * writing a token no consumer reads on the active family: a dial that
   * moves and does nothing, which is exactly what "frostness not working"
   * means. The gate must be an actual `disabled`, not a visual veil.
   */
  disabled?: boolean;
}) {
  return (
    <label className="block">
      <span className="flex items-baseline justify-between gap-3">
        <span className="text-[0.75rem] font-bold text-text-primary">{label}</span>
        <span className="font-mono text-[0.75rem] font-semibold tabular-nums text-text-muted">{display}</span>
      </span>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        aria-label={label}
        disabled={disabled}
        onChange={(event) => onChange(Number(event.target.value))}
        className="mt-1"
      />
      {hint ? <span className="mt-1 block text-[0.6875rem] leading-snug text-text-muted">{hint}</span> : null}
    </label>
  );
}

/**
 * How the card-edge dial reads out, in the application's own vocabulary.
 *
 * The product's settings describe 1.5 as the subtle hairline and 2 as the
 * defined card edge (see AppConfig.CardBorderThickness and CardEdgeThickness),
 * so the dial names those two rather than only reporting a pixel count.
 */
export function edgeReadout(value: number): string {
  if (Math.abs(value - 1.5) < 0.001) return "1.5 px · hairline";
  if (Math.abs(value - 2) < 0.001) return "2 px · defined edge";
  return `${value.toFixed(2)} px`;
}
