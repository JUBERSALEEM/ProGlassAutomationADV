"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { Reveal } from "@/components/ui/Reveal";
import { ScreenshotFrame } from "@/components/gallery/ScreenshotFrame";
import { useT } from "@/i18n/provider";
import {
  screenshotCategories,
  screenshots,
  type Screenshot,
  type ScreenshotCategory,
} from "@/data/screenshots";

/**
 * Filterable gallery with a keyboard-accessible lightbox.
 *
 * Filters are applied to the same registry that the rest of the site uses, so
 * a screenshot can never appear on one page and be missing on another.
 */
export function ScreenshotGallery({ initialCategory = "all" }: { initialCategory?: ScreenshotCategory | "all" }) {
  const t = useT();
  const [category, setCategory] = useState<ScreenshotCategory | "all">(initialCategory);
  const [openIndex, setOpenIndex] = useState<number | null>(null);
  /** The card that opened the lightbox, so focus can be handed back on close. */
  const openerRef = useRef<HTMLButtonElement | null>(null);

  const visible = useMemo(
    () => (category === "all" ? screenshots : screenshots.filter((s) => s.category === category)),
    [category],
  );

  // Counts per category, so an empty filter is never offered.
  const counts = useMemo(() => {
    const map = new Map<ScreenshotCategory | "all", number>();
    map.set("all", screenshots.length);
    for (const { key } of screenshotCategories) {
      if (key === "all") continue;
      map.set(key, screenshots.filter((s) => s.category === key).length);
    }
    return map;
  }, []);

  const close = useCallback(() => setOpenIndex(null), []);
  const step = useCallback(
    (delta: number) => setOpenIndex((current) => {
      if (current === null) return current;
      const next = current + delta;
      if (next < 0 || next >= visible.length) return current;
      return next;
    }),
    [visible.length],
  );

  useEffect(() => {
    // Closing the lightbox hands focus back to the card that opened it.
    if (openIndex === null) {
      openerRef.current?.focus();
      return;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") close();
      if (event.key === "ArrowRight") step(1);
      if (event.key === "ArrowLeft") step(-1);
    };
    window.addEventListener("keydown", onKey);
    document.body.style.overflow = "hidden";
    return () => {
      window.removeEventListener("keydown", onKey);
      document.body.style.overflow = "";
    };
  }, [openIndex, close, step]);

  const active = openIndex === null ? null : visible[openIndex] ?? null;

  return (
    <>
      <div className="flex flex-wrap gap-2" role="group" aria-label={t("Filter screenshots by module")}>
        {screenshotCategories.map(({ key, label }) => {
          const count = counts.get(key) ?? 0;
          const disabled = count === 0;
          const selected = category === key;
          return (
            <button
              key={key}
              type="button"
              disabled={disabled}
              aria-pressed={selected}
              onClick={() => {
                setCategory(key);
                setOpenIndex(null);
              }}
              className={cn(
                "rounded-full border px-3.5 py-1.5 text-xs font-medium transition-all duration-200",
                selected
                  ? "border-brand-400/60 bg-brand-400/12 text-brand-100"
                  : "border-line-strong/70 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                disabled && "cursor-not-allowed opacity-40 hover:border-line-strong/70 hover:text-text-muted",
              )}
            >
              {label}
              <span className="ml-1.5 font-mono text-[0.75rem] tabular-nums text-text-faint font-medium">{count}</span>
            </button>
          );
        })}
      </div>

      <p className="mt-5 text-xs leading-relaxed text-text-faint font-medium">
        {visible.length} {visible.length === 1 ? "screen" : "screens"}. Cards carrying a photograph are real assets
        either captured from the application or shipped inside it. Every other card draws the application&apos;s window
        chrome around a working, interactive preview of that module with sample data — labelled as a preview, never
        dressed up as a capture.
      </p>

      <ul className="mt-8 grid gap-6 sm:grid-cols-2 xl:grid-cols-3">
        {visible.map((shot, index) => (
          <Reveal
            key={shot.id}
            as="li"
            delay={Math.min(index, 6) * 55}
          >
            {/*
              The card is a container, not a button: the preview inside it holds
              its own controls (tabs, sort headers, stage buttons), and a button
              may not be nested inside a button. The explicit open control sits
              under the preview, where it cannot collide with any of them.
            */}
            {/* See `.defer-render` — the browser may skip this card's style,
                layout and paint until it approaches the viewport. */}
            <div className="defer-render group flex h-full flex-col">
              <div className="transition-transform duration-300 ease-industrial group-hover:-translate-y-1">
                <ScreenshotFrame
      t={t} shot={shot} />
              </div>
              <button
                type="button"
                onClick={(event) => {
                  openerRef.current = event.currentTarget;
                  setOpenIndex(index);
                }}
                className="mt-3 inline-flex w-full items-center justify-center gap-2 rounded-card border border-line-strong/80 bg-ink-800 px-3 py-2 text-xs font-semibold text-text-primary transition-colors duration-200 hover:border-brand-400/60 hover:bg-ink-700"
              >
                <span className="sr-only">{t(shot.title)} — </span>
                {t("Open a larger view")}
                <svg aria-hidden="true" viewBox="0 0 16 16" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M6 3h7v7M13 3 7.5 8.5M11 9.5V13H3V5h3.5" />
                </svg>
              </button>
            </div>
          </Reveal>
        ))}
      </ul>

      {active ? <Lightbox shot={active} onClose={close} onStep={step} position={openIndex!} total={visible.length} /> : null}
    </>
  );
}

/**
 * Makes everything outside the dialog inert — for real, not visually.
 *
 * `aria-modal` tells assistive technology that a dialog is modal, but it does
 * not stop a keyboard or a screen reader from walking into the page behind it.
 * The `inert` attribute does: the subtree is removed from the focus order and
 * from the accessibility tree.
 *
 * The walk goes up from the dialog and inerts each level's siblings, so it
 * covers the navigation and footer and also the sections of the page beside
 * the gallery, without needing a portal or guessing at the DOM. Previous values
 * are recorded and put back on close, including on elements that were already
 * inert for some other reason.
 */
function inertOutside(node: HTMLElement): () => void {
  const restored: { element: HTMLElement; previous: boolean }[] = [];
  let current: HTMLElement | null = node;

  while (current && current !== document.body) {
    const parent: HTMLElement | null = current.parentElement;
    if (!parent) break;
    for (const sibling of Array.from(parent.children)) {
      if (sibling === current || !(sibling instanceof HTMLElement)) continue;
      restored.push({ element: sibling, previous: sibling.inert });
      sibling.inert = true;
    }
    current = parent;
  }

  return () => {
    for (const { element, previous } of restored) element.inert = previous;
  };
}

/** Everything the browser will let a Tab key land on inside a container. */
const FOCUSABLE = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex=\"-1\"])",
].join(",");

function Lightbox({
  shot,
  onClose,
  onStep,
  position,
  total,
}: {
  shot: Screenshot;
  onClose: () => void;
  onStep: (delta: number) => void;
  position: number;
  total: number;
}) {
  const t = useT();
  const closeRef = useRef<HTMLButtonElement | null>(null);
  const dialogRef = useRef<HTMLDivElement | null>(null);
  /** Touch / drag start point, so a horizontal swipe moves to the next screen. */
  const swipe = useRef<{ x: number; y: number } | null>(null);

  useEffect(() => {
    const node = dialogRef.current;
    closeRef.current?.focus();
    if (!node) return;
    // Everything behind the dialog is taken out of the page entirely.
    return inertOutside(node);
  }, []);

  /**
   * The focus trap.
   *
   * `inert` handles this in every current browser, but a trap that depends on a
   * single attribute is one unsupported engine away from letting Tab walk off
   * into the page behind. This is the belt to that braces: Tab at the last
   * control wraps to the first, Shift+Tab at the first wraps to the last.
   */
  function onKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key !== "Tab") return;
    const node = dialogRef.current;
    if (!node) return;

    const focusable = Array.from(node.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
      (element) => element.offsetParent !== null || element === document.activeElement,
    );
    if (focusable.length === 0) return;

    const first = focusable[0]!;
    const last = focusable[focusable.length - 1]!;
    const activeElement = document.activeElement;

    if (event.shiftKey && (activeElement === first || activeElement === node)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  return (
    <div
      ref={dialogRef}
      role="dialog"
      aria-modal="true"
      aria-label={`${shot.title} — larger view`}
      onKeyDown={onKeyDown}
      className="animate-fade-in fixed inset-0 z-[60] flex flex-col"
    >
      {/*
        The backdrop is its own layer, carrying both the blur and the fill, and
        sitting behind the dialog's content. Put the blur on the container
        instead and the caption, the counter and the buttons — all of them text
        — inherit a composited layer, which is rasterised without subpixel
        antialiasing. Down here it costs nothing and blurs only the screenshot
        it is covering.
      */}
      <span aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10 bg-ink-950/95 backdrop-blur-md" />
      <div className="flex items-center justify-between gap-4 border-b border-line/60 px-4 py-3 sm:px-6">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-text-primary">{t(shot.title)}</p>
          <p className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
            {String(position + 1).padStart(2, "0")} / {String(total).padStart(2, "0")} · {shot.moduleKey}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => onStep(-1)}
            disabled={position === 0}
            className="grid h-9 w-9 place-items-center rounded-xl border border-line-strong/70 text-text-secondary transition-colors hover:border-brand-400/50 hover:text-text-primary disabled:opacity-35"
          >
            <span className="sr-only">{t("Previous screenshot")}</span>
            <svg aria-hidden="true" viewBox="0 0 16 16" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
              <path d="M10 3 5 8l5 5" />
            </svg>
          </button>
          <button
            type="button"
            onClick={() => onStep(1)}
            disabled={position === total - 1}
            className="grid h-9 w-9 place-items-center rounded-xl border border-line-strong/70 text-text-secondary transition-colors hover:border-brand-400/50 hover:text-text-primary disabled:opacity-35"
          >
            <span className="sr-only">{t("Next screenshot")}</span>
            <svg aria-hidden="true" viewBox="0 0 16 16" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
              <path d="M6 3l5 5-5 5" />
            </svg>
          </button>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            className="grid h-9 w-9 place-items-center rounded-xl border border-line-strong/70 text-text-secondary transition-colors hover:border-state-danger/50 hover:text-state-danger"
          >
            <span className="sr-only">{t("Close")}</span>
            <svg aria-hidden="true" viewBox="0 0 16 16" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round">
              <path d="M4 4l8 8M12 4l-8 8" />
            </svg>
          </button>
        </div>
      </div>

      <div
        className="flex-1 touch-pan-y overflow-y-auto px-3 py-4 sm:px-5 sm:py-6 lg:px-6"
        onClick={(event) => {
          if (event.target === event.currentTarget) onClose();
        }}
        onPointerDown={(event) => {
          swipe.current = { x: event.clientX, y: event.clientY };
        }}
        onPointerUp={(event) => {
          const start = swipe.current;
          swipe.current = null;
          if (!start) return;
          const dx = event.clientX - start.x;
          const dy = event.clientY - start.y;
          if (Math.abs(dx) < 56 || Math.abs(dx) <= Math.abs(dy)) return;
          onStep(dx < 0 ? 1 : -1);
        }}
      >
        {/*
          Full landscape, not a two-column squeeze.
          
          The preview used to sit in a `max-w-5xl` grid beside a details column,
          so on a large screen it was given about 57% of 1024px — roughly 580px
          to show an application window. Every label, figure and column heading
          in the reconstruction was too small to read, which defeats the point of
          opening it.
          
          The preview now takes the whole width up to a generous cap and the
          details move underneath it, where they cost nothing. A 16:9-ish window
          gets the full measure of the screen and every detail is legible.
        */}
        <div className="animate-scale-in mx-auto w-full max-w-[110rem]">
          <ScreenshotFrame t={t} shot={shot} modal />
        </div>

        {/*
          Two columns under the preview: the description on the left where the
          prose has room to breathe, the facts on the right where they can be
          scanned. A third column would be empty — there are only two blocks of
          content here, and padding the layout out to three would just make the
          caption narrow again.
        */}
        <div className="mx-auto mt-8 grid w-full max-w-[110rem] gap-8 lg:grid-cols-[minmax(0,1.55fr)_minmax(0,1fr)]">
          <div className="space-y-5">
            <div>
              <p className="eyebrow">{t("What this screen does")}</p>
              <p className="mt-3 text-sm leading-relaxed text-text-secondary">{t(shot.caption)}</p>
            </div>

            <div>
              <p className="eyebrow">{t("In the module")}</p>
              {/* Two columns of bullets: with the preview full width there is
                  no reason to stack five short lines into one tall list. */}
              <ul className="mt-3 grid gap-2 sm:grid-cols-2">
                {shot.highlights.map((item) => (
                  <li key={item} className="flex gap-2.5 text-sm leading-relaxed text-text-muted">
                    <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                    {item}
                  </li>
                ))}
              </ul>
            </div>
          </div>

          <dl className="surface h-fit space-y-3 p-5 text-xs">
            <div className="flex gap-3">
              <dt className="w-20 shrink-0 font-mono uppercase tracking-[0.12em] text-text-faint">{t("Module")}</dt>
              <dd className="font-mono text-text-secondary">{shot.moduleKey}</dd>
            </div>
            <div className="flex gap-3">
              <dt className="w-20 shrink-0 font-mono uppercase tracking-[0.12em] text-text-faint">{t("Source")}</dt>
              <dd className="text-text-secondary">{t(shot.evidence)}</dd>
            </div>
            <div className="flex gap-3">
              <dt className="w-20 shrink-0 font-mono uppercase tracking-[0.12em] text-text-faint">{t("Status")}</dt>
              <dd className="text-text-secondary">
                {shot.source === "capture"
                  ? "Screen capture of the application"
                  : shot.source === "repo"
                    ? "Asset shipped inside the application"
                    : "Interactive preview — real chrome and fields, sample data"}
              </dd>
            </div>
          </dl>
        </div>
      </div>

      <p className="border-t border-line/60 px-4 py-3 text-center font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium sm:px-6">
        {t("Arrow keys to move · Escape to close")}
      </p>
    </div>
  );
}
