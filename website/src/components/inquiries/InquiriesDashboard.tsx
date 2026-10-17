"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Badge } from "@/components/ui/Badge";
import { EmptyState } from "@/components/ui/EmptyState";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { InquiryAccessGate } from "@/components/inquiries/InquiryAccessGate";
import { OperationsTabs } from "@/components/operations/OperationsTabs";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type { InquiryListData, InquiryListResponse, InquiryPriority, InquiryStatus } from "@/types/inquiries";

const STATUSES: Array<InquiryStatus | ""> = ["", "Received", "Prepared", "Sent", "Confirmed", "Hold", "Cancelled"];
const PRIORITIES: Array<InquiryPriority | ""> = ["", "Urgent", "High", "Normal", "Low"];
const PAGE_SIZE = 50;

function statusTone(status: InquiryStatus): "neutral" | "accent" | "success" | "warning" | "danger" {
  if (status === "Confirmed") return "success";
  if (status === "Cancelled") return "danger";
  if (status === "Hold") return "warning";
  if (status === "Received") return "neutral";
  return "accent";
}

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(date);
}

export function InquiriesDashboard() {
  const router = useRouter();
  const pathname = usePathname() || "/inquiries";
  const searchParams = useSearchParams();
  const [search, setSearch] = useState(searchParams.get("search") || "");
  const [submittedSearch, setSubmittedSearch] = useState(searchParams.get("search") || "");
  const [status, setStatus] = useState(searchParams.get("status") || "");
  const [priority, setPriority] = useState(searchParams.get("priority") || "");
  const [hasDocuments, setHasDocuments] = useState(searchParams.get("hasDocuments") === "true");
  const [page, setPage] = useState(Math.max(1, Number(searchParams.get("page")) || 1));
  const [data, setData] = useState<InquiryListData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; code?: string } | null>(null);
  const [refresh, setRefresh] = useState(0);
  const abortRef = useRef<AbortController | null>(null);
  const { checking, userEmail, authToken, isAuthRequired, signOut, requireAuth } = useOperatorSession();

  const syncUrl = useCallback((next: { search: string; status: string; priority: string; hasDocuments: boolean; page: number }) => {
    const params = new URLSearchParams();
    if (next.search) params.set("search", next.search);
    if (next.status) params.set("status", next.status);
    if (next.priority) params.set("priority", next.priority);
    if (next.hasDocuments) params.set("hasDocuments", "true");
    if (next.page > 1) params.set("page", String(next.page));
    router.replace(params.size ? `${pathname}?${params}` : pathname, { scroll: false });
  }, [pathname, router]);

  useEffect(() => {
    if (checking || (isAuthRequired && !authToken)) return;
    const controller = new AbortController();
    abortRef.current?.abort();
    abortRef.current = controller;
    const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE), sortBy: "source_updated_at", sortOrder: "desc" });
    if (submittedSearch) params.set("search", submittedSearch);
    if (status) params.set("status", status);
    if (priority) params.set("priority", priority);
    if (hasDocuments) params.set("hasDocuments", "true");

    void (async () => {
      try {
        const response = await fetch(`/api/inquiries?${params}`, {
          headers: authToken ? { Authorization: `Bearer ${authToken}` } : {},
          cache: "no-store",
          signal: controller.signal,
        });
        const body = await response.json().catch(() => null) as InquiryListResponse | null;
        if (response.status === 401) { requireAuth(); return; }
        if (!response.ok || !body?.ok) {
          throw new Error(body && !body.ok ? `${body.error.message}\u0000${body.error.code}` : "Inquiry service is not available in this deployment.");
        }
        setData(body.data);
        setError(null);
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        const [message, code] = (reason instanceof Error ? reason.message : "Unable to load inquiries.").split("\u0000");
        if (code === "FORBIDDEN") { router.replace(`${pathname.replace(/\/$/, "")}/submit`); return; }
        setError({ message: message || "Unable to load inquiries.", code });
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [authToken, checking, hasDocuments, isAuthRequired, page, pathname, priority, refresh, requireAuth, router, status, submittedSearch]);

  const apply = (next: Partial<{ search: string; status: string; priority: string; hasDocuments: boolean; page: number }>) => {
    const values = { search: submittedSearch, status, priority, hasDocuments, page, ...next };
    if (next.search !== undefined) setSubmittedSearch(next.search);
    if (next.status !== undefined) setStatus(next.status);
    if (next.priority !== undefined) setPriority(next.priority);
    if (next.hasDocuments !== undefined) setHasDocuments(next.hasDocuments);
    if (next.page !== undefined) setPage(next.page);
    setLoading(true);
    syncUrl(values);
  };

  if (checking) return <div role="status" className="grid min-h-[55vh] place-items-center bg-ink-950 text-xs text-text-muted">Verifying operator access…</div>;
  if (isAuthRequired || error?.code === "FORBIDDEN") return <InquiryAccessGate redirectPath={pathname} />;

  const rows = data?.rows ?? [];
  const reset = () => {
    setSearch(""); setSubmittedSearch(""); setStatus(""); setPriority(""); setHasDocuments(false); setPage(1); setLoading(true);
    syncUrl({ search: "", status: "", priority: "", hasDocuments: false, page: 1 });
  };

  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <header className="border-b border-line-strong/60 bg-ink-950/80">
        <div className="shell flex flex-wrap items-center justify-between gap-4 py-5">
          <div>
            <div className="eyebrow text-brand-300">Internal operations · read-only publication</div>
            <h1 className="mt-1 text-2xl font-bold tracking-tight sm:text-3xl">Inquiries</h1>
            <p className="mt-1 max-w-2xl text-xs leading-relaxed text-text-muted">Search the desktop-authoritative inquiry register and privately published inquiry attachments. Changes must be made in ProGlass desktop.</p>
          </div>
          <div className="flex flex-wrap items-center justify-end gap-2">
            {userEmail && <span className="hidden text-2xs text-text-faint sm:inline">{userEmail}</span>}
            <LocaleLink href="/inquiries/customer-submissions" className="rounded-xl border border-brand-400 px-3 py-2 text-xs font-bold text-brand-200">Website submissions</LocaleLink>
            <LocaleLink href="/inquiries/submit" className="rounded-xl bg-brand-400 px-3 py-2 text-xs font-bold text-accent-ink hover:bg-brand-300">Submit customer inquiry</LocaleLink>
            <button type="button" onClick={() => { setLoading(true); setRefresh((value) => value + 1); }} className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary">Refresh</button>
            <button type="button" onClick={() => void signOut()} className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary">Sign out</button>
          </div>
        </div>
      </header>

      <div className="shell mt-6 space-y-5">
        <OperationsTabs className="w-full" />
        {data && <section aria-label="Inquiry summary" className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {[["Published", data.summary.total], ["Active", data.summary.active], ["Confirmed", data.summary.confirmed], ["With documents", data.summary.withDocuments]].map(([label, value]) => (
            <div key={label} className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4"><div className="text-3xs font-semibold uppercase tracking-wider text-text-faint">{label}</div><div className="mt-1 font-mono text-xl font-bold text-text-primary">{value}</div></div>
          ))}
        </section>}

        <section aria-label="Inquiry filters" className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel">
          <form className="grid gap-3 md:grid-cols-[minmax(16rem,1fr)_11rem_10rem_auto]" onSubmit={(event) => { event.preventDefault(); apply({ search: search.trim(), page: 1 }); }}>
            <label className="text-3xs font-semibold uppercase tracking-wider text-text-faint">Search inquiries
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Inquiry, customer, project, PI or JO" className="mt-1.5 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary placeholder:text-text-faint" />
            </label>
            <label className="text-3xs font-semibold uppercase tracking-wider text-text-faint">Status
              <select value={status} onChange={(event) => apply({ status: event.target.value, page: 1 })} className="mt-1.5 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary">{STATUSES.map((value) => <option key={value || "all"} value={value}>{value || "All statuses"}</option>)}</select>
            </label>
            <label className="text-3xs font-semibold uppercase tracking-wider text-text-faint">Priority
              <select value={priority} onChange={(event) => apply({ priority: event.target.value, page: 1 })} className="mt-1.5 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary">{PRIORITIES.map((value) => <option key={value || "all"} value={value}>{value || "All priorities"}</option>)}</select>
            </label>
            <button type="submit" className="h-10 self-end rounded-xl bg-brand-400 px-4 text-xs font-bold text-accent-ink hover:bg-brand-300">Search</button>
          </form>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
            <label className="flex items-center gap-2 text-xs text-text-muted"><input type="checkbox" checked={hasDocuments} onChange={(event) => apply({ hasDocuments: event.target.checked, page: 1 })} className="accent-brand-400" /> Has published documents</label>
            <button type="button" onClick={reset} className="text-xs font-semibold text-text-faint underline-offset-4 hover:text-brand-200 hover:underline">Reset filters</button>
          </div>
        </section>

        {error && <section role="alert" className="rounded-2xl border border-state-danger/45 bg-state-danger/10 p-4">
          <h2 className="text-sm font-bold text-state-danger">Inquiries could not be loaded</h2>
          <p className="mt-1 text-xs leading-relaxed text-text-secondary">{error.message}</p>
          {error.code === "FORBIDDEN" && <p className="mt-2 text-2xs text-text-muted">Your account is signed in but does not have an enabled inquiry role. Ask an inquiry administrator to review access.</p>}
          {error.code === "NOT_CONFIGURED" && <p className="mt-2 text-2xs text-text-muted">The private inquiry schema and storage policies must pass deployment verification before this page can show records.</p>}
          <button type="button" onClick={() => { setLoading(true); setRefresh((value) => value + 1); }} className="mt-3 rounded-lg border border-state-danger/40 px-3 py-1.5 text-xs font-semibold text-state-danger">Try again</button>
        </section>}

        {loading && rows.length === 0 ? <div aria-label="Loading inquiries" className="space-y-2 rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4">{Array.from({ length: 7 }).map((_, index) => <div key={index} className="h-12 animate-pulse rounded-xl bg-ink-850" />)}</div>
          : !error && rows.length === 0 ? <EmptyState glyph="M4 12h16M12 4v16" title="No inquiries match these filters" body="Clear one or more filters, or wait for the authorized Windows bridge to publish a complete source scan." action={<button type="button" onClick={reset} className="mt-4 rounded-xl border border-line-strong px-4 py-2 text-xs font-semibold text-brand-300">Reset filters</button>} />
          : rows.length > 0 && <>
            <section className="hidden overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/90 shadow-panel md:block" aria-label="Published inquiries">
              <div className="overflow-x-auto"><table className="w-full min-w-[880px] text-left text-xs"><thead className="border-b border-line-strong/70 bg-ink-950/80 text-3xs font-semibold uppercase tracking-wider text-text-faint"><tr><th className="px-4 py-3">Inquiry</th><th className="px-3 py-3">Customer / project</th><th className="px-3 py-3">Status</th><th className="px-3 py-3">Priority</th><th className="px-3 py-3">Downstream refs</th><th className="px-3 py-3 text-right">Documents</th><th className="px-4 py-3">Updated</th></tr></thead>
                <tbody className="divide-y divide-line/40">{rows.map((row) => <tr key={row.id} className="hover:bg-ink-850/70"><td className="px-4 py-3"><LocaleLink href={`/inquiries/${row.id}`} className="font-mono font-bold text-brand-300 hover:text-brand-200 hover:underline">{row.inquiry_number}</LocaleLink><div className="mt-0.5 max-w-44 truncate text-3xs text-text-faint">{row.inquiry_reference || "Customer Inquiry"}</div></td><td className="max-w-72 px-3 py-3"><div className="truncate font-semibold text-text-primary">{row.customer_name || "Unassigned customer"}</div><div className="mt-0.5 truncate text-3xs text-text-muted">{row.project_name || "No project"}</div></td><td className="px-3 py-3"><Badge tone={statusTone(row.status)}>{row.status}</Badge></td><td className="px-3 py-3 text-text-secondary">{row.priority}</td><td className="px-3 py-3 font-mono text-3xs text-text-muted">{row.proforma_number || "No PI"}<br />{row.job_order_number || "No JO"}</td><td className="px-3 py-3 text-right font-mono font-bold">{row.attachment_count}</td><td className="whitespace-nowrap px-4 py-3 text-3xs text-text-muted">{formatDate(row.source_updated_at)}</td></tr>)}</tbody></table></div>
            </section>
            <section className="grid gap-3 md:hidden" aria-label="Published inquiries">{rows.map((row) => <LocaleLink key={row.id} href={`/inquiries/${row.id}`} className="rounded-2xl border border-line-strong/60 bg-ink-900/90 p-4 shadow-panel"><div className="flex items-start justify-between gap-3"><div><div className="font-mono text-sm font-bold text-brand-300">{row.inquiry_number}</div><div className="mt-1 text-sm font-semibold text-text-primary">{row.customer_name || "Unassigned customer"}</div></div><Badge tone={statusTone(row.status)}>{row.status}</Badge></div><p className="mt-2 text-xs text-text-muted">{row.project_name || "No project"}</p><div className="mt-3 flex items-center justify-between text-3xs text-text-faint"><span>{row.attachment_count} document{row.attachment_count === 1 ? "" : "s"}</span><span>{formatDate(row.source_updated_at)}</span></div></LocaleLink>)}</section>
          </>}

        {data && data.pagination.totalPages > 1 && <nav aria-label="Inquiry pages" className="flex items-center justify-between rounded-2xl border border-line-strong/60 bg-ink-900/80 px-4 py-3"><button type="button" disabled={page <= 1} onClick={() => apply({ page: page - 1 })} className="rounded-lg border border-line-strong px-3 py-1.5 text-xs disabled:opacity-40">Previous</button><span className="text-xs text-text-muted">Page {page} of {data.pagination.totalPages} · {data.pagination.totalEntries.toLocaleString()} inquiries</span><button type="button" disabled={page >= data.pagination.totalPages} onClick={() => apply({ page: page + 1 })} className="rounded-lg border border-line-strong px-3 py-1.5 text-xs disabled:opacity-40">Next</button></nav>}

        <p className="text-3xs leading-relaxed text-text-faint">Last successful publication: {data?.lastSuccessfulSyncAt ? formatDate(data.lastSuccessfulSyncAt) : "not reported"}. This surface is read-only and never browses the Windows shared folder directly.</p>
      </div>
    </main>
  );
}
