"use client";

import { cn } from "@/lib/cn";
import { accentPresets, appearanceFamilies, themeModes } from "@/data/themes";
import { setThemeMode, useAppearance, useThemeMode } from "@/lib/useTheme";
import { isFamilyAccentKey, setAccent, setFamily, studioAccents, useStudio } from "@/lib/useStudio";
import { luminance } from "@/lib/color";

/**
 * The appearance controls, in the open on the home page.
 *
 * This is the same state the docked studio writes — one store, two places to
 * reach it — so a visitor who lands on the home page can see immediately that
 * the site takes its palette from the product rather than being a fixed design.
 */
export function AppearancePreview() {
  const mode = useThemeMode();
  const family = useAppearance();
  const studio = useStudio();

  const activeFamily = appearanceFamilies.find((entry) => entry.id === family) ?? appearanceFamilies[0]!;

  return (
    <div className="surface-raised p-5 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="eyebrow">Try it on this page</p>
        <p className="font-mono text-2xs font-semibold uppercase tracking-[0.14em] text-text-muted">
          {isFamilyAccentKey(studio.accentKey) ? activeFamily.label : studio.accentKey}
        </p>
      </div>

      {/* ── Mode ─────────────────────────────────────────────────────── */}
      <p className="mt-5 text-xs font-bold uppercase tracking-[0.14em] text-text-secondary">Mode</p>
      <div className="mt-2 grid grid-cols-3 gap-2">
        {themeModes.map((entry) => {
          const active = entry.id === mode;
          return (
            <button
              key={entry.id}
              type="button"
              aria-pressed={active}
              onClick={() => setThemeMode(entry.id)}
              className={cn(
                "rounded-card border-2 px-2 py-2 text-xs font-bold transition-colors",
                active
                  ? "border-brand-400 bg-brand-500/15 text-text-primary"
                  : "border-line-strong/70 text-text-secondary hover:border-brand-400/60 hover:text-text-primary",
              )}
            >
              {entry.label}
            </button>
          );
        })}
      </div>

      {/* ── Family ───────────────────────────────────────────────────── */}
      <p className="mt-5 text-xs font-bold uppercase tracking-[0.14em] text-text-secondary">Palette family</p>
      <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4">
        {appearanceFamilies.map((entry) => {
          const active = entry.id === family;
          return (
            <button
              key={entry.id}
              type="button"
              aria-pressed={active}
              onClick={() => setFamily(entry.id)}
              title={entry.origin}
              className={cn(
                "flex flex-col items-center gap-1.5 rounded-card border-2 px-2 py-2.5 text-[0.75rem] font-bold transition-colors",
                active
                  ? "border-brand-400 bg-brand-500/15 text-text-primary"
                  : "border-line-strong/70 text-text-secondary hover:border-brand-400/60 hover:text-text-primary",
              )}
            >
              <span
                aria-hidden="true"
                className="h-4 w-4 rounded-full ring-2 ring-white/20"
                style={{ background: entry.swatch }}
              />
              {entry.label}
            </button>
          );
        })}
      </div>

      {/* ── Colour plates ────────────────────────────────────────────── */}
      <p className="mt-5 text-xs font-bold uppercase tracking-[0.14em] text-text-secondary">
        Colour plates <span className="font-medium normal-case tracking-normal text-text-muted">— tap one</span>
      </p>
      <div className="mt-2 grid grid-cols-4 gap-2 sm:grid-cols-6">
        <Plate
          label="Family"
          colour={activeFamily.swatch}
          active={studio.accent === null}
          onClick={() => setAccent("GlassBlue", null)}
        />
        {studioAccents.map((entry) => (
          <Plate
            key={entry.key}
            label={entry.name}
            colour={entry.hex ?? "#0369A1"}
            active={studio.accentKey === entry.key && studio.accent !== null}
            onClick={() => setAccent(entry.key, entry.hex)}
          />
        ))}
      </div>

      <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-line/60 pt-4">
        <p className="text-xs font-medium leading-relaxed text-text-muted">
          {studioAccents.length + 1} plates, drawn from the application&apos;s own preset table.
        </p>
        <button
          type="button"
          onClick={() => window.dispatchEvent(new Event("proglass-open-studio"))}
          className="rounded-full border-2 border-brand-400/60 bg-brand-500/15 px-3 py-1.5 text-xs font-bold text-text-primary transition-colors hover:border-brand-300"
        >
          Open the full studio
        </button>
      </div>

      {/* A compact view of what the accent list actually is. */}
      <p className="sr-only">
        Accent presets: {accentPresets.map((entry) => entry.name).join(", ")}.
      </p>
    </div>
  );
}

function Plate({
  label,
  colour,
  active,
  onClick,
}: {
  label: string;
  colour: string;
  active: boolean;
  onClick: () => void;
}) {
  const onDark = luminance(colour) < 0.4;
  return (
    <button
      type="button"
      aria-pressed={active}
      aria-label={`${label} colour plate`}
      title={label}
      onClick={onClick}
      className={cn(
        "flex h-12 items-end justify-center rounded-card border-2 p-1 text-[0.625rem] font-bold uppercase tracking-wide transition-transform hover:-translate-y-0.5",
        active ? "border-text-primary" : "border-line-strong/60",
      )}
      style={{ background: colour, color: onDark ? "#FFFFFF" : "#0B0820" }}
    >
      <span className="max-w-full truncate">{label}</span>
    </button>
  );
}
