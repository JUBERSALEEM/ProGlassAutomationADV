import type { Config } from "tailwindcss";

/**
 * Design tokens.
 *
 * Every colour resolves to a CSS custom property declared in
 * `src/app/globals.css` for the active theme, as a raw RGB triplet so opacity
 * modifiers (`bg-ink-900/70`) keep working in both themes. Components only ever
 * say `ink` / `line` / `brand` / `text` / `state` — switching theme repaints the
 * whole site without a single markup change.
 *
 *   Midnight (default)  deep violet surfaces, violet accent
 *   Daylight            paper-white surfaces, same violet identity
 *
 * The violet is deliberate and product-led: the site is about glass, and violet
 * reads as technical and premium without borrowing the SaaS blue that every
 * dashboard product already uses.
 */
const config: Config = {
  content: ["./src/**/*.{ts,tsx,mdx}"],
  theme: {
    extend: {
      /**
       * One screen of our own, over Tailwind's defaults.
       *
       * The header is the one part of this site whose width budget can be
       * worked out exactly: it is a fixed-height row of fixed-size things — a
       * lockup, six nav cards, three settings controls, a call to action and
       * the build stamp — and every one of them is measured from the real font
       * files by `scripts/measure-panel.py`. Measured, the full row needs about
       * 1630 px of content width. No default breakpoint describes where that
       * becomes true: `2xl` is 1536, and the next stop is the whole of a 1920
       * monitor. `wide` is where everything the panel owns fits at once, which
       * is why the nav's glyph icons and the palette's word appear there and
       * not below it — and why the panel sheds items in a defined order on the
       * way down instead of being squeezed (see `measure-panel.py` for the
       * ladder, and `Navbar.tsx` for the classes that implement it).
       */
      screens: { wide: "1920px" },
      colors: {
        /** Surfaces, darkest page tier first. */
        ink: {
          950: "rgb(var(--ink-950) / <alpha-value>)",
          900: "rgb(var(--ink-900) / <alpha-value>)",
          850: "rgb(var(--ink-850) / <alpha-value>)",
          800: "rgb(var(--ink-800) / <alpha-value>)",
          700: "rgb(var(--ink-700) / <alpha-value>)",
        },
        line: {
          DEFAULT: "rgb(var(--line) / <alpha-value>)",
          soft: "rgb(var(--line-soft) / <alpha-value>)",
          strong: "rgb(var(--line-strong) / <alpha-value>)",
        },
        brand: {
          50: "rgb(var(--brand-50) / <alpha-value>)",
          /*
           * 100 exists because the site was already writing `text-brand-100`
           * in six places — a monogram, a pricing chip, a metric figure and a
           * code span — and Tailwind, finding no `brand.100` in this scale,
           * silently emitted nothing. The class was in the markup and had no
           * effect: those figures were inheriting their colour from whatever
           * surrounded them. It is defined here rather than renamed in the six
           * call sites because the intent of the call site was right — the
           * lightest accent step, for figure text on a tinted chip.
           */
          100: "rgb(var(--brand-100) / <alpha-value>)",
          200: "rgb(var(--brand-200) / <alpha-value>)",
          300: "rgb(var(--brand-300) / <alpha-value>)",
          400: "rgb(var(--brand-400) / <alpha-value>)",
          500: "rgb(var(--brand-500) / <alpha-value>)",
          600: "rgb(var(--brand-600) / <alpha-value>)",
          700: "rgb(var(--brand-700) / <alpha-value>)",
        },
        glass: {
          teal: "rgb(var(--glass-teal) / <alpha-value>)",
          cyan: "rgb(var(--glass-cyan) / <alpha-value>)",
          green: "rgb(var(--glass-green) / <alpha-value>)",
          rose: "rgb(var(--glass-rose) / <alpha-value>)",
        },
        text: {
          primary: "rgb(var(--text-1) / <alpha-value>)",
          secondary: "rgb(var(--text-2) / <alpha-value>)",
          muted: "rgb(var(--text-3) / <alpha-value>)",
          faint: "rgb(var(--text-4) / <alpha-value>)",
        },
        state: {
          success: "rgb(var(--state-success) / <alpha-value>)",
          warning: "rgb(var(--state-warning) / <alpha-value>)",
          danger: "rgb(var(--state-danger) / <alpha-value>)",
          info: "rgb(var(--state-info) / <alpha-value>)",
        },
        /** Text that sits on a filled brand surface. */
        "accent-ink": "rgb(var(--on-accent) / <alpha-value>)",
        /**
         * Money colours. Every figure on the site that a shop owner reads as
         * profit, cost or loss uses these three, so the visitor can re-point
         * them from the appearance studio and the whole site follows.
         */
        money: {
          positive: "rgb(var(--money-positive) / <alpha-value>)",
          negative: "rgb(var(--money-negative) / <alpha-value>)",
          neutral: "rgb(var(--money-neutral) / <alpha-value>)",
        },
        /** Colours used by the live module previews and the nesting diagram. */
        viz: {
          grid: "rgb(var(--viz-grid) / <alpha-value>)",
          part: "rgb(var(--viz-part) / <alpha-value>)",
          "part-2": "rgb(var(--viz-part-2) / <alpha-value>)",
          scrap: "rgb(var(--viz-scrap) / <alpha-value>)",
          plate: "rgb(var(--viz-plate) / <alpha-value>)",
        },
      },
      fontFamily: {
        /* Bound to the studio's typeface control; falls back to the site default. */
        sans: ["var(--font-sans, var(--font-inter))", "Segoe UI", "system-ui", "sans-serif"],
        mono: ["var(--font-mono)", "ui-monospace", "Consolas", "monospace"],
      },
      fontSize: {
        /*
         * The site's label size: eyebrows, mono labels, table captions and the
         * small print under a figure. 12px is the floor for anything a reader
         * is meant to read — below that it stops being a label and becomes
         * texture. Anything smaller than this in the codebase is inside a
         * miniature of the application window, where the whole mock is scaled.
         */
        "2xs": ["0.75rem", { lineHeight: "1.05rem", letterSpacing: "0.05em" }],
        /*
         * The size for chrome that lives *inside* a small card.
         *
         * A pill, a sample tag, a column heading in a preview table, a chip
         * beside a figure: these sit in a box a third the width of a page
         * column, and at 12px uppercase with wide tracking a row of four of
         * them — Excel · CSV · PDF · Share cut list — reads louder than the
         * heading above it and can wrap onto two lines. One step down, and the
         * tracking comes in with it, which is what actually decides how much
         * horizontal room the label takes.
         *
         * This is not a licence to go smaller everywhere: 2xs remains the floor
         * for prose, captions and anything a reader is meant to read across a
         * page. 3xs is for labels inside a card, and nothing here goes below it.
         */
        "3xs": ["0.6875rem", { lineHeight: "1rem", letterSpacing: "0.04em" }],
      },
      borderRadius: {
        /* Scaled by the appearance studio, so one control re-corners the site. */
        card: "calc(1rem * var(--radius-scale, 1))",
        panel: "calc(1.25rem * var(--radius-scale, 1))",
        hero: "calc(1.5rem * var(--radius-scale, 1))",
      },
      boxShadow: {
        panel: "var(--shadow-panel)",
        raised: "var(--shadow-raised)",
        glow: "var(--shadow-glow)",
        "glow-strong": "var(--shadow-glow-strong)",
        /* Static violet glows for elements that must not follow the theme. */
        accent: "0 12px 34px -12px rgb(var(--brand-600) / 0.75)",
        "accent-strong": "0 18px 44px -12px rgb(var(--brand-500) / 0.9)",
      },
      backgroundImage: {
        "grid-fine":
          "linear-gradient(to right, rgb(var(--line) / 0.55) 1px, transparent 1px), linear-gradient(to bottom, rgb(var(--line) / 0.55) 1px, transparent 1px)",
        "sheen-violet":
          "linear-gradient(135deg, rgb(var(--brand-500) / 0.3) 0%, rgb(var(--brand-400) / 0) 58%)",
        "sheen-glass":
          "linear-gradient(135deg, rgb(var(--glass-teal) / 0.28) 0%, rgb(var(--brand-500) / 0) 62%)",
        "plate-top":
          "linear-gradient(180deg, rgb(var(--text-1) / 0.08) 0%, rgb(var(--text-1) / 0) 42%)",
        /** A hairline that fades at both ends — the site's own section rule. */
        "rule-fade":
          "linear-gradient(90deg, transparent, rgb(var(--brand-400) / 0.55) 22%, rgb(var(--glass-cyan) / 0.4) 50%, rgb(var(--brand-400) / 0.55) 78%, transparent)",
      },
      backgroundSize: {
        "grid-fine": "56px 56px",
        "grid-coarse": "112px 112px",
      },
      maxWidth: {
        /**
         * The layout is full-bleed; a text column is capped for readability
         * only. `measure` is the body-copy cap used inside full-width bands.
         */
        shell: "100%",
        measure: "46rem",
        lede: "38rem",
        prose: "68ch",
      },
      transitionTimingFunction: {
        industrial: "cubic-bezier(0.22, 0.61, 0.36, 1)",
        reveal: "cubic-bezier(0.16, 1, 0.3, 1)",
      },
      keyframes: {
        "reveal-up": {
          from: { opacity: "0", transform: "translate3d(0,18px,0)" },
          to: { opacity: "1", transform: "translate3d(0,0,0)" },
        },
        "fade-in": {
          from: { opacity: "0" },
          to: { opacity: "1" },
        },
        "scale-in": {
          from: { opacity: "0", transform: "scale(0.97)" },
          to: { opacity: "1", transform: "scale(1)" },
        },
        "sweep-x": {
          "0%": { transform: "translateX(-100%)" },
          "100%": { transform: "translateX(100%)" },
        },
        "drift-slow": {
          "0%,100%": { transform: "translate3d(0,0,0) scale(1)" },
          "50%": { transform: "translate3d(0,-14px,0) scale(1.02)" },
        },
        "pulse-soft": {
          "0%,100%": { opacity: "0.35" },
          "50%": { opacity: "0.75" },
        },
        "scan-line": {
          "0%": { transform: "translateY(0%)", opacity: "0" },
          "12%": { opacity: "1" },
          "88%": { opacity: "1" },
          "100%": { transform: "translateY(100%)", opacity: "0" },
        },
        "sheen-sweep": {
          "0%": { transform: "translateX(-120%)", opacity: "0" },
          "40%,60%": { opacity: "1" },
          "100%": { transform: "translateX(220%)", opacity: "0" },
        },
        "border-pan": {
          "0%": { backgroundPosition: "0% 50%" },
          "100%": { backgroundPosition: "200% 50%" },
        },
        "rise-in": {
          from: { opacity: "0", transform: "translate3d(0,10px,0)" },
          to: { opacity: "1", transform: "translate3d(0,0,0)" },
        },
      },
      animation: {
        "reveal-up": "reveal-up 0.7s cubic-bezier(0.16,1,0.3,1) both",
        "fade-in": "fade-in 0.5s ease-out both",
        "scale-in": "scale-in 0.55s cubic-bezier(0.16,1,0.3,1) both",
        "sweep-x": "sweep-x 2.6s cubic-bezier(0.4,0,0.2,1) infinite",
        "drift-slow": "drift-slow 12s ease-in-out infinite",
        "pulse-soft": "pulse-soft 5s ease-in-out infinite",
        "scan-line": "scan-line 5.5s cubic-bezier(0.4,0,0.6,1) infinite",
        "sheen-sweep": "sheen-sweep 6.5s cubic-bezier(0.4,0,0.2,1) infinite",
        "border-pan": "border-pan 7s linear infinite",
        "rise-in": "rise-in 0.32s cubic-bezier(0.16,1,0.3,1) both",
      },
    },
  },
  plugins: [],
};

export default config;
