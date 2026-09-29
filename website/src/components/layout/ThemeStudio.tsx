"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { appearanceFamilies, themeModes, type AppearanceFamilyEntry, type ThemeModeEntry } from "@/data/themes";
import { setAppearance, setThemeMode, useAppearance, useThemeMode } from "@/lib/useTheme";
import { resetStudio, setAccent, setStudio, studioAccents, useStudio } from "@/lib/useStudio";
import { luminance } from "@/lib/color";

/**
 * The theme studio.
 *
 * A panel the visitor opens from the corner of the screen and re-points the site
 * with while they are reading it. Everything is a control you can see:
 *
 *   Mode        three plates — Light, Dark, System
 *   Colours     all sixteen accent plates the application ships, plus a custom
 *               colour you can pick or type
 *   Surfaces    sliders for tint, corner radius, brightness and vividness
 *   Money       the colours used wherever a figure is money
 *
 * Nothing is a preview image: every control writes straight to the live page,
 * and the choices survive a reload.
 */
export function ThemeStudio() {
  const [open, setOpen] = useState(false);
  const [customHex, setCustomHex] = useState("#7C3AED");
  const panel = useRef<HTMLDivElement | null>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);

  const mode = useThemeMode();
  const family = useAppearance();
  const studio = useStudio();

  // The header dropdown can open the studio, so there is one obvious way in
  // wherever the visitor happens to look first.
  useEffect(() => {
    const openStudio = () => setOpen(true);
    window.addEventListener("proglass-open-studio", openStudio);
    return () => window.removeEventListener("proglass-open-studio", openStudio);
  }, []);

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
        aria-controls="theme-studio"
        className="menu-card group fixed bottom-5 right-5 z-40 flex items-center gap-2.5 rounded-full px-4 py-3 text-xs font-bold text-text-primary shadow-raised transition-transform hover:-translate-y-0.5"
      >
        <span aria-hidden="true" className="flex items-center gap-1">
          <span className="h-4 w-4 rounded-full ring-2 ring-white/25" style={{ background: activeFamily.swatch }} />
          <span className="h-4 w-4 -ml-2 rounded-full ring-2 ring-white/25" style={{ background: studio.accent ?? "#8B5CF6" }} />
        </span>
        <span>Theme &amp; colour</span>
        <span className="sr-only">
          — open the theme studio. Current: {activeMode.label} mode, {activeFamily.label} palette.
        </span>
      </button>

      {open ? (
        <div
          ref={panel}
          id="theme-studio"
          role="dialog"
          aria-label="Theme and colour"
          className="menu-card animate-scale-in fixed inset-x-3 bottom-20 z-50 max-h-[78vh] overflow-y-auto rounded-panel p-4 shadow-raised sm:inset-x-auto sm:right-5 sm:w-[24rem] sm:p-5"
        >
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="eyebrow">Theme &amp; colour</p>
              <p className="mt-1.5 text-xs font-medium leading-relaxed text-text-secondary">
                Change how this site looks while you use it. Every choice is saved, and every one maps to a real
                setting in the application.
              </p>
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded-full border border-line-strong/80 px-2.5 py-1 text-[0.625rem] font-semibold text-text-secondary transition-colors hover:text-text-primary"
            >
              Close
            </button>
          </div>

          {/* ── Mode plates ───────────────────────────────────────────── */}
          <fieldset className="mt-5">
            <legend className="mono-label">Mode</legend>
            <div className="mt-2 grid grid-cols-3 gap-2">
              {themeModes.map((entry) => {
                const active = entry.id === mode;
                return (
                  <button
                    key={entry.id}
                    type="button"
                    aria-pressed={active}
                    onClick={() => setThemeMode(entry.id)}
                    title={entry.hint}
                    className={cn(
                      "flex flex-col items-center gap-1.5 rounded-card border-2 px-2 py-2.5 text-[0.6875rem] font-bold transition-colors",
                      active
                        ? "border-brand-400 bg-brand-500/15 text-text-primary"
                        : "border-line-strong/70 text-text-secondary hover:border-brand-400/60 hover:text-text-primary",
                    )}
                  >
                    {/* A miniature of the mode, so the plate reads at a glance. */}
                    <span
                      aria-hidden="true"
                      className={cn(
                        "flex h-6 w-full items-center justify-center rounded-[0.3rem] border text-[0.5rem] font-bold uppercase tracking-wider",
                        entry.id === "dark" && "border-white/20 bg-[#120D2E] text-white",
                        entry.id === "light" && "border-black/15 bg-white text-black",
                        entry.id === "system" && "border-line-strong bg-gradient-to-r from-white to-[#120D2E] text-black",
                      )}
                    >
                      {entry.label}
                    </span>
                    {entry.label}
                  </button>
                );
              })}
            </div>
          </fieldset>

          {/* ── Palette family ────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">Palette family</legend>
            <div className="mt-2 grid grid-cols-2 gap-2">
              {appearanceFamilies.map((entry) => {
                const active = entry.id === family;
                return (
                  <button
                    key={entry.id}
                    type="button"
                    aria-pressed={active}
                    onClick={() => setAppearance(entry.id)}
                    title={entry.origin}
                    className={cn(
                      "flex items-center gap-2 rounded-card border-2 px-2.5 py-2.5 text-left text-[0.6875rem] font-bold transition-colors",
                      active
                        ? "border-brand-400 bg-brand-500/15 text-text-primary"
                        : "border-line-strong/70 text-text-secondary hover:border-brand-400/60 hover:text-text-primary",
                    )}
                  >
                    <span
                      aria-hidden="true"
                      className="h-4 w-4 shrink-0 rounded-full ring-2 ring-white/20"
                      style={{ background: entry.swatch }}
                    />
                    {entry.label}
                  </button>
                );
              })}
            </div>
            <p className="mt-2 text-[0.6875rem] font-medium leading-relaxed text-text-muted">{activeFamily.origin}</p>
          </fieldset>

          {/* ── Colour plates ─────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">Colour plates</legend>
            <p className="mt-1.5 text-[0.6875rem] font-medium leading-relaxed text-text-muted">
              The sixteen accents the application ships. Tap one and the whole site takes it.
            </p>

            <div className="mt-2 grid grid-cols-4 gap-2">
              <PlateButton
                label="Family"
                colour={activeFamily.swatch}
                active={studio.accent === null}
                onClick={() => setAccent("GlassBlue", null)}
                onDark={luminance(activeFamily.swatch) < 0.4}
              />
              {studioAccents.map((entry) => (
                <PlateButton
                  key={entry.key}
                  label={entry.name}
                  colour={entry.hex ?? "#0369A1"}
                  active={studio.accentKey === entry.key && studio.accent !== null}
                  onClick={() => setAccent(entry.key, entry.hex)}
                  onDark={luminance(entry.hex ?? "#0369A1") < 0.4}
                />
              ))}
            </div>

            {/* Custom colour: a picker and a field you can type into. */}
            <div className="mt-3 flex items-center gap-2">
              <input
                type="color"
                aria-label="Pick a custom accent colour"
                value={customHex}
                onChange={(event) => {
                  setCustomHex(event.target.value);
                  setAccent("Custom", event.target.value);
                }}
                className="h-9 w-11 shrink-0"
              />
              <label className="flex flex-1 items-center gap-2 rounded-card border-2 border-line-strong/70 bg-ink-900 px-2.5 py-1.5">
                <span className="font-mono text-[0.625rem] font-bold uppercase tracking-[0.12em] text-text-muted">
                  Hex
                </span>
                <input
                  value={customHex}
                  onChange={(event) => {
                    const next = event.target.value;
                    setCustomHex(next);
                    if (/^#?[0-9a-fA-F]{6}$/.test(next.trim())) {
                      setAccent("Custom", next.trim().startsWith("#") ? next.trim() : `#${next.trim()}`);
                    }
                  }}
                  spellCheck={false}
                  className="w-full bg-transparent font-mono text-xs font-semibold text-text-primary outline-none"
                />
              </label>
            </div>
          </fieldset>

          {/* ── Surface ───────────────────────────────────────────────── */}
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

          {/* ── Money ─────────────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">Money colours</legend>
            <p className="mt-1.5 text-[0.6875rem] font-medium leading-relaxed text-text-muted">
              Used wherever a number is money rather than a label — margins, VAT, wastage, utilization.
            </p>
            <div className="mt-2 grid grid-cols-2 gap-2">
              <ColourPlate
                label="Positive"
                value={studio.moneyPositive}
                onChange={(value) => setStudio({ moneyPositive: value })}
              />
              <ColourPlate
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
              className="rounded-full border-2 border-line-strong/80 px-3 py-1.5 text-[0.6875rem] font-bold text-text-secondary transition-colors hover:border-brand-400/60 hover:text-text-primary"
            >
              Reset everything
            </button>
            <p className="text-right font-mono text-[0.5625rem] font-semibold uppercase tracking-[0.12em] text-text-muted">
              Saved · {activeMode.label} · {activeFamily.label}
            </p>
          </div>
        </div>
      ) : null}
    </>
  );
}

/** One accent plate: a filled swatch with its own name printed on it. */
function PlateButton({
  label,
  colour,
  active,
  onClick,
  onDark,
}: {
  label: string;
  colour: string;
  active: boolean;
  onClick: () => void;
  onDark: boolean;
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      aria-label={`${label} colour plate`}
      title={label}
      onClick={onClick}
      className={cn(
        "flex h-14 flex-col items-center justify-end gap-1 rounded-card border-2 p-1.5 text-[0.5625rem] font-bold uppercase tracking-wider transition-transform hover:-translate-y-0.5",
        active ? "border-text-primary" : "border-line-strong/60",
      )}
      style={{ background: colour, color: onDark ? "#FFFFFF" : "#0B0820" }}
    >
      <span className="max-w-full truncate drop-shadow-sm">{label}</span>
    </button>
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
        <span className="text-[0.75rem] font-bold text-text-primary">{label}</span>
        <span className="font-mono text-[0.625rem] font-semibold tabular-nums text-text-muted">{display}</span>
      </span>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        aria-label={label}
        onChange={(event) => onChange(Number(event.target.value))}
        className="mt-1"
      />
    </label>
  );
}

function ColourPlate({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="rounded-card border-2 border-line-strong/70 p-2">
      <p className="text-[0.6875rem] font-bold text-text-primary">{label}</p>
      <div className="mt-1.5 flex items-center gap-2">
        <input
          type="color"
          value={value}
          aria-label={`${label} amount colour`}
          onChange={(event) => onChange(event.target.value)}
          className="h-7 w-9 shrink-0"
        />
        <span className="font-mono text-[0.625rem] font-semibold uppercase text-text-muted">{value}</span>
      </div>
    </div>
  );
}
