import type { ModuleGroup } from "@/data/modules";

/**
 * One small glyph per rail group — the application's own nine, drawn on the
 * same 16-unit grid as the nav icons (`navIcons.ts`) so the whole site reads
 * one icon language. Each path names the thing the group holds, the way the
 * rail itself does.
 */
export const groupIcons: Record<ModuleGroup, string> = {
  Main: "M2.8 2.8h4.4v4.4H2.8zM8.8 2.8h4.4v4.4H8.8zM2.8 8.8h4.4v4.4H2.8zM8.8 8.8h4.4v4.4H8.8z",
  Inventory: "M8 2.5l6 3-6 3-6-3zM2 8l6 3 6-3M2 10.5l6 3 6-3",
  Sales: "M4 2.5h5.3L12 5v8.5H4zM9.3 2.5V5H12M6.2 8h3.6M6.2 10.5h3.6",
  Production:
    "M8 5.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5zM8 2v1.6M8 12.4V14M2.7 8h1.6M11.7 8h1.6M4.2 4.2l1.1 1.1M10.7 10.7l1.1 1.1M11.8 4.2l-1.1 1.1M5.3 10.7l-1.1 1.1",
  Dispatch:
    "M2 4.5h8v7H2zM10 7h2.3l1.7 2.3v2.2h-4zM4.7 13.2a1.3 1.3 0 1 0 0-2.6 1.3 1.3 0 0 0 0 2.6zM11.4 13.2a1.3 1.3 0 1 0 0-2.6 1.3 1.3 0 0 0 0 2.6z",
  Finance: "M2.5 5.5h11v6h-11zM8 7.3a1.9 1.9 0 1 0 0 3.8 1.9 1.9 0 0 0 0-3.8z",
  Reports: "M2.5 13.5h11M4 13.5V9M7 13.5V5.5M10 13.5V8M13 13.5V3.5",
  "Master Data":
    "M8 2.8c3.3 0 5.5.9 5.5 1.9v6.6c0 1-2.2 1.9-5.5 1.9S2.5 12.3 2.5 11.3V4.7c0-1 2.2-1.9 5.5-1.9zM2.5 4.7c0 1 2.2 1.9 5.5 1.9s5.5-.9 5.5-1.9M2.5 8c0 1 2.2 1.9 5.5 1.9S13.5 9 13.5 8",
  System:
    "M2.5 5h5.6M11 5h2.5M9.4 3.2a1.8 1.8 0 1 0 0 3.6 1.8 1.8 0 0 0 0-3.6zM2.5 11h2.6M8 11h5.5M6.4 9.2a1.8 1.8 0 1 0 0 3.6 1.8 1.8 0 0 0 0-3.6z",
};

/** The licence tiers, in the same hand. */
export const editionIcons: Record<string, string> = {
  trial: "M6.5 2.5h3M8 2.5v3l3.2 7a1.6 1.6 0 0 1-1.5 2.1H6.3a1.6 1.6 0 0 1-1.5-2.1zM5.8 10.5h4.4",
  basic: "M8 7.5a2.4 2.4 0 1 0 0-4.8 2.4 2.4 0 0 0 0 4.8zM3 13.5c0-2.2 2.2-3.6 5-3.6s5 1.4 5 3.6",
  business:
    "M6 7a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM11.5 6.5a1.7 1.7 0 1 0 0-3.4 1.7 1.7 0 0 0 0 3.4zM2.5 13c0-1.8 1.5-3 3.5-3S9.5 11.2 9.5 13M10.5 8.7A3.6 3.6 0 0 1 13.5 12.3",
  factory: "M2.5 13.5V6l3.5 2.2V6l3.5 2.2V6l4 2.4v5.1zM5 10.8h1.5M9 10.8h1.5",
  enterprise:
    "M8 2.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11zM2.5 8h11M8 2.5c1.7 1.4 2.5 3.2 2.5 5.5s-.8 4.1-2.5 5.5c-1.7-1.4-2.5-3.2-2.5-5.5S6.3 3.9 8 2.5z",
};

/** Lookup with a neutral grid for any future tier the map does not know yet. */
export function editionIcon(id: string): string {
  return editionIcons[id] ?? groupIcons.Main;
}
