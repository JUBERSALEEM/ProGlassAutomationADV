/**
 * The translation core — no React, no Next, no I/O.
 *
 * Everything here is a pure function so it can run in three places at once:
 * a server component rendering metadata, a client component rendering a label,
 * and a script checking that the dictionaries are complete. Those three must
 * agree, and the way to guarantee that is to have exactly one implementation.
 *
 * ## How a sentence is translated
 *
 * The site is not keyed by invented identifiers (`nav.product.features`). The
 * key *is* the English sentence, and the lookup is a plain object read:
 *
 *   1. the site's dictionary for this language, keyed by the English sentence;
 *   2. the application's own language file — `Themes/Languages/<code>.xaml` —
 *      indexed by its English value, which is where a term like "Deposit" or
 *      "Sheet size" comes from already translated, in the application's words;
 *   3. the English sentence itself.
 *
 * Two consequences are deliberate. A sentence that exists in the application
 * can never disagree with it, because the site never writes its own version.
 * And a sentence nobody has translated yet degrades to readable English rather
 * than to a key name or an empty string — the failure mode is a page in mixed
 * language, not a broken page.
 */

import type { Locale } from "./locales";
import { DEFAULT_LOCALE, LOCALE_CODES } from "./locales";

/**
 * The cookie the language menu writes and the proxy reads.
 *
 * One name, declared once, because two copies of a cookie name is a bug that
 * only shows up in production: the menu would write a cookie nobody reads.
 */
export const LOCALE_COOKIE = "proglass-locale";

/** The site's overlay for one language: English sentence → that language. */
export type Overlay = Record<string, string | undefined>;

/** The application's translated values for one language: English value → value. */
export type AppStrings = Record<string, string | undefined>;

export type Vars = Record<string, string | number>;

/**
 * Substitute `{name}` placeholders.
 *
 * Written by hand rather than with a `replace` per variable so that a brace in
 * the *value* — a SQL fragment, a code path — can never be mistaken for a
 * placeholder on the next pass.
 */
export function fill(template: string, vars?: Vars): string {
  if (!vars) return template;
  let out = "";
  let index = 0;
  while (index < template.length) {
    const open = template.indexOf("{", index);
    if (open === -1) break;
    const close = template.indexOf("}", open + 1);
    if (close === -1) break;
    const name = template.slice(open + 1, close);
    if (!(name in vars)) {
      out += template.slice(index, close + 1);
      index = close + 1;
      continue;
    }
    out += template.slice(index, open) + String(vars[name]);
    index = close + 1;
  }
  return out + template.slice(index);
}

export type Translator = {
  (key: string, vars?: Vars): string;
  /** The language this translator speaks. */
  locale: Locale;
  /** True when the sentence has a translation in this language. */
  has: (key: string) => boolean;
};

/**
 * Build the translator for one language.
 *
 * `overlay` is the site's own dictionary, `app` the application's values for
 * the same language indexed by their English original. Both are plain objects,
 * both are optional, and neither is required to be complete.
 */
export function makeTranslator(locale: Locale, overlay: Overlay = {}, app: AppStrings = {}): Translator {
  const translate = (key: string, vars?: Vars): string => {
    const hit = overlay[key] ?? app[key];
    const text = typeof hit === "string" && hit.length > 0 ? hit : key;
    return vars ? fill(text, vars) : text;
  };
  return Object.assign(translate, {
    locale,
    has: (key: string) => {
      const hit = overlay[key] ?? app[key];
      return typeof hit === "string" && hit.length > 0;
    },
  });
}

/* ── URLs ────────────────────────────────────────────────────────────
 *
 * English has no prefix: `https://site/features`. Every other language is
 * prefixed: `https://site/ja-JP/features`. The default language is not a
 * special case that links have to remember to handle — `hrefIn` is the only
 * function that builds a link, and it always applies this rule.
 */

/** `/de/pricing` → `/pricing`; the prefix is removed, the page is kept. */
export function pathWithoutLocale(pathname: string): string {
  const [path = ""] = pathname.split(/[?#]/);
  const segments = path.split("/").filter(Boolean);
  if (segments.length > 0 && LOCALE_CODES.includes(segments[0] as Locale)) {
    segments.shift();
  }
  const rest = segments.join("/");
  return rest ? `/${rest}` : "/";
}

/** The language a path is already written in, or the default. */
export function localeOfPath(pathname: string): Locale {
  const segments = (pathname.split(/[?#]/)[0] ?? "").split("/").filter(Boolean);
  const first = segments[0] as Locale | undefined;
  return first && LOCALE_CODES.includes(first) ? first : DEFAULT_LOCALE;
}

/** Build a link to `path` in `locale`, keeping any query and hash. */
export function hrefIn(locale: Locale, path: string): string {
  const bare = pathWithoutLocale(path);
  const match = path.match(/[?#].*$/);
  const suffix = match ? match[0] : "";
  if (locale === DEFAULT_LOCALE) return `${bare}${suffix}`;
  if (bare === "/") return `/${locale}${suffix}`;
  return `/${locale}${bare}${suffix}`;
}

/**
 * The same page in every language, for `hreflang` and the sitemap.
 *
 * `x-default` is English, and `absolute` is applied by the caller, which is the
 * only place that knows the site's origin.
 */
export function alternatesFor(path: string): { hreflang: string; path: string }[] {
  const bare = pathWithoutLocale(path);
  return LOCALE_CODES.map((code) => ({ hreflang: code, path: hrefIn(code, bare) }));
}

/**
 * Language tags for `Content-Language` and `og:locale`.
 *
 * Open Graph wants an underscore and a region (`de_DE`), the web wants a hyphen
 * (`de-DE`); the code the site uses is already the second form.
 */
export function ogLocale(locale: Locale): string {
  return locale.replace("-", "_");
}
