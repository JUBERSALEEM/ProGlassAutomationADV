"use client";

import { EmptyState } from "@/components/ui/EmptyState";
import { InquiryCard } from "@/components/inquiries/InquiryCard";
import type { InquiryListRow } from "@/types/inquiries";

/**
 * The card grid, with the two states that replace it.
 *
 * A first load shows the shape of six cards rather than a blank page, and an
 * empty result says why it is empty and offers the way back. The grid is one
 * column on a phone, two on a tablet and three on a wide screen, from the same
 * card component, so there is no second layout to keep in step.
 */
export function InboxCards({
  rows,
  loading,
  onReset,
  filtered = true,
}: {
  rows: InquiryListRow[];
  loading: boolean;
  onReset: () => void;
  /**
   * Whether a filter is narrowing the register. An empty register with no
   * filters is a different sentence from an empty result: the desktop register
   * is authored by the Windows bridge, and the website queue is a separate page
   * that the inbox now counts above the cards.
   */
  filtered?: boolean;
}) {
  if (loading && rows.length === 0) {
    return (
      <div aria-label="Loading inquiries" aria-busy="true" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {Array.from({ length: 6 }).map((_, index) => (
          <div key={index} className="h-56 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60" />
        ))}
      </div>
    );
  }

  if (rows.length === 0) {
    return (
      <EmptyState
        glyph="M4 12h16M12 4v16"
        title={filtered ? "No inquiries match these filters" : "The desktop register is empty"}
        body={
          filtered
            ? "Clear one or more filters, or wait for the authorized Windows bridge to publish a complete source scan."
            : "This register is published by the Windows bridge and fills when an accepted website submission is downloaded there. The website queue holds every complete submission already, in every review state."
        }
        action={
          filtered ? (
            <button
              type="button"
              onClick={onReset}
              className="mt-4 rounded-xl border border-line-strong px-4 py-2 text-xs font-semibold text-brand-300"
            >
              Reset filters
            </button>
          ) : undefined
        }
      />
    );
  }

  return (
    <section aria-label="Published inquiries" aria-busy={loading} className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {rows.map((row) => (
        <InquiryCard key={row.id} row={row} />
      ))}
    </section>
  );
}
