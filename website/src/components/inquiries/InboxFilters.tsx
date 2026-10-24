"use client";

import { useState } from "react";
import { activeFilterCount, type InboxFilters as InboxFilterState } from "@/lib/inquiry-inbox-view";
import type { InquiryPriority, InquiryStatus } from "@/types/inquiries";

const STATUSES: Array<InquiryStatus | ""> = ["", "Received", "Prepared", "Sent", "Confirmed", "Hold", "Cancelled"];
const PRIORITIES: Array<InquiryPriority | ""> = ["", "Urgent", "High", "Normal", "Low"];

/**
 * The filter bar.
 *
 * Search submits on enter or the button; the two selects apply as soon as they
 * change. Every change goes through the caller, which writes it to the URL and
 * asks the server again. No rows are filtered in the browser.
 */
export function InboxFilters({
  filters,
  busy,
  onChange,
  onReset,
}: {
  filters: InboxFilterState;
  busy: boolean;
  onChange: (next: Partial<InboxFilterState>) => void;
  onReset: () => void;
}) {
  const [search, setSearch] = useState(filters.search);
  const count = activeFilterCount(filters);

  return (
    <section aria-label="Inquiry filters" className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3 shadow-panel sm:p-4">
      <form
        className="grid gap-2 sm:grid-cols-2 lg:grid-cols-[minmax(14rem,1fr)_10rem_9rem_auto]"
        onSubmit={(event) => {
          event.preventDefault();
          onChange({ search: search.trim(), page: 1 });
        }}
      >
        <label className="flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
          Search
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Inquiry, customer, project, PI or JO"
            className="h-9 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary placeholder:text-text-faint"
          />
        </label>
        <label className="flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
          Status
          <select
            value={filters.status}
            onChange={(event) => onChange({ status: event.target.value, page: 1 })}
            className="h-9 w-full rounded-xl border border-line-strong bg-ink-950 px-2 text-xs normal-case tracking-normal text-text-primary"
          >
            {STATUSES.map((value) => (
              <option key={value || "all"} value={value}>{value || "All statuses"}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-3xs font-semibold uppercase tracking-wider text-text-faint">
          Priority
          <select
            value={filters.priority}
            onChange={(event) => onChange({ priority: event.target.value, page: 1 })}
            className="h-9 w-full rounded-xl border border-line-strong bg-ink-950 px-2 text-xs normal-case tracking-normal text-text-primary"
          >
            {PRIORITIES.map((value) => (
              <option key={value || "all"} value={value}>{value || "All priorities"}</option>
            ))}
          </select>
        </label>
        <button
          type="submit"
          disabled={busy}
          className="h-9 self-end rounded-xl bg-brand-400 px-4 text-xs font-bold text-accent-ink hover:bg-brand-300 disabled:opacity-60"
        >
          {busy ? "Searching" : "Search"}
        </button>
      </form>
      <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
        <label className="flex items-center gap-2 text-xs text-text-muted">
          <input
            type="checkbox"
            checked={filters.hasDocuments}
            onChange={(event) => onChange({ hasDocuments: event.target.checked, page: 1 })}
            className="accent-brand-400"
          />
          Has published documents
        </label>
        <div className="flex items-center gap-3">
          {count > 0 ? <span className="text-3xs text-text-faint">{count} filter{count === 1 ? "" : "s"} on</span> : null}
          <button
            type="button"
            onClick={() => {
              setSearch("");
              onReset();
            }}
            disabled={count === 0}
            className="text-xs font-semibold text-text-faint underline-offset-4 hover:text-brand-200 hover:underline disabled:opacity-50 disabled:hover:no-underline"
          >
            Reset filters
          </button>
        </div>
      </div>
    </section>
  );
}
