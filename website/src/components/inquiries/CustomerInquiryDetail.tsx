"use client";
import { useEffect, useRef, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useOperatorSession } from "@/lib/useOperatorSession";
import { InquiryCommunicationPanel } from "@/components/inquiries/InquiryCommunicationPanel";
import type { CustomerInquirySubmission } from "@/types/inquiry-submissions";

export function CustomerInquiryDetail({ id }: { id: string }) {
  const { checking, authToken, isAuthRequired, requireAuth, unavailable: sessionUnavailable, error: sessionError, refreshSession } = useOperatorSession();
  const [data, setData] = useState<CustomerInquirySubmission | null>(null);
  const [error, setError] = useState("");
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const downloadLock = useRef<string | null>(null);

  async function download(documentId: string, name: string) {
    if (!authToken || downloadLock.current) return;
    downloadLock.current = documentId; setDownloadingId(documentId); setError("");
    try {
      const response = await fetch(`/api/inquiry-submissions/${id}/documents/${documentId}`, { headers: { Authorization: `Bearer ${authToken}` }, cache: "no-store" });
      if (!response.ok) { const body = await response.json().catch(() => null); throw new Error(body?.error?.message || "Unable to download this document."); }
      const url = URL.createObjectURL(await response.blob());
      const anchor = document.createElement("a"); anchor.href = url; anchor.download = name; anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Unable to download this document."); }
    finally { downloadLock.current = null; setDownloadingId(null); }
  }

  useEffect(() => {
    if (!authToken) return;
    const controller = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/inquiry-submissions/${id}`, { headers: { Authorization: `Bearer ${authToken}` }, cache: "no-store", signal: controller.signal });
        const body = await response.json();
        if (response.status === 401) { requireAuth(); return; }
        if (!response.ok || !body.ok) throw new Error(body?.error?.message || "Unable to load submission.");
        setData(body.data);
      } catch (reason) { if (reason instanceof Error && reason.name !== "AbortError") setError(reason.message); }
    })();
    return () => controller.abort();
  }, [authToken, id, requireAuth]);

  if (checking) return <div className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">Checking secure access…</div>;
  if (sessionUnavailable && !authToken) return <main className="min-h-[60vh] bg-ink-950 px-4 py-20 text-center text-text-primary"><section className="mx-auto max-w-md rounded-2xl border border-state-warning/40 bg-ink-900 p-6"><h1 className="text-xl font-bold">Saved sign-in could not be confirmed yet</h1><p className="mt-2 text-xs leading-relaxed text-text-muted">{sessionError || "The authentication service is temporarily unreachable. Your saved sign-in has not been discarded."}</p><button type="button" onClick={() => void refreshSession()} className="mt-5 rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink">Retry saved sign-in</button></section></main>;
  if (isAuthRequired || !authToken) return <main className="min-h-[60vh] bg-ink-950 px-4 py-20 text-center text-text-primary"><LocaleLink href={`/account?redirect=${encodeURIComponent(`/inquiries/submissions/${id}`)}`} className="rounded-xl bg-brand-400 px-5 py-3 font-bold text-accent-ink">Sign in to view this inquiry</LocaleLink></main>;
  if (error) return <main className="min-h-[60vh] bg-ink-950 px-4 py-20 text-center text-state-danger">{error}</main>;
  if (!data) return <div className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">Loading submission…</div>;

  return <main className="min-h-screen bg-ink-950 pb-20 text-text-primary"><div className="shell py-8">
    <LocaleLink href={`/inquiries/chat?inquiry=${encodeURIComponent(id)}`} className="inline-flex items-center gap-2 rounded-xl border border-line-strong bg-ink-900 px-4 py-2.5 text-xs font-bold text-brand-300 shadow-panel hover:border-brand-400/60 hover:text-brand-200">← <span>My inquiries &amp; chat</span></LocaleLink>
    <section className="mt-4 rounded-2xl border border-line-strong bg-ink-900 p-5 shadow-panel">
      <div className="flex flex-wrap items-start justify-between gap-4"><div><div className="font-mono text-sm text-brand-300">{data.submission_reference}</div><h1 className="mt-1 text-2xl font-bold">{data.project_name}</h1><p className="mt-1 text-xs text-text-muted">{data.company_name} · {data.project_location || "Location not supplied"}</p></div><span className="rounded-full border border-line-strong px-3 py-1 text-xs uppercase text-text-muted">{data.status.replaceAll("_", " ")}</span></div>
      <dl className="mt-4 grid gap-3 border-t border-line/50 pt-4 text-xs sm:grid-cols-2 lg:grid-cols-3"><div><dt className="text-text-faint">Your reference</dt><dd className="mt-1 font-semibold">{data.customer_reference || "—"}</dd></div><div><dt className="text-text-faint">Attention</dt><dd className="mt-1 font-semibold">{data.attention_name || "—"}</dd></div><div><dt className="text-text-faint">VAT / TRN number</dt><dd className="mt-1 font-semibold">{data.vat_trn_number || "—"}</dd></div><div><dt className="text-text-faint">Salesman reference</dt><dd className="mt-1 font-semibold">{data.salesman_reference || "—"}</dd></div><div><dt className="text-text-faint">Handover estimator</dt><dd className="mt-1 font-semibold">{data.handover_estimator_name || "—"}</dd></div><div><dt className="text-text-faint">Submitted</dt><dd className="mt-1 font-semibold">{new Date(data.submitted_at).toLocaleString()}</dd></div></dl>
    </section>
    <section className="mt-5 rounded-2xl border border-line-strong bg-ink-900 p-5"><div className="inline-flex rounded-lg border border-line-strong bg-ink-950 px-3 py-1.5"><h2 className="font-bold">Requirements</h2></div><p className="mt-3 whitespace-pre-wrap text-sm leading-relaxed text-text-secondary">{data.description}</p></section>
    <section className="mt-5 rounded-2xl border border-line-strong bg-ink-900 p-5"><div className="inline-flex rounded-lg border border-line-strong bg-ink-950 px-3 py-1.5"><h2 className="font-bold">Private documents</h2></div>{data.documents.length === 0 ? <p className="mt-3 text-xs text-text-muted">No documents attached.</p> : <ul className="mt-3 space-y-2">{data.documents.map((document) => <li key={document.id} className="flex items-center justify-between gap-3 rounded-xl border border-line-strong bg-ink-950 p-3"><div><div className="text-sm font-semibold">{document.original_filename}</div><div className="mt-1 text-3xs text-text-faint">{Math.ceil(document.byte_size / 1024).toLocaleString()} KB · integrity recorded</div></div><button type="button" disabled={downloadingId !== null} aria-busy={downloadingId === document.id} onClick={() => void download(document.id, document.original_filename)} className="min-w-24 rounded-lg border border-line-strong px-3 py-1.5 text-xs font-semibold text-brand-300 disabled:cursor-wait disabled:opacity-60">{downloadingId === document.id ? "Preparing…" : "Download"}</button></li>)}</ul>}</section>
    <InquiryCommunicationPanel submissionId={id} token={authToken} internalAccess={false} onUnauthorized={requireAuth} />
  </div></main>;
}
