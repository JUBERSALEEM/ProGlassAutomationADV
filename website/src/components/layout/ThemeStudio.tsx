"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { appearanceFamilies, themeModes, type AppearanceFamilyEntry, type ThemeModeEntry } from "@/data/themes";
import { setThemeMode, useAppearance, useResolvedTheme, useThemeMode } from "@/lib/useTheme";
import {
  bundledTypefaceKeys,
  CLAY_PRESETS,
  installedTypefaceKeys,
  resetStudio,
  setAccent,
  setFamily,
  setStudio,
  studioAccents,
  studioTypography,
  typefaces,
  useStudio,
} from "@/lib/useStudio";
import { luminance } from "@/lib/color";
import { Slider, edgeReadout } from "@/components/ui/Slider";
import { useT } from "@/i18n/provider";

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

export function ThemeStudio({ autoOpen = false }: { autoOpen?: boolean } = {}) {
  const t = useT();
  /* The lazy host (ThemeStudioLazy) summons the drawer chunk already-asked-for:
     when it mounts this component the open state is owed at once, so the
     visitor clicks the trigger once and the panel is simply there. */
  const [open, setOpen] = useState(autoOpen);
  const [customHex, setCustomHex] = useState("#6546B5");
  const panel = useRef<HTMLDivElement | null>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);

  const mode = useThemeMode();
  const family = useAppearance();
  /* What `System` currently resolves to — see the note under the mode plates. */
  const resolved = useResolvedTheme();
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
          <span className="h-4 w-4 -ml-2 rounded-full ring-2 ring-white/25" style={{ background: studio.accent ?? "#7E57CE" }} />
        </span>
        <span>Theme &amp; colour</span>
        <span className="sr-only">{t("— open the theme studio. Current: {v1} mode, {v2} palette.", { v1: t(activeMode.label), v2: t(activeFamily.label) })}</span>
      </button>

      {open ? (
        <div
          ref={panel}
          id="theme-studio"
          role="dialog"
          aria-label={t("Theme and colour")}
          className="no-print menu-card animate-scale-in fixed inset-x-3 bottom-20 z-50 max-h-[78vh] overflow-y-auto rounded-panel p-4 shadow-raised sm:inset-x-auto sm:right-5 sm:w-[24rem] sm:p-5"
        >
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="eyebrow">Theme &amp; colour</p>
              <p className="mt-1.5 text-xs font-medium leading-relaxed text-text-secondary">{t("{v1}setting in the application.", { v1: t("Change how this site looks while you use it. Every choice is saved, and every one maps to a real") })}</p>
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded-full border border-line-strong/80 px-2.5 py-1 text-[0.75rem] font-semibold text-text-secondary transition-colors hover:text-text-primary"
            >
              {t("Close")}
            </button>
          </div>

          {/* ── Mode plates ───────────────────────────────────────────── */}
          <fieldset className="mt-5">
            <legend className="mono-label">{t("Mode")}</legend>
            <div className="mt-2 grid grid-cols-3 gap-2">
              {themeModes.map((entry) => {
                const active = entry.id === mode;
                return (
                  <button
                    key={entry.id}
                    type="button"
                    aria-pressed={active}
                    onClick={() => setThemeMode(entry.id)}
                    title={t(entry.hint)}
                    className={cn(
                      "flex flex-col items-center gap-1.5 rounded-card border-2 px-2 py-2.5 text-[0.75rem] font-bold transition-colors",
                      active
                        ? "border-brand-400 bg-brand-500/15 text-text-primary"
                        : "border-line-strong/70 text-text-secondary hover:border-brand-400/60 hover:text-text-primary",
                    )}
                  >
                    {/*
                      A miniature of the mode, so the plate reads at a glance.
                      The two colours are the site's own two page colours — the
                      midnight page and the ivory one — rather than white and
                      black: these are the actual surfaces the choice paints,
                      and black is not one of them.
                    */}
                    {entry.id === "system" ? (
                      <span
                        aria-hidden="true"
                        className="flex h-6 w-full overflow-hidden rounded-[0.3rem] border border-line-strong"
                      >
                        <span className="h-full w-1/2 bg-[rgb(248_245_238)]" />
                        <span className="h-full w-1/2 bg-[rgb(8_5_22)]" />
                      </span>
                    ) : (
                      <span
                        aria-hidden="true"
                        className={cn(
                          "flex h-6 w-full items-center justify-center rounded-[0.3rem] border text-[0.625rem] font-bold uppercase tracking-wider",
                          entry.id === "dark" && "border-white/20 bg-[rgb(8_5_22)] text-[rgb(248_245_238)]",
                          entry.id === "light" && "border-[rgb(8_5_22)]/25 bg-[rgb(248_245_238)] text-[rgb(8_5_22)]",
                        )}
                      >
                        {t(entry.label)}
                      </span>
                    )}
                    {t(entry.label)}
                  </button>
                );
              })}
            </div>
            {/*
              The engine's third value, printed where it is decided.

              `System` is not a shade — it is a deferral, so the plates above
              cannot show what it currently means. This line does: it says what
              the page is wearing right now, which is the answer a visitor who
              clicked System is looking for.
            */}
            <p className="mt-2 font-mono text-[0.6875rem] font-medium text-text-faint">
              {mode === "system"
                ? t.template`Following the system — this page is ${t(resolved === "daylight" ? "light" : "dark")} right now.`
                : t.template`Set to ${t(mode === "light" ? "Light" : "Dark")} — this page does not follow the system while an explicit reading is chosen.`}
            </p>
          </fieldset>

          {/* ── Palette family ────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">{t("Palette family")}</legend>
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
                    {t(entry.label)}
                  </button>
                );
              })}
            </div>
            <p className="mt-2 text-[0.75rem] font-medium leading-relaxed text-text-muted">{activeFamily.origin}</p>
            <p className="mt-1.5 text-xs leading-relaxed text-text-faint">{t("Choosing a family sets the palette to that family's own accent. A colour plate below overrides it again — the last one you touch wins.")}</p>
          </fieldset>

          {/* ── Colour plates ─────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">{t("Colour plates")}</legend>
            <p className="mt-1.5 text-[0.75rem] font-medium leading-relaxed text-text-muted">
              {t("The sixteen accents the application ships. Tap one and the whole site takes it.")}
            </p>

            <div className="mt-2 grid grid-cols-4 gap-2">
              <PlateButton
                label={t("Family")}
                colour={activeFamily.swatch}
                active={studio.accent === null}
                onClick={() => setAccent("Family", null)}
                onDark={luminance(activeFamily.swatch) < 0.4}
              />
              {studioAccents.map((entry) => (
                <PlateButton
                  key={entry.key}
                  label={t(entry.name)}
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
                aria-label={t("Pick a custom accent colour")}
                value={customHex}
                onChange={(event) => {
                  setCustomHex(event.target.value);
                  setAccent("Custom", event.target.value);
                }}
                className="h-9 w-11 shrink-0"
              />
              <label className="flex flex-1 items-center gap-2 rounded-card border-2 border-line-strong/70 bg-ink-900 px-2.5 py-1.5">
                <span className="font-mono text-[0.75rem] font-bold uppercase tracking-[0.12em] text-text-muted">
                  {t("Hex")}
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

          {/* ── Clay engine · the plate bench ─────────────────────────
              Clay is a separate engine, so its colours get their own set of
              dials instead of living inside the shared accent plates: the
              six-plate bench preset, a hand-picked house plate, and the
              free custom plate slot. One tap re-colors every clay card on
              every page at once — that is the point of a material that is
              many colors, not one brand violet. The plum ink never moves;
              picks are pulled pastel so plates always carry it. */}
          <fieldset className="mt-4">
            <legend className="mono-label">{t("Clay engine — plate bench")}</legend>
            <p className="mt-1.5 text-[0.75rem] font-medium leading-relaxed text-text-muted">
              {t("Six pastel plates plus the accent channels, recolored as one bench. Applies when the Clay family is active; the plum print on the plates never changes.")}
            </p>
            <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4">
              {(Object.keys(CLAY_PRESETS) as (keyof typeof CLAY_PRESETS)[]).map((preset) => {
                const tokens = CLAY_PRESETS[preset].tokens;
                const dots = [tokens["--plate-1"], tokens["--plate-3"], tokens["--plate-4"], tokens["--plate-6"]];
                return (
                  <button
                    key={preset}
                    type="button"
                    aria-pressed={studio.clayPreset === preset}
                    onClick={() => setStudio({ clayPreset: preset })}
                    className={cn(
                      "flex flex-col items-center gap-1.5 rounded-card border-2 px-2 py-2 text-center transition-colors",
                      studio.clayPreset === preset
                        ? "border-brand-500/70 bg-brand-600/15"
                        : "border-line-strong/60 hover:border-line-strong",
                    )}
                  >
                    <span className="flex -space-x-1.5" aria-hidden="true">
                      {dots.map((tripletValue) => (
                        <span
                          key={tripletValue}
                          className="h-4 w-4 rounded-full ring-2 ring-white/40"
                          style={{ background: `rgb(${tripletValue})` }}
                        />
                      ))}
                    </span>
                    <span className="text-[0.72rem] font-semibold text-text-primary">{t(CLAY_PRESETS[preset].label)}</span>
                  </button>
                );
              })}
            </div>
            <div className="mt-3 grid gap-2 sm:grid-cols-2">
              <label className="flex items-center gap-2 rounded-card border-2 border-line-strong/70 bg-ink-900 px-2.5 py-1.5">
                <input
                  type="color"
                  aria-label={t("Recolor the house plate")}
                  value={studio.clayAccent || "#b8a6ff"}
                  onChange={(event) => setStudio({ clayAccent: event.target.value })}
                  className="h-8 w-10 shrink-0"
                />
                <span className="text-[0.72rem] font-semibold text-text-primary">{t("House plate, by hand")}</span>
              </label>
              <label className="flex items-center gap-2 rounded-card border-2 border-line-strong/70 bg-ink-900 px-2.5 py-1.5">
                <input
                  type="color"
                  aria-label={t("Recolor the free plate")}
                  value={studio.clayCustom || "#cec4ff"}
                  onChange={(event) => setStudio({ clayCustom: event.target.value, clayPreset: "custom" })}
                  className="h-8 w-10 shrink-0"
                />
                <span className="text-[0.72rem] font-semibold text-text-primary">{t("Free plate (.plate-custom)")}</span>
              </label>
            </div>
            <button
              type="button"
              onClick={() => setStudio({ clayPreset: "house", clayAccent: "", clayCustom: "" })}
              className="mt-2 text-[0.72rem] font-semibold text-text-faint underline-offset-2 hover:underline"
            >
              {t("Reset the bench to House")}
            </button>
          </fieldset>

          {/* ── Typeface ──────────────────────────────────────────────── */}
          <fieldset className="mt-4">
            <legend className="mono-label">{t("Typeface")}</legend>
            <p className="mt-1.5 text-xs font-medium leading-relaxed text-text-muted">{t("{v1} {v2}underlining below are drawn rather than slanted and thickened.", { v1: t("Fourteen families in two groups — the four this site serves, and ten the browser resolves from your own"), v2: t("computer. The bundled four carry a real italic and a full weight range, so bold, bold italic and") })}</p>

            <FaceGrid
              caption={t("Bundled with this site")}
              note={t("Served from here, so these four are the same face on every machine.")}
              keys={bundledTypefaceKeys}
              current={studio.typeface}
              columns="grid-cols-2 sm:grid-cols-4"
            />
            <FaceGrid
              caption={t("Installed on your machine")}
              note={t("Named, not downloaded — the browser takes them from your own system, the way FontService takes a font from the computer it runs on.")}
              keys={installedTypefaceKeys}
              current={studio.typeface}
              columns="grid-cols-2 sm:grid-cols-3 lg:grid-cols-5"
            />

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
                <strong className="font-bold">{t("Cut length")}</strong> is <strong className="font-bold">bold</strong> ·{" "}
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
                <p className="mono-label">{t("Font weight")}</p>
                <div className="mt-1.5 flex gap-1.5">
                  {studioTypography.weights.map((option) => (
                    <button
                      key={option.id}
                      type="button"
                      aria-pressed={studio.weight === option.id}
                      title={t(option.note)}
                      onClick={() => setStudio({ weight: option.id })}
                      style={{ fontWeight: option.id === "bold" ? 700 : 400 }}
                      className={cn(
                        "flex-1 rounded-lg border px-2 py-1.5 text-xs transition-colors",
                        studio.weight === option.id
                          ? "border-brand-400 bg-brand-500/15 text-text-primary"
                          : "border-line-strong/70 text-text-muted hover:border-brand-400/60 hover:text-text-primary",
                      )}
                    >
                      {t(option.label)}
                    </button>
                  ))}
                </div>
              </div>

              <div className="rounded-card border-2 border-line-strong/70 px-3 py-2.5">
                <p className="mono-label">Font style &amp; decoration</p>
                <div className="mt-1.5 flex gap-1.5">
                  <ToggleChip
                    label={t("Italic")}
                    active={studio.italic}
                    title={t("AppConfig.FontStyleName = Italic")}
                    onClick={() => setStudio({ italic: !studio.italic })}
                  />
                  <ToggleChip
                    label={t("Underline")}
                    active={studio.underline}
                    title={t("AppConfig.FontUnderline = true")}
                    onClick={() => setStudio({ underline: !studio.underline })}
                  />
                </div>
              </div>
            </div>

            <Slider
              label={t("Interface scale")}
              value={studio.uiScale}
              min={0.9}
              max={1.15}
              step={0.01}
              display={`${Math.round(studio.uiScale * 100)}%`}
              onChange={(value) => setStudio({ uiScale: value })}
            />

            <div className="rounded-card border-2 border-line-strong/70 px-3 py-2.5">
              <p className="mono-label">{t("Display density")}</p>
              <div className="mt-1.5 flex gap-1.5">
                {studioTypography.densities.map((option) => (
                  <button
                    key={option.id}
                    type="button"
                    aria-pressed={studio.density === option.id}
                    title={t(option.note)}
                    onClick={() => setStudio({ density: option.id })}
                    className={cn(
                      "flex-1 rounded-lg border px-2 py-1.5 text-xs transition-colors",
                      studio.density === option.id
                        ? "border-brand-400 bg-brand-500/15 text-text-primary"
                        : "border-line-strong/70 text-text-muted hover:border-brand-400/60 hover:text-text-primary",
                    )}
                  >
                    {t(option.label)}
                  </button>
                ))}
              </div>
              <p className="mt-1.5 text-xs font-medium leading-relaxed text-text-muted">
                The application&apos;s own spacing setting — <em className="not-italic font-semibold">{t("Compact fits more on a laptop; Comfortable gives larger touch targets")}</em>{t(". This site ships comfortable, so nothing moves until you choose compact.")}</p>
            </div>
          </fieldset>

          {/* ── Surface ───────────────────────────────────────────────── */}
          <fieldset className="mt-4 space-y-3">
            <legend className="mono-label">{t("Surface")}</legend>
            <Slider
              label={t("Card tint")}
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
              <p className="text-[0.75rem] font-bold uppercase tracking-[0.14em] text-text-secondary">{t("Card edge")}</p>
              <p className="mt-1 text-[0.6875rem] leading-relaxed text-text-muted">
                {t("The border line on every card, the corner it turns on, and the padding that follows it.")}
              </p>

              <div className="mt-3 space-y-3.5">
                <Slider
                  label={t("Border line")}
                  value={studio.edge}
                  min={1}
                  max={10}
                  step={0.25}
                  display={edgeReadout(studio.edge)}
                  onChange={(value) => setStudio({ edge: value })}
                  hint={t("1.5 px is the hairline the app uses for inputs and gridlines; 2 px is the card edge it ships with; the dial runs to 10 px for plate-strength borders.")}
                />
                <Slider
                  label={t("Corner radius")}
                  value={studio.radius}
                  min={0.3}
                  max={1.8}
                  step={0.05}
                  display={`${studio.radius.toFixed(2)}×`}
                  onChange={(value) => setStudio({ radius: value })}
                  hint={t("Scales the 12 px card radius, exactly as the radius setting scales it in the application.")}
                />
              </div>

              <div className="mt-3.5">
                <p className="mono-label mb-2">{t("Edge colour")}</p>
                <div className="flex flex-wrap items-center gap-1.5">
                  {[
                    { key: "", name: "Family colour", hex: null as string | null },
                    { key: "#FFFFFF", name: "White", hex: "#FFFFFF" },
                    { key: "#161226", name: "Ink", hex: "#161226" },
                    { key: "#8B7CF0", name: "Violet", hex: "#8B7CF0" },
                    { key: "#F27EA2", name: "Rose", hex: "#F27EA2" },
                    { key: "#38BDF8", name: "Sky", hex: "#38BDF8" },
                    { key: "#4ADE80", name: "Leaf", hex: "#4ADE80" },
                    { key: "#FBBF24", name: "Gold", hex: "#FBBF24" },
                  ].map((plate) => (
                    <button
                      key={plate.name}
                      type="button"
                      aria-pressed={studio.edgeColor === (plate.hex ?? "")}
                      aria-label={t(plate.name)}
                      title={t(plate.name)}
                      onClick={() => setStudio({ edgeColor: plate.hex ?? "" })}
                      className={cn(
                        "grid h-7 w-7 place-items-center rounded-full border-2 transition-transform duration-150 hover:scale-110",
                        studio.edgeColor === (plate.hex ?? "")
                          ? "border-text-primary"
                          : "border-line-strong/60",
                      )}
                      style={{
                        background: plate.hex ?? "linear-gradient(135deg, rgb(var(--brand-500)), rgb(var(--brand-300)))",
                      }}
                    >
                      <span className="sr-only">{t(plate.name)}</span>
                    </button>
                  ))}
                  <input
                    type="color"
                    value={studio.edgeColor || "#8B7CF0"}
                    aria-label={t("Pick a custom edge colour")}
                    onChange={(event) => setStudio({ edgeColor: event.target.value })}
                    className="h-7 w-9 shrink-0"
                  />
                </div>
                <p className="mt-1.5 text-[0.6875rem] leading-relaxed text-text-faint">
                  {t("Follows the family until you plate it yourself — every card edge on the site takes the same colour.")}
                </p>
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
              label={t("Brightness")}
              value={studio.brightness}
              min={-0.09}
              max={0.09}
              step={0.005}
              display={`${studio.brightness > 0 ? "+" : ""}${(studio.brightness * 100).toFixed(1)}%`}
              onChange={(value) => setStudio({ brightness: value })}
            />
            <Slider
              label={t("Vividness")}
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
            <legend className="mono-label">{t("Money colours")}</legend>
            <p className="mt-1.5 text-[0.75rem] font-medium leading-relaxed text-text-muted">
              {t("Used wherever a number is money rather than a label — margins, VAT, wastage, utilization.")}
            </p>
            <div className="mt-2 grid grid-cols-2 gap-2">
              <ColourPlate
                label={t("Positive")}
                value={studio.moneyPositive}
                onChange={(value) => setStudio({ moneyPositive: value })}
              />
              <ColourPlate
                label={t("Negative")}
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
              {t("Reset everything")}
            </button>
            <p className="text-right font-mono text-[0.6875rem] font-semibold uppercase tracking-[0.12em] text-text-muted">
              Saved · {t(activeMode.label)} · {t(activeFamily.label)}
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
  const t = useT();
  return (
    <button
      type="button"
      aria-pressed={active}
      aria-label={t.template`${label} colour plate`}
      title={label}
      onClick={onClick}
      className={cn(
        "flex h-14 flex-col items-center justify-end gap-1 rounded-card border-2 p-1.5 text-[0.6875rem] font-bold uppercase tracking-wider transition-transform hover:-translate-y-0.5",
        active ? "border-text-primary" : "border-line-strong/60",
      )}
      style={{ background: colour, color: onDark ? "#FFFFFF" : "#080516" }}
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
  const t = useT();
  return (
    <div className="rounded-card border-2 border-line-strong/70 p-2">
      <p className="text-[0.75rem] font-bold text-text-primary">{label}</p>
      <div className="mt-1.5 flex items-center gap-2">
        <input
          type="color"
          value={value}
          aria-label={t.template`${label} amount colour`}
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

/**
 * One group of families.
 *
 * The desktop application's font picker has exactly these two halves — what it
 * ships and what the machine has — and each button is set in the face it
 * offers, so the choice is made by looking at the thing itself rather than at
 * its name.
 */
function FaceGrid({
  caption,
  note,
  keys,
  current,
  columns,
}: {
  caption: string;
  note: string;
  keys: readonly string[];
  current: string;
  columns: string;
}) {
  const t = useT();
  return (
    <div className="mt-3">
      <p className="mono-label">{caption}</p>
      <p className="mt-1 text-xs font-medium leading-relaxed text-text-muted">{note}</p>
      <div className={cn("mt-2 grid gap-2", columns)}>
        {keys.map((key) => {
          const face = typefaces[key as keyof typeof typefaces];
          if (!face) return null;
          const active = key === current;
          return (
            <button
              key={key}
              type="button"
              aria-pressed={active}
              onClick={() => setStudio({ typeface: key as keyof typeof typefaces })}
              title={face.character}
              className={cn(
                "rounded-card border-2 px-2 py-2 text-left transition-colors",
                active ? "border-brand-400 bg-brand-500/15" : "border-line-strong/70 hover:border-brand-400/60",
              )}
            >
              <span
                className={cn("block text-xs font-bold leading-tight", active ? "text-text-primary" : "text-text-secondary")}
                style={{ fontFamily: face.stack }}
              >
                {t(face.name)}
              </span>
              <span className="mt-0.5 block text-xs font-semibold leading-tight text-text-muted" style={{ fontFamily: face.stack }}>
                Aa Bb 0123
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
