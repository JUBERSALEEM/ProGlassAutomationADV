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
 * area.
 *
 * One column is missing on purpose. The application's seventh column carries
 * the glass specification the run was composed from; this sample job is a single
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
    <div className={cn("plate flex flex-col p-4 sm:p-5", className)}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="text-sm font-semibold text-text-primary">
          {t("Piece cutting schedule for this sheet")}
        </h3>
        <p className="font-mono text-2xs uppercase tracking-[0.12em] text-text-faint font-medium">
          {t("Total sheet pieces: {v1}", { v1: rows.length })}
        </p>
      </div>

      {/*
        Scrolled sideways rather than squeezed: six columns of figures in a
        narrow column is how a schedule becomes unreadable. The application
        scrolls this table too.
      */}
      <div className="mt-4 -mx-1 overflow-x-auto px-1">
        <table className="w-full min-w-[26rem] border-separate border-spacing-0 text-sm">
          <caption className="sr-only">
            {t("Cut sequence, references and sizes of the pieces on this sheet")}
          </caption>
          <thead>
            <tr className="text-left">
              {[
                "Seq #",
                "Cut tag",
                "Piece ref",
                "Dimensions (W×H)",
                "Rotated",
                "Area",
              ].map((heading) => (
                <th
                  key={heading}
                  scope="col"
                  className="border-b border-line-strong/70 px-3 py-2 font-mono text-2xs uppercase tracking-[0.12em] text-text-faint font-medium"
                >
                  {t(heading)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.cutTag} className="transition-colors hover:bg-ink-800/40">
                <td className="border-b border-line/50 px-3 py-2 font-mono tabular-nums text-text-muted">
                  {row.sequence}
                </td>
                <td className="border-b border-line/50 px-3 py-2">
                  <span className="rounded-md border border-brand-400/45 bg-brand-500/14 px-2 py-0.5 font-mono text-[0.6875rem] font-bold text-brand-100">
                    {row.cutTag}
                  </span>
                </td>
                <td className="border-b border-line/50 px-3 py-2 font-semibold text-text-primary">
                  {row.reference}
                </td>
                <td className="border-b border-line/50 px-3 py-2 font-mono font-semibold tabular-nums text-text-primary">
                  {row.dimensions}
                </td>
                <td className="border-b border-line/50 px-3 py-2 text-text-secondary">
                  {t(row.rotated)}
                </td>
                <td className="border-b border-line/50 px-3 py-2 font-mono tabular-nums text-text-secondary">
                  {row.area}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="mt-4 text-xs font-medium leading-relaxed text-text-faint">
        {t("The sequence is the order the packer placed the pieces, which is the order the schedule lists and the operator works through. The application's table carries a seventh column, the glass specification the run was composed from; this sample job is one thickness, so it is stated in the panel header instead.")}
      </p>
    </div>
  );
}
