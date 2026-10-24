"use client";

import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import {
  documentLabel,
  formatDate,
  formatNumber,
  priorityTone,
  proformaAction,
  statusTone,
} from "@/lib/inquiry-inbox-view";
import type { InquiryListRow } from "@/types/inquiries";

/**
 * One inquiry, at every width.
 *
 * The order is the order an operator needs it in: what it is, who it is for,
 * what it is about, how urgent it is, what is attached, which downstream
 * references exist, then the two things they came to do. The desktop
 * publication supplies every field here; nothing is inferred, and there is no
 * estimator or owner line because the register holds no assignment.
 */
export function InquiryCard({ row }: { row: InquiryListRow }) {
  const action = proformaAction(row);
  const proformaReference = row.proforma_number || (row.proforma_draft ? "Website draft" : "No proforma");

  return (
    <article
      className="flex h-full flex-col rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel transition-colors hover:border-brand-400/50"
      aria-label={`Inquiry ${row.inquiry_number}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <LocaleLink
            href={`/inquiries/${row.id}`}
            className="font-mono text-sm font-bold text-brand-300 hover:text-brand-200 hover:underline"
          >
            {row.inquiry_number}
          </LocaleLink>
          <p className="mt-0.5 truncate text-3xs text-text-faint">{row.inquiry_reference || "Customer inquiry"}</p>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-1">
          <Badge tone={statusTone(row.status)}>{row.status}</Badge>
          <Badge tone={priorityTone(row.priority)}>{row.priority}</Badge>
        </div>
      </div>

      <h3 className="mt-3 truncate text-sm font-semibold text-text-primary">{row.customer_name || "Customer not recorded"}</h3>
      <p className="mt-0.5 line-clamp-2 text-xs leading-relaxed text-text-muted">{row.project_name || "No project recorded"}</p>

      <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-3xs">
        <div className="min-w-0">
          <dt className="text-text-faint">Documents</dt>
          <dd className="font-mono text-text-secondary">{documentLabel(row.attachment_count)}</dd>
        </div>
        <div className="min-w-0">
          <dt className="text-text-faint">Proforma</dt>
          <dd className="truncate font-mono text-text-secondary" title={proformaReference}>{proformaReference}</dd>
        </div>
        <div className="min-w-0">
          <dt className="text-text-faint">Job order</dt>
          <dd className="truncate font-mono text-text-secondary" title={row.job_order_number || "No job order"}>
            {row.job_order_number || "No job order"}
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-text-faint">Inquiry value</dt>
          <dd className="font-mono text-text-secondary">{formatNumber(row.inquiry_value)}</dd>
        </div>
      </dl>

      {row.availability === "source_missing" ? (
        <p className="mt-2 rounded-lg border border-state-warning/40 bg-state-warning/10 px-2 py-1 text-3xs text-state-warning">
          The desktop no longer reports this inquiry. Published documents may be incomplete.
        </p>
      ) : null}

      <div className="mt-auto pt-3">
        <p className="text-3xs text-text-faint">
          Updated {formatDate(row.source_updated_at)}. {action.detail}.
        </p>
        <div className="mt-2 grid gap-2 sm:grid-cols-2">
          <LocaleLink
            href={`/inquiries/${row.id}`}
            className="flex h-9 items-center justify-center rounded-xl bg-brand-400 px-3 text-xs font-bold text-accent-ink hover:bg-brand-300"
          >
            Open inquiry
          </LocaleLink>
          <LocaleLink
            href={`/inquiries/${row.id}/proforma`}
            className="flex h-9 items-center justify-center rounded-xl border border-brand-400/70 bg-brand-500/10 px-3 text-xs font-bold text-brand-200 hover:bg-brand-500/20"
          >
            {action.label}
          </LocaleLink>
        </div>
      </div>
    </article>
  );
}
