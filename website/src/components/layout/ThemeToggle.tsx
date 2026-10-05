"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import {
  appearanceFamilies,
  themeModes,
  type AppearanceFamilyEntry,
  type ThemeModeEntry,
} from "@/data/themes";
import { setThemeMode, useAppearance, useThemeGuard, useThemeMode } from "@/lib/useTheme";
import { setFamily } from "@/lib/useStudio";
import { useT } from "@/i18n/provider";

/**
 * Appearance control.
 *
 * One click on the left half cycles the mode: Light → Dark → System → Light.
 * One click on the chevron opens the full picker, where any mode or any of the
 * four palette families can be chosen directly. Two clicks never happen by
 * accident, and nothing is hidden behind a settings page.
 *
 *   Mode    Light · Dark · System
 *   Family  Studio · Monet · Industrial · Crystal
 *
 * `Studio` is this site's own violet palette; the other three follow the
 * application's own appearance families (see `src/data/themes.ts` for the
 * evidence). Both attributes are written to <html> immediately, persisted, and
 * applied before first paint by the boot script, so there is no flash.
 */
export function ThemeToggle({ className }: { className?: string }) {
  const t = useT();
  /* The control lives in the header of every page, so this is where the
     visitor's choice is re-applied after any navigation. See useThemeGuard. */
  useThemeGuard();
  const mode = useThemeMode();
  const family = useAppearance();
  const [open, setOpen] = useState(false);
  const wrapper = useRef<HTMLDivElement | null>(null);

  // Close on outside click or Escape.
  useEffect(() => {
    if (!open) return;
    const onPointer = (event: PointerEvent) => {
      if (!wrapper.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    window.addEventListener("pointerdown", onPointer);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("pointerdown", onPointer);
      window.removeEventListener("keydown", onKey);
    };
  }, [open]);

  // `noUncheckedIndexedAccess` makes element access optional, so both lookups
  // carry an explicit fallback to the first entry / System.
  const activeFamily: AppearanceFamilyEntry =
    appearanceFamilies.find((entry) => entry.id === family) ?? appearanceFamilies[0]!;
  const activeMode: ThemeModeEntry = themeModes.find((entry) => entry.id === mode) ?? themeModes[2]!;
  const modeIndex = Math.max(
    0,
    themeModes.findIndex((entry) => entry.id === mode),
  );
  const nextMode: ThemeModeEntry = themeModes[(modeIndex + 1) % themeModes.length] ?? themeModes[0]!;

  /** One click: Light → Dark → System → Light. */
  const cycleMode = () => setThemeMode(nextMode.id);

  return (
    <div ref={wrapper} className={cn("relative", className)}>
      <div className="flex h-10 items-center overflow-hidden rounded-xl border border-line-strong/70 bg-ink-800/70 sm:h-9">
        <button
          type="button"
          onClick={cycleMode}
          aria-label={t.template`Appearance: ${t(activeMode.label)} mode. Click to switch to ${t(nextMode.label)}.`}
          title={t.template`${t(activeMode.label)} mode — click for ${t(nextMode.label)}`}
          className="flex h-full items-center gap-2 pl-2 pr-1.5 text-text-muted transition-colors duration-200 hover:bg-ink-700/60 hover:text-text-primary"
        >
          <span
            aria-hidden="true"
            className="h-3.5 w-3.5 rounded-full ring-1 ring-inset ring-black/20"
            style={{ background: activeFamily.swatch }}
          />
          <ModeIcon name={activeMode.icon} />
          <span className="sr-only">{t("Switch appearance mode")}</span>
        </button>
        <button
          type="button"
          onClick={() => setOpen((value) => !value)}
          aria-expanded={open}
          aria-haspopup="dialog"
          aria-label={t.template`Appearance settings — ${t(activeMode.label)} mode, ${t(activeFamily.label)} palette`}
          title={`Appearance — ${t(activeMode.label)} · ${t(activeFamily.label)}`}
          className="flex h-full items-center border-l border-line/70 px-1.5 text-text-faint transition-colors duration-200 hover:bg-ink-700/60 hover:text-text-primary"
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 12 12"
            className="h-3 w-3"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M3 4.6 6 7.6l3-3" />
          </svg>
        </button>
      </div>

      {open ? (
        <div
          role="dialog"
          aria-label={t("Appearance")}
          className="menu-card animate-scale-in absolute right-0 top-full z-50 mt-3 w-[17.5rem] origin-top-right rounded-panel p-3 shadow-raised"
        >
          <p className="mono-label px-1">{t("Mode")}</p>
          <div className="mt-2 grid grid-cols-3 gap-1.5">
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
                    "flex flex-col items-center gap-1.5 rounded-card border px-2 py-2.5 text-[0.75rem] font-medium transition-colors duration-200",
                    active
                      ? "border-brand-400/60 bg-brand-400/12 text-text-primary"
                      : "border-line/70 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                  )}
                >
                  <ModeIcon name={entry.icon} />
                  {t(entry.label)}
                </button>
              );
            })}
          </div>

          <p className="mono-label mt-4 px-1">{t("Palette")}</p>
          <div className="mt-2 space-y-1">
            {appearanceFamilies.map((entry) => {
              const active = entry.id === family;
              return (
                <button
                  key={entry.id}
                  type="button"
                  aria-pressed={active}
                  onClick={() => setFamily(entry.id)}
                  className={cn(
                    "flex w-full items-start gap-3 rounded-card border px-2.5 py-2 text-left transition-colors duration-200",
                    active
                      ? "border-brand-400/55 bg-brand-400/10"
                      : "border-transparent hover:border-line-strong/70 hover:bg-ink-800/60",
                  )}
                >
                  <span
                    aria-hidden="true"
                    className="mt-0.5 h-4 w-4 shrink-0 rounded-full ring-1 ring-inset ring-black/20"
                    style={{ background: entry.swatch }}
                  />
                  <span className="min-w-0">
                    <span
                      className={cn(
                        "block text-sm font-medium",
                        active ? "text-text-primary" : "text-text-secondary",
                      )}
                    >
                      {t(entry.label)}
                    </span>
                    <span className="mt-0.5 block text-[0.75rem] leading-snug text-text-faint">{entry.origin}</span>
                  </span>
                </button>
              );
            })}
          </div>

          <p className="mt-3 border-t border-line/60 px-1 pt-3 text-[0.75rem] font-medium leading-relaxed text-text-muted">
            {t("The application ships the same idea in Settings → Appearance. For the full set — sixteen colour plates,")}
            corner radius, money colours — open the{" "}
            <button
              type="button"
              onClick={() => {
                setOpen(false);
                window.dispatchEvent(new Event("proglass-open-studio"));
              }}
              className="font-bold text-brand-300 underline decoration-brand-400/50 underline-offset-2 hover:text-brand-200"
            >
              theme studio
            </button>
            .
          </p>
        </div>
      ) : null}
    </div>
  );
}

function ModeIcon({ name }: { name: "sun" | "moon" | "system" }) {
  const common = {
    "aria-hidden": true,
    viewBox: "0 0 16 16",
    className: "h-3.5 w-3.5",
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

  return (
    <svg {...common}>
      <rect x="2" y="3" width="12" height="8.5" rx="1.4" />
      <path d="M6 14h4M8 11.5V14" />
    </svg>
  );
}
