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
  Optimization: "M3.5 12.5 12.5 3.5M6 3.5h6.5V10M4 10l4 4",
  Resources: "M3.5 3.5h6a2 2 0 0 1 2 2v7h-8zM11.5 5.5h3v7h-3zM5 6.5h5M5 9h5",
  About: "M8 2.5a5.5 5.5 0 1 1 0 11 5.5 5.5 0 0 1 0-11zM8 7.5v4M8 5.2v.6",
  Developer: "M6 4 2.5 8 6 12M10 4l3.5 4L10 12",
  Sponsors: "M8 13S3 10 3 6.6A2.6 2.6 0 0 1 8 5.2 2.6 2.6 0 0 1 13 6.6C13 10 8 13 8 13z",
} as const;
