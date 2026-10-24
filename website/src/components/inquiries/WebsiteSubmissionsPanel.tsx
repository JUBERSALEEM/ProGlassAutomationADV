"use client";

import { useEffect, useState, type ReactNode } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { formatNumber } from "@/lib/inquiry-inbox-view";
import { statusLabel } from "@/lib/inquiry-submission-status";
import { CUSTOMER_INQUIRY_STATUSES, type CustomerInquiryPage, type CustomerInquiryStatusCounts } from "@/types/inquiry-submissions";

export type SubmissionTally = { allTotal: number; counts: CustomerInquiryStatusCounts };

/**
 * The website queue, counted inside the desktop inbox.
 *
 * The inbox lists `inquiries`, which the Windows bridge fills after a
 * submission is accepted and downloaded. In production that register is empty
 * until the first handoff, so the inbox looked like a site with no
 * submissions at all while seven were waiting on another page. This strip is
 * the missing signpost, and it carries the counts so the operator can see the
 * queue without opening it.
 */
export function SubmissionCountStrip({ tally, children }: { tally: SubmissionTally; children?: ReactNode }) {
  const waiting = (tally.counts.submitted ?? 0) + (tally.counts.under_review ?? 0);
  return (
    <section aria-label="Website submissions" className="rounded-2xl border border-brand-400/30 bg-brand-500/[0.07] p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-3xs font-bold uppercase tracking-wider text-brand-300">Website submissions</div>
          <p className="mt-1 text-sm font-semibold text-text-primary">
            {formatNumber(tally.allTotal)} complete in the website queue, {formatNumber(waiting)} waiting for a first or further review.
          </p>
          <p className="mt-1 max-w-2xl text-3xs leading-relaxed text-text-muted">
            This register fills when the Windows bridge publishes an accepted, downloaded submission. The website queue holds every complete submission in every review state, and nothing in it is accepted automatically.
          </p>
        </div>
        <LocaleLink
          href="/inquiries/customer-submissions"
          className="rounded-xl border border-brand-400/70 bg-brand-500/15 px-3 py-2 text-xs font-bold text-brand-200 hover:bg-brand-500/25"
        >
          Open the website queue
        </LocaleLink>
      </div>
      <ul className="mt-3 flex flex-wrap gap-2">
        {CUSTOMER_INQUIRY_STATUSES.map((status) => {
          const count = tally.counts[status] ?? 0;
          return (
            <li key={status}>
              <LocaleLink
                href={`/inquiries/customer-submissions?status=${status}`}
                className="inline-flex items-center gap-2 rounded-xl border border-line-strong/60 bg-ink-950/40 px-2.5 py-1.5 text-3xs font-semibold text-text-muted hover:text-text-primary"
              >
                {statusLabel(status)}
                <span className="font-mono text-text-faint">{formatNumber(count)}</span>
              </LocaleLink>
            </li>
          );
        })}
      </ul>
      {children}
    </section>
  );
}

/**
 * Fetches the tally with the operator's own token, and stays quiet when the
 * caller has no queue access: the inbox already explains that case.
 */
export function WebsiteSubmissionsPanel({ authToken, checking }: { authToken: string | null; checking: boolean }) {
  const [tally, setTally] = useState<SubmissionTally | null>(null);

  useEffect(() => {
    if (checking || !authToken) return;
    const controller = new AbortController();

    void (async () => {
      try {
        const response = await fetch("/api/inquiry-submissions/operator?pageSize=1", {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
          signal: controller.signal,
        });
        if (!response.ok) return;
        const body = (await response.json().catch(() => null)) as { ok: true; data: CustomerInquiryPage } | null;
        if (!body?.ok) return;
        setTally({ allTotal: body.data.allTotal, counts: body.data.counts });
      } catch {
        // The strip is a signpost, not the page. Its failure must not disturb
        // the inbox, so it renders nothing rather than an error nobody can act on.
      }
    })();

    return () => controller.abort();
  }, [authToken, checking]);

  if (!tally) return null;
  return <SubmissionCountStrip tally={tally} />;
}
