import { navIcons } from "./navIcons";
import { groupIcons } from "./groupIcons";
import { footerLinkIcons } from "./footerIcons";

/**
 * The picture marks for content cards, keyed by the destination they name.
 *
 * Where a card on a page points at a page, the card's glyph should be the
 * glyph that page already owns elsewhere — navbar, rail or footer — so the
 * visitor rehearse the same doorway everywhere the site offers it. This table
 * is therefore a thin alias layer over the three maps that exist, with a few
 * extra concepts (records, deliveries, demo, sponsor tiers) that only ever
 * appear on destination cards and so have no older owner. Same 16-unit grid,
 * same 1.6px stroke as every other glyph file.
 */
export const cardIcons: Record<string, string> = {
  "/records": "M3 3.5h10v9H3zM5.5 6.5h5M5.5 9h5M5.5 11.5h3",
  "/reports?status=Confirmed": groupIcons.Reports!,
  "/job-orders": footerLinkIcons["/job-orders"]!,
  "/deliveries": groupIcons.Dispatch!,
  "/contact": footerLinkIcons["/contact"]!,
  "/screenshots": footerLinkIcons["/screenshots"]!,
  "/worked-example": footerLinkIcons["/worked-example"]!,
  "/pricing": groupIcons.Finance!,
  "/features": navIcons.Product!,
  "/optimization": navIcons.Optimization!,
  "/how-it-works": footerLinkIcons["/how-it-works"]!,
  "/sponsors": navIcons.Sponsors!,
  "/developer": navIcons.Developer!,
  "/resources": navIcons.Resources!,
  "/blog": footerLinkIcons["/blog"]!,
};

export function cardIcon(href: string): string | undefined {
  return cardIcons[href];
}
