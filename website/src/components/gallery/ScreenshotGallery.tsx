"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { Reveal } from "@/components/ui/Reveal";
import { ScreenshotFrame } from "@/components/gallery/ScreenshotFrame";
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
      <div className="flex flex-wrap gap-2" role="group" aria-label="Filter screenshots by module">
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
              <span className="ml-1.5 font-mono text-[0.625rem] tabular-nums text-text-faint">{count}</span>
            </button>
          );
        })}
      </div>

      <p className="mt-5 text-xs leading-relaxed text-text-faint">
        {visible.length} {visible.length === 1 ? "screen" : "screens"}. Cards carrying a photograph are real assets
        either captured from the application or shipped inside it. Every other card draws the application&apos;s window
        chrome around a working, interactive preview of that module with sample data — labelled as a preview, never
        dressed up as a capture.
      </p>

      <ul className="mt-8 grid gap-6 sm:grid-cols-2 xl:grid-cols-3">
        {visible.map((shot, index) => (
          <Reveal key={shot.id} as="li" delay={Math.min(index, 6) * 55}>
            <button
              type="button"
              onClick={(event) => {
                openerRef.current = event.currentTarget;
                setOpenIndex(index);
              }}
              className="group block w-full text-left"
              aria-label={`Open ${shot.title} in a larger view`}
            >
              <div className="transition-transform duration-300 ease-industrial group-hover:-translate-y-1">
                <ScreenshotFrame shot={shot} />
              </div>
            </button>
          </Reveal>
        ))}
      </ul>

      {active ? <Lightbox shot={active} onClose={close} onStep={step} position={openIndex!} total={visible.length} /> : null}
    </>
  );
}

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
  const closeRef = useRef<HTMLButtonElement | null>(null);
  /** Touch / drag start point, so a horizontal swipe moves to the next screen. */
  const swipe = useRef<{ x: number; y: number } | null>(null);

  useEffect(() => {
    closeRef.current?.focus();
  }, []);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={`${shot.title} — larger view`}
      className="animate-fade-in fixed inset-0 z-[60] flex flex-col bg-ink-950/92 backdrop-blur-md"
    >
      <div className="flex items-center justify-between gap-4 border-b border-line/60 px-4 py-3 sm:px-6">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-text-primary">{shot.title}</p>
          <p className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
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
            <span className="sr-only">Previous screenshot</span>
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
            <span className="sr-only">Next screenshot</span>
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
            <span className="sr-only">Close</span>
            <svg aria-hidden="true" viewBox="0 0 16 16" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round">
              <path d="M4 4l8 8M12 4l-8 8" />
            </svg>
          </button>
        </div>
      </div>

      <div
        className="flex-1 touch-pan-y overflow-y-auto px-4 py-6 sm:px-6"
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
        <div className="mx-auto grid max-w-5xl gap-6 lg:grid-cols-[minmax(0,1.7fr)_minmax(0,1fr)]">
          <div className="animate-scale-in">
            <ScreenshotFrame shot={shot} modal />
          </div>

          <div className="space-y-5">
            <div>
              <p className="eyebrow">What this screen does</p>
              <p className="mt-3 text-sm leading-relaxed text-text-secondary">{shot.caption}</p>
            </div>

            <div>
              <p className="eyebrow">In the module</p>
              <ul className="mt-3 space-y-2">
                {shot.highlights.map((item) => (
                  <li key={item} className="flex gap-2.5 text-sm leading-relaxed text-text-muted">
                    <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                    {item}
                  </li>
                ))}
              </ul>
            </div>

            <dl className="surface space-y-2 p-4 text-xs">
              <div className="flex gap-3">
                <dt className="w-20 shrink-0 font-mono uppercase tracking-[0.12em] text-text-faint">Module</dt>
                <dd className="font-mono text-text-secondary">{shot.moduleKey}</dd>
              </div>
              <div className="flex gap-3">
                <dt className="w-20 shrink-0 font-mono uppercase tracking-[0.12em] text-text-faint">Source</dt>
                <dd className="text-text-secondary">{shot.evidence}</dd>
              </div>
              <div className="flex gap-3">
                <dt className="w-20 shrink-0 font-mono uppercase tracking-[0.12em] text-text-faint">Status</dt>
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
      </div>

      <p className="border-t border-line/60 px-4 py-3 text-center font-mono text-2xs uppercase tracking-[0.14em] text-text-faint sm:px-6">
        Arrow keys to move · Escape to close
      </p>
    </div>
  );
}
