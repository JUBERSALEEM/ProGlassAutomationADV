"use client";

import { useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { accentPresets, appearanceFamilies, themeModes } from "@/data/themes";
import { setThemeMode, useAppearance, useThemeMode } from "@/lib/useTheme";
import { isFamilyAccentKey, setAccent, setFamily, setStudio, studioAccents, useStudio } from "@/lib/useStudio";
import { Slider, edgeReadout } from "@/components/ui/Slider";
import { luminance } from "@/lib/color";
import { useT } from "@/i18n/provider";

/**
 * The appearance controls, in the open on the home page.
 *
 * This is the same state the docked studio writes — one store, two places to
 * reach it — so a visitor who lands on the home page can see immediately that
 * the site takes its palette from the product rather than being a fixed design.
 */
/**
 * The card-edge weights worth naming.
 *
 * 1.5 and 2 are the application's own two: `CardBorderThickness` for the line
 * an input or a gridline carries, `CardEdgeThickness` for the defined edge a
 * card carries. The two above them are the same dial taken further, for a
 * display where 2 px is too fine to see.
 */
const EDGE_PRESETS = [
  { label: "Hairline", edge: 1.5, note: "1.5 px — the line the application gives inputs and gridlines" },
  { label: "Defined", edge: 2, note: "2 px — the card edge the application ships with" },
  { label: "Bold", edge: 3, note: "3 px — a heavier edge, with the padding following it" },
  { label: "Heavy", edge: 4, note: "4 px — the top of the dial" },
] as const;

export function AppearancePreview() {
  const t = useT();
  const mode = useThemeMode();
  const family = useAppearance();
  const studio = useStudio();

  /* The Clay-paused edge controls must ANSWER, not freeze (the "Border
     line button is unresponsive" fix): hard-disabled buttons and a
     pointer-events veil read exactly like a hung UI. In Clay the gate
     stays — no border token is ever written — but touching a paused
     preset or the paused dial explains itself and takes the visitor to
     Corner depth, the dial that replaces it. */
  const [edgePausedNotice, setEdgePausedNotice] = useState(false);
  const [cornerDepthHot, setCornerDepthHot] = useState(false);
  const cornerDepthRef = useRef<HTMLDivElement | null>(null);
  const cornerDepthHotTimer = useRef<number | null>(null);
  const explainClayEdge = () => {
    setEdgePausedNotice(true);
    setCornerDepthHot(true);
    if (cornerDepthHotTimer.current) window.clearTimeout(cornerDepthHotTimer.current);
    cornerDepthHotTimer.current = window.setTimeout(() => setCornerDepthHot(false), 2200);
    requestAnimationFrame(() => {
      const box = cornerDepthRef.current;
      if (!box) return;
      box.scrollIntoView?.({ behavior: "smooth", block: "center" });
      const input = box.querySelector('input[type="range"]');
      if (input instanceof HTMLInputElement && !input.disabled) input.focus({ preventScroll: true });
    });
  };

  const activeFamily = appearanceFamilies.find((entry) => entry.id === family) ?? appearanceFamilies[0]!;

  return (
    <div className="surface-raised card-pad">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="eyebrow">{t("Try it on this page")}</p>
        <p className="font-mono text-2xs font-semibold uppercase tracking-[0.14em] text-text-muted">
          {isFamilyAccentKey(studio.accentKey) ? activeFamily.label : studio.accentKey}
        </p>
      </div>

      {/* ── Mode ─────────────────────────────────────────────────────── */}
      <p className="mt-5 text-xs font-bold uppercase tracking-[0.14em] text-text-secondary">{t("Mode")}</p>
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
              {t(entry.label)}
            </button>
          );
        })}
      </div>

      {/* ── Family ───────────────────────────────────────────────────── */}
      <p className="mt-5 text-xs font-bold uppercase tracking-[0.14em] text-text-secondary">{t("Palette family")}</p>
      <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-5">
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
              {t(entry.label)}
            </button>
          );
        })}
      </div>

      {/* ── Colour plates ────────────────────────────────────────────── */}
      <p className="mt-5 text-xs font-bold uppercase tracking-[0.14em] text-text-secondary">{t("Colour plates")}<span className="font-medium normal-case tracking-normal text-text-muted">— tap one</span>
      </p>
      <div className="mt-2 grid grid-cols-4 gap-2 sm:grid-cols-6">
        <Plate
          label={t("Family")}
          colour={activeFamily.swatch}
          active={studio.accent === null}
          onClick={() => setAccent("Family", null)}
        />
        {studioAccents.map((entry) => (
          <Plate
            key={entry.key}
            label={t(entry.name)}
            colour={entry.hex ?? "#0369A1"}
            active={studio.accentKey === entry.key && studio.accent !== null}
            onClick={() => setAccent(entry.key, entry.hex ?? "#0369A1")}
          />
        ))}
      </div>

      {/* ── Card edge and corner ─────────────────────────────────────── */}
      <p className="mt-5 text-xs font-bold uppercase tracking-[0.14em] text-text-secondary">
        {family === "clay" ? (
          <>{t("Clay corner")}<span className="font-medium normal-case tracking-normal text-text-muted">{t("— light and shade, no border line")}</span></>
        ) : (
          <>{t("Card edge")}<span className="font-medium normal-case tracking-normal text-text-muted">{t("— the border line and the corner")}</span></>
        )}
      </p>
      {/*
        The product sets these together in SETTINGS: CardEdgeThickness for the
        border line, CardCornerRadius for the corner it turns on, and the card
        padding follows the edge so a bolder line never crowds its contents.
        Both dials write the same `--card-edge-width` / `--radius-user` the
        docked studio writes (the family factor multiplies in, it is never
        overwritten), and everything drawn with `.surface` — including
        the card samples below — repaints as they move.
      */}
      {/* Preset plates, because every other choice in this panel is a plate you
          press. Four weights the application's settings describe: the 1.5 px
          hairline its inputs carry, the 2 px card edge it ships with, and two
          heavier edges for high-DPI or accessibility setups. */}
      <div className={family === "clay" ? "mt-2 grid grid-cols-2 gap-2 opacity-70 saturate-[0.7] sm:grid-cols-4" : "mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4"}>
        {EDGE_PRESETS.map((preset) => {
          const active = Math.abs(studio.edge - preset.edge) < 0.001;
          return (
            <button
              key={t(preset.label)}
              type="button"
              aria-pressed={active}
              aria-disabled={family === "clay"}
              onClick={() => (family === "clay" ? explainClayEdge() : setStudio({ edge: preset.edge }))}
              title={t(preset.note)}
              className={cn(
                "flex flex-col items-center gap-1.5 rounded-card border-2 px-2 py-2.5 text-[0.75rem] font-bold transition-colors",
                active
                  ? "border-brand-400 bg-brand-500/15 text-text-primary"
                  : "border-line-strong/70 text-text-secondary hover:border-brand-400/60 hover:text-text-primary",
              )}
            >
              <span
                aria-hidden="true"
                className="h-4 w-8 rounded-[0.35rem] bg-ink-900"
                style={{ border: `${preset.edge}px solid var(--card-edge)` }}
              />
              {t(preset.label)}
            </button>
          );
        })}
      </div>

      {family === "clay" && edgePausedNotice ? (
        <p role="status" className="mt-3 rounded-card bg-brand-500/15 px-3 py-2 text-[0.6875rem] font-semibold leading-relaxed text-text-primary">
          {t("That control is paused in Clay, not broken: clay cards draw no border line. Corner depth — highlighted for you below — is the dial that shapes the clay edge.")}
        </p>
      ) : null}

      <div className="mt-3 grid gap-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] sm:items-start">
        <div className="space-y-3.5">
          <div
            onPointerDownCapture={family === "clay" ? (event) => { event.preventDefault(); explainClayEdge(); } : undefined}
            onKeyDownCapture={family === "clay" ? (event) => { if (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown"].includes(event.key)) { event.preventDefault(); explainClayEdge(); } } : undefined}
          >
            <Slider
              label={t("Card edge")}
              value={studio.edge}
              min={1}
              max={4}
              step={0.25}
              display={edgeReadout(studio.edge)}
              onChange={(value) => setStudio({ edge: value })}
              hint={family === "clay"
                ? t("Paused in Clay — touch it and it takes you to Corner depth, the dial that replaces it.")
                : t("1.5 px is the hairline the app uses for inputs; 2 px is the defined card edge it ships with.")}
            />
          </div>
          {family === "clay" ? (
            <div ref={cornerDepthRef} className={cn("rounded-card", cornerDepthHot && "ring-2 ring-brand-400/80")}>
            <Slider
              label={t("Corner depth")}
              value={studio.clayInnerEdge}
              min={0}
              max={10}
              step={0.1}
              display={`${studio.clayInnerEdge.toFixed(2)}×`}
              onChange={(value) => setStudio({ clayInnerEdge: value })}
              hint={t("Soft light from the top-left and shade pooling bottom-right inside the rounded corner. No border is drawn.")}
            />
            </div>
          ) : null}
          <Slider
            label={t("Corner radius")}
            value={studio.radius}
            min={0.3}
            max={2}
            step={0.05}
            display={`${studio.radius.toFixed(2)}×`}
            onChange={(value) => setStudio({ radius: value })}
            hint={t("Multiplies your family's own roundness, the way the radius slider scales it in the app.")}
          />
        </div>

        {/* Three sample cards, drawn with the real card classes and the real
            tokens, so the edge and the corner are shown rather than described. */}
        <div className="grid grid-cols-3 gap-2 rounded-card border-2 border-line-strong/60 bg-ink-950/50 p-2.5">
          {(["surface", "surface-raised", "stat-tile"] as const).map((sample) => (
            <div key={sample} className={`${sample} flex h-20 flex-col justify-between p-2.5`}>
              <span
                aria-hidden="true"
                className="h-1.5 w-6 rounded-full"
                style={{ background: "var(--card-edge-hover)" }}
              />
              <span className="font-mono text-[0.5625rem] font-semibold uppercase tracking-[0.1em] text-text-faint">
                {sample === "surface" ? t("Card") : sample === "surface-raised" ? t("Raised") : t("Stat")}
              </span>
            </div>
          ))}
          <p className="col-span-3 text-[0.6875rem] leading-snug text-text-muted">
            {family === "clay"
              ? t("Live cards, not a picture: every clay surface takes this corner depth and radius as you move the dials — with no drawn edge.")
              : t("Live cards, not a picture: every `.surface` on the page takes this edge and this corner as you move the dials.")}
          </p>
        </div>
      </div>

      <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-line/60 pt-4">
        <p className="text-xs font-medium leading-relaxed text-text-muted">
          {t.template`${studioAccents.length + 1} plates, drawn from the application's own preset table.`}
        </p>
        <button
          type="button"
          onClick={() => window.dispatchEvent(new Event("proglass-open-studio"))}
          className="rounded-full border-2 border-brand-400/60 bg-brand-500/15 px-3 py-1.5 text-xs font-bold text-text-primary transition-colors hover:border-brand-300"
        >
          {t("Open the full studio")}
        </button>
      </div>

      {/* A compact view of what the accent list actually is. */}
      <p className="sr-only">
        {t("Accent presets:")} {accentPresets.map((entry) => entry.name).join(", ")}.
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
  const t = useT();
  const onDark = luminance(colour) < 0.4;
  return (
    <button
      type="button"
      data-color-plate="true"
      aria-pressed={active}
      aria-label={t.template`${label} colour plate`}
      title={label}
      onClick={onClick}
      className={cn(
        "flex h-12 items-end justify-center rounded-card border-2 p-1 text-[0.625rem] font-bold uppercase tracking-wide transition-transform hover:-translate-y-0.5",
        active ? "border-text-primary ring-2 ring-text-primary/60" : "border-line-strong/60",
      )}
      style={{ background: colour, color: onDark ? "#FFFFFF" : "#080516" }}
    >
      <span className="max-w-full truncate">{label}</span>
    </button>
  );
}
