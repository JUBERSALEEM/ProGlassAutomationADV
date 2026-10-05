import type { Overlay } from "../core";

/**
 * English — the source language, and deliberately empty.
 *
 * There is nothing to translate: the key of every dictionary entry *is* the
 * English sentence, so an empty overlay renders the site exactly as written.
 * Populating this file would mean maintaining a second copy of the site's own
 * copy, which is the one thing this design exists to avoid.
 */
const dict: Overlay = {};

export default dict;
