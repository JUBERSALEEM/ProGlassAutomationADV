"use client";

import { ThemeStudio } from "./ThemeStudio";

/**
 * Hosts the appearance studio in the root layout.
 *
 * `ThemeStudio` already gates the 4-screen dialog behind `{open ? ... : null}`
 * so initial render only paints the 15-line corner launcher with the live
 * family and accent swatches, and clicking the launcher or dispatching
 * `proglass-open-studio` opens the studio drawer synchronously on the same
 * frame without waiting on a secondary network chunk.
 */
export function ThemeStudioLazy() {
  return <ThemeStudio />;
}
