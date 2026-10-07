import { marginZoneText } from "@/components/optimization/cad";
import type { PackMargins } from "@/lib/guillotinePack";
import type { Translator } from "@/i18n/core";

/**
 * The CAD key, transcribed from the footer of the application's 2D view
 * (`Views/Optimization/Tabs/TwoDViewTabView.xaml`): trim, breakout, saw cut,
 * piece, a rotated piece, and a reusable offcut.
 *
 * Each swatch is drawn with the same class the drawing itself uses, so the key
 * cannot describe a colour the plate does not have.
 *
 * The line above the swatches is the application's own status text — the
 * margins and blade gap the plan was cut to — spelled out rather than left to be
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
    { label: t("Trim"), swatch: "border-glass-teal bg-transparent" },
    { label: t("Breakout"), swatch: "border-state-warning/70 bg-transparent" },
    { label: t("Saw cut"), swatch: "border-line-strong bg-viz-plate" },
    { label: t("Piece"), swatch: "border-brand-200/70 bg-viz-part" },
    { label: t("Rotated 90° ⟳"), swatch: "border-glass-cyan bg-viz-part-2" },
    { label: t("Reusable offcut"), swatch: "border-dashed border-brand-300 bg-brand-400/20" },
  ];

  return (
    <div className={className}>
      <p className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
        {t(marginZoneText(margins))}
      </p>
      <ul className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-2">
        {entries.map((entry) => (
          <li key={entry.label} className="flex items-center gap-2">
            <span aria-hidden="true" className={`h-3 w-3 rounded-[0.2rem] border-2 ${entry.swatch}`} />
            <span className="text-xs font-medium text-text-muted">{entry.label}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
