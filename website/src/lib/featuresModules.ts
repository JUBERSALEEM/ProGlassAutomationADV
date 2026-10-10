/**
 * The seven focus modules of the Features stack.
 *
 * The homepage exhibit presents exactly these seven,four as the depth stage
 * (Optimization, Proforma, Sheet Store, Delivery Note) and the remaining
 * three as the compact rail beneath it. The set is the page's own: the page
 * chose these because a visitor can see each one's real interface on
 * /screenshots without leaving the site, and they cover the chain's first
 * half inch for inch — pricing, cutting, stock, and the paperwork that
 * leaves with the glass.
 *
 * The labels are the modules' own keys, so the chips stay short in every
 * language the site speaks; the full sentence lives on the Features page.
 */
export interface FocusModule {
  id: string;
  /** The route `<Tilt>`-free label: the module chip name. */
  short: string;
  /** The Features page anchor id as the data declares it. */
  pageHref: string;
}

export const focusModules: FocusModule[] = [
  { id: "optimization", short: "Optimization", pageHref: "/features#optimization" },
  { id: "proforma", short: "Proforma", pageHref: "/features#proforma" },
  { id: "sheet-store", short: "Sheet Store", pageHref: "/features#sheet-store" },
  { id: "delivery-note", short: "Delivery Note", pageHref: "/features#delivery-note" },
];

export const railModules: FocusModule[] = [
  { id: "statement", short: "Statement", pageHref: "/features#statement" },
  { id: "whatsapp", short: "WhatsApp", pageHref: "/features#whatsapp" },
  { id: "labels", short: "Labels", pageHref: "/features#labels" },
];

/**
 * The one-line captions the stack prints under each module name. They live
 * here — in the plain data module — rather than in the component file
 * because both halves need them: the server wrapper translates them into
 * the visitor's language, and the client exhibit places the translation it
 * is given. A table exported from a "use client" module reaches the
 * server as a reference proxy, and proxies enumerate to nothing: that is
 * the one-line story of every "the caption was empty" report.
 */
export const FEATURES_CAPTIONS: Record<string, string> = {
  optimization: "Sheet yield in labour time, not marketing percentages",
  proforma: "Every line priced from a real sheet cost",
  "sheet-store": "Racks, remnants and grades, drift-audited",
  "delivery-note": "What left the yard, and what still owes",
  statement: "Once written, forever reconciled",
  whatsapp: "Plain-text offers, one click away",
  labels: "Barcode to job order in one scan",
};
