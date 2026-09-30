"use client";

import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { LOCALES, type Locale } from "@/i18n/locales";
import { LOCALE_COOKIE, pathWithoutLocale } from "@/i18n/core";
import { useI18n } from "@/i18n/provider";
import { coverageOf } from "@/i18n/coverage";

/**
 * The language menu.
 *
 * Three decisions are deliberate here.
 *
 * **The list shows each language in its own script.** A visitor who reads only
 * Korean is looking for 한국어, not for the word "Korean" — a menu that lists
 * languages in the language you do not read is a menu for the wrong reader.
 * The English name is the second line, for someone who does read it.
 *
 * **Switching keeps the page.** The menu rewrites the language segment of the
 * current path (`/de-DE/pricing` → `/ja-JP/pricing`) rather than sending
 * everyone to the home page, because a visitor who is reading the pricing page
 * and changes language wants the pricing page.
 *
 * **The choice is remembered.** The cookie is what the proxy reads on the next
 * visit, so the language someone picked survives going away and coming back —
 * and it outranks the country guess, because a person's choice is better
 * evidence than their IP address.
 *
 * **The menu says how complete each language is, and where the rest comes
 * from.** Every language that is not finished shows its real share, computed
 * from the dictionary in this build, and the menu states the alternative the
 * browser already offers: translate the remainder in the browser. A menu that
 * shows fifteen languages and lets the reader assume they are all finished is
 * lying by omission; a menu that shows twelve at 14 % invites the reader to use
 * the better tool for the remainder, which is the browser.
 *
 * **The menu is marked `translate="no"`.** A language list is a list of names —
 * "Bahasa Indonesia", "العربية", "日本語" — and a machine translator rendering
 * those into the page's current language would destroy the only thing the list
 * is for. Chrome, Edge and Safari all honour the attribute (and the
 * `notranslate` class, which is set alongside it).
 */
/**
 * Remember the choice.
 *
 * Defined outside the component so the assignment is to a value the component
 * did not close over — and because writing a cookie is not a rendering
 * concern, it does not belong in the middle of one.
 */
function remember(locale: Locale) {
  document.cookie = `${LOCALE_COOKIE}=${locale}; path=/; max-age=${60 * 60 * 24 * 365}; samesite=lax`;
}

/** The address of the current page in another language. */
function addressFor(locale: Locale, pathname: string, search: string): string {
  const bare = pathWithoutLocale(pathname);
  if (locale === "en-US") return `${bare}${search}`;
  return `${bare === "/" ? `/${locale}` : `/${locale}${bare}`}${search}`;
}

/**
 * Go to the chosen language — as a document navigation, not a soft one.
 *
 * This is deliberate, and it is the fix for two bugs that shared one cause.
 *
 * The root layout renders `<html>` with the theme attributes at their defaults
 * (`data-theme="midnight"`, `data-theme-mode="system"`, `data-appearance="studio"`);
 * the inline boot script overwrites them before first paint with what the
 * visitor actually chose. That works on a load, because the script runs on
 * every load. It does *not* work on a client-side navigation: changing language
 * is the only navigation that re-renders the root layout — its `locale` param
 * changed — so the payload's `<html>` attributes are applied over the script's
 * values, and a visitor who had chosen Light is dropped into the dark default
 * by the act of choosing a language. A document navigation re-runs the boot
 * script, so the palette is correct in the first frame.
 *
 * The second symptom was that the switch did not look like it worked: a soft
 * navigation changes `lang` and `dir` on the document already on screen, leaves
 * a mobile sheet mounted, and may answer from the router cache. A load cannot
 * do any of that — the server sends the new language's `<html lang>`, `dir`,
 * translated metadata and dictionaries in one response.
 *
 * The cost is one page load for an action a visitor takes rarely, and the
 * cookie written above is what the proxy reads on that load, so the choice is
 * honoured on the way in.
 */
function goTo(address: string) {
  window.location.assign(address);
}

function GlobeIcon({ className }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 16 16"
      className={className}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <circle cx="8" cy="8" r="6" />
      <path d="M2 8h12M8 2c1.7 1.7 2.6 3.8 2.6 6S9.7 12.3 8 14c-1.7-1.7-2.6-3.8-2.6-6S6.3 3.7 8 2z" />
    </svg>
  );
}

export function LanguageMenu({ compact = false }: { compact?: boolean }) {
  const { locale, t } = useI18n();
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const wrap = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (event: MouseEvent) => {
      if (!wrap.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const current = LOCALES.find((entry) => entry.code === locale) ?? LOCALES[0];

  const choose = (next: Locale) => {
    remember(next);
    setOpen(false);
    /* Choosing the language already being read: remember it and close, but do
       not reload — a menu that reloads the page you are already on reads as a
       broken menu. */
    if (next === locale) return;
    goTo(addressFor(next, pathname, window.location.search));
  };

  return (
    <div className="notranslate relative" ref={wrap} translate="no" data-notranslate="">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-label={t("Change language")}
        className={cn(
          "relative flex items-center gap-1.5 rounded-card border-2 border-transparent px-2 py-1.5 text-sm font-semibold text-text-muted transition-colors duration-200 hover:border-line-strong/80 hover:bg-ink-800/70 hover:text-text-primary",
          open && "border-brand-400/60 bg-brand-500/14 text-text-primary",
        )}
      >
        <GlobeIcon className="h-4 w-4 shrink-0 opacity-80" />
        <span className={cn("truncate", compact ? "max-w-[5.5rem]" : "max-w-[7rem]")}>{current?.native}</span>
        <svg
          aria-hidden="true"
          viewBox="0 0 12 12"
          className={cn("h-2.5 w-2.5 transition-transform duration-200", open && "rotate-180")}
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinecap="round"
        >
          <path d="M3 4.5 6 7.5 9 4.5" />
        </svg>
      </button>

      {open ? (
        <div
          role="listbox"
          aria-label={t("Language")}
          className={cn(
            "absolute z-50 mt-2 max-h-[70vh] w-[19rem] overflow-y-auto rounded-panel border-2 border-line-strong/60 bg-ink-900/98 p-2 shadow-2xl backdrop-blur-sm",
            compact ? "end-0" : "end-0",
          )}
        >
          <p className="px-2 pb-2 pt-1 text-2xs font-semibold uppercase tracking-[0.14em] text-text-faint">
            {t("{count} languages", { count: LOCALES.length })}
          </p>
          {LOCALES.map((entry) => {
            const active = entry.code === locale;
            return (
              <button
                key={entry.code}
                type="button"
                role="option"
                aria-selected={active}
                onClick={() => choose(entry.code)}
                dir={entry.direction}
                className={cn(
                  "flex w-full items-center justify-between gap-3 rounded-card px-2.5 py-2 text-start transition-colors duration-150",
                  active ? "bg-brand-500/18 text-text-primary" : "text-text-secondary hover:bg-ink-800/80 hover:text-text-primary",
                )}
              >
                <span className="flex flex-col">
                  <span className="text-sm font-semibold leading-tight">{entry.native}</span>
                  <span className="text-2xs font-medium leading-tight text-text-faint" dir="ltr">
                    {entry.english}
                  </span>
                </span>
                <span
                  dir="ltr"
                  className={cn(
                    "ms-auto shrink-0 rounded-full border px-1.5 py-0.5 font-mono text-3xs tabular-nums",
                    entry.code === "en-US" || coverageOf(entry.code).complete
                      ? "border-money-positive/35 text-money-positive"
                      : "border-line-strong/60 text-text-faint",
                  )}
                >
                  {entry.code === "en-US" ? "source" : `${coverageOf(entry.code).percent}%`}
                </span>
                {active ? (
                  <svg
                    aria-hidden="true"
                    viewBox="0 0 16 16"
                    className="h-3.5 w-3.5 shrink-0 text-brand-300"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M3 8.5 6.5 12 13 4.5" />
                  </svg>
                ) : null}
              </button>
            );
          })}
          <p className="mt-2 border-t border-line/60 px-2 pt-2 text-2xs leading-relaxed text-text-faint">
            {t("{count} languages, chosen from the menu above and remembered on this device.", { count: LOCALES.length })}
          </p>
          <p className="px-2 pb-1 pt-1.5 text-2xs leading-relaxed text-text-faint">
            {t("The percentage is the share of this site's sentences that ship in that language. For the rest, use your browser's own translation — the numbers and part numbers are marked so it leaves them alone.")}
          </p>
        </div>
      ) : null}
    </div>
  );
}

/** The same menu, sized for the mobile sheet rather than the top bar. */
export function LanguageMenuBlock() {
  const { locale, t } = useI18n();
  const pathname = usePathname();

  const choose = (next: Locale) => {
    remember(next);
    if (next === locale) return;
    goTo(addressFor(next, pathname, window.location.search));
  };

  return (
    <div
      className="notranslate rounded-card border-2 border-line-strong/50 bg-ink-900/60 p-2"
      translate="no"
      data-notranslate=""
    >
      <p className="flex items-center gap-2 px-1 pb-2 text-2xs font-semibold uppercase tracking-[0.14em] text-text-faint">
        <GlobeIcon className="h-3.5 w-3.5" />
        {t("Language")}
      </p>
      <div dir="ltr" className="grid grid-cols-2 gap-1.5">
        {LOCALES.map((entry) => {
          const active = entry.code === locale;
          return (
            <button
              key={entry.code}
              type="button"
              onClick={() => choose(entry.code)}
              aria-pressed={active}
              className={cn(
                "rounded-card border-2 px-2 py-1.5 text-start text-xs font-semibold transition-colors duration-150",
                active
                  ? "border-brand-400/60 bg-brand-500/16 text-text-primary"
                  : "border-transparent text-text-muted hover:border-line-strong/70 hover:text-text-primary",
              )}
            >
              {entry.native}
            </button>
          );
        })}
      </div>
    </div>
  );
}

export default LanguageMenu;
