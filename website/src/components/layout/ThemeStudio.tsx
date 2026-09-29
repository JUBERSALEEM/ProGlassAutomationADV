"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { appearanceFamilies, themeModes, type AppearanceFamilyEntry, type ThemeModeEntry } from "@/data/themes";
import { setAppearance, setThemeMode, useAppearance, useThemeMode } from "@/lib/useTheme";
import { resetStudio, setAccent, setStudio, studioAccents, useStudio } from "@/lib/useStudio";

/**
 * The appearance studio.
 *
 * A panel the visitor opens from the edge of the screen and re-points the site
 * with while they are reading it: mode, palette family, accent, how rich the
 * surfaces are, how round the corners are, and the colour of money. Nothing is
 * a preview — every control writes straight to the live page, and the choices
 * survive a reload.
 *
 * It exists because a shop owner looking at a website about their own software
 * will want to see it in their colour, not in the developer's.
 */
export function ThemeStudio() {
  const [open, setOpen] = useState(false);
  const [customHex, setCustomHex] = useState("#7C3AED");
  const panel = useRef<HTMLDivElement | null>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);

  const mode = useThemeMode();
  const family = useAppearance();
  const studio = useStudio();

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
        trigger.current?.focus();
      }
    };
    const onPointer = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!panel.current?.contains(target) && !trigger.current?.contains(target)) setOpen(false);
    };
    window.addEventListener("keydown", onKey);
    window.addEventListener("pointerdown", onPointer);
    return () => {
      window.removeEventListener("keydown", onKey);
      window.removeEventListener("pointerdown", onPointer);
    };
  }, [open]);

  const activeMode: ThemeModeEntry = themeModes.find((entry) => entry.id === mode) ?? themeModes[2]!;
  const activeFamily: AppearanceFamilyEntry =
    appearanceFamilies.find((entry) => entry.id === family) ?? appearanceFamilies[0]!;

  return (
    <>
      <button
        ref={trigger}
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-controls="appearance-studio"
        className="group fixed bottom-5 right-5 z-40 flex items-center gap-2 rounded-full border border-brand-400/45 bg-ink-900 px-3.5 py-2.5 text-xs font-semibold text-text-primary shadow-raised transition-colors hover:border-brand-300/70"
      >
        <span aria-hidden="true" className="grid h-4 w-4 place-items-center">
          <span className="h-3.5 w-3.5 rounded-full" style={{ background: activeFamily.swatch }} />
        </span>
        <span className="hidden sm:inline">Appearance</span>
        <span className="sr-only">Open the appearance studio</span>
      </button>

      {open ? (
        <div
          ref={panel}
          id="appearance-studio"
          role="dialog"
          aria-label="Appearance studio"
          className="animate-scale-in fixed inset-x-3 bottom-20 z-50 max-h-[74vh] overflow-y-auto rounded-panel border border-line-strong/80 bg-ink-900 p-4 shadow-raised sm:inset-x-auto sm:right-5 sm:w-[22rem] sm:p-5"
        >
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="eyebrow">Appearance studio</p>
              <p className="mt-1.5 text-xs leading-relaxed text-text-muted">
                Adjust this site while it runs. Everything here maps to a real setting in the application.
              </p>
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded-full border border-line-strong/70 px-2 py-0.5 text-[0.625rem] text-text-muted transition-colors hover:text-text-primary"
            >
              Close
            </button>
          </div>

          {/* ── Mode ──────────────────────────────────────────────────── */}
          <fieldset className="mt-5">
            <legend className="mono-label">Mode</legend>
            <div className="mt-2 grid grid-cols-3 gap-1.5">
              {themeModes.map((entry) => (
                <button
                  key={entry.id}
                  type="button"
                  aria-pressed={entry.id === mode}
                  onClick={() => setThemeMode(entry.id)}
                  title={entry.hint}
                  className={cn(
                    "rounded-card border px-2 py-2 text-[0.6875rem] font-medium transition-colors",
                    entry.id === mode
                      ? "border-brand-400/60 bg-brand-400/12 text-text-primary"
                      : "border-line/70 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                  )}
                >
                  {entry.label}
                </button>
              ))}
            </div>
          </fieldset>

          {/* ── Palette family ────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">Palette family</legend>
            <div className="mt-2 grid grid-cols-2 gap-1.5">
              {appearanceFamilies.map((entry) => (
                <button
                  key={entry.id}
                  type="button"
                  aria-pressed={entry.id === family}
                  onClick={() => setAppearance(entry.id)}
                  title={entry.origin}
                  className={cn(
                    "flex items-center gap-2 rounded-card border px-2.5 py-2 text-left text-[0.6875rem] font-medium transition-colors",
                    entry.id === family
                      ? "border-brand-400/60 bg-brand-400/10 text-text-primary"
                      : "border-line/70 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                  )}
                >
                  <span aria-hidden="true" className="h-3 w-3 rounded-full" style={{ background: entry.swatch }} />
                  {entry.label}
                </button>
              ))}
            </div>
            <p className="mt-2 text-[0.625rem] leading-relaxed text-text-faint">{activeFamily.origin}</p>
          </fieldset>

          {/* ── Accent ───────────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">Accent</legend>
            <div className="mt-2 flex flex-wrap gap-1.5">
              <button
                type="button"
                aria-pressed={studio.accent === null}
                onClick={() => setAccent("GlassBlue", null)}
                className={cn(
                  "rounded-full border px-2 py-0.5 text-[0.625rem] transition-colors",
                  studio.accent === null
                    ? "border-brand-400/60 text-text-primary"
                    : "border-line/70 text-text-muted hover:text-text-primary",
                )}
              >
                Family
              </button>
              {studioAccents.map((entry) => (
                <button
                  key={entry.key}
                  type="button"
                  aria-pressed={studio.accentKey === entry.key && studio.accent !== null}
                  aria-label={entry.name}
                  title={entry.name}
                  onClick={() => setAccent(entry.key, entry.hex)}
                  className={cn(
                    "h-6 w-6 rounded-full ring-1 ring-inset ring-black/25 transition-transform hover:scale-110",
                    studio.accentKey === entry.key && studio.accent !== null && "ring-2 ring-brand-300",
                  )}
                  style={{ background: entry.hex ?? "rgb(var(--brand-500))" }}
                />
              ))}
            </div>

            <div className="mt-3 flex items-center gap-2">
              <input
                type="color"
                aria-label="Custom accent colour"
                value={customHex}
                onChange={(event) => {
                  setCustomHex(event.target.value);
                  setAccent("Custom", event.target.value);
                }}
                className="h-7 w-9 cursor-pointer rounded border border-line-strong/70 bg-transparent"
              />
              <span className="font-mono text-[0.625rem] uppercase tracking-[0.12em] text-text-faint">
                Custom · {studio.accent ?? "family default"}
              </span>
            </div>
          </fieldset>

          {/* ── Surface character ────────────────────────────────────── */}
          <fieldset className="mt-4 space-y-3">
            <legend className="mono-label">Surface</legend>
            <Slider
              label="Card tint"
              value={studio.tint}
              min={0}
              max={2}
              step={0.05}
              display={`${Math.round(studio.tint * 100)}%`}
              onChange={(value) => setStudio({ tint: value })}
            />
            <Slider
              label="Corner radius"
              value={studio.radius}
              min={0.3}
              max={1.8}
              step={0.05}
              display={`${studio.radius.toFixed(2)}×`}
              onChange={(value) => setStudio({ radius: value })}
            />
            <Slider
              label="Brightness"
              value={studio.brightness}
              min={-0.09}
              max={0.09}
              step={0.005}
              display={`${studio.brightness > 0 ? "+" : ""}${(studio.brightness * 100).toFixed(1)}%`}
              onChange={(value) => setStudio({ brightness: value })}
            />
            <Slider
              label="Vividness"
              value={studio.vividness}
              min={-0.16}
              max={0.16}
              step={0.01}
              display={`${studio.vividness > 0 ? "+" : ""}${Math.round(studio.vividness * 100)}%`}
              onChange={(value) => setStudio({ vividness: value })}
            />
          </fieldset>

          {/* ── Money ────────────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">Money colours</legend>
            <p className="mt-1.5 text-[0.625rem] leading-relaxed text-text-faint">
              Used wherever a number is money rather than a label — margins, VAT, wastage, utilization.
            </p>
            <div className="mt-2 space-y-2">
              <ColourRow
                label="Positive"
                value={studio.moneyPositive}
                onChange={(value) => setStudio({ moneyPositive: value })}
              />
              <ColourRow
                label="Negative"
                value={studio.moneyNegative}
                onChange={(value) => setStudio({ moneyNegative: value })}
              />
            </div>
          </fieldset>

          <div className="mt-5 flex items-center justify-between gap-3 border-t border-line/60 pt-4">
            <button
              type="button"
              onClick={resetStudio}
              className="rounded-full border border-line-strong/70 px-3 py-1.5 text-[0.6875rem] font-medium text-text-muted transition-colors hover:border-brand-400/50 hover:text-text-primary"
            >
              Reset everything
            </button>
            <p className="text-right font-mono text-[0.5625rem] uppercase tracking-[0.12em] text-text-faint">
              Saved · {activeMode.label} · {activeFamily.label}
            </p>
          </div>
        </div>
      ) : null}
    </>
  );
}

function Slider({
  label,
  value,
  min,
  max,
  step,
  display,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step: number;
  display: string;
  onChange: (value: number) => void;
}) {
  return (
    <label className="block">
      <span className="flex items-baseline justify-between gap-3">
        <span className="text-[0.6875rem] font-medium text-text-secondary">{label}</span>
        <span className="font-mono text-[0.625rem] tabular-nums text-text-faint">{display}</span>
      </span>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        className="mt-1.5 w-full accent-[rgb(var(--brand-400))]"
      />
    </label>
  );
}

function ColourRow({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="flex items-center justify-between gap-3">
      <span className="text-[0.6875rem] font-medium text-text-secondary">{label}</span>
      <span className="flex items-center gap-2">
        <span className="font-mono text-[0.625rem] uppercase tracking-[0.1em] text-text-faint">{value}</span>
        <input
          type="color"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          className="h-6 w-8 cursor-pointer rounded border border-line-strong/70 bg-transparent"
        />
      </span>
    </label>
  );
}
