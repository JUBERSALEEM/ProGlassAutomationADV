"use client";

import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { ProformaDocument, printDocument } from "@/components/inquiries/proforma/ProformaDocument";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type { CustomerProformaDocument, CustomerProformaSummary } from "@/lib/customer-proforma-api";

/**
 * The customer's own proforma invoices.
 *
 * What is listed, and what is readable at all, is decided by the database: a
 * proforma appears here only when this account holds a share for the revision the
 * document is on. This page adds no rule of its own and no way to guess another
 * document's identifier that would work.
 */
export function CustomerProformaList() {
  const { authToken, checking, isAuthRequired, requireAuth } = useOperatorSession();
  const [rows, setRows] = useState<CustomerProformaSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!authToken) return;
    let cancelled = false;
    void (async () => {
      try {
        const response = await fetch("/api/account/proformas", {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
        });
        const body = (await response.json().catch(() => null)) as
          | { ok: true; data: { proformas: CustomerProformaSummary[] } }
          | { ok: false; error: { message: string } }
          | null;
        if (cancelled) return;
        if (response.status === 401) {
          requireAuth();
          return;
        }
        if (!body || !body.ok) {
          setError(body && !body.ok ? body.error.message : "Your documents could not be loaded.");
          return;
        }
        setRows(body.data.proformas);
      } catch {
        if (!cancelled) setError("Your documents could not be loaded.");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [authToken, requireAuth]);

  if (checking) return <p className="p-10 text-text-muted">Checking your session…</p>;
  if (isAuthRequired) {
    return (
      <p className="p-10 text-text-muted">
        <LocaleLink href="/reports/login?redirect=%2Faccount%2Fproformas" className="text-brand-300 underline">
          Sign in
        </LocaleLink>{" "}
        to see your proforma invoices.
      </p>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <div className="eyebrow text-brand-300">Your documents</div>
        <h1 className="mt-1 text-2xl font-bold">Proforma invoices</h1>
        <p className="mt-1 max-w-2xl text-xs leading-relaxed text-text-muted">
          A proforma appears here once it has been shared with this account. Only documents shared with you are listed,
          and the figures are the figures that were shared.
        </p>
      </div>

      {error ? (
        <p role="alert" className="rounded-xl border border-state-danger/45 bg-state-danger/10 p-3 text-xs text-state-danger">
          {error}
        </p>
      ) : null}

      {rows === null ? (
        <p className="text-xs text-text-muted">Loading your documents…</p>
      ) : rows.length === 0 ? (
        <div className="rounded-2xl border border-line-strong/60 bg-ink-900/70 p-6">
          <h2 className="text-sm font-bold">No proforma invoice has been shared with this account yet.</h2>
          <p className="mt-1 text-xs text-text-muted">
            Ask the estimating team to share it, or check the inquiries you have already submitted.
          </p>
          <LocaleLink href="/inquiries/submit" className="mt-3 inline-block rounded-xl border border-brand-400/50 bg-brand-500/10 px-3 py-2 text-xs font-semibold text-brand-200">
            Submit a customer inquiry
          </LocaleLink>
        </div>
      ) : (
        <ul className="grid gap-3">
          {rows.map((row) => (
            <li key={row.id} className="rounded-2xl border border-line-strong/60 bg-ink-900/70 p-4">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-sm font-bold">{row.proforma_number}</span>
                    <Badge tone={row.status === "Cancelled" ? "danger" : row.status === "Draft" ? "warning" : "success"}>
                      {row.status}
                    </Badge>
                    <span className="text-3xs uppercase tracking-wider text-text-faint">Revision {row.revision}</span>
                  </div>
                  <p className="mt-1 text-xs text-text-muted">
                    {row.inquiry_number} · {row.line_count} line{row.line_count === 1 ? "" : "s"} ·{" "}
                    {row.total_sqm.toLocaleString(undefined, { maximumFractionDigits: 2 })} m²
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-sm font-bold">
                    {row.currency} {row.total_value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                  <LocaleLink
                    href={`/account/proformas/${row.id}`}
                    className="rounded-xl border border-brand-400/60 bg-brand-500/10 px-3 py-2 text-xs font-bold text-brand-200 hover:bg-brand-500/20"
                  >
                    Open
                  </LocaleLink>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** One shared revision, printed from the values that were stored and shared. */
export function CustomerProformaView({ id }: { id: string }) {
  const { authToken, checking, isAuthRequired, requireAuth } = useOperatorSession();
  const [data, setData] = useState<CustomerProformaDocument | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!authToken) return;
    void (async () => {
      try {
        const response = await fetch(`/api/account/proformas/${id}`, {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
        });
        const body = (await response.json().catch(() => null)) as
          | { ok: true; data: CustomerProformaDocument }
          | { ok: false; error: { message: string } }
          | null;
        if (response.status === 401) {
          requireAuth();
          return;
        }
        if (!body || !body.ok) {
          setError(body && !body.ok ? body.error.message : "That document could not be opened.");
          return;
        }
        setData(body.data);
      } catch {
        setError("That document could not be opened.");
      }
    })();
  }, [authToken, id, requireAuth]);

  if (checking) return <p className="p-10 text-text-muted">Checking your session…</p>;
  if (isAuthRequired) {
    return (
      <p className="p-10 text-text-muted">
        <LocaleLink href={`/reports/login?redirect=${encodeURIComponent(`/account/proformas/${id}`)}`} className="text-brand-300 underline">
          Sign in
        </LocaleLink>{" "}
        to open this document.
      </p>
    );
  }
  if (error) {
    return (
      <div className="rounded-2xl border border-state-danger/45 bg-state-danger/10 p-5">
        <h1 className="text-lg font-bold text-state-danger">This document is not available</h1>
        <p className="mt-1 text-xs text-text-secondary">{error}</p>
        <LocaleLink href="/account/proformas" className="mt-3 inline-block rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted">
          Back to your proforma invoices
        </LocaleLink>
      </div>
    );
  }
  if (!data) return <p className="p-10 text-text-muted">Opening the document…</p>;

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <LocaleLink href="/account/proformas" className="text-xs text-brand-200 hover:underline">
          ← All proforma invoices
        </LocaleLink>
        <button type="button" onClick={printDocument} className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold">
          Print or save as PDF
        </button>
      </div>
      <ProformaDocument proforma={data.proforma} inquiry={data.inquiry} lines={data.lines} />
    </div>
  );
}
