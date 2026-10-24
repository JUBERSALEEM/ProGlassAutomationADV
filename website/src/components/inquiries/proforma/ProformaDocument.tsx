"use client";

import { formatMoney } from "@/lib/proforma-draft";
import { buildProformaDocument, documentCaveats } from "@/lib/proforma-document";
import type { ProformaInquiryHeader, ProformaLine, ProformaRecord } from "@/types/proforma";

/**
 * The saved revision of a proforma, laid out as a document.
 *
 * Every figure comes from the database record that `save_inquiry_proforma_v1`
 * wrote. Nothing here re-prices a line, re-rounds a total, applies a floor,
 * applies a surcharge or re-derives the grand total. The desktop application
 * owns what a PI says; this page is a faithful reprint of one stored revision.
 *
 * `buildProformaDocument` still adds the stored line values together, because a
 * document needs a total. When that sum disagrees with the stored header total,
 * the page says so and shows both, rather than quietly printing either one.
 */

export function printDocument(): void {
  if (typeof window === "undefined") return;
  window.print();
}

/**
 * Area as the database stores it, up to four decimals and never fewer than two.
 *
 * `formatSqm` rounds to two places for the working grid. A printed document must
 * not lose the stored precision, so 6.0000 m² prints as `6.00` and 0.5004 m²
 * prints as `0.5004` instead of being rounded into a different figure.
 */
export function printSqm(value: number): string {
  return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 });
}

function size(w1: number, h1: number, w2: number, h2: number): string {
  const first = `${w1} × ${h1}`;
  if (w2 > 0 && h2 > 0) return `${first} / ${w2} × ${h2}`;
  return first;
}

function documentDate(value: string | null | undefined): string {
  if (!value) return "not recorded";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "not recorded";
  return parsed.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

const STATUS_TONE: Record<ProformaRecord["status"], string> = {
  Draft: "border-state-warning/45 bg-state-warning/10 text-state-warning",
  Sent: "border-brand-400/45 bg-brand-400/10 text-brand-200",
  Approved: "border-state-success/45 bg-state-success/10 text-state-success",
  Issued: "border-state-success/45 bg-state-success/10 text-state-success",
  Cancelled: "border-state-danger/45 bg-state-danger/10 text-state-danger",
};

export function ProformaDocument({
  proforma,
  inquiry,
  lines,
  hasUnsavedChanges = false,
  onClose,
}: {
  proforma: ProformaRecord;
  inquiry: ProformaInquiryHeader;
  lines: ProformaLine[];
  /** The grid holds edits that the saved revision, and therefore this document, does not. */
  hasUnsavedChanges?: boolean;
  onClose?: () => void;
}) {
  const model = buildProformaDocument(proforma, lines);
  const caveats = documentCaveats(proforma, model);
  const currency = proforma.currency || "AED";

  return (
    <section className="space-y-3" aria-labelledby="proforma-document-title">
      <div className="flex flex-wrap items-center justify-between gap-2 print:hidden">
        <div>
          <h2 id="proforma-document-title" className="text-sm font-bold text-text-primary">
            Saved document, revision {proforma.revision}
          </h2>
          <p className="mt-0.5 text-3xs text-text-faint">
            {model.lineCount} line{model.lineCount === 1 ? "" : "s"} stored in revision {proforma.revision}. Figures are printed as stored, not recalculated, and VAT is not computed here.
          </p>
        </div>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={printDocument}
            className="rounded-xl bg-brand-400 px-4 py-2 text-xs font-bold text-accent-ink hover:bg-brand-300"
          >
            Print document
          </button>
          {onClose ? (
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary"
            >
              Back to specification
            </button>
          ) : null}
        </div>
      </div>

      <article className="rounded-2xl border border-line-strong/70 bg-ink-900/80 p-5 print:border-0 print:bg-white print:p-0 print:text-black">
        <header className="flex flex-wrap items-start justify-between gap-3 border-b border-line-strong/60 pb-3 print:border-black/30">
          <div>
            <p className="text-3xs font-semibold uppercase tracking-[0.2em] text-text-faint print:text-black/60">Proforma invoice</p>
            <p className="mt-1 font-mono text-lg font-bold text-text-primary print:text-black">{proforma.proforma_number}</p>
            <p className="mt-0.5 text-3xs text-text-muted print:text-black/70">
              Revision {proforma.revision} · {proforma.source_system === "desktop" ? "Desktop document" : "Website document"}
            </p>
          </div>
          <span
            className={`rounded-full border px-2 py-0.5 text-3xs font-semibold uppercase tracking-wider ${STATUS_TONE[proforma.status]} print:border-black/40 print:bg-transparent print:text-black`}
          >
            {proforma.status}
          </span>
        </header>

        <dl className="grid gap-x-6 gap-y-2 border-b border-line-strong/60 py-3 text-xs sm:grid-cols-2 print:border-black/30 print:text-black">
          <div>
            <dt className="text-3xs uppercase tracking-wider text-text-faint print:text-black/60">Customer</dt>
            <dd className="mt-0.5 font-semibold text-text-primary print:text-black">{proforma.customer_name || inquiry.customer_name || "Not recorded"}</dd>
          </div>
          <div>
            <dt className="text-3xs uppercase tracking-wider text-text-faint print:text-black/60">Project</dt>
            <dd className="mt-0.5 text-text-secondary print:text-black">{inquiry.project_name || "Not recorded"}</dd>
          </div>
          <div>
            <dt className="text-3xs uppercase tracking-wider text-text-faint print:text-black/60">Inquiry</dt>
            <dd className="mt-0.5 font-mono text-text-secondary print:text-black">{proforma.inquiry_number || inquiry.inquiry_number}</dd>
          </div>
          <div>
            <dt className="text-3xs uppercase tracking-wider text-text-faint print:text-black/60">Last saved</dt>
            <dd className="mt-0.5 text-text-secondary print:text-black">{documentDate(proforma.updated_at)}</dd>
          </div>
        </dl>

        {hasUnsavedChanges ? (
          <p
            role="status"
            className="mt-3 rounded-lg border border-brand-400/45 bg-brand-400/10 px-2 py-1 text-3xs text-brand-200 print:border-black/40 print:bg-transparent print:text-black"
          >
            The specification has unsaved changes. They are not part of revision {proforma.revision} and do not appear in this document until a revision is saved.
          </p>
        ) : null}

        {caveats.length ? (
          <ul className="mt-3 space-y-1 print:mt-2">
            {caveats.map((caveat) => (
              <li
                key={caveat}
                className="rounded-lg border border-state-warning/40 bg-state-warning/10 px-2 py-1 text-3xs text-state-warning print:border-black/40 print:bg-transparent print:text-black"
              >
                {caveat}
              </li>
            ))}
          </ul>
        ) : null}

        <div className="mt-4 space-y-5">
          {model.groups.map((group) => (
            <section key={group.groupNo} aria-label={`Specification ${group.groupNo}`}>
              <h3 className="mb-1 text-3xs font-bold uppercase tracking-[0.18em] text-text-faint print:text-black/70">
                Specification {group.groupNo}
              </h3>
              <table className="w-full border-collapse text-left text-xs">
                <thead>
                  <tr className="border-b border-line-strong/70 text-3xs uppercase tracking-wider text-text-faint print:border-black/40 print:text-black/70">
                    <th scope="col" className="py-1 pr-2 font-semibold">Line</th>
                    <th scope="col" className="py-1 pr-2 font-semibold">Glass</th>
                    <th scope="col" className="py-1 pr-2 font-semibold">Size (mm)</th>
                    <th scope="col" className="py-1 pr-2 text-right font-semibold">Qty</th>
                    <th scope="col" className="py-1 pr-2 text-right font-semibold">Area / panel</th>
                    <th scope="col" className="py-1 pr-2 text-right font-semibold">Total area</th>
                    <th scope="col" className="py-1 pr-2 text-right font-semibold">Rate</th>
                    <th scope="col" className="py-1 text-right font-semibold">Value</th>
                  </tr>
                </thead>
                <tbody>
                  {group.lines.map((line) => (
                    <tr key={`${group.groupNo}-${line.lineNo}`} className="border-b border-line-strong/30 align-top print:border-black/20">
                      <td className="py-1 pr-2 font-mono text-text-muted print:text-black">{line.lineNo}</td>
                      <td className="py-1 pr-2 text-text-primary print:text-black">{line.glassRef || "Not recorded"}</td>
                      <td className="py-1 pr-2 font-mono text-text-secondary print:text-black">{size(line.w1, line.h1, line.w2, line.h2)}</td>
                      <td className="py-1 pr-2 text-right font-mono text-text-secondary print:text-black">{line.qty}</td>
                      <td className="py-1 pr-2 text-right font-mono text-text-secondary print:text-black">{printSqm(line.sqm)}</td>
                      <td className="py-1 pr-2 text-right font-mono text-text-secondary print:text-black">{printSqm(line.totalSqm)}</td>
                      <td className="py-1 pr-2 text-right font-mono text-text-secondary print:text-black">{formatMoney(line.rate, currency)}</td>
                      <td className="py-1 text-right font-mono font-semibold text-text-primary print:text-black">{formatMoney(line.value, currency)}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="text-3xs font-semibold text-text-muted print:text-black">
                    <td className="py-1 pr-2" colSpan={5}>
                      Specification {group.groupNo} total
                    </td>
                    <td className="py-1 pr-2 text-right font-mono">{printSqm(group.totalSqm)}</td>
                    <td className="py-1 pr-2" />
                    <td className="py-1 text-right font-mono">{formatMoney(group.value, currency)}</td>
                  </tr>
                </tfoot>
              </table>
            </section>
          ))}
        </div>

        <footer className="mt-5 border-t border-line-strong/60 pt-3 print:border-black/30">
          <dl className="flex flex-wrap items-baseline justify-end gap-x-6 gap-y-1 text-xs">
            <div className="flex items-baseline gap-2">
              <dt className="text-3xs uppercase tracking-wider text-text-faint print:text-black/60">Lines</dt>
              <dd className="font-mono text-text-secondary print:text-black">{model.lineCount}</dd>
            </div>
            <div className="flex items-baseline gap-2">
              <dt className="text-3xs uppercase tracking-wider text-text-faint print:text-black/60">Total area</dt>
              <dd className="font-mono text-text-secondary print:text-black">{printSqm(model.totalSqm)}</dd>
            </div>
            <div className="flex items-baseline gap-2">
              <dt className="text-3xs uppercase tracking-wider text-text-faint print:text-black/60">Glass materials subtotal</dt>
              <dd className="font-mono text-base font-bold text-text-primary print:text-black">{formatMoney(model.totalValue, currency)}</dd>
            </div>
          </dl>
          <p className="mt-2 text-right text-3xs text-text-faint print:text-black/60">
            Values as stored by the database at revision {proforma.revision}, and not recalculated on the website.
          </p>
          <p className="mt-1 text-right text-3xs text-text-faint print:text-black/60">
            This subtotal covers the glass lines only. Other charges, discount and VAT are applied by the desktop summary and are not computed here.
          </p>
        </footer>
      </article>
    </section>
  );
}
