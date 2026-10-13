/**
 * Shared SVG gradients — one definition for the whole document.
 *
 * Every gradient the site paints with CSS custom properties rather than fixed
 * colours, which means the definition is *identical* in every instance: the
 * mark in the header, the mark in all 39 gallery frames and the tint behind
 * every page banner were each drawing their own copy of the same three or two
 * gradients, under a unique id, purely so the ids would not collide.
 *
 * On `/screenshots` that was 38 separate `<defs>` blocks and roughly 30 KB of
 * duplicated markup in the HTML. They are defined once here instead, in the
 * root layout, and referenced by a fixed id (`url(#pg-logo-face)`), which is
 * both smaller and the way SVG gradients are meant to be shared.
 *
 * Two details matter and are easy to get wrong:
 *
 *   1. The sprite must be in the document, not `display:none`. A hidden
 *      subtree is not painted, and gradients referenced from it stop
 *      resolving in some engines. Hence a zero-sized, absolutely positioned
 *      element that is still rendered.
 *   2. Referencing across `<svg>` elements is fine as long as they are in the
 *      same document — all of these are.
 *
 * The colours are tokens, so the sprite re-tints with the palette family and
 * the appearance studio exactly as before.
 */
export function SvgDefs() {
  return (
    <svg
      aria-hidden="true"
      focusable="false"
      width="0"
      height="0"
      style={{ position: "absolute", width: 0, height: 0, overflow: "hidden" }}
    >
      <defs>
        {/* The mark: front sheet, pane behind it, and the specular glint. */}
        <linearGradient id="pg-logo-face" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="rgb(var(--brand-300))" />
          <stop offset="52%" stopColor="rgb(var(--brand-500))" />
          <stop offset="100%" stopColor="rgb(var(--brand-700))" />
        </linearGradient>
        <linearGradient id="pg-logo-inner" x1="0" y1="1" x2="1" y2="0">
          <stop offset="0%" stopColor="rgb(var(--glass-cyan))" stopOpacity="0.85" />
          <stop offset="100%" stopColor="rgb(var(--brand-300))" stopOpacity="0.55" />
        </linearGradient>
        <linearGradient id="pg-logo-glint" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#FFFFFF" stopOpacity="0.5" />
          <stop offset="60%" stopColor="#FFFFFF" stopOpacity="0" />
        </linearGradient>

        {/*
          The brand emblem's three faces — see components/brand/GlassCubeEmblem.

          The splash screen paints them from three fixed brushes; these paint
          them from the site's tokens so the emblem follows the mode and the
          appearance family. In the Studio family at the dark default the three
          ramps land on the splash's own #EFEAFB→#B4A1E1, #8568CF→#5A3FA8 and
          #A78BDA→#6546B5, which is the same violet at the same three tones.
        */}
        <linearGradient id="pg-cube-top" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="rgb(var(--brand-50))" />
          <stop offset="100%" stopColor="rgb(var(--brand-200))" />
        </linearGradient>
        <linearGradient id="pg-cube-left" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="rgb(var(--brand-400))" />
          <stop offset="100%" stopColor="rgb(var(--brand-600))" />
        </linearGradient>
        <linearGradient id="pg-cube-right" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="rgb(var(--brand-300))" />
          <stop offset="100%" stopColor="rgb(var(--brand-500))" />
        </linearGradient>
        {/* The right face re-tinted, which is what SplashThemeManager does to
            GlassFacePath when the accent changes. */}
        <linearGradient id="pg-cube-accent" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="rgb(var(--brand-400))" />
          <stop offset="100%" stopColor="rgb(var(--brand-700))" />
        </linearGradient>

        {/* Page banner: a warm-to-cool wash and the ink fade that grounds it. */}
        <linearGradient id="pg-banner" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="rgb(var(--brand-400))" stopOpacity="0.5" />
          <stop offset="55%" stopColor="rgb(var(--brand-500))" stopOpacity="0.22" />
          <stop offset="100%" stopColor="rgb(var(--glass-cyan))" stopOpacity="0.3" />
        </linearGradient>
        <linearGradient id="pg-banner-fade" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0%" stopColor="rgb(var(--ink-950))" stopOpacity="1" />
          <stop offset="42%" stopColor="rgb(var(--ink-950))" stopOpacity="0" />
        </linearGradient>
      </defs>
    </svg>
  );
}
