/**
 * The fifteen languages this site speaks.
 *
 * The list is not a wish list and not a market survey: it is exactly the set of
 * language files that ship inside the application (`Themes/Languages/*.xaml`),
 * so the vocabulary a visitor reads here is the vocabulary the software itself
 * uses. Anything the site says that also exists as an application string is
 * taken from that file for the same language, which is why the two agree word
 * for word.
 *
 * `native` is the name of the language written in that language. A Korean
 * reader scanning a language menu looks for 한국어, not for the English word
 * "Korean", and a menu that lists every language in a script the reader cannot
 * read is a menu aimed at the wrong person.
 */

export type Locale =
  | "en-US"
  | "ar-AE"
  | "bn-BD"
  | "de-DE"
  | "es-ES"
  | "fr-FR"
  | "hi-IN"
  | "id-ID"
  | "ja-JP"
  | "ko-KR"
  | "pt-BR"
  | "ru-RU"
  | "tr-TR"
  | "ur-PK"
  | "zh-CN";

export type Direction = "ltr" | "rtl";

export type LocaleInfo = {
  /** The code used in the URL and the application's own language files. */
  code: Locale;
  /** English name, for a reader who does not read the language itself. */
  english: string;
  /** Endonym — the language naming itself. */
  native: string;
  direction: Direction;
  /** The application's rough coverage of this language, from its own files. */
  region: string;
};

/* English first: it is the default, the fallback and the source language of
   every key in the dictionary. The rest follow the application's own order of
   language files, which is alphabetical by code. */
export const LOCALES: LocaleInfo[] = [
  { code: "en-US", english: "English", native: "English", direction: "ltr", region: "International" },
  { code: "ar-AE", english: "Arabic", native: "العربية", direction: "rtl", region: "United Arab Emirates" },
  { code: "bn-BD", english: "Bengali", native: "বাংলা", direction: "ltr", region: "Bangladesh" },
  { code: "de-DE", english: "German", native: "Deutsch", direction: "ltr", region: "Germany, Austria, Switzerland" },
  { code: "es-ES", english: "Spanish", native: "Español", direction: "ltr", region: "Spain, Latin America" },
  { code: "fr-FR", english: "French", native: "Français", direction: "ltr", region: "France, Belgium" },
  { code: "hi-IN", english: "Hindi", native: "हिन्दी", direction: "ltr", region: "India" },
  { code: "id-ID", english: "Indonesian", native: "Bahasa Indonesia", direction: "ltr", region: "Indonesia" },
  { code: "ja-JP", english: "Japanese", native: "日本語", direction: "ltr", region: "Japan" },
  { code: "ko-KR", english: "Korean", native: "한국어", direction: "ltr", region: "South Korea" },
  { code: "pt-BR", english: "Portuguese", native: "Português", direction: "ltr", region: "Brazil, Portugal" },
  { code: "ru-RU", english: "Russian", native: "Русский", direction: "ltr", region: "Russia, Kazakhstan" },
  { code: "tr-TR", english: "Turkish", native: "Türkçe", direction: "ltr", region: "Türkiye, Azerbaijan" },
  { code: "ur-PK", english: "Urdu", native: "اردو", direction: "rtl", region: "Pakistan" },
  { code: "zh-CN", english: "Chinese", native: "中文", direction: "ltr", region: "China, Taiwan, Hong Kong" },
];

export const DEFAULT_LOCALE: Locale = "en-US";

export const LOCALE_CODES: Locale[] = LOCALES.map((entry) => entry.code);

const BY_CODE = new Map<string, LocaleInfo>(LOCALES.map((entry) => [entry.code, entry]));

export function isLocale(value: string): value is Locale {
  return BY_CODE.has(value);
}

export function localeInfo(code: Locale): LocaleInfo {
  return BY_CODE.get(code) ?? BY_CODE.get(DEFAULT_LOCALE)!;
}

export function dirOf(code: Locale): Direction {
  return localeInfo(code).direction;
}

export const RTL_LOCALES: Locale[] = LOCALES.filter((entry) => entry.direction === "rtl").map((entry) => entry.code);

/**
 * Which country gets which language by default.
 *
 * This is the only place that decision lives, and it is deliberately short.
 * Two rules shaped it:
 *
 * **A country only appears when the language is the everyday working language
 * there**, not merely taught or understood. That is why India is absent even
 * though the application ships Hindi — English is the working language of the
 * Indian glass trade, and dropping an Indian visitor into a page they did not
 * ask for is worse than leaving them in the site's own language.
 *
 * **Countries with more than one official language choose one.** Belgium is
 * mapped to French, Switzerland to German, Canada to neither — in each case the
 * larger trade language for this industry. A visitor who disagrees has the menu,
 * and the menu remembers the answer.
 */
export const COUNTRY_LOCALE: Record<string, Locale> = {
  // Arabic — the Gulf and Egypt, where the application is sold in Arabic.
  AE: "ar-AE", SA: "ar-AE", QA: "ar-AE", KW: "ar-AE", BH: "ar-AE", OM: "ar-AE",
  JO: "ar-AE", LB: "ar-AE", IQ: "ar-AE", EG: "ar-AE", YE: "ar-AE", SY: "ar-AE",
  // Bengali
  BD: "bn-BD",
  // German
  DE: "de-DE", AT: "de-DE", CH: "de-DE", LI: "de-DE",
  // Spanish — Spain and Latin America
  ES: "es-ES", MX: "es-ES", AR: "es-ES", CO: "es-ES", CL: "es-ES", PE: "es-ES",
  EC: "es-ES", GT: "es-ES", UY: "es-ES", PY: "es-ES", BO: "es-ES", VE: "es-ES",
  CR: "es-ES", PA: "es-ES", DO: "es-ES", HN: "es-ES", NI: "es-ES", SV: "es-ES", PR: "es-ES",
  // French
  FR: "fr-FR", BE: "fr-FR", MC: "fr-FR", SN: "fr-FR", CI: "fr-FR", CM: "fr-FR",
  // Indonesian
  ID: "id-ID",
  // Japanese
  JP: "ja-JP",
  // Korean
  KR: "ko-KR",
  // Portuguese
  BR: "pt-BR", PT: "pt-BR", AO: "pt-BR", MZ: "pt-BR", CV: "pt-BR",
  // Russian
  RU: "ru-RU", KZ: "ru-RU", BY: "ru-RU", KG: "ru-RU", AM: "ru-RU",
  // Turkish
  TR: "tr-TR", AZ: "tr-TR", CY: "tr-TR",
  // Urdu
  PK: "ur-PK",
  // Chinese
  CN: "zh-CN", TW: "zh-CN", HK: "zh-CN", MO: "zh-CN", SG: "zh-CN",
};

/** The language a country gets, or null when the site's default is right. */
export function localeForCountry(country: string | null | undefined): Locale | null {
  if (!country) return null;
  return COUNTRY_LOCALE[country.trim().toUpperCase()] ?? null;
}

/**
 * A browser's `Accept-Language` header, best effort.
 *
 * Region matters: `pt-BR` and `pt-PT` both resolve to the Brazilian file the
 * application ships, and `zh-TW` resolves to the Simplified file because that
 * is the only Chinese file there is. An exact match on a shipped code wins;
 * otherwise the first shipped language with the same base tag is used.
 */
export function localeForAcceptLanguage(header: string | null | undefined): Locale | null {
  if (!header) return null;
  const ranked = header
    .split(",")
    .map((part) => {
      const [tag = "", q] = part.trim().split(";q=");
      return { tag: tag.trim().toLowerCase(), q: q ? Number(q) : 1 };
    })
    .filter((entry) => entry.tag && entry.tag !== "*" && Number.isFinite(entry.q))
    .sort((a, b) => b.q - a.q);

  for (const { tag } of ranked) {
    const exact = LOCALES.find((entry) => entry.code.toLowerCase() === tag);
    if (exact) return exact.code;
  }
  for (const { tag } of ranked) {
    const base = tag.split("-")[0] ?? tag;
    const near = LOCALES.find((entry) => entry.code.toLowerCase().split("-")[0] === base);
    if (near) return near.code;
  }
  return null;
}
