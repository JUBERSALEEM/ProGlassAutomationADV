"use client";

/**
 * The translator, as React sees it.
 *
 * A client component cannot read a file, so the dictionary it needs arrives as
 * a prop from the server component that rendered it: the locale layout builds
 * the overlay for the language in the URL once, and every client component
 * below it reads that same object.
 *
 * Only one language's dictionary is ever serialised into a page. A visitor
 * reading Japanese downloads Japanese and nothing else.
 */

import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { Locale } from "./locales";
import { DEFAULT_LOCALE, dirOf, localeInfo } from "./locales";
import { hrefIn, makeTranslator, type Overlay, type Translator } from "./core";

export type I18nValue = {
  locale: Locale;
  /** `ltr` or `rtl` — set on `<html>` by the server, mirrored here for widgets. */
  dir: "ltr" | "rtl";
  /** The language's own name, for the menu's current-selection label. */
  native: string;
  t: Translator;
  /** Build a link to a page in the current language. */
  href: (path: string) => string;
};

const I18nContext = createContext<I18nValue | null>(null);

export function I18nProvider({
  locale,
  overlay,
  children,
}: {
  locale: Locale;
  overlay: Overlay;
  children: ReactNode;
}) {
  const value = useMemo<I18nValue>(
    () => ({
      locale,
      dir: dirOf(locale),
      native: localeInfo(locale).native,
      t: makeTranslator(locale, overlay),
      href: (path: string) => hrefIn(locale, path),
    }),
    [locale, overlay],
  );
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nValue {
  const value = useContext(I18nContext);
  if (value) return value;
  /* A component rendered outside the provider is a wiring mistake, and it
     should say so loudly in development rather than print raw keys in
     production. The fallback keeps the page readable either way: English, and
     a link that does not change language. */
  return {
    locale: DEFAULT_LOCALE,
    dir: "ltr",
    native: localeInfo(DEFAULT_LOCALE).native,
    t: makeTranslator(DEFAULT_LOCALE),
    href: (path: string) => hrefIn(DEFAULT_LOCALE, path),
  };
}

/** The translator alone — the common case. */
export function useT(): Translator {
  return useI18n().t;
}

export function useLocale(): Locale {
  return useI18n().locale;
}

/** `const href = useHref()` — links that keep the reader's language. */
export function useHref(): (path: string) => string {
  return useI18n().href;
}
