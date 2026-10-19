"use client";

import { setFamily, useAppearance } from "@/lib/useTheme";
import { setThemeMode, useThemeMode } from "@/lib/useTheme";
import { appearanceFamilies, type AppearanceFamily, type ThemeMode } from "@/data/themes";
import { useT } from "@/i18n/provider";
import { cn } from "@/lib/cn";

const modes: { id: Exclude<ThemeMode, "system">; label: string; detail: string }[] = [
  { id: "light", label: "Light", detail: "Bright, low-glare surfaces" },
  { id: "dark", label: "Dark", detail: "Deep, controlled contrast" },
];

const summaries: Record<AppearanceFamily, { number: string; material: string; motion: string }> = {
  bento: { number: "01", material: "Structured panels", motion: "Quiet transitions" },
  spatial: { number: "02", material: "Layered depth", motion: "Gentle parallax-free lift" },
  swiss: { number: "03", material: "Editorial precision", motion: "Purposeful, minimal" },
  holographic: { number: "04", material: "Prismatic accents", motion: "Lightweight edge shimmer" },
  neumorphism: { number: "05", material: "Soft extrusion", motion: "Subtle press feedback" },
  clay: { number: "06", material: "Sculpted solid surfaces", motion: "Soft spring, no blur" },
  "liquid-glass": { number: "07", material: "Luminous liquid panes", motion: "Restrained fluid movement" },
  glassmorphism: { number: "08", material: "Frosted layered panes", motion: "Short, smooth reveals" },
};

export function ThemeCenter() {
  const t = useT();
  const activeFamily = useAppearance();
  const mode = useThemeMode();

  return (
    <div className="theme-center">
      <section className="theme-center-toolbar" aria-label={t("Theme appearance controls")}>
        <div>
          <p className="mono-label">{t("16 appearance combinations")}</p>
          <h2 className="mt-1 text-xl font-semibold text-text-primary">{t("Choose your material")}</h2>
          <p className="mt-1 max-w-xl text-sm leading-relaxed text-text-muted">
            {t("Choose a visual family, then choose Light or Dark. Your selection updates the live website and is saved for your next visit.")}
          </p>
        </div>
        <div className="theme-center-mode" role="group" aria-label={t("Appearance mode")}>
          {modes.map((entry) => (
            <button
              key={entry.id}
              type="button"
              aria-pressed={mode === entry.id}
              onClick={() => setThemeMode(entry.id)}
              className={cn("theme-center-mode-button", mode === entry.id && "is-active")}
            >
              <span>{t(entry.label)}</span>
              <small>{t(entry.detail)}</small>
            </button>
          ))}
        </div>
      </section>

      <div className="theme-center-grid" aria-label={t("Eight theme families")}>
        {appearanceFamilies.map((entry) => {
          const active = activeFamily === entry.id;
          const detail = summaries[entry.id];
          return (
            <article key={entry.id} className={cn("theme-center-card", active && "is-selected")}>
              <button
                type="button"
                className="theme-center-select"
                aria-pressed={active}
                onClick={() => setFamily(entry.id)}
              >
                <div
                  className="theme-center-mini"
                  data-theme={mode === "dark" ? "midnight" : "daylight"}
                  data-appearance={entry.id}
                  aria-hidden="true"
                >
                  <div className="theme-center-mini-top">
                    <span className="theme-center-mini-mark" style={{ background: entry.swatch }} />
                    <span className="theme-center-mini-lines"><i /><i /></span>
                    <span className="theme-center-mini-dot" />
                  </div>
                  <div className="theme-center-mini-layout">
                    <div className="theme-center-mini-rail"><i /><i /><i /><i /></div>
                    <div className="theme-center-mini-content">
                      <div className="theme-center-mini-heading" />
                      <div className="theme-center-mini-stats">
                        <span /><span /><span />
                      </div>
                      <div className="theme-center-mini-pane">
                        <i /><i /><i />
                      </div>
                    </div>
                  </div>
                </div>
                <div className="theme-center-card-copy">
                  <div className="flex items-center justify-between gap-3">
                    <span className="mono-label">{detail.number} / 08</span>
                    <span className={cn("theme-center-check", active && "is-active")} aria-hidden="true">
                      {active ? "✓" : ""}
                    </span>
                  </div>
                  <h3>{t(entry.label)}</h3>
                  <p>{t(entry.origin)}</p>
                  <div className="theme-center-tags">
                    <span>{t(detail.material)}</span>
                    <span>{t(detail.motion)}</span>
                  </div>
                </div>
              </button>
            </article>
          );
        })}
      </div>
      <p className="theme-center-footnote">
        {t("Performance rule: blur is limited to selected glass surfaces, and motion respects your operating system’s reduced-motion setting.")}
      </p>
    </div>
  );
}
