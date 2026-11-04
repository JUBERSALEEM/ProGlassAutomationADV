"use client";

import { useEffect, useState } from "react";
import type { ProformaShareState } from "@/lib/proforma-api";

function formatWhen(value: string): string {
  if (!value) return "an unknown time";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "an unknown time" : date.toLocaleString();
}

/** Staff-only status for legacy customer shares; new priced customer sharing is retired. */
export function SharePanelView({
  state,
  busy,
  error,
  notice,
  onRevoke,
}: {
  state: ProformaShareState;
  busy: boolean;
  error: string | null;
  notice: string | null;
  onRevoke: () => void;
}) {
  return (
    <section className="rounded-2xl border border-line-strong/60 bg-ink-900/70 p-3" aria-label="Internal PI release">
      <h2 className="text-3xs font-semibold uppercase tracking-wider text-text-faint">Internal PI release</h2>
      <p className="mt-1.5 text-xs leading-relaxed text-text-muted">
        Customer access to priced proformas is disabled. The customer portal accepts unpriced technical inquiries only;
        authorized staff continue PI review and release in the WPF workflow.
      </p>
      {state.share ? (
        <div className="mt-2 rounded-xl border border-state-warning/35 bg-state-warning/10 p-3">
          <p className="text-xs leading-relaxed text-state-warning">
            {state.share.revoked_at
              ? `Legacy share record retained in the internal audit ledger (revision ${state.share.revision}, shared ${formatWhen(state.share.shared_at)}, withdrawn ${formatWhen(state.share.revoked_at)}). Customer accounts cannot read the proforma.`
              : `Legacy share record retained in the internal audit ledger (revision ${state.share.revision}, shared ${formatWhen(state.share.shared_at)}). Customer accounts cannot read the proforma. An administrator may mark the legacy share withdrawn without deleting its audit record.`}
          </p>
          {!state.share.revoked_at && state.can_write ? (
            <button
              type="button"
              onClick={onRevoke}
              disabled={busy}
              className="mt-2 rounded-xl border border-state-danger/40 px-3 py-2 text-xs font-semibold text-state-danger disabled:opacity-50"
            >
              {busy ? "Recording withdrawal…" : "Mark legacy share withdrawn"}
            </button>
          ) : !state.share.revoked_at ? (
            <p className="mt-2 text-3xs text-text-faint">Withdrawal requires an inquiry administrator role.</p>
          ) : null}
        </div>
      ) : null}
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
          setError(body && !body.ok ? body.error.message : "The internal share audit state could not be read.");
          return;
        }
        setState(body.data);
      } catch {
        if (!cancelled) setError("The internal share audit state could not be read.");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [authToken, inquiryId]);

  const revokeLegacyShare = async () => {
    if (!authToken) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const response = await fetch(`/api/inquiries/${inquiryId}/proforma/share`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${authToken}` },
      });
      const body = (await response.json().catch(() => null)) as
        | { ok: true; data: { state: ProformaShareState } }
        | { ok: false; error: { message: string } }
        | null;
      if (!body || !body.ok) {
        setError(body && !body.ok ? body.error.message : "The audit update did not reach the server.");
        return;
      }
      setState(body.data.state);
      setNotice("Legacy customer access is marked withdrawn; its audit record is retained.");
    } catch {
      setError("The audit update did not reach the server.");
    } finally {
      setBusy(false);
    }
  };

  if (!state) {
    return error ? (
      <p role="alert" className="text-xs text-state-danger">{error}</p>
    ) : (
      <p className="text-3xs text-text-faint">Checking the internal PI release boundary…</p>
    );
  }

  return <SharePanelView state={state} busy={busy} error={error} notice={notice} onRevoke={() => void revokeLegacyShare()} />;
}
