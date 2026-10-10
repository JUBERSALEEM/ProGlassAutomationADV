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
import routeOverlays from "./route-overlays.json";

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

/**
 * The client-side overlays, per route.
 *
 * The layout and every page used to ship the whole locale dictionary inside
 * the page's flight payload, so a German visitor to /legal/privacy carried
 * all 3,600 German sentences (+549 KB of HTML) to translate the dozen things
 * a client component on that page can actually render. `route-overlays.json`
 * — built by `scripts/build-route-overlays.mjs` (see its header for the
 * measurement, the mining rules, and the anchor check that fails a build
 * whose route set narrows) — splits the dictionary by what each route's
 * client-reachable code imports.
 *
 * `__chrome` is the layout's own set (navbar, footer, studio trigger, support
 * bubble, build stamp); every other route's set is already unioned with the
 * chrome set at build time, so a page island that renders a chrome string
 * resolves it either way. English ships nothing: the key IS the English
 * sentence, and the translator's fallback answers it.
 *
 * Server-side translation is untouched — `overlayWithApp` above still reads
 * the full dictionary, because a server component pays for it once in
 * memory, not once per flight payload.
 */
type RouteOverlayTable = Record<string, Record<string, Overlay>>;

export function chromeOverlayFor(locale: Locale): Overlay {
  if (locale === DEFAULT_LOCALE) return {};
  const table = (routeOverlays as RouteOverlayTable)[locale];
  if (!table) return {};
  /* The 404 and error boundaries render under THIS provider — they are
     client components mounted by Next without route params, so their
     sentences belong to chrome whatever address produced them. */
  return { ...(table.__notfound ?? {}), ...(table.__error ?? {}), ...(table.__chrome ?? {}) };
}

export function pageOverlayFor(route: string, locale: Locale): Overlay {
  if (locale === DEFAULT_LOCALE) return {};
  const table = (routeOverlays as RouteOverlayTable)[locale];
  if (!table) return {};
  /* A route the build had no subsets for falls back to nothing client-side;
     the translator's fallback per key — the English sentence — keeps every
     label readable (exactly what an untranslated key always did). */
  return table[route] ?? table.__chrome ?? {};
}

export async function translatorFor(params: PageParams | undefined): Promise<Translator> {
  const locale = await localeOf(params);
  return makeTranslator(locale, overlayWithApp(locale));
}

/** The same, when the locale is already known (layouts, error pages). */
export function translatorForLocale(locale: Locale): Translator {
  return makeTranslator(locale, overlayWithApp(locale));
}
