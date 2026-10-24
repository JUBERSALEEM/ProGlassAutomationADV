"use client";

import { formatNumber } from "@/lib/inquiry-inbox-view";
import type { InquiryListData } from "@/types/inquiries";

/**
 * Four values, all of them returned by the API.
 *
 * The counts are global: they describe the whole publication, not the current
 * filter. The label says so, so nobody reads a filtered total into them.
 */
export function InboxSummary({ summary }: { summary: InquiryListData["summary"] }) {
  const items: Array<{ label: string; value: number }> = [
    { label: "Published", value: summary.total },
    { label: "Active", value: summary.active },
    { label: "Confirmed", value: summary.confirmed },
    { label: "With documents", value: summary.withDocuments },
  ];
  return (
    <section aria-label="Publication summary, all inquiries" className="dense-card-grid grid grid-cols-2 gap-2 sm:grid-cols-4">
      {items.map((item) => (
        <div key={item.label} className="rounded-xl border border-line-strong/50 bg-ink-900/70 px-3 py-2">
          <div className="text-3xs font-semibold uppercase tracking-wider text-text-faint">{item.label}</div>
          <div className="mt-0.5 font-mono text-lg font-bold leading-tight text-text-primary">{formatNumber(item.value)}</div>
        </div>
      ))}
    </section>
  );
}
