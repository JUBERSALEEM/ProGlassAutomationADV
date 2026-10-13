"use client";

import { useState } from "react";
import { houseMarginProfiles } from "@/data/optimization";
import { SampleTag } from "@/components/product/previewAtoms";
import { useT } from "@/i18n/provider";

/**
 * The SETTINGS tab, as a preview.
 *
 * Taken from `Views/Optimization/Tabs/SettingsTabView.xaml` and drawn on the
 * application's own light panel — the same `OptBentoSurface`, `OptBentoBorder`
 * and `OptBentoInk` the desktop screen uses, so the two screens read as one
 * product. Its two groups are here: `PLATE GEOMETRY & TRIM MARGINS` (glass
 * thickness and the four safety edges) and `CUT RULES & SEARCH EFFORT` (kerf,
 * breakout, complexity, rotation, effort, plate pattern, offcut reuse).
 *
 * One control here is real, because one control here is worth being real: the
 * thickness. In the application the house profile resolves *from* the thickness
 * (`MarginProfile.DefaultFor`), and an untouched Settings tab follows it — pick
 * 6 mm and the trims are 15 mm all round with a 3 mm blade gap; pick 10 mm and
 * they are 50 / 50 / 0 / 50 with a 4 mm gap. Everything else is printed as the
 * value it holds rather than dressed up as an input that does nothing, which is
 * the rule this site holds every preview to.
 */
export function OptimizerSettingsSurface() {
  const t = useT();
  const thicknesses = [6, 8, 10] as const;
  const [thickness, setThickness] = useState<(typeof thicknesses)[number]>(6);

  const profile =
    thickness <= houseMarginProfiles.thin.thicknessMm
      ? houseMarginProfiles.thin
      : thickness <= houseMarginProfiles.medium.thicknessMm
        ? houseMarginProfiles.medium
        : houseMarginProfiles.thick;

  const bucket =
    thickness <= houseMarginProfiles.thin.thicknessMm
      ? t("Thin")
      : thickness <= houseMarginProfiles.medium.thicknessMm
        ? t("Medium")
        : t("Thick");

  const rows: { label: string; value: string }[] = [
    { label: t("Left margin LM (mm)"), value: String(profile.left) },
    { label: t("Right margin RM (mm)"), value: String(profile.right) },
    { label: t("Top margin TM (mm)"), value: String(profile.top) },
    { label: t("Bottom margin BM (mm)"), value: String(profile.bottom) },
    { label: t("Kerf / blade gap (mm)"), value: String(profile.kerf) },
    {
      label: t("Minimum breakout (mm)"),
      value:
        profile.breakout === houseMarginProfiles.thick.breakout
          ? t("{v1} — switchable to {v2}", { v1: profile.breakout, v2: 40 })
          : String(profile.breakout),
    },
    { label: t("Allow rotation"), value: t("On — a piece turns 0° / 90° adaptively to fit") },
    { label: t("Optimize effort / strategy stage"), value: t("1 to 5, or Auto") },
    { label: t("Plate pattern"), value: t("Six patterns, applied once the limits are equal") },
    { label: t("Offcut reuse"), value: t("Off by default — it moves pieces, so it is asked for") },
  ];

  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <span className="studio-label">{t("Settings · margins and cut rules")}</span>
      </div>

      <div>
        <p className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] font-semibold" style={{ color: "var(--cad-text)" }}>
          {t("Optimization engine configuration")}
        </p>
        <p className="mt-1 text-[0.75rem] leading-relaxed font-medium" style={{ color: "var(--cad-subtle)" }}>
          {t("Configure guillotine cut allowances, trim safety margins, kerf blade width and search intensity.")}
        </p>
      </div>

      <div className="studio-sheet p-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] font-semibold" style={{ color: "var(--cad-muted)" }}>
            {t("Plate geometry & trim margins")}
          </p>
          <span className="studio-chip text-[0.625rem] py-0.5">{t("Safety edges")}</span>
        </div>

        <label className="mt-3 flex flex-wrap items-center gap-2">
          <span className="studio-label">{t("Glass thickness (mm)")}</span>
          <select
            value={thickness}
            onChange={(event) => setThickness(Number(event.target.value) as (typeof thicknesses)[number])}
            className="studio-inset px-2 py-1 text-[0.75rem]"
          >
            {thicknesses.map((value) => (
              <option key={value} value={value}>
                {t("{v1} mm", { v1: value })}
              </option>
            ))}
          </select>
          <span className="studio-chip text-[0.625rem] py-0.5">{t("House {v1} profile", { v1: bucket })}</span>
        </label>

        <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-2">
          {rows.slice(0, 4).map((row) => (
            <div
              key={row.label}
              className="flex items-baseline justify-between gap-2 pb-1"
              style={{ borderBottom: "1px solid var(--cad-keyline)" }}
            >
              <dt className="studio-label">{row.label}</dt>
              <dd className="font-mono text-[0.8125rem] font-bold tabular-nums" style={{ color: "var(--cad-text)" }}>
                {row.value}
              </dd>
            </div>
          ))}
        </dl>
      </div>

      <div className="studio-sheet p-3">
        <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] font-semibold" style={{ color: "var(--cad-muted)" }}>
          {t("Cut rules & search effort")}
        </p>
        <dl className="mt-3 space-y-1.5">
          {rows.slice(4).map((row) => (
            <div
              key={row.label}
              className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-0.5 pb-1 last:border-0"
              style={{ borderBottom: "1px solid var(--cad-keyline)" }}
            >
              <dt className="studio-label">{row.label}</dt>
              <dd className="text-[0.75rem] font-semibold" style={{ color: "var(--cad-text)" }}>
                {row.value}
              </dd>
            </div>
          ))}
        </dl>
      </div>

      <p className="text-[0.75rem] leading-relaxed font-medium" style={{ color: "var(--cad-subtle)" }}>
        {t("The trims and the blade gap are the application's own house defaults, resolved from the glass thickness; the setting itself is the thickness. Everything below it is printed as the value it holds in this preview rather than offered as a control that would not change anything.")}
      </p>
    </div>
  );
}
