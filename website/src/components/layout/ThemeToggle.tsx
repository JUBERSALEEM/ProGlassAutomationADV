"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { appearanceFamilies, type AppearanceFamilyEntry } from "@/data/themes";
import { useAppearance, useThemeGuard } from "@/lib/useTheme";
import { setFamily, useStudio } from "@/lib/useStudio";
import { useT } from "@/i18n/provider";

/**
 * Palette control — the *colours* of the site, on its own control.
 *
 * One click opens the four families, each with the swatch it paints and one
 * line of where it comes from. The reading — Light, System, Dark — is a
 * separate switch beside it (`ThemeModeSwitch`): the two questions are
 * independent, and a control that answered both at once was a control where a
 * person reaching for "darker" could land on "Monet" instead.
 *
 * The trigger carries the palette's own swatch, so the current family is
 * legible at a glance without opening anything, and it sits directly above the
 * popover that changes it.
 *
 * `useThemeGuard` is called here because this control is rendered in the header
 * of every page, which makes it the natural place for the one effect that
 * re-applies the visitor's choice after a navigation re-renders `<html>`.
 */
export function ThemeToggle({ className }: { className?: string }) {
  const t = useT();
  /* See useThemeGuard: this control is in the header of every page. */
  useThemeGuard();
  const family = useAppearance();
  const studio = useStudio();
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

  // `noUncheckedIndexedAccess` makes element access optional, so this lookup
  // carries an explicit fallback to the first entry.
  const activeFamily: AppearanceFamilyEntry =
    appearanceFamilies.find((entry) => entry.id === family) ?? appearanceFamilies[0]!;

  return (
    <div ref={wrapper} className={cn("relative", className)}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-label={t.template`Palette — ${t(activeFamily.label)}. Open the palette picker.`}
        title={t.template`Palette — ${t(activeFamily.label)}`}
        className={cn(
          "flex h-10 items-center gap-2 rounded-xl border px-2.5 text-text-muted transition-colors duration-200 sm:h-9",
          open
            ? "border-brand-400/60 bg-brand-400/12 text-text-primary"
            : "border-line-strong/70 bg-ink-800/70 hover:border-brand-400/50 hover:text-text-primary",
        )}
      >
        <span
          aria-hidden="true"
          className="h-3.5 w-3.5 rounded-full ring-1 ring-inset ring-white/25"
          style={{ background: studio.accent ?? activeFamily.swatch }}
        />
        <span className="hidden font-mono text-2xs font-semibold uppercase tracking-[0.12em] wide:inline">
          {t(activeFamily.label)}
        </span>
        <svg
          aria-hidden="true"
          viewBox="0 0 12 12"
          className={cn("h-3 w-3 transition-transform duration-200", open && "rotate-180")}
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M3 4.6 6 7.6l3-3" />
        </svg>
      </button>

      {open ? (
        <div
          role="dialog"
          aria-label={t("Palette")}
          className="menu-card animate-scale-in absolute end-0 top-full z-50 mt-3 w-[19rem] origin-top-right rounded-panel p-3 shadow-raised"
        >
          <p className="mono-label px-1">{t("Palette")}</p>
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
                    className="mt-0.5 h-4 w-4 shrink-0 rounded-full ring-1 ring-inset ring-white/25"
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
                    <span className="mt-0.5 block text-[0.75rem] leading-snug text-text-faint">{t(entry.origin)}</span>
                  </span>
                </button>
              );
            })}
          </div>

          <p className="mt-3 border-t border-line/60 px-1 pt-3 text-[0.75rem] font-medium leading-relaxed text-text-muted">
            {t("The application ships the same four in Settings → Appearance. For the full set — sixteen colour plates, corner radius, money colours — open the")}{" "}
            <button
              type="button"
              onClick={() => {
                setOpen(false);
                window.dispatchEvent(new Event("proglass-open-studio"));
              }}
              className="font-bold text-brand-300 underline decoration-brand-400/50 underline-offset-2 hover:text-brand-200"
            >
              {t("theme studio")}
            </button>
            .
          </p>
        </div>
      ) : null}
    </div>
  );
}
