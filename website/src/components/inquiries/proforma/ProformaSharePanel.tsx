"use client";

import { useEffect, useState } from "react";
import type { ProformaShareState } from "@/lib/proforma-api";

function formatWhen(value: string): string {
  if (!value) return "an unknown time";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "an unknown time" : date.toLocaleString();
}

/**
 * Who holds this proforma, and the one action that changes it.
 *
 * The customer sees a document only while a share names their account and the
 * revision it names is the revision the document is on. That is why this panel
 * reports a stale share instead of pretending the customer still has the current
 * figures: once the operator saves a newer revision, the customer's access lapses
 * until it is shared again.
 */
export function SharePanelView({
  state,
  busy,
  error,
  notice,
  onShare,
  onRevoke,
}: {
  state: ProformaShareState;
  busy: boolean;
  error: string | null;
  notice: string | null;
  onShare: () => void;
  onRevoke: () => void;
}) {
  const current = state.current_revision;
  const canWrite = state.can_write;

  let body: React.ReactNode;
  if (!state.proforma_id) {
    body = <p className="text-xs text-text-muted">Save the proforma once before it can be shared with the customer.</p>;
  } else if (!state.customer_linked) {
    body = (
      <p className="text-xs text-text-muted">
        This inquiry is not linked to a customer account, so there is nobody to share it with. The link comes from the
        customer submission the inquiry was published from.
      </p>
    );
  } else if (!state.share) {
    body = (
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-text-muted">Not shared with the customer. Nothing is visible in their account.</p>
        <button
          type="button"
          onClick={onShare}
          disabled={busy || !canWrite}
          title={canWrite ? undefined : "Sharing needs an inquiry administrator role"}
          className="rounded-xl bg-brand-400 px-3 py-2 text-xs font-bold text-accent-ink hover:bg-brand-300 disabled:opacity-50"
        >
          Share revision {current} with the customer
        </button>
      </div>
    );
  } else if (state.stale) {
    body = (
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-state-warning">
          Shared revision {state.share.revision} on {formatWhen(state.share.shared_at)}. The document is now on revision{" "}
          {current}, so the customer cannot see it until this revision is shared.
        </p>
        <button
          type="button"
          onClick={onShare}
          disabled={busy || !canWrite}
          className="rounded-xl bg-brand-400 px-3 py-2 text-xs font-bold text-accent-ink hover:bg-brand-300 disabled:opacity-50"
        >
          Share revision {current}
        </button>
      </div>
    );
  } else {
    body = (
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-state-success">
          Shared with the customer on {formatWhen(state.share.shared_at)} at revision {state.share.revision}.
        </p>
        <button
          type="button"
          onClick={onRevoke}
          disabled={busy || !canWrite}
          className="rounded-xl border border-state-danger/40 px-3 py-2 text-xs font-semibold text-state-danger disabled:opacity-50"
        >
          Withdraw the customer&apos;s access
        </button>
      </div>
    );
  }

  return (
    <section className="rounded-2xl border border-line-strong/60 bg-ink-900/70 p-3" aria-label="Customer access">
      <div className="flex flex-wrap items-baseline gap-2">
        <h2 className="text-3xs font-semibold uppercase tracking-wider text-text-faint">Customer access</h2>
        {!canWrite ? <span className="text-3xs text-text-faint">read only for this role</span> : null}
      </div>
      <div className="mt-1.5">{body}</div>
      {error ? <p role="alert" className="mt-2 text-xs text-state-danger">{error}</p> : null}
      {notice ? <p role="status" className="mt-2 text-xs text-state-success">{notice}</p> : null}
    </section>
  );
}

export function ProformaSharePanel({ inquiryId, authToken }: { inquiryId: string; authToken: string | null }) {
  const [state, setState] = useState<ProformaShareState | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (!authToken) return;
    let cancelled = false;
    void (async () => {
      try {
        const response = await fetch(`/api/inquiries/${inquiryId}/proforma/share`, {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
        });
        const body = (await response.json().catch(() => null)) as
          | { ok: true; data: ProformaShareState }
          | { ok: false; error: { message: string } }
          | null;
        if (cancelled) return;
        if (!body || !body.ok) {
          setError(body && !body.ok ? body.error.message : "The sharing state could not be read.");
          return;
        }
        setState(body.data);
      } catch {
        if (!cancelled) setError("The sharing state could not be read.");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [authToken, inquiryId]);

  const act = async (method: "POST" | "DELETE") => {
    if (!authToken) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const response = await fetch(`/api/inquiries/${inquiryId}/proforma/share`, {
        method,
        headers: { Authorization: `Bearer ${authToken}` },
      });
      const body = (await response.json().catch(() => null)) as
        | { ok: true; data: { revision?: number; state: ProformaShareState } }
        | { ok: false; error: { message: string } }
        | null;
      if (!body || !body.ok) {
        setError(body && !body.ok ? body.error.message : "The change did not reach the server.");
        return;
      }
      setState(body.data.state);
      setNotice(
        method === "POST"
          ? `Shared revision ${body.data.revision ?? body.data.state.current_revision} with the customer.`
          : "The customer can no longer open this proforma.",
      );
    } catch {
      setError("The change did not reach the server.");
    } finally {
      setBusy(false);
    }
  };

  if (!state) {
    return error ? (
      <p role="alert" className="text-xs text-state-danger">{error}</p>
    ) : (
      <p className="text-3xs text-text-faint">Checking who holds this document…</p>
    );
  }

  return (
    <SharePanelView
      state={state}
      busy={busy}
      error={error}
      notice={notice}
      onShare={() => void act("POST")}
      onRevoke={() => void act("DELETE")}
    />
  );
}
