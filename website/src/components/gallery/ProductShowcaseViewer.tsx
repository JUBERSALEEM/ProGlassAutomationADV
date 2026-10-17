"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { ScreenshotFrame } from "@/components/gallery/ScreenshotFrame";
import {
  capabilityGroups,
  screenshots,
  type CapabilityGroupId,
  type Screenshot,
} from "@/data/screenshots";
import { useT } from "@/i18n/provider";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";

function GroupIcon({ name, className }: { name: string; className?: string }) {
  switch (name) {
    case "calculator":
      return (
        <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className={className}>
          <rect x="3" y="2" width="14" height="16" rx="2" strokeLinecap="round" strokeLinejoin="round" />
          <line x1="6" y1="6" x2="14" y2="6" strokeLinecap="round" />
          <circle cx="7" cy="10" r="0.8" fill="currentColor" />
          <circle cx="10" cy="10" r="0.8" fill="currentColor" />
          <circle cx="13" cy="10" r="0.8" fill="currentColor" />
          <circle cx="7" cy="14" r="0.8" fill="currentColor" />
          <circle cx="10" cy="14" r="0.8" fill="currentColor" />
          <circle cx="13" cy="14" r="0.8" fill="currentColor" />
        </svg>
      );
    case "layers":
      return (
        <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className={className}>
          <path d="M10 2 2.5 6 10 10l7.5-4L10 2z" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M2.5 10 10 14l7.5-4" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M2.5 14 10 18l7.5-4" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      );
    case "scissors":
      return (
        <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className={className}>
          <circle cx="5" cy="6" r="2.5" />
          <circle cx="5" cy="14" r="2.5" />
          <path d="M7 7.5 16 16M7 12.5 16 4" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      );
    case "factory":
      return (
        <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className={className}>
          <path d="M3 18V8l5 4V8l5 4V4h4v14H3z" strokeLinecap="round" strokeLinejoin="round" />
          <line x1="7" y1="18" x2="7" y2="15" strokeLinecap="round" />
          <line x1="11" y1="18" x2="11" y2="15" strokeLinecap="round" />
        </svg>
      );
    case "warehouse":
      return (
        <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className={className}>
          <path d="M3 7.5 10 3l7 4.5V17H3V7.5z" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M7 17v-6h6v6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      );
    case "chart":
      return (
        <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className={className}>
          <line x1="3" y1="17" x2="17" y2="17" strokeLinecap="round" />
          <rect x="5" y="10" width="2.5" height="7" rx="0.5" strokeLinecap="round" />
          <rect x="9.5" y="6" width="2.5" height="11" rx="0.5" strokeLinecap="round" />
          <rect x="14" y="3" width="2.5" height="14" rx="0.5" strokeLinecap="round" />
        </svg>
      );
    default:
      return null;
  }
}

export function ProductShowcaseViewer() {
  const t = useT();
  const [activeGroupId, setActiveGroupId] = useState<CapabilityGroupId>("estimation-commercial");
  const [slideIndex, setSlideIndex] = useState(0);

  const containerRef = useRef<HTMLDivElement | null>(null);
  const touchStart = useRef<{ x: number; y: number } | null>(null);

  // Map screenshot IDs by group
  const activeGroup = useMemo(
    () => capabilityGroups.find((g) => g.id === activeGroupId) ?? capabilityGroups[0]!,
    [activeGroupId],
  );

  const groupShots: Screenshot[] = useMemo(() => {
    const list: Screenshot[] = [];
    for (const id of activeGroup.screenshotIds) {
      const match = screenshots.find((s) => s.id === id);
      if (match) list.push(match);
    }
    return list.length > 0 ? list : [screenshots[0]!];
  }, [activeGroup]);

  // Ensure safe slide index
  const safeIndex = Math.min(Math.max(slideIndex, 0), groupShots.length - 1);
  const activeShot = groupShots[safeIndex] ?? groupShots[0]!;

  const switchGroup = useCallback((id: CapabilityGroupId) => {
    setActiveGroupId(id);
    setSlideIndex(0);
  }, []);

  const stepSlide = useCallback(
    (delta: number) => {
      setSlideIndex((curr) => {
        const next = curr + delta;
        if (next < 0) return groupShots.length - 1;
        if (next >= groupShots.length) return 0;
        return next;
      });
    },
    [groupShots.length],
  );

  // Keyboard navigation when user is focused on the page or viewer
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      // Don't intercept if user is typing in an input or textarea
      if (
        document.activeElement?.tagName === "INPUT" ||
        document.activeElement?.tagName === "TEXTAREA"
      ) {
        return;
      }

      if (e.key === "ArrowLeft") {
        e.preventDefault();
        stepSlide(-1);
      } else if (e.key === "ArrowRight") {
        e.preventDefault();
        stepSlide(1);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [stepSlide]);

  return (
    <div
      ref={containerRef}
      className="space-y-8"
      role="region"
      aria-roledescription="carousel"
      aria-label={t("ProGlass Automation Interface Showcase")}
    >
      {/* ── 1. Capability Group Selector Tabs ────────────────────── */}
      <div className="flex flex-col gap-3">
        <p className="eyebrow text-text-faint">
          {t("Product Capability Suites")}
        </p>

        <div
          role="tablist"
          aria-label={t("Select capability suite")}
          className="dense-card-grid dense-fixed-grid grid grid-cols-3 gap-2 sm:grid-cols-6"
        >
          {capabilityGroups.map((group) => {
            const isSelected = group.id === activeGroupId;
            const count = group.screenshotIds.length;

            return (
              <button
                key={group.id}
                role="tab"
                id={`tab-${group.id}`}
                aria-selected={isSelected}
                aria-controls={`panel-${group.id}`}
                tabIndex={isSelected ? 0 : -1}
                onClick={() => switchGroup(group.id)}
                className={cn(
                  "group relative flex flex-col items-start rounded-xl border p-3.5 text-left transition-all duration-200",
                  isSelected
                    ? "border-brand-400/80 bg-ink-900/95 text-text-primary shadow-glow ring-1 ring-brand-400/40"
                    : "border-line-strong/60 bg-ink-950/70 text-text-muted hover:border-line-strong/90 hover:bg-ink-900/50 hover:text-text-primary",
                )}
              >
                <div className="flex w-full items-center justify-between">
                  <span
                    className={cn(
                      "grid h-8 w-8 place-items-center rounded-lg border transition-colors",
                      isSelected
                        ? "border-brand-400/50 bg-brand-500/20 text-brand-200"
                        : "border-line/70 bg-ink-900 text-text-faint group-hover:text-text-secondary",
                    )}
                  >
                    <GroupIcon name={group.icon} className="h-4 w-4" />
                  </span>
                  <span className="font-mono text-3xs font-medium tabular-nums text-text-faint">
                    {count} {count === 1 ? t("screen") : t("screens")}
                  </span>
                </div>

                <div className="mt-3">
                  <p
                    className={cn(
                      "text-xs font-semibold leading-tight",
                      isSelected ? "text-brand-100" : "text-text-secondary",
                    )}
                  >
                    {t(group.label)}
                  </p>
                  <p className="mt-1 line-clamp-1 text-3xs text-text-faint">
                    {t(group.shortLabel)}
                  </p>
                </div>

                {isSelected && (
                  <span
                    aria-hidden="true"
                    className="absolute -bottom-px left-3 right-3 h-0.5 bg-brand-400"
                  />
                )}
              </button>
            );
          })}
        </div>
      </div>

      {/* ── 2. Primary Showcase Viewer Plate ────────────────────── */}
      <div
        id={`panel-${activeGroupId}`}
        role="tabpanel"
        aria-labelledby={`tab-${activeGroupId}`}
        className="rounded-2xl border border-line-strong/80 bg-ink-950 p-3 sm:p-5 shadow-raised"
      >
        {/* Active Group Header & Slide Controls */}
        <div className="mb-4 flex flex-col gap-3 border-b border-line/60 pb-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <span className="eyebrow text-brand-300">
                {t(activeGroup.label)}
              </span>
              <span className="text-text-faint">/</span>
              <span className="font-mono text-2xs uppercase tracking-wider text-text-muted">
                {t.template`Screen ${safeIndex + 1} of ${groupShots.length}`}
              </span>
            </div>
            <h3 className="mt-1 text-base font-bold text-text-primary sm:text-lg">
              {t(activeShot.title)}
            </h3>
            <p className="mt-0.5 text-xs text-text-muted">
              {t(activeGroup.tagline)}
            </p>
          </div>

          {/* Slide Navigation Buttons & Indicators */}
          <div className="flex flex-wrap items-center gap-2">
            {/* Slide Indicator Pills */}
            <div
              className="hidden sm:flex items-center gap-1 rounded-lg border border-line/60 bg-ink-900/60 p-1"
              role="group"
              aria-label={t("Jump to slide")}
            >
              {groupShots.map((shot, idx) => (
                <button
                  key={shot.id}
                  type="button"
                  onClick={() => setSlideIndex(idx)}
                  aria-label={t.template`View screen ${idx + 1}: ${t(shot.title)}`}
                  className={cn(
                    "flex h-7 items-center gap-1.5 rounded-md px-2 text-2xs font-medium transition-all",
                    idx === safeIndex
                      ? "bg-brand-500/25 text-brand-100 font-semibold shadow-sm"
                      : "text-text-faint hover:text-text-secondary",
                  )}
                >
                  <span
                    className={cn(
                      "h-1.5 w-1.5 rounded-full",
                      idx === safeIndex ? "bg-brand-400" : "bg-line-strong",
                    )}
                  />
                  <span className="max-w-[7.5rem] truncate">{t(shot.title)}</span>
                </button>
              ))}
            </div>

            {/* Previous / Next Arrows */}
            <div className="flex items-center gap-1.5">
              <button
                type="button"
                onClick={() => stepSlide(-1)}
                aria-label={t("Previous screen (Left Arrow)")}
                title={t("Previous screen (Left Arrow)")}
                className="grid h-9 w-9 place-items-center rounded-xl border border-line-strong/80 bg-ink-900 text-text-secondary transition-all hover:border-brand-400/60 hover:bg-ink-850 hover:text-text-primary active:scale-95"
              >
                <svg
                  aria-hidden="true"
                  viewBox="0 0 16 16"
                  className="h-4 w-4"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M10 3 5 8l5 5" />
                </svg>
              </button>
              <button
                type="button"
                onClick={() => stepSlide(1)}
                aria-label={t("Next screen (Right Arrow)")}
                title={t("Next screen (Right Arrow)")}
                className="grid h-9 w-9 place-items-center rounded-xl border border-line-strong/80 bg-ink-900 text-text-secondary transition-all hover:border-brand-400/60 hover:bg-ink-850 hover:text-text-primary active:scale-95"
              >
                <svg
                  aria-hidden="true"
                  viewBox="0 0 16 16"
                  className="h-4 w-4"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M6 3l5 5-5 5" />
                </svg>
              </button>
            </div>
          </div>
        </div>

        {/* The Primary Active Preview Canvas */}
        <div
          className="relative min-w-0 overflow-hidden rounded-xl bg-ink-900/40 p-1 sm:p-2.5 transition-all duration-300"
          onPointerDown={(e) => {
            touchStart.current = { x: e.clientX, y: e.clientY };
          }}
          onPointerUp={(e) => {
            const start = touchStart.current;
            touchStart.current = null;
            if (!start) return;
            const dx = e.clientX - start.x;
            const dy = e.clientY - start.y;
            if (Math.abs(dx) > 50 && Math.abs(dx) > Math.abs(dy)) {
              stepSlide(dx < 0 ? 1 : -1);
            }
          }}
        >
          {/* Active Primary Preview */}
          <div
            key={activeShot.id}
            role="group"
            aria-roledescription="slide"
            aria-label={t(activeShot.title)}
            className="animate-fade-in w-full transition-opacity duration-200"
          >
            <ScreenshotFrame t={t} shot={activeShot} modal priority />
          </div>
        </div>

        {/* Keyboard and Swipe Hint Bar */}
        <div className="mt-3 flex items-center justify-between border-t border-line/40 pt-2.5 text-3xs font-mono text-text-faint">
          <div className="flex items-center gap-2">
            <span className="rounded border border-line/60 bg-ink-900 px-1.5 py-0.5">←</span>
            <span className="rounded border border-line/60 bg-ink-900 px-1.5 py-0.5">→</span>
            <span>{t("Use arrow keys or swipe horizontally to navigate screens")}</span>
          </div>
          <span className="hidden sm:inline text-text-muted">
            {t(activeShot.evidence)}
          </span>
        </div>

        {/* ── 3. Customer-Facing Capability Details ───────────────── */}
        <div className="mt-6 grid gap-6 border-t border-line/60 pt-6 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
          {/* Left: What this screen delivers */}
          <div className="space-y-4">
            <div>
              <p className="eyebrow text-brand-300">
                {t("Operational Capability")}
              </p>
              <h4 className="mt-1 text-sm font-semibold text-text-primary">
                {t(activeShot.title)}
              </h4>
              <p className="mt-2 text-xs leading-relaxed text-text-secondary sm:text-sm">
                {t(activeShot.caption)}
              </p>
            </div>

            <div>
              <p className="font-mono text-2xs uppercase tracking-wider text-text-faint font-semibold">
                {t("Key Fabrication Features")}
              </p>
              <ul className="mt-2.5 grid gap-2 sm:grid-cols-2">
                {activeShot.highlights.map((item) => (
                  <li key={item} className="flex items-start gap-2 text-xs leading-relaxed text-text-muted">
                    <span
                      aria-hidden="true"
                      className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-brand-400"
                    />
                    <span>{t(item)}</span>
                  </li>
                ))}
              </ul>
            </div>
          </div>

          {/* Right: Neutral Fabrication Specifications & Standards */}
          <div className="rounded-xl border border-line-strong/60 bg-ink-900/60 p-4 sm:p-5 text-xs space-y-3.5">
            <div>
              <p className="font-mono text-2xs uppercase tracking-wider text-brand-300 font-semibold">
                {t("Fabrication Specifications")}
              </p>
              <p className="mt-1 text-2xs text-text-faint">
                {t("Engineered parameters supported by this module:")}
              </p>
            </div>

            <dl className="space-y-2.5 border-t border-line/40 pt-3 text-2xs">
              <div className="flex justify-between gap-2">
                <dt className="font-mono text-text-faint uppercase tracking-wider">{t("Glass Types")}</dt>
                <dd className="text-right text-text-secondary font-medium">
                  {t("Float · Tempered · DGU · Laminated · Low-E")}
                </dd>
              </div>

              <div className="flex justify-between gap-2">
                <dt className="font-mono text-text-faint uppercase tracking-wider">{t("Sheet Sizes")}</dt>
                <dd className="text-right text-text-secondary font-medium">
                  {t("Standard (3210×2250) · Jumbo (3660×2440)")}
                </dd>
              </div>

              <div className="flex justify-between gap-2">
                <dt className="font-mono text-text-faint uppercase tracking-wider">{t("Billing Logic")}</dt>
                <dd className="text-right text-text-secondary font-medium">
                  {t("0.5 m² Minimum Panel Area · Rate Matrices")}
                </dd>
              </div>

              <div className="flex justify-between gap-2">
                <dt className="font-mono text-text-faint uppercase tracking-wider">{t("Industry Standards")}</dt>
                <dd className="text-right text-text-secondary font-medium">
                  {t("BS EN 12150 · ASTM C1036 · UAE VAT (5%)")}
                </dd>
              </div>

              <div className="flex justify-between gap-2">
                <dt className="font-mono text-text-faint uppercase tracking-wider">{t("Integration")}</dt>
                <dd className="text-right text-text-secondary font-medium">
                  {t(activeShot.evidence)}
                </dd>
              </div>
            </dl>

            <div className="border-t border-line/40 pt-3">
              <ButtonLink
                href="/contact"
                variant="secondary"
                size="sm"
                className="w-full justify-center text-2xs"
                trailing={<ArrowRight />}
              >
                {t("Request Live Walkthrough")}
              </ButtonLink>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
