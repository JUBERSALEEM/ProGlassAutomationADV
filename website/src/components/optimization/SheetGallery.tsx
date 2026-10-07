"use client";

import { useMemo, useState } from "react";
import { cn } from "@/lib/cn";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { SheetCard } from "@/components/optimization/SheetCard";
import type { PackMargins, PlateView } from "@/lib/guillotinePack";
import { useT } from "@/i18n/provider";

/**
 * Every plate of a run, small, several to a row.
 *
 * Modelled on the 2D View tab's Multi-Sheet Gallery
 * (`Views/Optimization/Tabs/TwoDViewTabView.xaml`): a wrapping row of miniature
 * sheet cards, a page size the reader chooses, and a pager. The application
 * paginates its gallery for exactly the reason this one does — a real cutting
 * list runs to more plates than fit on a screen, and a stack of full-size
 * drawings is unreadable long before it is complete.
 *
 * Two differences from the desktop gallery, both deliberate:
 *
 *   · The grid is responsive rather than a fixed 276 px card, because a browser
 *     window is not a fixed-width viewport. Same wrap, same pagination.
 *   · The card does not open a CAD view, because a website has no such view. It
 *     *focuses* its sheet on the canvas above instead, when the caller wires it
 *     up — the same job "Inspect CAD →" does, one click sooner, with no control
 *     that leads nowhere.
 *
 * What the reader gets either way is the thing the gallery is for: which plates
 * the job needs, how full each one is, and where the leftover glass is.
 */
export function SheetGallery({
  plates,
  margins,
  minBreakoutMm = 15,
  thicknessMm,
  /** Plates per page. The application offers 4, 8 and 12; these are the web sizes. */
  pageSize = 6,
  /** Show the pager. Off for a preview that is deliberately only a glance. */
  paginate = true,
  /** The plate currently on the canvas, when this gallery drives one. */
  selectedPlate,
  onSelectPlate,
  columns = "grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6",
  className,
}: {
  plates: PlateView[];
  margins: PackMargins;
  minBreakoutMm?: number;
  thicknessMm?: number;
  pageSize?: number;
  paginate?: boolean;
  selectedPlate?: number;
  onSelectPlate?: (plate: number) => void;
  /** Tailwind grid classes — the caller decides how dense a row should be. */
  columns?: string;
  className?: string;
}) {
  const t = useT();
  const [page, setPage] = useState(0);
  const pageCount = Math.max(1, Math.ceil(plates.length / pageSize));
  const safePage = Math.min(page, pageCount - 1);

  const shown = useMemo(
    () => plates.slice(safePage * pageSize, safePage * pageSize + pageSize),
    [plates, safePage, pageSize],
  );

  if (plates.length === 0) {
    return (
      <p className="surface p-5 text-sm leading-relaxed text-text-muted">
        {t("No plates to show — nothing was placed.")}
      </p>
    );
  }

  return (
    <div className={className}>
      <div className={cn("grid gap-3", columns)}>
        {shown.map((plate) => (
          <SheetCard
            key={plate.plate}
            t={t}
            plate={plate}
            margins={margins}
            minBreakoutMm={minBreakoutMm}
            thicknessMm={thicknessMm}
            /* One row of a pager is a preview; a single page of a short run is not. */
            compact={!paginate}
            selected={selectedPlate === plate.plate}
            onSelect={onSelectPlate ? () => onSelectPlate(plate.plate) : undefined}
          />
        ))}
      </div>

      {!paginate && plates.length > shown.length ? (
        <p className="mt-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
          {t("{v1} of {v2} sheets · the rest page through on", { v1: shown.length, v2: plates.length })}{" "}
          <Link href="/optimization" className="link-underline">
            {t("the engine page")}
          </Link>
        </p>
      ) : null}

      {paginate && pageCount > 1 ? (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
          <p className="font-mono text-2xs uppercase tracking-[0.14em] text-text-muted">
            {t("Sheets {from}–{to} of {total}", {
              from: safePage * pageSize + 1,
              to: safePage * pageSize + shown.length,
              total: plates.length,
            })}
          </p>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setPage(Math.max(0, safePage - 1))}
              disabled={safePage === 0}
              className="rounded-full border-2 border-line-strong/80 px-3 py-1 text-xs font-semibold text-text-secondary transition-colors hover:border-brand-400/60 hover:text-text-primary disabled:opacity-40 disabled:hover:border-line-strong/80 disabled:hover:text-text-secondary"
            >
              {t("Previous")}
            </button>
            <span className="font-mono text-xs tabular-nums text-text-faint">
              {safePage + 1} / {pageCount}
            </span>
            <button
              type="button"
              onClick={() => setPage(Math.min(pageCount - 1, safePage + 1))}
              disabled={safePage >= pageCount - 1}
              className="rounded-full border-2 border-line-strong/80 px-3 py-1 text-xs font-semibold text-text-secondary transition-colors hover:border-brand-400/60 hover:text-text-primary disabled:opacity-40 disabled:hover:border-line-strong/80 disabled:hover:text-text-secondary"
            >
              {t("Next")}
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
