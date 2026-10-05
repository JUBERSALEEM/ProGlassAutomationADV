/**
 * The translator, as a server component sees it.
 *
 * Every page under `src/app/[locale]/` receives `params.locale`, and this is
 * where that string becomes a language: an unknown tag is a 404, never a
 * silent fallback to English, because a URL that says `/xx/` and renders
 * English is a lie about what is there.
 *
 * Metadata needs the same translator as the page body — the title, the
 * description and the Open Graph copy are all translated — so `translatorFor`
 * is called twice per route (once for `generateMetadata`, once for the page)
 * and returns the same object both times.
 */

import { notFound } from "next/navigation";
import type { Locale } from "./locales";
import { DEFAULT_LOCALE, isLocale } from "./locales";
import { makeTranslator, type Overlay, type Translator } from "./core";
import { overlayFor } from "./dict";
import { APP_STRINGS } from "./app-strings";

/** Route params, in the shape Next actually hands them over: a promise. */
export type PageParams = { locale?: string } | Promise<{ locale?: string }>;

/** The language of a route, or a 404. */
export async function localeOf(params: PageParams | undefined): Promise<Locale> {
  const tag = (await params)?.locale;
  if (!tag) return DEFAULT_LOCALE;
  if (!isLocale(tag)) notFound();
  return tag;
}

/**
 * The overlay a language actually uses.
 *
 * Two sources, one object: the application's own keyed vocabulary first (so
 * `t("Dg.Base")` answers with the software's own words), then the site's
 * dictionary of sentences on top. They cannot collide — one is keyed by XAML
 * key names, the other by English sentences — and if they ever did, the site's
 * own copy is the one written for a page and should win.
 */
export function overlayWithApp(locale: Locale): Overlay {
  return { ...APP_STRINGS[locale], ...overlayFor(locale) };
}

export async function translatorFor(params: PageParams | undefined): Promise<Translator> {
  const locale = await localeOf(params);
  return makeTranslator(locale, overlayWithApp(locale));
}

/** The same, when the locale is already known (layouts, error pages). */
export function translatorForLocale(locale: Locale): Translator {
  return makeTranslator(locale, overlayWithApp(locale));
}
