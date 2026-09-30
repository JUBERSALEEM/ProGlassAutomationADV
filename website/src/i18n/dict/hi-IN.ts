import type { Overlay } from "../core";

/**
 * Hindi — हिन्दी.
 *
 * India — the application ships Hindi even where the trade works in English.
 *
 * The key of every entry is the English sentence the site renders; the value is
 * what a reader of this language sees instead. Entries come from two places:
 * the hand-written translations below, and the application's own language file
 * (`Themes/Languages/hi-IN.xaml`), imported by `scripts/i18n-merge-app.mjs` so
 * that a term the software already names — a base rate, a delivery note, a
 * stage — is called the same thing in both.
 *
 * A sentence with no entry here is shown in English rather than hidden, which
 * is why this file can be incomplete without the site being broken.
 */
const dict: Overlay = {
};

export default dict;
