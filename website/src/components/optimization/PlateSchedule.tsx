import type { PlateView } from "@/lib/guillotinePack";
import { scheduleFor } from "@/components/optimization/cad";
import { cn } from "@/lib/cn";
import type { Translator } from "@/i18n/core";

/**
 * The piece cutting schedule for one sheet.
 *
 * The application's own table, column for column and in its order
 * (`TwoDViewTabView.xaml` → Piece Cutting Schedule Matrix): sequence number,
 * cut tag, piece reference, the size as placed, whether it was turned, and its
 * area — on the application's light panel, in its keylines and its ink.
 *
 * One column is missing on purpose. The application's seventh column carries the
 * glass specification the run was composed from; this sample job is a single
 * thickness of one glass, so the specification is stated once in the panel
 * header instead of being repeated down thirty rows. The caption says so,
 * because a reader comparing this with the application should know which column
 * they are not looking at.
 */
export function PlateSchedule({
  t,
  plate,
  className,
}: {
  t: Translator;
  plate: PlateView;
  className?: string;
}) {
  const rows = scheduleFor(plate);

  return (
    <div className={cn("studio-sheet flex flex-col p-4 sm:p-5", className)}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="font-mono text-xs font-semibold uppercase tracking-[0.1em]" style={{ color: "var(--cad-text)" }}>
          {t("Piece cutting schedule for this sheet")}
        </h3>
        <p className="studio-label">
          {t("Total sheet pieces: {v1}", { v1: rows.length })}
        </p>
      </div>

      {/*
        Scrolled sideways rather than squeezed: six columns of figures in a
        narrow column is how a schedule becomes unreadable. The application
        scrolls this table too.
      */}
      <div className="studio-inset mt-4 max-h-[26rem] overflow-auto p-1">
        <table className="studio-table w-full min-w-[26rem] border-separate border-spacing-0 text-sm">
          <caption className="sr-only">
            {t("Cut sequence, references and sizes of the pieces on this sheet")}
          </caption>
          <thead>
            <tr className="text-left">
              {["Seq #", "Cut tag", "Piece ref", "Dimensions (W×H)", "Rotated", "Area"].map((heading) => (
                <th
                  key={heading}
                  scope="col"
                  className="sticky top-0 px-3 py-2 font-mono text-2xs uppercase tracking-[0.12em] font-medium"
                  style={{ backgroundColor: "var(--cad-canvas)" }}
                >
                  {t(heading)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.cutTag}>
                <td className="px-3 py-2 font-mono tabular-nums" style={{ color: "var(--cad-muted)" }}>
                  {row.sequence}
                </td>
                <td className="px-3 py-2">
                  <span className="studio-chip px-2 py-0.5 text-[0.6875rem]">{t.template`Cut #${row.sequence}`}</span>
                </td>
                <td className="px-3 py-2 font-semibold">{row.reference}</td>
                <td className="px-3 py-2 font-mono font-semibold tabular-nums">{row.dimensions}</td>
                <td className="px-3 py-2" style={{ color: "var(--cad-muted)" }}>
                  {t(row.rotated)}
                </td>
                <td className="px-3 py-2 font-mono tabular-nums" style={{ color: "var(--cad-muted)" }}>
                  {row.area}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="mt-4 text-xs font-medium leading-relaxed" style={{ color: "var(--cad-subtle)" }}>
        {t("The sequence is the order the packer placed the pieces, which is the order the schedule lists and the operator works through. The application's table carries a seventh column, the glass specification the run was composed from; this sample job is one thickness, so it is stated in the panel header instead.")}
      </p>
    </div>
  );
}
