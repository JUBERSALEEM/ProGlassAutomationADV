"use client";

import { useEffect } from "react";
import { initGlassLife } from "@/lib/glassLife";

/**
 * §C5 — mounts the crystal catch-light listener once for the whole app.
 * Renders nothing; the paint lives in the engine (crystal-engine.css §C5)
 * and the coordinates live in lib/glassLife.ts.
 */
export function GlassLife() {
  useEffect(() => initGlassLife(), []);
  return null;
}
