"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { InboxCards } from "@/components/inquiries/InboxCards";
import { InboxFilters } from "@/components/inquiries/InboxFilters";
import { InboxHeader } from "@/components/inquiries/InboxHeader";
import { InboxSummary } from "@/components/inquiries/InboxSummary";
import { InquiryAccessGate } from "@/components/inquiries/InquiryAccessGate";
import { OperationsTabs } from "@/components/operations/OperationsTabs";
import { WebsiteSubmissionsPanel } from "@/components/inquiries/WebsiteSubmissionsPanel";
import { ProformaLauncher } from "@/components/inquiries/ProformaLauncher";
import { useOperatorSession } from "@/lib/useOperatorSession";
import {
  activeFilterCount,
  EMPTY_FILTERS,
  formatDate,
  inboxQuery,
  parseInboxFilters,
  type InboxFilters as InboxFilterState,
} from "@/lib/inquiry-inbox-view";
import type { InquiryListData, InquiryListResponse } from "@/types/inquiries";

const PAGE_SIZE = 50;

/**
 * The inquiry inbox.
 *
 * One composer: session, request, URL state and the states that surround the
 * cards. The header, filters, summary and cards each own their own markup, and
 * the server owns filtering, ordering and counting. A filter change is a new
 * request and a new query string, never a client side reduction of the fifty
 * rows already on the page.
 */
export function InquiriesDashboard() {
  const router = useRouter();
  const pathname = usePathname() || "/inquiries";
  const searchParams = useSearchParams();
  const [filters, setFilters] = useState<InboxFilterState>(() => parseInboxFilters(searchParams));
  const [data, setData] = useState<InquiryListData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; code?: string } | null>(null);
  const [refresh, setRefresh] = useState(0);
  const abortRef = useRef<AbortController | null>(null);
  const { checking, userEmail, authToken, isAuthRequired, signOut, requireAuth } = useOperatorSession();

  const apply = useCallback(
    (next: Partial<InboxFilterState>) => {
      const merged = { ...filters, ...next };
      setFilters(merged);
      setLoading(true);
      router.replace(`${pathname}${inboxQuery(merged)}`, { scroll: false });
    },
    [filters, pathname, router],
  );

  const reset = useCallback(() => {
    setFilters(EMPTY_FILTERS);
    setLoading(true);
    router.replace(pathname, { scroll: false });
  }, [pathname, router]);

  useEffect(() => {
    if (checking || (isAuthRequired && !authToken)) return;
    const controller = new AbortController();
    abortRef.current?.abort();
    abortRef.current = controller;
    const params = new URLSearchParams({ page: String(filters.page), pageSize: String(PAGE_SIZE), sortBy: "source_updated_at", sortOrder: "desc" });
    if (filters.search) params.set("search", filters.search);
    if (filters.status) params.set("status", filters.status);
    if (filters.priority) params.set("priority", filters.priority);
    if (filters.hasDocuments) params.set("hasDocuments", "true");

    void (async () => {
      try {
        const response = await fetch(`/api/inquiries?${params}`, {
          headers: authToken ? { Authorization: `Bearer ${authToken}` } : {},
          cache: "no-store",
          signal: controller.signal,
        });
        const body = (await response.json().catch(() => null)) as InquiryListResponse | null;
        if (response.status === 401) {
          requireAuth();
          return;
        }
        if (!response.ok || !body?.ok) {
          throw new Error(
            body && !body.ok
              ? `${body.error.message}\u0000${body.error.code}`
              : "Inquiry service is not available in this deployment.",
          );
        }
        setData(body.data);
        setError(null);
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        const [message, code] = (reason instanceof Error ? reason.message : "Unable to load inquiries.").split("\u0000");
        // A refused role is shown on this page. It is not a reason to move the
        // reader somewhere else: the gate names the account, the role it holds
        // and the role the register needs.
        setError({ message: message || "Unable to load inquiries.", code });
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [authToken, checking, filters, isAuthRequired, pathname, refresh, requireAuth, router]);

  if (checking) {
    return (
      <div role="status" className="grid min-h-[55vh] place-items-center bg-ink-950 text-xs text-text-muted">
        Verifying operator access
      </div>
    );
  }
  if (isAuthRequired || error?.code === "FORBIDDEN") return <InquiryAccessGate redirectPath={pathname} />;

  const rows = data?.rows ?? [];
  const page = filters.page;
  const totalPages = data?.pagination.totalPages ?? 1;

  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <InboxHeader
        email={userEmail}
        activeFilters={activeFilterCount(filters)}
        refreshing={loading && data !== null}
        onRefresh={() => {
          setLoading(true);
          setRefresh((value) => value + 1);
        }}
        onSignOut={() => void signOut()}
      />

      <div className="shell mt-5 space-y-4">
        <OperationsTabs className="w-full" />
        {data ? (
          <ProformaLauncher rows={rows} total={data.pagination.totalEntries} filtered={activeFilterCount(filters) > 0} />
        ) : null}
        <WebsiteSubmissionsPanel authToken={authToken} checking={checking} />
        {data ? <InboxSummary summary={data.summary} /> : null}
        <InboxFilters filters={filters} busy={loading} onChange={apply} onReset={reset} />

        {error ? (
          <section role="alert" className="rounded-2xl border border-state-danger/45 bg-state-danger/10 p-4">
            <h2 className="text-sm font-bold text-state-danger">Inquiries could not be loaded</h2>
            <p className="mt-1 text-xs leading-relaxed text-text-secondary">{error.message}</p>
            {error.code === "NOT_CONFIGURED" ? (
              <p className="mt-2 text-2xs text-text-muted">
                The private inquiry schema and storage policies must pass deployment verification before this page can show records.
              </p>
            ) : null}
            <button
              type="button"
              onClick={() => {
                setLoading(true);
                setRefresh((value) => value + 1);
              }}
              className="mt-3 rounded-lg border border-state-danger/40 px-3 py-1.5 text-xs font-semibold text-state-danger"
            >
              Try again
            </button>
          </section>
        ) : null}

        {!error ? (
          <>
            <InboxCards rows={rows} loading={loading} onReset={reset} filtered={activeFilterCount(filters) > 0} />
            {data && rows.length > 0 ? (
              <p className="text-3xs text-text-faint">
                Showing {rows.length} of {data.pagination.totalEntries.toLocaleString()} published inquiries
                {data.pagination.totalPages > 1 ? `, page ${page} of ${data.pagination.totalPages}` : ""}.
              </p>
            ) : null}
          </>
        ) : null}

        {data && data.pagination.totalPages > 1 ? (
          <nav aria-label="Inquiry pages" className="flex items-center justify-between rounded-2xl border border-line-strong/60 bg-ink-900/80 px-4 py-3">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => apply({ page: page - 1 })}
              className="rounded-lg border border-line-strong px-3 py-1.5 text-xs disabled:opacity-40"
            >
              Previous
            </button>
            <span className="text-xs text-text-muted">
              Page {page} of {totalPages}, {data.pagination.totalEntries.toLocaleString()} inquiries
            </span>
            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => apply({ page: page + 1 })}
              className="rounded-lg border border-line-strong px-3 py-1.5 text-xs disabled:opacity-40"
            >
              Next
            </button>
          </nav>
        ) : null}

        <p className="text-3xs leading-relaxed text-text-faint">
          Last successful publication: {data?.lastSuccessfulSyncAt ? formatDate(data.lastSuccessfulSyncAt) : "not reported"}. This surface is read only and never browses the Windows shared folder directly.
        </p>
      </div>
    </main>
  );
}
