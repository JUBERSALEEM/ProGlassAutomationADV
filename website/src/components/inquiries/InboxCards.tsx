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
}: {
  rows: InquiryListRow[];
  loading: boolean;
  onReset: () => void;
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
        title="No inquiries match these filters"
        body="Clear one or more filters, or wait for the authorized Windows bridge to publish a complete source scan."
        action={
          <button
            type="button"
            onClick={onReset}
            className="mt-4 rounded-xl border border-line-strong px-4 py-2 text-xs font-semibold text-brand-300"
          >
            Reset filters
          </button>
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
