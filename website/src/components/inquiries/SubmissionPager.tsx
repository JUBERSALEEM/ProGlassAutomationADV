"use client";

import { formatNumber } from "@/lib/inquiry-inbox-view";

/**
 * The footer of the register: what is on screen, out of how many.
 *
 * "7 complete submissions" with no range was the old page's whole answer, so
 * an operator could not tell a short queue from a truncated one. The range is
 * printed from the server's count, and the two buttons exist only when there
 * is a page to move to.
 */
export function SubmissionPager({
  page,
  pageCount,
  total,
  from,
  to,
  busy,
  onPage,
}: {
  page: number;
  pageCount: number;
  total: number;
  from: number;
  to: number;
  busy: boolean;
  onPage: (page: number) => void;
}) {
  const button = "rounded-lg border border-line-strong px-3 py-1.5 text-xs font-semibold text-text-secondary disabled:opacity-40";

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-line-strong/60 bg-ink-900/80 px-4 py-3">
      <p className="text-xs text-text-muted">
        Showing <span className="font-mono text-text-primary">{formatNumber(from)}</span>
        {" to "}
        <span className="font-mono text-text-primary">{formatNumber(to)}</span> of{" "}
        <span className="font-mono text-text-primary">{formatNumber(total)}</span> complete submission
        {total === 1 ? "" : "s"}
        {pageCount > 1 ? <> · page {page} of {pageCount}</> : null}
      </p>
      {pageCount > 1 ? (
        <nav aria-label="Submission pages" className="flex items-center gap-2">
          <button type="button" className={button} disabled={busy || page <= 1} onClick={() => onPage(page - 1)}>
            Previous
          </button>
          <button type="button" className={button} disabled={busy || page >= pageCount} onClick={() => onPage(page + 1)}>
            Next
          </button>
        </nav>
      ) : null}
    </div>
  );
}
