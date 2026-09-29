import type { Config } from "tailwindcss";

/**
 * Design tokens are derived from the actual ProGlass Automation application
 * theme (Themes/DarkTheme.xaml) and its design system
 * (design-system/proglass-industrial/MASTER.md).
 *
 *   AccentColor          #38BDF8   (DarkTheme.xaml)
 *   AccentDarkColor      #0EA5E9   (DarkTheme.xaml)
 *   AccentHoverColor     #7DD3FC   (DarkTheme.xaml)
 *   Background           #0F172A   (DarkTheme.xaml BackgroundBrush)
 *   ModuleBackground     #0B1220   (DarkTheme.xaml ModuleBackgroundColor)
 *   Success              #34D399   (DarkTheme.xaml SuccessColor)
 *   Warning              #F59E0B   (DarkTheme.xaml WarningColor)
 *   Danger               #FB7185   (DarkTheme.xaml DangerColor)
 *   Heading/body font    Inter     (design-system master file)
 */
const config: Config = {
  content: ["./src/**/*.{ts,tsx,mdx}"],
  theme: {
    extend: {
      colors: {
        ink: {
          /** Page background — the deepest tier of the surface system. */
          950: "#070B14",
          900: "#0B1120",
          850: "#0F172A",
          800: "#111C2E",
          700: "#16223A",
        },
        line: {
          DEFAULT: "#1E293B",
          soft: "#172033",
          strong: "#2C3E56",
        },
        brand: {
          50: "#F0F9FF",
          200: "#BAE6FD",
          300: "#7DD3FC",
          400: "#38BDF8",
          500: "#0EA5E9",
          600: "#0284C7",
          700: "#0369A1",
        },
        glass: {
          teal: "#2DD4BF",
          cyan: "#22D3EE",
          green: "#4ADE80",
        },
        text: {
          primary: "#F8FAFC",
          secondary: "#CBD5E1",
          muted: "#94A3B8",
          faint: "#64748B",
        },
        state: {
          success: "#34D399",
          warning: "#F59E0B",
          danger: "#FB7185",
          info: "#38BDF8",
        },
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
        panel: "0 1px 0 0 rgba(248,250,252,0.03) inset, 0 24px 48px -24px rgba(2,6,23,0.9)",
        raised: "0 32px 80px -32px rgba(2,6,23,0.95), 0 0 0 1px rgba(56,189,248,0.06)",
        glow: "0 0 0 1px rgba(56,189,248,0.18), 0 24px 64px -28px rgba(56,189,248,0.28)",
        "glow-strong": "0 0 0 1px rgba(56,189,248,0.3), 0 32px 80px -24px rgba(56,189,248,0.4)",
      },
      backgroundImage: {
        "grid-fine":
          "linear-gradient(to right, rgba(30,41,59,0.55) 1px, transparent 1px), linear-gradient(to bottom, rgba(30,41,59,0.55) 1px, transparent 1px)",
        "sheen-sky":
          "linear-gradient(135deg, rgba(56,189,248,0.14) 0%, rgba(56,189,248,0) 55%)",
        "plate-top":
          "linear-gradient(180deg, rgba(248,250,252,0.06) 0%, rgba(248,250,252,0) 42%)",
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
      },
      animation: {
        "reveal-up": "reveal-up 0.7s cubic-bezier(0.16,1,0.3,1) both",
        "fade-in": "fade-in 0.5s ease-out both",
        "scale-in": "scale-in 0.55s cubic-bezier(0.16,1,0.3,1) both",
        "sweep-x": "sweep-x 2.6s cubic-bezier(0.4,0,0.2,1) infinite",
        "drift-slow": "drift-slow 12s ease-in-out infinite",
        "pulse-soft": "pulse-soft 5s ease-in-out infinite",
        "scan-line": "scan-line 5.5s cubic-bezier(0.4,0,0.6,1) infinite",
      },
    },
  },
  plugins: [],
};

export default config;
