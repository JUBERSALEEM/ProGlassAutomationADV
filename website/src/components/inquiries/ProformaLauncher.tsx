"use client";

import { useMemo, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { proformaAction } from "@/lib/inquiry-inbox-view";
import type { InquiryListRow } from "@/types/inquiries";

/**
 * The one place on the register where a proforma invoice starts.
 *
 * A website proforma hangs off a published inquiry: `inquiry_proformas.inquiry_id`
 * is required and points at `public.inquiries`, which the desktop publishes. So
 * "New PI" cannot invent a document out of nothing; it opens this chooser, which
 * lists the inquiries that exist and sends the operator to the workspace for the
 * one they pick. When the register is empty it says exactly that, instead of
 * leaving a button that does nothing.
 *
 * Nothing here is a permission boundary: each workspace load and each save checks
 * the operator role again on the server.
 */
export function ProformaLauncher({
  rows,
  total,
  filtered,
}: {
  rows: InquiryListRow[];
  total: number;
  /** True when a filter is narrowing the register, so "empty" may be self-inflicted. */
  filtered: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");

  const matches = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return rows;
    return rows.filter((row) =>
      [row.inquiry_number, row.customer_name, row.project_name]
        .join(" ")
        .toLowerCase()
        .includes(needle),
    );
  }, [query, rows]);

  // An empty register explains itself without a click: the honest reason is a
  // publication that has not happened yet, not a missing button.
  const emptyRegister = total === 0 && !filtered;
  const showing = open || emptyRegister;

  return (
    <section className="rounded-2xl border border-line-strong/60 bg-ink-900/70 p-3" aria-label="Proforma invoice actions">
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => setOpen((value) => !value)}
          aria-expanded={showing}
          className="rounded-xl bg-brand-400 px-3 py-2 text-xs font-bold text-accent-ink hover:bg-brand-300"
        >
          {open ? "Close new PI" : "New PI"}
        </button>
        <LocaleLink
          href="/inquiries/submit"
          className="rounded-xl border border-brand-400/50 bg-brand-500/10 px-3 py-2 text-xs font-semibold text-brand-200 hover:bg-brand-500/20"
        >
          Submit customer inquiry
        </LocaleLink>
        <span className="text-3xs text-text-faint">
          {total === 0 ? "No published inquiries yet" : `${total.toLocaleString()} published ${total === 1 ? "inquiry" : "inquiries"} can carry a PI`}
        </span>
      </div>

      {showing ? (
        <div className="mt-3 border-t border-line-strong/50 pt-3">
          <div className="flex flex-wrap items-end justify-between gap-2">
            <div>
              <h2 className="text-sm font-bold">Start a proforma invoice</h2>
              <p className="mt-0.5 max-w-2xl text-3xs leading-relaxed text-text-muted">
                A proforma is opened against a published inquiry. Inquiries reach this register from the desktop; a
                customer submission has to be handed off and published before it can carry one.
              </p>
            </div>
            {rows.length > 0 ? (
              <input
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Inquiry, customer or project"
                aria-label="Find an inquiry to start a proforma for"
                className="h-9 w-full max-w-xs rounded-lg border border-line-strong bg-ink-950 px-2 text-xs"
              />
            ) : null}
          </div>

          {rows.length === 0 ? (
            <div className="mt-3 rounded-xl border border-line-strong/60 bg-ink-950/60 p-3">
              <p className="text-xs font-semibold text-text-primary">
                {filtered ? "No inquiry matches the current filters." : "This register has no published inquiries yet."}
              </p>
              <p className="mt-1 text-3xs leading-relaxed text-text-muted">
                {filtered
                  ? "Clear the filters to see the whole register, then open the inquiry that should carry the PI."
                  : "Publish an inquiry from the desktop, or review the customer submissions waiting in the queue. Neither step can be faked on the website: the inquiry number, customer and attachments come from the desktop publication."}
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                <LocaleLink
                  href="/inquiries/customer-submissions"
                  className="rounded-lg border border-brand-400/50 bg-brand-500/10 px-3 py-1.5 text-xs font-semibold text-brand-200"
                >
                  Review customer submissions
                </LocaleLink>
                <LocaleLink
                  href="/inquiries/submit"
                  className="rounded-lg border border-line-strong px-3 py-1.5 text-xs font-semibold text-text-muted"
                >
                  Submit a customer inquiry
                </LocaleLink>
              </div>
            </div>
          ) : (
            <ul className="mt-3 divide-y divide-line-strong/40 rounded-xl border border-line-strong/60 bg-ink-950/40">
              {matches.map((row) => {
                const action = proformaAction(row);
                return (
                  <li key={row.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-baseline gap-2">
                        <span className="font-mono text-xs font-bold text-text-primary">{row.inquiry_number}</span>
                        <span className="text-3xs uppercase tracking-wider text-text-faint">{row.status}</span>
                      </div>
                      <p className="truncate text-3xs text-text-muted">
                        {row.customer_name || "Unassigned customer"} · {row.project_name || "No project"}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="text-3xs text-text-faint">{action.detail}</span>
                      <LocaleLink
                        href={`/inquiries/${row.id}/proforma`}
                        className="rounded-lg border border-brand-400/60 bg-brand-500/10 px-3 py-1.5 text-xs font-bold text-brand-200 hover:bg-brand-500/20"
                      >
                        {action.kind === "create" ? "Create PI" : action.kind === "resume" ? "Resume PI" : "Open PI"}
                      </LocaleLink>
                    </div>
                  </li>
                );
              })}
              {matches.length === 0 ? (
                <li className="px-3 py-3 text-xs text-text-muted">No inquiry on this page matches that search.</li>
              ) : null}
            </ul>
          )}

          <p className="mt-2 text-3xs text-text-faint">
            The chooser lists the inquiries on the current page. Search and paging above narrow the register itself.
          </p>
        </div>
      ) : null}
    </section>
  );
}
