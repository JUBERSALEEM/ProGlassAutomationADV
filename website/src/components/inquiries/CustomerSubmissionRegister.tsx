"use client";

import { useEffect, useRef, useState } from "react";
import { Badge } from "@/components/ui/Badge";
import { EmptyState } from "@/components/ui/EmptyState";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { InquiryAccessGate } from "@/components/inquiries/InquiryAccessGate";
import { SubmissionPager } from "@/components/inquiries/SubmissionPager";
import { SubmissionStatusTabs, type StatusFilter } from "@/components/inquiries/SubmissionStatusTabs";
import { documentLabel, formatDate, formatNumber } from "@/lib/inquiry-inbox-view";
import {
  hasInquiryPipelineStatus,
  inquiryHandoffLabel,
  inquiryHandoffSummary,
  inquiryPipelineLabel,
  inquiryPipelineSummary,
} from "@/lib/inquiry-pipeline";
import { isCustomerInquiryStatus, statusLabel, statusSummary, statusTone } from "@/lib/inquiry-submission-status";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type { CustomerInquiryPage, CustomerInquiryStatusCounts, CustomerInquiryListItem } from "@/types/inquiry-submissions";

const PAGE_SIZE = 25;
const SEARCH_DELAY = 250;

/**
 * The website submission register.
 *
 * The queue is answered by the server: status, search and page are query
 * parameters, the counts come from the same restricted read, and the browser
 * renders the page it was given. That is the whole fix for "I cannot see the
 * submissions": the page can now prove how many rows exist, in which status,
 * and show all of them, instead of quietly narrowing the two hundred rows it
 * happened to receive.
 *
 * Review stays explicit. This screen opens a submission; it never accepts one,
 * never downloads a document, and never writes a status.
 */
export function CustomerSubmissionRegister({
  initialStatus = "",
  initialSearch = "",
  initialPage = 1,
}: {
  initialStatus?: string;
  initialSearch?: string;
  initialPage?: number;
} = {}) {
  const { checking, authToken, isAuthRequired, requireAuth } = useOperatorSession();
  const startStatus: StatusFilter = isCustomerInquiryStatus(initialStatus) ? initialStatus : "";
  const startSearch = initialSearch.trim().slice(0, 80);

  const [status, setStatus] = useState<StatusFilter>(startStatus);
  const [search, setSearch] = useState(startSearch);
  const [appliedSearch, setAppliedSearch] = useState(startSearch);
  const [page, setPage] = useState(Math.max(1, Math.trunc(initialPage) || 1));
  const [data, setData] = useState<CustomerInquiryPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [forbidden, setForbidden] = useState(false);
  const [reload, setReload] = useState(0);
  const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (checking || !authToken) return;
    const controller = new AbortController();
    const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
    if (status) params.set("status", status);
    if (appliedSearch) params.set("q", appliedSearch);

    void (async () => {
      try {
        const response = await fetch(`/api/inquiry-submissions/operator?${params.toString()}`, {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
          signal: controller.signal,
        });
        const body = (await response.json().catch(() => null)) as
          | { ok: true; data: CustomerInquiryPage }
          | { ok: false; error: { code: string; message: string } }
          | null;
        if (response.status === 401) {
          requireAuth();
          return;
        }
        if (response.status === 403) {
          setForbidden(true);
          return;
        }
        if (!response.ok || !body || !body.ok) {
          throw new Error(body && !body.ok ? body.error.message : "Unable to load website submissions.");
        }
        setData(body.data);
        setError("");
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        setError(reason instanceof Error ? reason.message : "Unable to load website submissions.");
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();

    return () => controller.abort();
  }, [appliedSearch, authToken, checking, page, reload, requireAuth, status]);

  useEffect(() => () => {
    if (searchTimer.current) clearTimeout(searchTimer.current);
  }, []);

  function onSearchChange(value: string) {
    setSearch(value);
    if (searchTimer.current) clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => {
      setAppliedSearch(value.trim().slice(0, 80));
      setPage(1);
      setLoading(true);
    }, SEARCH_DELAY);
  }

  function onSelectStatus(next: StatusFilter) {
    if (next === status) return;
    setStatus(next);
    setPage(1);
    setLoading(true);
  }

  function onPage(next: number) {
    setPage(Math.max(1, next));
    setLoading(true);
  }

  function resetFilters() {
    if (searchTimer.current) clearTimeout(searchTimer.current);
    setSearch("");
    setAppliedSearch("");
    setStatus("");
    setPage(1);
    setLoading(true);
  }

  if (checking) {
    return (
      <div role="status" className="p-10 text-xs text-text-muted">
        Verifying operator access
      </div>
    );
  }
  if (isAuthRequired || forbidden) return <InquiryAccessGate redirectPath="/inquiries/customer-submissions" />;

  const rows: CustomerInquiryListItem[] = data?.rows ?? [];
  const counts: CustomerInquiryStatusCounts | null = data?.counts ?? null;
  const total = data?.total ?? 0;
  const pageCount = data?.pageCount ?? 0;
  const start = total === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const end = total === 0 ? 0 : Math.min(total, start + Math.max(rows.length, 0) - 1);
  const filtered = Boolean(status || appliedSearch);
  const outOfRange = pageCount > 0 && page > pageCount;
  const headline = counts
    ? [
        `${formatNumber(data?.allTotal ?? 0)} complete`,
        `${formatNumber(counts.submitted)} submitted`,
        `${formatNumber(counts.under_review)} under review`,
        `${formatNumber(counts.accepted)} accepted`,
        `${formatNumber(counts.downloaded)} downloaded`,
      ].join(" · ")
    : "Loading the complete queue";

  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="shell py-8">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="eyebrow text-brand-300">Internal operations · website intake</div>
            <h1 className="mt-1 text-3xl font-bold tracking-tight">Customer submissions</h1>
            <p className="mt-2 max-w-2xl text-xs leading-relaxed text-text-muted">
              Every complete website submission, in every review state. This queue is separate from the desktop inquiry
              register, and opening a submission here never accepts it or downloads its documents.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <LocaleLink href="/inquiries" className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary">
              Inquiry inbox
            </LocaleLink>
            <LocaleLink href="/inquiries/estimator" className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary">
              Estimator workspace
            </LocaleLink>
          </div>
        </div>

        <p className="mt-4 text-xs text-text-muted" aria-live="polite">
          {headline}
        </p>

        <section aria-label="Submission filters" className="mt-4 space-y-3 rounded-2xl border border-line-strong/70 bg-ink-900/60 p-4">
          <div className="flex flex-wrap items-center gap-2">
            <input
              aria-label="Search customer submissions"
              value={search}
              onChange={(event) => onSearchChange(event.target.value)}
              placeholder="Reference, company, project or location"
              className="h-10 min-w-0 flex-1 rounded-xl border border-line-strong bg-ink-950 px-3 text-xs text-text-primary placeholder:text-text-faint"
            />
            {search ? (
              <button
                type="button"
                onClick={() => onSearchChange("")}
                className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary"
              >
                Clear search
              </button>
            ) : null}
            {filtered ? (
              <button type="button" onClick={resetFilters} className="rounded-xl border border-brand-400/60 px-3 py-2 text-xs font-semibold text-brand-200">
                Reset filters
              </button>
            ) : null}
          </div>
          <SubmissionStatusTabs counts={counts} allTotal={data?.allTotal ?? 0} active={status} busy={loading && data !== null} onSelect={onSelectStatus} />
        </section>

        {error ? (
          <section role="alert" className="mt-4 rounded-2xl border border-state-danger/45 bg-state-danger/10 p-4">
            <h2 className="text-sm font-bold text-state-danger">Submissions could not be loaded</h2>
            <p className="mt-1 text-xs leading-relaxed text-text-secondary">{error}</p>
            <button
              type="button"
              onClick={() => {
                setLoading(true);
                setReload((value) => value + 1);
              }}
              className="mt-3 rounded-lg border border-state-danger/40 px-3 py-1.5 text-xs font-semibold text-state-danger"
            >
              Try again
            </button>
          </section>
        ) : null}

        {!error && loading && !data ? (
          <div aria-label="Loading submissions" aria-busy="true" className="mt-4 space-y-3">
            {Array.from({ length: 4 }).map((_, index) => (
              <div key={index} className="h-28 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60" />
            ))}
          </div>
        ) : null}

        {!error && outOfRange ? (
          <div className="mt-4 rounded-2xl border border-line-strong/70 bg-ink-900/70 p-4">
            <p className="text-xs text-text-muted">This queue has {pageCount === 1 ? "one page" : `${pageCount} pages`}; page {page} is past its end.</p>
            <button type="button" onClick={() => onPage(1)} className="mt-2 rounded-lg border border-brand-400/60 px-3 py-1.5 text-xs font-semibold text-brand-200">
              Go to the first page
            </button>
          </div>
        ) : null}

        {!error && !outOfRange && data && rows.length === 0 ? (
          <div className="mt-4">
            <EmptyState
              glyph="M4 12h16M12 4v16"
              title={filtered ? "No submissions match these filters" : "No complete website submissions yet"}
              body={
                filtered
                  ? "Every status is included when the filter is reset, so the row you are looking for is either in another status or has not been finalized."
                  : "A submission appears here once the customer finalizes it on the website. Nothing in this queue is accepted or downloaded automatically."
              }
              action={
                filtered ? (
                  <button type="button" onClick={resetFilters} className="mt-4 rounded-xl border border-line-strong px-4 py-2 text-xs font-semibold text-brand-300">
                    Reset filters
                  </button>
                ) : undefined
              }
            />
          </div>
        ) : null}

        {!error && rows.length > 0 ? (
          <section aria-label="Website submissions" aria-busy={loading} className={loading ? "mt-4 space-y-3 opacity-60" : "mt-4 space-y-3"}>
            {rows.map((r) => (
              <LocaleLink
                key={r.id}
                href={`/inquiries/customer-submissions/${r.id}`}
                className="block rounded-2xl border border-line-strong bg-ink-900 p-4 transition-colors hover:border-brand-400/70"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <strong className="font-mono text-sm tracking-tight">{r.submission_reference}</strong>
                      <Badge tone={statusTone(r.status)}>{statusLabel(r.status)}</Badge>
                    </div>
                    <p className="mt-2 text-sm font-semibold">{r.company_name} · {r.project_name}</p>
                    <p className="mt-1 text-xs text-text-muted">
                      {r.project_location || "No location"} · {r.inquiry_type.replaceAll("_", " ")} · {documentLabel(r.document_count)}
                    </p>
                    <p className="mt-1 text-3xs text-text-faint">
                      Submitted {formatDate(r.submitted_at)} · {statusSummary(r.status)}
                    </p>
                    {hasInquiryPipelineStatus(r) && (
                      <p className="mt-1 text-3xs font-semibold text-brand-200" title={inquiryPipelineSummary(r) || undefined}>
                        Desktop: {inquiryPipelineLabel(r.pipeline_status)}
                      </p>
                    )}
                    {inquiryHandoffLabel(r) && (
                      <p className="mt-1 text-3xs font-semibold text-brand-200" title={inquiryHandoffSummary(r) || undefined}>
                        {inquiryHandoffLabel(r)}
                      </p>
                    )}
                  </div>
                  <span aria-hidden="true" className="text-xs font-semibold text-brand-300">
                    Open
                  </span>
                </div>
              </LocaleLink>
            ))}
          </section>
        ) : null}

        {!error && data && !outOfRange && total > 0 ? (
          <div className="mt-4">
            <SubmissionPager page={page} pageCount={pageCount} total={total} from={start} to={end} busy={loading} onPage={onPage} />
          </div>
        ) : null}
      </div>
    </main>
  );
}
