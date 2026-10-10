"use client";

import dynamic from "next/dynamic";
import { useEffect, useState } from "react";
import { useStudio } from "@/lib/useStudio";
import { useT } from "@/i18n/provider";

const ThemeStudio = dynamic(() => import("./ThemeStudio").then((m) => m.ThemeStudio), {
  ssr: false,
});

/**
 * The theme studio, gated behind first demand.
 *
 * The studio drawer is four screens of dials, sliders, specimen grids and
 * colour plates (a direct port of the application's own Appearance settings),
 * and before this wrapper existed every visitor paid that weight at launch —
 * on every page — for a control most sessions never open. The drawer chunk is
 * now fetched when the corner trigger or the header's appearance entry first
 * asks for it, and only then.
 *
 * Two jobs stay first-load, on purpose:
 *
 *   1. `useStudio()` here is the keeper. Its subscription is what re-applies
 *      the visitor's stored radius/tint/edge dials whenever the theme
 *      attributes change (see useStudio.ts — the MutationObserver that fixed
 *      the intermittent accent plates). The drawer used to be that keeper,
 *      which is why deferring the whole component was not an option: on a page
 *      without an AppearancePreview mounted, nobody else reinstalls the dials
 *      after a mode switch. One hook, no UI.
 *
 *   2. The corner trigger renders as a static stand-in. The real trigger
 *      shows the active family and accent as two dots; the stand-in paints
 *      them from the live CSS variables the boot script already applied, so
 *      the pixels a first-time visitor sees are the same ones. When the real
 *      trigger replaces it, the dots keep their meaning.
 *
 * Hand-off: both summon paths render the studio with `autoOpen`, so the panel
 * is already open when the chunk arrives. There is no event to miss and no
 * second click — the dispatch in ThemeToggle and this button produce the same
 * outcome.
 */
export function ThemeStudioLazy() {
  useStudio(); // keeper — see the comment above, item 1
  const t = useT();
  const [summoned, setSummoned] = useState(false);

  useEffect(() => {
    const summon = () => setSummoned(true);
    window.addEventListener("proglass-open-studio", summon);
    return () => window.removeEventListener("proglass-open-studio", summon);
  }, []);

  if (summoned) return <ThemeStudio autoOpen />;

  return (
    <button
      type="button"
      aria-label={t("Theme and colour")}
      onClick={() => setSummoned(true)}
      className="no-print menu-card group fixed bottom-5 right-5 z-40 flex items-center gap-2.5 rounded-full px-4 py-3 text-xs font-bold text-text-primary shadow-raised transition-transform hover:-translate-y-0.5"
    >
      <span aria-hidden="true" className="flex items-center gap-1">
        <span className="h-4 w-4 rounded-full ring-2 ring-white/25" style={{ background: "var(--brand-600)" }} />
        <span className="h-4 w-4 -ml-2 rounded-full ring-2 ring-white/25" style={{ background: "var(--brand-400)" }} />
      </span>
      <span>Theme &amp; colour</span>
    </button>
  );
}
