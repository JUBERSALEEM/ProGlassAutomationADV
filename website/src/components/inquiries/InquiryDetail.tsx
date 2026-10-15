"use client";

import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { InquiryAccessGate } from "@/components/inquiries/InquiryAccessGate";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type { InquiryDetail as InquiryDetailData, InquiryDetailResponse, InquiryDocumentRow, InquiryStatus } from "@/types/inquiries";

function formatDate(value: string | null): string {
  if (!value) return "Not reached";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
}
function statusTone(status: InquiryStatus): "neutral" | "accent" | "success" | "warning" | "danger" {
  return status === "Confirmed" ? "success" : status === "Cancelled" ? "danger" : status === "Hold" ? "warning" : status === "Received" ? "neutral" : "accent";
}
function sizeLabel(bytes: number): string {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

export function InquiryDetail({ inquiryId }: { inquiryId: string }) {
  const pathname = usePathname() || `/inquiries/${inquiryId}`;
  const { checking, authToken, isAuthRequired, requireAuth } = useOperatorSession();
  const [data, setData] = useState<InquiryDetailData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; code?: string } | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    if (checking || (isAuthRequired && !authToken)) return;
    const controller = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/inquiries/${encodeURIComponent(inquiryId)}`, {
          headers: authToken ? { Authorization: `Bearer ${authToken}` } : {}, cache: "no-store", signal: controller.signal,
        });
        const body = await response.json().catch(() => null) as InquiryDetailResponse | null;
        if (response.status === 401) { requireAuth(); return; }
        if (!response.ok || !body?.ok) throw new Error(body && !body.ok ? `${body.error.message}\u0000${body.error.code}` : "Inquiry detail service is unavailable.");
        setData(body.data); setError(null);
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        const [message, code] = (reason instanceof Error ? reason.message : "Unable to load this inquiry.").split("\u0000");
        setError({ message: message || "Unable to load this inquiry.", code });
      } finally { if (!controller.signal.aborted) setLoading(false); }
    })();
    return () => controller.abort();
  }, [authToken, checking, inquiryId, isAuthRequired, refresh, requireAuth]);

  const download = async (document: InquiryDocumentRow) => {
    if (!authToken || document.availability !== "available" || !document.current_version) return;
    setDownloading(document.id);
    setError(null);
    try {
      const response = await fetch(`/api/inquiries/${encodeURIComponent(inquiryId)}/documents/${encodeURIComponent(document.id)}`, {
        headers: { Authorization: `Bearer ${authToken}` }, cache: "no-store", redirect: "follow",
      });
      if (response.status === 401) { requireAuth(); return; }
      if (!response.ok) {
        const body = await response.json().catch(() => null) as { error?: { message?: string } } | null;
        throw new Error(body?.error?.message || "The private document could not be opened.");
      }
      const disposition = response.headers.get("content-disposition");
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = window.document.createElement("a");
      anchor.href = url;
      anchor.download = disposition?.match(/filename="([^"]+)"/)?.[1] || document.original_filename;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (reason) { setError({ message: reason instanceof Error ? reason.message : "Unable to download this document." }); }
    finally { setDownloading(null); }
  };

  if (checking) return <div role="status" className="grid min-h-[55vh] place-items-center bg-ink-950 text-xs text-text-muted">Verifying operator access…</div>;
  if (isAuthRequired) return <InquiryAccessGate redirectPath={pathname} />;

  return <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
    <div className="shell py-6">
      <LocaleLink href="/inquiries" className="text-xs font-semibold text-text-muted hover:text-brand-200">← Inquiry register</LocaleLink>
      {loading && !data && <div className="mt-5 space-y-3 rounded-2xl border border-line-strong/60 bg-ink-900/80 p-5">{Array.from({ length: 6 }).map((_, index) => <div key={index} className="h-12 animate-pulse rounded-xl bg-ink-850" />)}</div>}
      {error && !data && <section role="alert" className="mt-5 rounded-2xl border border-state-danger/45 bg-state-danger/10 p-5"><h1 className="text-lg font-bold text-state-danger">Inquiry unavailable</h1><p className="mt-2 text-xs text-text-secondary">{error.message}</p><div className="mt-4 flex gap-2"><button onClick={() => { setLoading(true); setRefresh((value) => value + 1); }} className="rounded-xl border border-state-danger/40 px-3 py-2 text-xs font-semibold text-state-danger">Try again</button><LocaleLink href="/inquiries" className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted">Return to register</LocaleLink></div></section>}
      {data && <>
        <header className="mt-5 rounded-2xl border border-line-strong/60 bg-ink-900/90 p-5 shadow-panel">
          <div className="flex flex-wrap items-start justify-between gap-4"><div><div className="flex flex-wrap items-center gap-2"><span className="eyebrow text-brand-300">Desktop-authoritative inquiry</span><Badge tone={statusTone(data.status)}>{data.status}</Badge>{data.availability !== "available" && <Badge tone="warning">{data.availability.replace("_", " ")}</Badge>}</div><h1 className="mt-2 font-mono text-2xl font-bold sm:text-3xl">{data.inquiry_number}</h1><p className="mt-1 text-sm text-text-muted">{data.customer_name || "Unassigned customer"} · {data.project_name || "No project"}</p></div><div className="rounded-xl border border-line-strong/60 bg-ink-950/60 px-3 py-2 text-right"><div className="text-3xs uppercase tracking-wider text-text-faint">Priority</div><div className="mt-0.5 text-sm font-bold">{data.priority}</div></div></div>
          <p className="mt-4 border-t border-line/50 pt-4 text-xs leading-relaxed text-text-secondary">{data.project_description || "No project description was published."}</p>
        </header>

        {error && <div role="alert" className="mt-4 rounded-xl border border-state-danger/40 bg-state-danger/10 p-3 text-xs text-state-danger">{error.message}</div>}

        <section aria-labelledby="inquiry-metadata-title" className="mt-5 rounded-2xl border border-line-strong/60 bg-ink-900/80 p-5"><h2 id="inquiry-metadata-title" className="text-sm font-bold">Inquiry metadata</h2><div className="mt-4 grid grid-cols-2 gap-x-5 gap-y-4 sm:grid-cols-3 lg:grid-cols-4">
          {[["Reference", data.inquiry_reference || "Customer Inquiry"],["Location", data.customer_location || "—"],["Salesman", data.salesman_name || "—"],["Prepared by", data.prepared_by_name || "—"],["Received", formatDate(data.received_at)],["Prepared", formatDate(data.prepared_at)],["Sent", formatDate(data.sent_at)],["Confirmed", formatDate(data.confirmed_at)],["Proforma", data.proforma_number || "Not linked"],["Job order", data.job_order_number || "Not linked"],["Published", formatDate(data.published_at)],["Source observed", formatDate(data.last_observed_at)]].map(([label,value]) => <div key={label} className="min-w-0"><div className="text-3xs font-semibold uppercase tracking-wider text-text-faint">{label}</div><div className="mt-1 break-words text-xs text-text-primary">{value}</div></div>)}
        </div></section>

        <section aria-labelledby="inquiry-documents-title" className="mt-5 rounded-2xl border border-line-strong/60 bg-ink-900/80 p-5"><div className="flex flex-wrap items-end justify-between gap-2"><div><h2 id="inquiry-documents-title" className="text-sm font-bold">Inquiry documents</h2><p className="mt-1 text-2xs text-text-muted">Private read-only copies published from this inquiry’s local attachment manifest.</p></div><Badge tone="neutral">{data.documents.length} file{data.documents.length === 1 ? "" : "s"}</Badge></div>
          {data.documents.length === 0 ? <div className="mt-4 rounded-xl border border-dashed border-line-strong p-6 text-center"><p className="text-sm font-semibold">No inquiry documents published</p><p className="mt-1 text-xs text-text-muted">The inquiry is available, but its manifest has no approved current files.</p></div> : <ul className="mt-4 divide-y divide-line/40 overflow-hidden rounded-xl border border-line-strong/60">{data.documents.map((document) => <li key={document.id} className="flex flex-wrap items-center justify-between gap-3 bg-ink-950/35 p-3.5"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><span className="max-w-full break-all text-xs font-semibold text-text-primary">{document.original_filename}</span>{document.availability !== "available" && <Badge tone={document.availability === "quarantined" ? "danger" : "warning"}>{document.availability.replace("_", " ")}</Badge>}</div>{document.current_version && <p className="mt-1 font-mono text-3xs text-text-faint">{sizeLabel(document.current_version.byte_size)} · {document.current_version.detected_mime} · SHA-256 {document.current_version.content_sha256.slice(0, 12)}…</p>}</div><button type="button" onClick={() => void download(document)} disabled={downloading === document.id || document.availability !== "available" || !document.current_version} className="rounded-lg border border-brand-400/40 bg-brand-400/10 px-3 py-2 text-xs font-semibold text-brand-200 disabled:cursor-not-allowed disabled:opacity-40">{downloading === document.id ? "Opening…" : "Download"}</button></li>)}</ul>}
          <p className="mt-3 text-3xs leading-relaxed text-text-faint">Order documents are outside the first-release scope. PI and Job Order values above are references only; this website does not browse their local folders.</p>
        </section>
      </>}
    </div>
  </main>;
}
