"use client";

import type { ReactNode } from "react";
import { useCallback, useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/provider";

/**
 * Horizontal slider used across the site.
 *
 * Deliberately does NOT auto-advance: a visitor reading a cutting layout or a
 * pricing table should never have it slide away underneath them. Navigation
 * is arrows, dots, drag/swipe and the left/right arrow keys.
 */
export function ProductSlider({
  slides,
  ariaLabel,
  className,
  aspect = "video",
}: {
  slides: { id: string; label: string; caption?: string; content: ReactNode }[];
  ariaLabel: string;
  className?: string;
  aspect?: "video" | "wide" | "auto";
}) {
  const t = useT();
  const [index, setIndex] = useState(0);
  const trackRef = useRef<HTMLDivElement | null>(null);
  const drag = useRef<{ startX: number; startScroll: number; active: boolean }>({
    startX: 0,
    startScroll: 0,
    active: false,
  });
  const count = slides.length;

  const goTo = useCallback(
    (next: number) => {
      const clamped = Math.max(0, Math.min(count - 1, next));
      setIndex(clamped);
      const track = trackRef.current;
      if (track) track.scrollTo({ left: clamped * track.clientWidth, behavior: "smooth" });
    },
    [count],
  );

  // Keep the track in step when the viewport changes.
  useEffect(() => {
    const track = trackRef.current;
    if (!track) return;
    const onResize = () => track.scrollTo({ left: index * track.clientWidth });
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, [index]);

  const onScroll = () => {
    const track = trackRef.current;
    if (!track || track.clientWidth === 0) return;
    const next = Math.round(track.scrollLeft / track.clientWidth);
    if (next !== index) setIndex(Math.max(0, Math.min(count - 1, next)));
  };

  const aspectClass = aspect === "video" ? "aspect-[16/10]" : aspect === "wide" ? "aspect-[21/9]" : "";

  return (
    <div className={cn("relative", className)} role="group" aria-label={ariaLabel} aria-roledescription="carousel">
      <div
        ref={trackRef}
        onScroll={onScroll}
        onKeyDown={(event) => {
          if (event.key === "ArrowRight") {
            event.preventDefault();
            goTo(index + 1);
          }
          if (event.key === "ArrowLeft") {
            event.preventDefault();
            goTo(index - 1);
          }
        }}
        onPointerDown={(event) => {
          drag.current = { startX: event.clientX, startScroll: event.currentTarget.scrollLeft, active: true };
        }}
        onPointerMove={(event) => {
          if (!drag.current.active) return;
          const delta = event.clientX - drag.current.startX;
          event.currentTarget.scrollLeft = drag.current.startScroll - delta;
        }}
        onPointerUp={() => {
          drag.current.active = false;
        }}
        onPointerLeave={() => {
          drag.current.active = false;
        }}
        tabIndex={0}
        className="no-scrollbar flex snap-x snap-mandatory overflow-x-auto overscroll-x-contain rounded-panel focus-visible:outline-none"
        aria-live="polite"
      >
        {slides.map((slide, slideIndex) => (
          <div
            key={slide.id}
            className="w-full shrink-0 snap-center px-0.5"
            role="group"
            aria-roledescription="slide"
            aria-label={`${slideIndex + 1} of ${count}: ${t(slide.label)}`}
            aria-hidden={slideIndex !== index}
          >
            <div className={cn("overflow-hidden rounded-panel", aspectClass)}>{slide.content}</div>
            <div className="mt-3 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
              <p className="text-sm font-medium text-text-primary">{t(slide.label)}</p>
              {slide.caption ? <p className="text-xs text-text-faint font-medium">{t(slide.caption)}</p> : null}
            </div>
          </div>
        ))}
      </div>

      <div className="mt-4 flex items-center justify-between gap-4">
        <div className="flex items-center gap-2">
          <SliderButton label={t("Previous slide")} onClick={() => goTo(index - 1)} disabled={index === 0} direction="prev" />
          <SliderButton
            label={t("Next slide")}
            onClick={() => goTo(index + 1)}
            disabled={index === count - 1}
            direction="next"
          />
        </div>

        <ul className="flex items-center gap-2" role="tablist" aria-label={`${ariaLabel} slides`}>
          {slides.map((slide, slideIndex) => (
            <li key={slide.id}>
              <button
                type="button"
                role="tab"
                aria-selected={slideIndex === index}
                aria-label={t(slide.label)}
                onClick={() => goTo(slideIndex)}
                className={cn(
                  "h-1.5 rounded-full transition-all duration-300",
                  slideIndex === index ? "w-6 bg-brand-400" : "w-1.5 bg-line-strong hover:bg-text-faint",
                )}
              />
            </li>
          ))}
        </ul>

        <p className="font-mono text-2xs tabular-nums text-text-faint font-medium">
          {String(index + 1).padStart(2, "0")} / {String(count).padStart(2, "0")}
        </p>
      </div>
    </div>
  );
}

function SliderButton({
  label,
  onClick,
  disabled,
  direction,
}: {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  direction: "prev" | "next";
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className="grid h-11 w-11 place-items-center rounded-xl border border-line-strong/70 bg-ink-900/70 text-text-secondary transition-all duration-200 hover:border-brand-400/50 hover:text-text-primary disabled:opacity-35 disabled:hover:border-line-strong/70 disabled:hover:text-text-secondary"
    >
      <span className="sr-only">{label}</span>
      <svg
        aria-hidden="true"
        viewBox="0 0 16 16"
        className="h-3.5 w-3.5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        {direction === "prev" ? <path d="M10 3 5 8l5 5" /> : <path d="M6 3l5 5-5 5" />}
      </svg>
    </button>
  );
}
