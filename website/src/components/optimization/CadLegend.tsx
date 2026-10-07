import { marginZoneText } from "@/components/optimization/cad";
import type { PackMargins } from "@/lib/guillotinePack";
import type { Translator } from "@/i18n/core";

/**
 * The CAD key, transcribed from the footer of the application's 2D view
 * (`Views/Optimization/Tabs/TwoDViewTabView.xaml`): trim, breakout, saw cut,
 * piece, a rotated piece, and a reusable offcut.
 *
 * Each swatch is a two-centimetre drawing using the *same* class the plate uses,
 * so the key cannot describe a colour the drawing does not have — and it sits on
 * the same light panel, because that is where the application puts it.
 *
 * The line above the swatches is the application's own status text — the margins
 * and blade gap the plan was cut to — spelled out rather than left to be
 * inferred from the dashes.
 */
export function CadLegend({
  t,
  margins,
  className,
}: {
  t: Translator;
  margins: PackMargins;
  className?: string;
}) {
  const entries: { label: string; swatch: string }[] = [
    { label: t("Trim"), swatch: "cad-trim" },
    { label: t("Breakout"), swatch: "cad-breakout" },
    { label: t("Saw cut"), swatch: "cad-bed" },
    { label: t("Piece"), swatch: "cad-piece" },
    { label: t("Rotated 90° ⟳"), swatch: "cad-piece-rotated" },
    { label: t("Reusable offcut"), swatch: "cad-offcut" },
  ];

  return (
    <div className={className}>
      <p className="studio-label">{t(marginZoneText(margins))}</p>
      <ul className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-2">
        {entries.map((entry) => (
          <li key={entry.label} className="flex items-center gap-2">
            <svg aria-hidden="true" viewBox="0 0 12 12" className="h-3 w-3">
              <rect
                x="0.6"
                y="0.6"
                width="10.8"
                height="10.8"
                rx="1.6"
                className={entry.swatch}
                strokeWidth="1.2"
                strokeDasharray={entry.swatch === "cad-offcut" ? "2 1.6" : undefined}
              />
            </svg>
            <span className="text-xs font-medium" style={{ color: "var(--cad-muted)" }}>
              {entry.label}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
