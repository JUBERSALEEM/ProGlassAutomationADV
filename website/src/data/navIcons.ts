/**
 * The glyphs on the top panel.
 *
 * One owner for the paths: the navigation renders them and so does the hero
 * banner renderer (`scripts/render-hero-banner.mts`), which draws the same
 * panel. Kept out of the component so a build-time script can read it without
 * pulling React in, and written on the same 16-unit grid the rest of the site's
 * marks use, with 1.6 px strokes.
 */
export const navIcons: Record<string, string> = {
  Home: "M2.5 7.5 8 3l5.5 4.5V13a.5.5 0 0 1-.5.5H3a.5.5 0 0 1-.5-.5z",
  Product: "M3 3.5h4v4H3zM9 3.5h4v4H9zM3 9.5h4v4H3zM9 9.5h4v4H9z",
  "Job Orders": "M3 3.5h10v8H3zM5 6h6M5 8.5h4M9.5 11.5l1.5 1.5 2.5-2.5",
  Optimization: "M3.5 12.5 12.5 3.5M6 3.5h6.5V10M4 10l4 4",
  Resources: "M3.5 3.5h6a2 2 0 0 1 2 2v7h-8zM11.5 5.5h3v7h-3zM5 6.5h5M5 9h5",
  About: "M8 2.5a5.5 5.5 0 1 1 0 11 5.5 5.5 0 0 1 0-11zM8 7.5v4M8 5.2v.6",
  Developer: "M6 4 2.5 8 6 12M10 4l3.5 4L10 12",
  Sponsors: "M8 13S3 10 3 6.6A2.6 2.6 0 0 1 8 5.2 2.6 2.6 0 0 1 13 6.6C13 10 8 13 8 13z",
  /* Two figures and a joined hand: the roles on the collaboration page. */
  Collaborate: "M6 7.5a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM12.5 7a1.6 1.6 0 1 0 0-3.2 1.6 1.6 0 0 0 0 3.2zM2.5 13c0-1.9 1.6-3.2 3.5-3.2S9.5 11.1 9.5 13M10.5 10.2c1.6.2 3 1.4 3 2.8",
} as const;
