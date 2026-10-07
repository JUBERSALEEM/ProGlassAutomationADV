"use client";

import { cn } from "@/lib/cn";
import { themeModes } from "@/data/themes";
import { setThemeMode, useResolvedTheme, useThemeMode } from "@/lib/useTheme";
import { useT } from "@/i18n/provider";

/**
 * Light · System · Dark — the reading, on its own control.
 *
 * This is the platform convention rather than an invention: iOS, macOS and
 * Windows all present appearance as three states in one switch (Automatic,
 * Light, Dark), because "follow the system" is a *third answer* to the question
 * "which reading do you want", not a fourth setting beside it. So the switch is
 * three segments, and the middle one is the system.
 *
 * It used to be the left half of the theme control, where one click cycled
 * Light → Dark → System and the only way to see what the current state was, was
 * to work it out from an icon. Two things were wrong with that. A visitor who
 * wants the page light had to pass through two other states to get there, and
 * the cycle shares a control with the palette — so a person reaching for
 * "darker" could land on "Monet" instead. The two questions now have two
 * controls: this one answers *how light*, the palette control beside it answers
 * *in what colours*.
 *
 * The thumb slides on `transform` only — it is one 3% -wide element moving on
 * the compositor, not a gradient being repainted — and it does not animate at
 * all for a visitor who has asked their system for reduced motion.
 */
export function ThemeModeSwitch({ className }: { className?: string }) {
  const t = useT();
  const mode = useThemeMode();
  const resolved = useResolvedTheme();

  const activeIndex = Math.max(
    0,
    themeModes.findIndex((entry) => entry.id === mode),
  );

  return (
    <div
      role="group"
      aria-label={t("Reading")}
      className={cn(
        "relative flex h-10 items-center rounded-xl border border-line-strong/70 bg-ink-800/70 p-0.5 sm:h-9",
        className,
      )}
    >
      {/* The sliding thumb. One element, moved, never re-drawn. */}
      <span
        aria-hidden="true"
        className="pointer-events-none absolute inset-y-0.5 start-0.5 w-[calc((100%-0.25rem)/3)] rounded-[0.5rem] bg-brand-600/40 shadow-[inset_0_1px_0_0_rgb(255_255_255/0.14)] transition-transform duration-200 ease-reveal motion-reduce:transition-none"
        style={{ transform: `translateX(${activeIndex * 100}%)` }}
      />
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
              "relative z-10 flex h-full w-10 items-center justify-center gap-1.5 rounded-[0.5rem] text-[0.75rem] font-medium transition-colors duration-200 lg:w-8 xl:w-9",
              active ? "text-text-primary" : "text-text-faint hover:text-text-secondary",
            )}
          >
            <ModeIcon name={entry.icon} />
            <span className="sr-only">
              {entry.id === "system" && mode === "system"
                ? t.template`${t(entry.label)} — following the system, now ${t(resolved === "daylight" ? "light" : "dark")}`
                : t(entry.label)}
            </span>
          </button>
        );
      })}
    </div>
  );
}

/** The three state glyphs, at the weight the rest of the panel's icons use. */
function ModeIcon({ name }: { name: "sun" | "moon" | "system" }) {
  const common = {
    "aria-hidden": true,
    viewBox: "0 0 16 16",
    className: "h-4 w-4",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 1.5,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
  };

  if (name === "sun") {
    return (
      <svg {...common}>
        <circle cx="8" cy="8" r="3" />
        <path d="M8 1.5v1.4M8 13.1v1.4M1.5 8h1.4M13.1 8h1.4M3.4 3.4l1 1M11.6 11.6l1 1M12.6 3.4l-1 1M4.4 11.6l-1 1" />
      </svg>
    );
  }

  if (name === "moon") {
    return (
      <svg {...common}>
        <path d="M13 9.6A5.4 5.4 0 0 1 6.4 3a5.5 5.5 0 1 0 6.6 6.6Z" />
      </svg>
    );
  }

  /*
   * System: a display with a half-filled field. The mark says "this follows
   * something else" without a second label, which is what the state means.
   */
  return (
    <svg {...common}>
      <rect x="1.75" y="3" width="12.5" height="8.5" rx="1.4" />
      <path d="M8 3v8.5" />
      <path d="M6 14h4M8 11.5V14" />
    </svg>
  );
}
