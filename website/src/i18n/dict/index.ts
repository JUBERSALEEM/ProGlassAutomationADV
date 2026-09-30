/**
 * The dictionary, one overlay per language.
 *
 * These are static imports on purpose. A dynamic `import()` per request would
 * make the server wait on the filesystem before it could say the word "Pricing",
 * and all fifteen together are a few hundred kilobytes of text on a machine
 * that is already holding the whole application's language files in memory.
 *
 * `en-US` is empty and stays empty: the key of an entry is the English
 * sentence, so English needs no values. See `dict/en-US.ts`.
 */

import type { Overlay } from "../core";
import type { Locale } from "../locales";
import { DEFAULT_LOCALE, LOCALE_CODES } from "../locales";

import arAE from "./ar-AE";
import bnBD from "./bn-BD";
import deDE from "./de-DE";
import enUS from "./en-US";
import esES from "./es-ES";
import frFR from "./fr-FR";
import hiIN from "./hi-IN";
import idID from "./id-ID";
import jaJP from "./ja-JP";
import koKR from "./ko-KR";
import ptBR from "./pt-BR";
import ruRU from "./ru-RU";
import trTR from "./tr-TR";
import urPK from "./ur-PK";
import zhCN from "./zh-CN";
import { KEYS } from "./keys";

export const OVERLAYS: Record<Locale, Overlay> = {
  "en-US": enUS,
  "ar-AE": arAE,
  "bn-BD": bnBD,
  "de-DE": deDE,
  "es-ES": esES,
  "fr-FR": frFR,
  "hi-IN": hiIN,
  "id-ID": idID,
  "ja-JP": jaJP,
  "ko-KR": koKR,
  "pt-BR": ptBR,
  "ru-RU": ruRU,
  "tr-TR": trTR,
  "ur-PK": urPK,
  "zh-CN": zhCN,
};

export function overlayFor(locale: Locale): Overlay {
  return OVERLAYS[locale] ?? OVERLAYS[DEFAULT_LOCALE];
}

export type Coverage = {
  locale: Locale;
  translated: number;
  total: number;
  percent: number;
  /** Sentences still to translate, newest first is the extractor's order. */
  missing: string[];
};

/** How much of the catalogue one language has, for the audit script. */
export function coverageOf(locale: Locale): Coverage {
  const overlay = overlayFor(locale);
  const missing = KEYS.filter((key) => {
    const value = overlay[key];
    return typeof value !== "string" || value.length === 0;
  });
  const total = KEYS.length;
  const translated = total - missing.length;
  return {
    locale,
    translated,
    total,
    percent: total === 0 ? 100 : Math.round((translated / total) * 100),
    missing,
  };
}

/** Every language's coverage, worst first — the order a translator wants. */
export function coverageTable(): Coverage[] {
  return LOCALE_CODES.filter((code) => code !== DEFAULT_LOCALE)
    .map(coverageOf)
    .sort((a, b) => a.percent - b.percent || a.locale.localeCompare(b.locale));
}
