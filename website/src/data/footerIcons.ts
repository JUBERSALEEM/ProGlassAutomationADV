import { navIcons } from "./navIcons";
import { groupIcons } from "./groupIcons";

/**
 * The footer's card glyphs, keyed by destination.
 *
 * Every path sits on the same 16-unit grid as the nav glyphs and the nine rail
 * groups so the bottom of the page reads the same icon hand as the top of it.
 * Where a top-level destination already owns a path in `navIcons` or a rail
 * group does in `groupIcons`, the footer reuses it — one destination, one
 * mark, from header to footer. The handful of destinations with no existing
 * owner (worked example, contact, blog, the legal three) gain one here, drawn
 * to the same grid and stroke.
 */
export const footerLinkIcons: Record<string, string> = {
  "/features": navIcons.Product!,
  "/optimization": navIcons.Optimization!,
  "/how-it-works":
    "M3.5 9.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zM8 9.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zM12.5 9.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zM5 8h1.5M9.5 8h1.5",
  "/worked-example": "M4.5 2.5h7v11l-1.75-1.17L8 13.5l-1.75-1.17L4.5 13.5zM6.5 6.5h3M6.5 9h3",
  "/screenshots":
    "M3 3.5h10v9H3zM9.6 6.4a1.2 1.2 0 1 0-2.4 0 1.2 1.2 0 0 0 2.4 0zM3 12l3.2-3.2L9.6 12M9 12l1.8-1.8L13 12",
  "/pricing": groupIcons.Finance!,
  "/reports": groupIcons.Reports!,
  "/job-orders": "M4.5 3.5h7v10h-7zM6.6 2.5h2.8v2H6.6zM6.6 8h2.8M6.6 10.5h2.8",
  "/deliveries": groupIcons.Dispatch!,
  "/about": navIcons.About!,
  "/developer": navIcons.Developer!,
  "/collaborate": navIcons.Collaborate!,
  "/sponsors": navIcons.Sponsors!,
  "/contact": "M2.5 4.5h11v7h-11zM3 5.5l5 4 5-4",
  "/resources": navIcons.Resources!,
  "/blog": "M9.8 3.2l3 3-6.5 6.5-3.8.8.8-3.8zM11 5l-3 3",
  "/legal/privacy": "M8 2.5l4.5 2v3.2c0 3-1.9 4.9-4.5 5.8-2.6-.9-4.5-2.8-4.5-5.8V4.5z",
  "/legal/terms": groupIcons.Sales!,
  "/legal/license": "M9 7.5a3 3 0 1 0-3 3.8L7.5 10v2l1.5 1.5v-1h1.5v-2zM9.9 6h.4",
};

/**
 * The contact-channel glyphs on the brand column. Distinct marks for distinct
 * doors: WhatsApp is a chat bubble, the address is an envelope, and the
 * source-repository mark is the platform's own silhouette reduced to the grid.
 */
export const channelIcons = {
  whatsapp: "M8 2.5a5.5 5.5 0 0 0-4.8 8.2L2.5 13.5l2.9-.7A5.5 5.5 0 1 0 8 2.5zM5.8 6.7c.4 2 2.3 3.9 4.3 4.3",
  email: "M2.5 4.5h11v7h-11zM3 5.5l5 4 5-4",
  github:
    "M8 2.5A5.5 5.5 0 0 0 2.5 8c0 2.4 1.55 4.45 3.7 5.2.27.05.37-.12.37-.26v-.9c-1.5.32-1.83-.63-1.83-.63-.25-.63-.61-.8-.61-.8-.5-.34.04-.33.04-.33.55.04.85.57.85.57.49.84 1.29.6 1.6.46.05-.36.2-.6.35-.74-1.2-.14-2.47-.6-2.47-2.68 0-.6.21-1.08.56-1.46-.05-.14-.24-.7.05-1.45 0 0 .46-.15 1.5.56a5.2 5.2 0 0 1 2.74 0c1.04-.71 1.5-.56 1.5-.56.29.75.1 1.31.05 1.45.35.38.55.87.55 1.46 0 2.08-1.27 2.54-2.48 2.67.2.17.37.5.37 1.01v1.49c0 .14.1.31.37.26A5.5 5.5 0 0 0 8 2.5z",
};

/** A neutral grid mark for a footer destination without a mapped path yet. */
export function footerIconFor(href: string): string {
  const bare = href.split("?")[0] ?? "";
  return footerLinkIcons[bare] ?? navIcons.Product!;
}
