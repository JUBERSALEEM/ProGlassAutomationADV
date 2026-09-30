/**
 * How much of this site ships in a language — computed, never typed.
 *
 * The number in the language menu has to be true for the build it is in, so it
 * is derived from the dictionary itself rather than kept in a table somewhere
 * that drifts the moment a batch is applied. It counts the site's own sentences
 * that have an entry in that language; the application's vocabulary is counted
 * separately, because "the software's word for a proforma is here" and "the
 * sentence you are reading is in your language" are two different promises.
 *
 * A number in the menu is also a claim about what still reads English, and the
 * honest way to present it is as a share of the site's sentences — not as a
 * promise about a page, which depends on which page you are on.
 */

import type { Locale } from "./locales";
import { APP_STRINGS } from "./app-strings";
import { OVERLAYS } from "./dict";
import { KEYS } from "./dict/keys";

export type Coverage = {
  /** Sentences of this site that have an entry in this language. */
  sentences: number;
  /** The site's total sentence count. */
  total: number;
  /** `sentences / total`, as a whole percent. */
  percent: number;
  /** Entries the application itself supplies for this language. */
  appVocabulary: number;
  /** True when nothing falls back to English. */
  complete: boolean;
};

const TOTAL = KEYS.length;

/** Cached per locale: the dictionaries are static and this is a pure read. */
const memo = new Map<Locale, Coverage>();

export function coverageOf(locale: Locale): Coverage {
  const cached = memo.get(locale);
  if (cached) return cached;

  const overlay = OVERLAYS[locale];
  let sentences = 0;
  for (const key of KEYS) {
    const value = overlay[key];
    if (typeof value === "string" && value.trim() !== "") sentences += 1;
  }

  const coverage: Coverage = {
    sentences,
    total: TOTAL,
    percent: Math.round((sentences / TOTAL) * 100),
    appVocabulary: Object.keys(APP_STRINGS[locale]).length,
    complete: sentences === TOTAL,
  };
  memo.set(locale, coverage);
  return coverage;
}
