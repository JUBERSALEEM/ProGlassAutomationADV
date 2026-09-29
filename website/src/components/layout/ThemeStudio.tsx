"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { appearanceFamilies, themeModes, type AppearanceFamilyEntry, type ThemeModeEntry } from "@/data/themes";
import { setThemeMode, useAppearance, useThemeMode } from "@/lib/useTheme";
import {
  resetStudio,
  setAccent,
  setFamily,
  setStudio,
  studioAccents,
  studioTypography,
  typefaces,
  typefaceKeys,
  useStudio,
} from "@/lib/useStudio";
import { luminance } from "@/lib/color";
import { Slider, edgeReadout } from "@/components/ui/Slider";

/**
 * The theme studio.
 *
 * A panel the visitor opens from the corner of the screen and re-points the site
 * with while they are reading it. Everything is a control you can see:
 *
 *   Mode        three plates — Light, Dark, System
 *   Colours     all sixteen accent plates the application ships, plus a custom
 *               colour you can pick or type
 *   Typefaces   four self-hosted faces, with a specimen in bold, bold italic
 *               and underline
 *   Surfaces    sliders for tint, the card edge, corner radius and brightness
 *   Money       the colours used wherever a figure is money
 *
 * Nothing is a preview image: every control writes straight to the live page,
 * and the choices survive a reload.
 */
function tintReadout(value: number): string {
  if (value === 0) return "None";
  if (Math.abs(value - 1) < 0.001) return "Family default · 1.00×";
  return `${value.toFixed(2)}×`;
}

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
        className="no-print menu-card group fixed bottom-5 right-5 z-40 flex items-center gap-2.5 rounded-full px-4 py-3 text-xs font-bold text-text-primary shadow-raised transition-transform hover:-translate-y-0.5"
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
          className="no-print menu-card animate-scale-in fixed inset-x-3 bottom-20 z-50 max-h-[78vh] overflow-y-auto rounded-panel p-4 shadow-raised sm:inset-x-auto sm:right-5 sm:w-[24rem] sm:p-5"
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
              className="rounded-full border border-line-strong/80 px-2.5 py-1 text-[0.75rem] font-semibold text-text-secondary transition-colors hover:text-text-primary"
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
                      "flex flex-col items-center gap-1.5 rounded-card border-2 px-2 py-2.5 text-[0.75rem] font-bold transition-colors",
                      active
                        ? "border-brand-400 bg-brand-500/15 text-text-primary"
                        : "border-line-strong/70 text-text-secondary hover:border-brand-400/60 hover:text-text-primary",
                    )}
                  >
                    {/* A miniature of the mode, so the plate reads at a glance. */}
                    <span
                      aria-hidden="true"
                      className={cn(
                        "flex h-6 w-full items-center justify-center rounded-[0.3rem] border text-[0.625rem] font-bold uppercase tracking-wider",
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
                    /* Re-bases the palette, so this plate always visibly does
                       something even after a colour plate was picked. */
                    onClick={() => setFamily(entry.id)}
                    title={entry.origin}
                    className={cn(
                      "flex items-center gap-2 rounded-card border-2 px-2.5 py-2.5 text-left text-[0.75rem] font-bold transition-colors",
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
            <p className="mt-2 text-[0.75rem] font-medium leading-relaxed text-text-muted">{activeFamily.origin}</p>
            <p className="mt-1.5 text-xs leading-relaxed text-text-faint">
              Choosing a family sets the palette to that family&apos;s own accent. A colour plate below overrides it
              again — the last one you touch wins.
            </p>
          </fieldset>

          {/* ── Colour plates ─────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">Colour plates</legend>
            <p className="mt-1.5 text-[0.75rem] font-medium leading-relaxed text-text-muted">
              The sixteen accents the application ships. Tap one and the whole site takes it.
            </p>

            <div className="mt-2 grid grid-cols-4 gap-2">
              <PlateButton
                label="Family"
                colour={activeFamily.swatch}
                active={studio.accent === null}
                onClick={() => setAccent("Family", null)}
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
                <span className="font-mono text-[0.75rem] font-bold uppercase tracking-[0.12em] text-text-muted">
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

          {/* ── Typeface ──────────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">Typeface</legend>
            <p className="mt-1.5 text-xs font-medium leading-relaxed text-text-muted">
              Four faces, all served from this site. Every one carries a real italic and a full weight range, so bold,
              bold italic and underlining below are drawn — not slanted and thickened by the browser.
            </p>

            <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4">
              {typefaceKeys.map((key) => {
                const face = typefaces[key];
                const active = key === studio.typeface;
                return (
                  <button
                    key={key}
                    type="button"
                    aria-pressed={active}
                    onClick={() => setStudio({ typeface: key })}
                    title={face.character}
                    className={cn(
                      "rounded-card border-2 px-2 py-2 text-left transition-colors",
                      active
                        ? "border-brand-400 bg-brand-500/15"
                        : "border-line-strong/70 hover:border-brand-400/60",
                    )}
                  >
                    <span
                      className={cn("block text-sm font-bold leading-tight", active ? "text-text-primary" : "text-text-secondary")}
                      style={{ fontFamily: face.stack }}
                    >
                      {face.name}
                    </span>
                    <span className="mt-0.5 block text-xs font-semibold leading-tight text-text-muted" style={{ fontFamily: face.stack }}>
                      Aa Bb 0123
                    </span>
                  </button>
                );
              })}
            </div>

            {/*
              The specimen. Not a picture of the choice — the live body font, in
              the four treatments the site actually uses. Adding a face that
              cannot draw its own italic would show up right here.
            */}
            <div className="mt-2 rounded-card border-2 border-line-strong/70 bg-ink-900/70 p-3">
              <p className="text-base leading-relaxed text-text-primary">
                {typefaces[studio.typeface].sample}
              </p>
              <p className="mt-2 text-sm leading-relaxed text-text-secondary">
                <strong className="font-bold">Cut length</strong> is <strong className="font-bold">bold</strong> ·{" "}
                <em className="font-bold italic">offcut area</em> is <strong className="font-bold italic">bold italic</strong> ·{" "}
                <span className="underline decoration-brand-400 decoration-2 underline-offset-2">net area</span> is{" "}
                <span className="font-bold underline decoration-brand-400 decoration-2 underline-offset-2">
                  bold underlined
                </span>
              </p>
              <p className="mt-2 font-mono text-xs text-text-muted">
                1,600 × 1,100 mm · 2.5 kg/m²/mm · 34,762.19
              </p>
            </div>

            {/*
              The application's typography settings, beside the family.
              `AppConfig` holds FontWeightName, FontStyleName, FontUnderline,
              UiScale and DisplayDensity, and Settings → Typography exposes all
              five; the desktop app applies them to the whole interface. So does
              this: choosing Bold thickens every weight on the page, not the
              headings alone.
            */}
            <div className="mt-3 grid gap-2 sm:grid-cols-2">
              <div className="rounded-card border-2 border-line-strong/70 px-3 py-2.5">
                <p className="mono-label">Font weight</p>
                <div className="mt-1.5 flex gap-1.5">
                  {studioTypography.weights.map((option) => (
                    <button
                      key={option.id}
                      type="button"
                      aria-pressed={studio.weight === option.id}
                      title={option.note}
                      onClick={() => setStudio({ weight: option.id })}
                      style={{ fontWeight: option.id === "bold" ? 700 : 400 }}
                      className={cn(
                        "flex-1 rounded-lg border px-2 py-1.5 text-xs transition-colors",
                        studio.weight === option.id
                          ? "border-brand-400 bg-brand-500/15 text-text-primary"
                          : "border-line-strong/70 text-text-muted hover:border-brand-400/60 hover:text-text-primary",
                      )}
                    >
                      {option.label}
                    </button>
                  ))}
                </div>
              </div>

              <div className="rounded-card border-2 border-line-strong/70 px-3 py-2.5">
                <p className="mono-label">Font style &amp; decoration</p>
                <div className="mt-1.5 flex gap-1.5">
                  <ToggleChip
                    label="Italic"
                    active={studio.italic}
                    title="AppConfig.FontStyleName = Italic"
                    onClick={() => setStudio({ italic: !studio.italic })}
                  />
                  <ToggleChip
                    label="Underline"
                    active={studio.underline}
                    title="AppConfig.FontUnderline = true"
                    onClick={() => setStudio({ underline: !studio.underline })}
                  />
                </div>
              </div>
            </div>

            <Slider
              label="Interface scale"
              value={studio.uiScale}
              min={0.9}
              max={1.15}
              step={0.01}
              display={`${Math.round(studio.uiScale * 100)}%`}
              onChange={(value) => setStudio({ uiScale: value })}
            />

            <div className="rounded-card border-2 border-line-strong/70 px-3 py-2.5">
              <p className="mono-label">Display density</p>
              <div className="mt-1.5 flex gap-1.5">
                {studioTypography.densities.map((option) => (
                  <button
                    key={option.id}
                    type="button"
                    aria-pressed={studio.density === option.id}
                    title={option.note}
                    onClick={() => setStudio({ density: option.id })}
                    className={cn(
                      "flex-1 rounded-lg border px-2 py-1.5 text-xs transition-colors",
                      studio.density === option.id
                        ? "border-brand-400 bg-brand-500/15 text-text-primary"
                        : "border-line-strong/70 text-text-muted hover:border-brand-400/60 hover:text-text-primary",
                    )}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
              <p className="mt-1.5 text-xs font-medium leading-relaxed text-text-muted">
                The application&apos;s own spacing setting — <em className="not-italic font-semibold">Compact fits more on a
                laptop; Comfortable gives larger touch targets</em>. This site ships comfortable, so nothing moves until you
                choose compact.
              </p>
            </div>
          </fieldset>

          {/* ── Surface ───────────────────────────────────────────────── */}
          <fieldset className="mt-4 space-y-3">
            <legend className="mono-label">Surface</legend>
            <Slider
              label="Card tint"
              value={studio.tint}
              min={0}
              max={3}
              step={0.05}
              display={tintReadout(studio.tint)}
              onChange={(value) => setStudio({ tint: value })}
            />

            {/*
              The swatch paints the card fill the dial is controlling: the real
              `.surface` class, the real accent, taken through the same
              `--tint-mix` the cards read. Nothing here is a colour mixed for a
              preview image — at 0 it is the surface colour exactly, which in
              daylight mode is white.
            */}
            <div className="flex items-center gap-3 rounded-card border-2 border-line-strong/70 bg-ink-950/60 p-2.5">
              <span
                aria-hidden="true"
                className="surface h-10 w-16 shrink-0"
                style={{ ["--tint-mix" as string]: `calc(var(--tint-base, 0.1) * ${studio.tint.toFixed(2)})` }}
              />
              <div className="min-w-0">
                <p className="text-xs font-semibold text-text-primary">{tintReadout(studio.tint)}</p>
                <p className="mt-0.5 text-xs leading-relaxed text-text-muted">
                  {studio.tint === 0
                    ? "No accent in the fill — cards are exactly the surface colour."
                    : "The same accent wash every card on the site carries."}
                </p>
              </div>
            </div>
            {/*
              ── Card edge ────────────────────────────────────────────────
              The card's border line and the corner it turns on a radius, under
              one heading, because that is one decision in the product:
              `CardEdgeThickness` and `CardCornerRadius` are set side by side in
              the application's SETTINGS, and the preview shows both at once
              rather than making the reader imagine them.

              `CardBorderThickness` and `CardEdgeThickness` are two different
              weights in the product — 1.5 px for the line an input or a
              gridline carries, 2 px for the defined edge a card carries — so
              the dial names both, and the padding follows the edge through
              `max(0, edge − 2)` the way `ApplyCardEdgePaddingResources` does.
            */}
            <div className="rounded-card border-2 border-line-strong/60 bg-ink-950/40 p-3">
              <p className="text-[0.75rem] font-bold uppercase tracking-[0.14em] text-text-secondary">Card edge</p>
              <p className="mt-1 text-[0.6875rem] leading-relaxed text-text-muted">
                The border line on every card, the corner it turns on, and the padding that follows it.
              </p>

              <div className="mt-3 space-y-3.5">
                <Slider
                  label="Border line"
                  value={studio.edge}
                  min={1}
                  max={4}
                  step={0.25}
                  display={edgeReadout(studio.edge)}
                  onChange={(value) => setStudio({ edge: value })}
                  hint="1.5 px is the hairline the app uses for inputs and gridlines; 2 px is the card edge it ships with."
                />
                <Slider
                  label="Corner radius"
                  value={studio.radius}
                  min={0.3}
                  max={1.8}
                  step={0.05}
                  display={`${studio.radius.toFixed(2)}×`}
                  onChange={(value) => setStudio({ radius: value })}
                  hint="Scales the 12 px card radius, exactly as the radius setting scales it in the application."
                />
              </div>

              <div className="mt-3 flex items-center gap-3 rounded-card border-2 border-line-strong/70 bg-ink-950/60 p-2.5">
                <span
                  aria-hidden="true"
                  className="grid h-10 w-16 shrink-0 place-items-center bg-ink-900"
                  style={{
                    border: `${studio.edge}px solid var(--card-edge)`,
                    borderRadius: `calc(12px * ${studio.radius})`,
                  }}
                />
                <div className="min-w-0">
                  <p className="text-xs font-semibold text-text-primary">
                    {edgeReadout(studio.edge)} · {studio.radius.toFixed(2)}× radius
                  </p>
                  <p className="mt-0.5 text-xs leading-relaxed text-text-muted">
                    {studio.edge <= 1.5
                      ? "The subtle hairline the application uses for inputs and gridlines."
                      : studio.edge <= 2
                        ? "The defined card edge the application ships as its default."
                        : "A bold industrial edge — padding follows it, so content is never crowded."}
                  </p>
                </div>
              </div>
            </div>
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
            <p className="mt-1.5 text-[0.75rem] font-medium leading-relaxed text-text-muted">
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
              className="rounded-full border-2 border-line-strong/80 px-3 py-1.5 text-[0.75rem] font-bold text-text-secondary transition-colors hover:border-brand-400/60 hover:text-text-primary"
            >
              Reset everything
            </button>
            <p className="text-right font-mono text-[0.6875rem] font-semibold uppercase tracking-[0.12em] text-text-muted">
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
        "flex h-14 flex-col items-center justify-end gap-1 rounded-card border-2 p-1.5 text-[0.6875rem] font-bold uppercase tracking-wider transition-transform hover:-translate-y-0.5",
        active ? "border-text-primary" : "border-line-strong/60",
      )}
      style={{ background: colour, color: onDark ? "#FFFFFF" : "#0B0820" }}
    >
      <span className="max-w-full truncate drop-shadow-sm">{label}</span>
    </button>
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
      <p className="text-[0.75rem] font-bold text-text-primary">{label}</p>
      <div className="mt-1.5 flex items-center gap-2">
        <input
          type="color"
          value={value}
          aria-label={`${label} amount colour`}
          onChange={(event) => onChange(event.target.value)}
          className="h-7 w-9 shrink-0"
        />
        <span className="font-mono text-[0.75rem] font-semibold uppercase text-text-muted">{value}</span>
      </div>
    </div>
  );
}

/**
 * A small on/off pill for the typography switches.
 *
 * Two states, both stated: the app's own fields are booleans and a switch that
 * shows only the state it is in leaves the visitor guessing what the other one
 * does.
 */
function ToggleChip({
  label,
  active,
  title,
  onClick,
}: {
  label: string;
  active: boolean;
  title: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      title={title}
      onClick={onClick}
      className={cn(
        "flex-1 rounded-lg border px-2 py-1.5 text-xs font-medium transition-colors",
        active
          ? "border-brand-400 bg-brand-500/15 text-text-primary"
          : "border-line-strong/70 text-text-muted hover:border-brand-400/60 hover:text-text-primary",
      )}
    >
      {label} · {active ? "on" : "off"}
    </button>
  );
}
