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
