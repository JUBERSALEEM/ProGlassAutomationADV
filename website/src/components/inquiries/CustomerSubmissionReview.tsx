"use client";

import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { InquiryAccessGate } from "@/components/inquiries/InquiryAccessGate";
import { documentLabel } from "@/lib/inquiry-inbox-view";
import {
  formatInquiryPipelineTime,
  hasInquiryPipelineStatus,
  inquiryHandoffSummary,
  inquiryPipelineLabel,
} from "@/lib/inquiry-pipeline";
import { isReviewLocked, isReviewStatus, reviewSelection, statusLabel, statusSummary, statusTone } from "@/lib/inquiry-submission-status";
import { useOperatorSession } from "@/lib/useOperatorSession";
import {
  CUSTOMER_INQUIRY_REVIEW_STATUSES,
  type CustomerInquiryReviewStatus,
  type CustomerInquiryStatus,
  type CustomerInquirySubmission,
} from "@/types/inquiry-submissions";

type SubmissionDocument = CustomerInquirySubmission["documents"][number];

/**
 * One website submission, read and reviewed.
 *
 * Three things the old screen got wrong and this one does not:
 *
 * 1. The status is printed as a badge, in words, before anything else.
 * 2. A status the review RPC cannot write (`submitted`, `downloaded`) is shown
 *    as the current state rather than silently becoming "Under review", so a
 *    downloaded submission cannot be re-queued for the desktop by a stray save.
 * 3. A sealed review (rejected or closed) cannot be reopened, and says so.
 *
 * Downloads go through the existing authorization RPC with the operator's own
 * session; accepting is a separate, explicit choice.
 */
export function CustomerSubmissionReview({ id }: { id: string }) {
  const { checking, authToken, isAuthRequired, requireAuth } = useOperatorSession();
  const [item, setItem] = useState<CustomerInquirySubmission | null>(null);
  const [current, setCurrent] = useState<CustomerInquiryStatus | null>(null);
  const [status, setStatus] = useState<CustomerInquiryReviewStatus | "">("");
  const [estimate, setEstimate] = useState("");
  const [note, setNote] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [forbidden, setForbidden] = useState(false);
  const [saving, setSaving] = useState(false);
  const [downloading, setDownloading] = useState("");
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (checking || !authToken) return;
    const controller = new AbortController();

    void (async () => {
      try {
        const response = await fetch(`/api/inquiry-submissions/operator/${id}`, {
          headers: {Authorization:`Bearer ${authToken}`},
          cache: "no-store",
          signal: controller.signal,
        });
        const body = (await response.json().catch(() => null)) as
          | { ok: true; data: CustomerInquirySubmission }
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
          throw new Error(body && !body.ok ? body.error.message : "Unable to load this submission.");
        }
        const data = body.data;
        setItem(data);
        setCurrent(data.status);
        setStatus(reviewSelection(data.status));
        setEstimate(data.estimated_response_at ? data.estimated_response_at.slice(0, 16) : "");
        setNote(data.operator_note || "");
        setError("");
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        setError(reason instanceof Error ? reason.message : "Unable to load this submission.");
      }
    })();

    return () => controller.abort();
  }, [authToken, checking, id, reload, requireAuth]);

  async function saveReview() {
    if (!authToken || !item) return;
    if (!status) {
      setMessage("Choose a review status before saving.");
      return;
    }
    setSaving(true);
    setMessage("Saving review");
    try {
      const response = await fetch(`/api/inquiry-submissions/operator/${id}`, {
        method: "PATCH",
        headers: {Authorization:`Bearer ${authToken}`,"Content-Type":"application/json"},
        body: JSON.stringify({ status, estimatedResponseAt: estimate ? new Date(estimate).toISOString() : null, operatorNote: note }),
      });
      const body = (await response.json().catch(() => null)) as
        | { ok: true; data: CustomerInquirySubmission }
        | { ok: false; error: { code: string; message: string } }
        | null;
      if (response.status === 401) {
        requireAuth();
        return;
      }
      if (!response.ok || !body || !body.ok) {
        throw new Error(body && !body.ok ? body.error.message : "The review could not be saved.");
      }
      setItem(body.data);
      setCurrent(body.data.status);
      setStatus(reviewSelection(body.data.status));
      setMessage("Review saved and audited.");
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : "The review could not be saved.");
    } finally {
      setSaving(false);
    }
  }

  async function download(document: SubmissionDocument) {
    if (!authToken || !item) return;
    setDownloading(document.id);
    setMessage("");
    try {
      const response = await fetch(`/api/inquiry-submissions/${item.id}/documents/${document.id}`, {
        headers: {Authorization:`Bearer ${authToken}`},
      });
      if (!response.ok) {
        setMessage("Document could not be downloaded.");
        return;
      }
      const url = URL.createObjectURL(await response.blob());
      const link = window.document.createElement("a");
      link.href = url;
      link.download = document.original_filename;
      link.click();
      URL.revokeObjectURL(url);
      setMessage(`Downloaded ${document.original_filename}.`);
    } catch {
      setMessage("Document could not be downloaded.");
    } finally {
      setDownloading("");
    }
  }

  if (checking) {
    return (
      <div role="status" className="p-10 text-xs text-text-muted">
        Verifying operator access
      </div>
    );
  }
  if (isAuthRequired || forbidden) return <InquiryAccessGate redirectPath={`/inquiries/customer-submissions/${id}`} />;

  if (error && !item) {
    return (
      <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
        <div className="shell py-8">
          <section role="alert" className="rounded-2xl border border-state-danger/45 bg-state-danger/10 p-5">
            <h1 className="text-lg font-bold text-state-danger">This submission could not be loaded</h1>
            <p className="mt-1 text-xs leading-relaxed text-text-secondary">{error}</p>
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => setReload((value) => value + 1)}
                className="rounded-lg border border-state-danger/40 px-3 py-1.5 text-xs font-semibold text-state-danger"
              >
                Try again
              </button>
              <LocaleLink href="/inquiries/customer-submissions" className="rounded-lg border border-line-strong px-3 py-1.5 text-xs font-semibold text-text-muted">
                Back to the queue
              </LocaleLink>
            </div>
          </section>
        </div>
      </main>
    );
  }

  if (!item) {
    return (
      <div role="status" aria-busy="true" className="shell py-8">
        <div className="h-6 w-56 animate-pulse rounded bg-ink-800" />
        <div className="mt-4 h-40 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60" />
      </div>
    );
  }

  const locked = current ? isReviewLocked(current) : false;
  const options: readonly CustomerInquiryReviewStatus[] = current && locked ? [current as CustomerInquiryReviewStatus] : CUSTOMER_INQUIRY_REVIEW_STATUSES;
  const showCurrentOption = Boolean(current && !isReviewStatus(current));
  const field = "mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs text-text-primary";

  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="shell py-8">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="eyebrow text-brand-300">Website submission review</div>
            <div className="mt-1 flex flex-wrap items-center gap-3">
              <h1 className="font-mono text-2xl font-bold tracking-tight">{item.submission_reference}</h1>
              <Badge tone={statusTone(item.status)}>{statusLabel(item.status)}</Badge>
            </div>
            <p className="mt-2 text-xs text-text-muted">
              {statusSummary(item.status)}
              {formatInquiryPipelineTime(item.status_updated_at) ? ` Status last changed ${formatInquiryPipelineTime(item.status_updated_at)}.` : ""}
            </p>
          </div>
          <LocaleLink href="/inquiries/customer-submissions" className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary">
            Back to the queue
          </LocaleLink>
        </div>

        <section aria-label="Submission details" className="mt-5 grid gap-3 rounded-2xl border border-line-strong bg-ink-900 p-5 text-sm sm:grid-cols-2">
          <p><b>Customer:</b> {item.contact_name} · {item.company_name}</p>
          <p><b>Phone:</b> {item.phone || "Not provided"}</p>
          <p><b>Project:</b> {item.project_name}</p>
          <p><b>Location:</b> {item.project_location || "Not provided"}</p>
          <p><b>Type:</b> {item.inquiry_type.replaceAll("_", " ")}</p>
          <p><b>Customer reference:</b> {item.customer_reference || "Not provided"}</p>
          <p><b>Desktop pipeline:</b>{" "}
            {hasInquiryPipelineStatus(item) ? (
              <>
                {inquiryPipelineLabel(item.pipeline_status)}
                {item.pipeline_note ? ` · ${item.pipeline_note}` : ""}
                {formatInquiryPipelineTime(item.pipeline_updated_at) ? ` · updated ${formatInquiryPipelineTime(item.pipeline_updated_at)}` : ""}
              </>
            ) : (
              "Not reported"
            )}
          </p>
          <p><b>Desktop handoff:</b> {inquiryHandoffSummary(item) || "Not in the desktop queue"}</p>
          <p className="whitespace-pre-wrap sm:col-span-2"><b>Requirements:</b> {item.description}</p>
        </section>

        <section aria-label="Private documents" className="mt-5 rounded-2xl border border-line-strong bg-ink-900 p-5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 className="text-sm font-bold">Private documents</h2>
            <span className="text-3xs text-text-faint">{documentLabel(item.documents.length)}</span>
          </div>
          {item.documents.length === 0 ? (
            <p className="mt-3 text-xs text-text-muted">This submission arrived without attachments.</p>
          ) : (
            <ul className="mt-3 space-y-2">
              {item.documents.map((document) => (
                <li key={document.id} className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-line-strong/60 bg-ink-950/60 px-3 py-2">
                  <div className="min-w-0">
                    <p className="truncate text-xs font-semibold">{document.original_filename}</p>
                    <p className="text-3xs text-text-faint">
                      {Math.max(1, Math.ceil(document.byte_size / 1024)).toLocaleString()} KB · uploaded {formatInquiryPipelineTime(document.uploaded_at) || "not reported"}
                    </p>
                  </div>
                  <button
                    type="button"
                    disabled={downloading === document.id}
                    onClick={() => void download(document)}
                    className="rounded-lg border border-brand-400/60 px-3 py-1.5 text-3xs font-bold uppercase tracking-wider text-brand-200 disabled:opacity-50"
                  >
                    {downloading === document.id ? "Downloading" : "Download"}
                  </button>
                </li>
              ))}
            </ul>
          )}
          <p className="mt-3 text-3xs leading-relaxed text-text-faint">
            Downloads use your own signed-in session against the private intake bucket, and each one is recorded in the audit trail of the submission. No download changes the review status.
          </p>
        </section>

        <section aria-labelledby="review-heading" className="mt-5 rounded-2xl border border-line-strong bg-ink-900 p-5">
          <h2 id="review-heading" className="text-sm font-bold">Audited customer response</h2>
          <p className="mt-1 text-3xs text-text-faint">Saving a review requires the inquiry admin role. Auditors can read the queue and download documents.</p>

          {current === "downloaded" ? (
            <p className="mt-3 rounded-xl border border-state-warning/40 bg-state-warning/10 p-3 text-3xs leading-relaxed text-state-warning">
              This submission was already handed to the desktop and downloaded there. Saving it as Accepted puts it back in the desktop delivery queue; choose the outcome deliberately.
            </p>
          ) : null}
          {current === "submitted" ? (
            <p className="mt-3 rounded-xl border border-brand-400/40 bg-brand-500/10 p-3 text-3xs leading-relaxed text-brand-200">
              Saving this review moves the submission from Submitted to Under review, unless you choose another outcome first.
            </p>
          ) : null}
          {locked ? (
            <p className="mt-3 rounded-xl border border-line-strong/70 bg-ink-950/60 p-3 text-3xs leading-relaxed text-text-muted">
              A {statusLabel(current ?? "").toLowerCase()} review cannot be reopened. The status stays as it is; the note and the estimated response date can still be updated.
            </p>
          ) : null}

          <div className="mt-4 grid gap-4">
            <label className="text-xs font-semibold text-text-secondary">
              Review status
              <select
                aria-label="Review status"
                value={status}
                disabled={saving || locked}
                onChange={(event) => setStatus(event.target.value as CustomerInquiryReviewStatus | "")}
                className={field}
              >
                {status === "" ? <option value="">Choose a review status</option> : null}
                {showCurrentOption && current ? (
                  <option value={current} disabled>
                    {statusLabel(current)} (current)
                  </option>
                ) : null}
                {options.map((option) => (
                  <option key={option} value={option}>
                    {statusLabel(option)}
                  </option>
                ))}
              </select>
            </label>

            <label className="text-xs font-semibold text-text-secondary">
              Estimated response date and time
              <input
                type="datetime-local"
                value={estimate}
                disabled={saving}
                onChange={(event) => setEstimate(event.target.value)}
                className={field}
              />
            </label>

            <label className="text-xs font-semibold text-text-secondary">
              Customer-visible note
              <textarea
                maxLength={2000}
                value={note}
                disabled={saving}
                onChange={(event) => setNote(event.target.value)}
                className="mt-1 min-h-28 w-full rounded-xl border border-line-strong bg-ink-950 p-3 text-xs text-text-primary"
              />
            </label>

            <div className="flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={() => void saveReview()}
                disabled={saving || !status}
                className="rounded-xl bg-brand-400 px-4 py-3 text-sm font-bold text-accent-ink disabled:opacity-50"
              >
                {saving ? "Saving review" : "Save reviewed status"}
              </button>
              <p aria-live="polite" className="text-xs text-text-muted">{message}</p>
            </div>
          </div>
        </section>
      </div>
    </main>
  );
}
