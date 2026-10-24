"use client";

import { cn } from "@/lib/cn";
import { statusLabel } from "@/lib/inquiry-submission-status";
import { CUSTOMER_INQUIRY_STATUSES, type CustomerInquiryStatus, type CustomerInquiryStatusCounts } from "@/types/inquiry-submissions";

export type StatusFilter = CustomerInquiryStatus | "";

/**
 * The status filter, rebuilt as a tally.
 *
 * Every one of the six statuses is present, in the database's own order, and
 * each carries the count the server returned for it. The old control was a
 * `<select>` typed out by hand, which is how `downloaded` came to be missing:
 * a list written twice drifts.
 */
export function SubmissionStatusTabs({
  counts,
  allTotal,
  active,
  busy,
  onSelect,
}: {
  counts: CustomerInquiryStatusCounts | null;
  allTotal: number;
  active: StatusFilter;
  busy: boolean;
  onSelect: (status: StatusFilter) => void;
}) {
  const tabs: Array<{ value: StatusFilter; label: string; count: number }> = [
    { value: "", label: "All statuses", count: allTotal },
    ...CUSTOMER_INQUIRY_STATUSES.map((status) => ({ value: status, label: statusLabel(status), count: counts?.[status] ?? 0 })),
  ];

  return (
    <nav aria-label="Filter submissions by status" className="flex gap-2 overflow-x-auto pb-1">
      {tabs.map((tab) => {
        const selected = active === tab.value;
        return (
          <button
            key={tab.value || "all"}
            type="button"
            aria-pressed={selected}
            disabled={busy}
            onClick={() => onSelect(tab.value)}
            className={cn(
              "shrink-0 rounded-xl border px-3 py-2 text-xs font-semibold transition-colors",
              selected
                ? "border-brand-400/70 bg-brand-500/15 text-brand-200"
                : "border-line-strong/60 text-text-muted hover:border-line-strong hover:text-text-primary",
              !selected && tab.count === 0 ? "opacity-60" : "",
            )}
          >
            {tab.label}
            <span className={cn("ml-2 font-mono text-3xs", selected ? "text-brand-200" : "text-text-faint")}>{tab.count}</span>
          </button>
        );
      })}
    </nav>
  );
}
