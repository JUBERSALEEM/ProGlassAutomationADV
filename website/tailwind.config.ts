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
      },
      fontFamily: {
        sans: ["var(--font-inter)", "Segoe UI", "system-ui", "sans-serif"],
        mono: ["var(--font-mono)", "ui-monospace", "Consolas", "monospace"],
      },
      fontSize: {
        "2xs": ["0.6875rem", { lineHeight: "1rem", letterSpacing: "0.06em" }],
      },
      borderRadius: {
        card: "1rem",
        panel: "1.25rem",
        hero: "1.5rem",
      },
      boxShadow: {
        panel: "var(--shadow-panel)",
        raised: "var(--shadow-raised)",
        glow: "var(--shadow-glow)",
        "glow-strong": "var(--shadow-glow-strong)",
        /* Static violet glows for elements that must not follow the theme. */
        accent: "0 10px 30px -12px rgb(139 92 246 / 0.65)",
        "accent-strong": "0 16px 38px -12px rgb(139 92 246 / 0.8)",
      },
      backgroundImage: {
        "grid-fine":
          "linear-gradient(to right, rgb(var(--line) / 0.55) 1px, transparent 1px), linear-gradient(to bottom, rgb(var(--line) / 0.55) 1px, transparent 1px)",
        "sheen-violet":
          "linear-gradient(135deg, rgb(var(--brand-400) / 0.16) 0%, rgb(var(--brand-400) / 0) 55%)",
        "sheen-glass":
          "linear-gradient(135deg, rgb(var(--glass-teal) / 0.16) 0%, rgb(var(--brand-500) / 0) 60%)",
        "plate-top":
          "linear-gradient(180deg, rgb(var(--text-1) / 0.06) 0%, rgb(var(--text-1) / 0) 42%)",
      },
      backgroundSize: {
        "grid-fine": "56px 56px",
        "grid-coarse": "112px 112px",
      },
      maxWidth: {
        shell: "84rem",
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
      },
    },
  },
  plugins: [],
};

export default config;
