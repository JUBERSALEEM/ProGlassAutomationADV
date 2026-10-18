"use client";

import { useEffect, useState } from "react";
import type { RecordRow } from "@/types/records";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { formatDateDDMMYYYY } from "@/lib/date-utils";

type RecordsRecordDetailModalProps = {
  record: RecordRow | null;
  onClose: () => void;
};

export function RecordsRecordDetailModal({ record, onClose }: RecordsRecordDetailModalProps) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  if (!record) return null;

  const handleCopyJson = async () => {
    try {
      await navigator.clipboard.writeText(JSON.stringify(record, null, 2));
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3.5 bg-ink-950/80 backdrop-blur-sm animate-fade-in">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="record-detail-title"
        className="relative w-full max-w-2xl rounded-2xl border border-line-strong/80 bg-ink-900 p-5 shadow-raised overflow-hidden"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-start justify-between border-b border-line-strong/60 pb-3.5">
          <div>
            <div className="flex items-center gap-2">
              <span className="eyebrow text-brand-300">Record Inspection</span>
              <span className="font-mono text-2xs text-text-faint">#{record.id}</span>
            </div>
            <h2 id="record-detail-title" className="mt-1 text-lg font-bold text-text-primary sm:text-xl font-mono">
              {record.pi_number}
            </h2>
            <p className="text-xs text-text-muted mt-0.5">
              Client: <strong className="text-text-primary">{record.company}</strong> · Customer Ref:{" "}
              <strong className="text-text-primary">{record.customer_ref || "N/A"}</strong>
            </p>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-line-strong p-1.5 text-text-faint hover:text-text-primary hover:bg-ink-850"
            aria-label="Close details"
          >
            ✕
          </button>
        </div>

        {/* Content Grid */}
        <div className="mt-4 grid grid-cols-2 sm:grid-cols-3 gap-3.5 max-h-[60vh] overflow-y-auto pr-1">
          <DetailField label="Date" value={formatDateDDMMYYYY(record.date)} mono />
          <DetailField label="Salesman" value={record.salesman} />
          <DetailField label="Prepared By" value={record.prepared_by} />
          <DetailField label="Type of Work" value={record.type_of_work} highlight />
          <DetailField label="Quantity" value={`${(Number(record.qty) || 0).toLocaleString()} pcs`} mono />
          <DetailField label="Total Area" value={`${(Number(record.sqm) || 0).toFixed(2)} m²`} mono highlight />
          
          <div className="col-span-1">
            <span className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
              Status
            </span>
            <Badge tone={record.status === "Completed" ? "success" : "accent"}>
              {record.status}
            </Badge>
          </div>

          <div className="col-span-1">
            <span className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
              PI Status
            </span>
            <Badge tone={record.pi_status === "Approved" ? "success" : "warning"}>
              {record.pi_status}
            </Badge>
          </div>

          <div className="col-span-1">
            <span className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
              Zoho Status
            </span>
            <Badge tone={record.zoho_status === "Synced" ? "success" : "neutral"}>
              {record.zoho_status}
            </Badge>
          </div>

          <div className="col-span-2 sm:col-span-3 border-t border-line/60 pt-3">
            <span className="eyebrow text-text-faint mb-2 block">Technical Provenance</span>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
              <DetailField label="Source File" value={record.source_file || "N/A"} mono />
              <DetailField label="Source Worksheet" value={record.source_worksheet || "N/A"} mono />
              <DetailField label="Created At" value={record.created_at ? new Date(record.created_at).toLocaleString() : "N/A"} mono />
              <DetailField label="Updated At" value={record.updated_at ? new Date(record.updated_at).toLocaleString() : "N/A"} mono />
            </div>
          </div>
        </div>

        {/* Footer Actions */}
        <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-line-strong/60 pt-3.5">
          <Button variant="secondary" size="sm" onClick={handleCopyJson}>
            {copied ? "✓ Copied JSON" : "Copy Raw JSON"}
          </Button>

          <div className="flex flex-wrap items-center gap-2">
            {record.status.toLocaleLowerCase() === "confirmed" ? (
              <LocaleLink
                href={`/job-orders?search=${encodeURIComponent(record.pi_number)}`}
                className="inline-flex h-8 items-center justify-center rounded-xl border border-brand-400/40 bg-brand-400/10 px-3.5 text-xs font-semibold text-brand-200 transition-colors hover:bg-brand-400/20"
              >
                Open Job Order Queue →
              </LocaleLink>
            ) : (
              <span className="inline-flex h-8 items-center rounded-xl border border-line-strong/70 bg-ink-950/60 px-3 text-2xs font-mono text-text-faint" title="Only confirmed proformas enter the Job Order queue">
                Job Order requires Confirmed
              </span>
            )}
            <Button variant="primary" size="sm" onClick={onClose}>
              Done
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function DetailField({
  label,
  value,
  mono = false,
  highlight = false,
}: {
  label: string;
  value: string;
  mono?: boolean;
  highlight?: boolean;
}) {
  return (
    <div>
      <span className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-0.5">
        {label}
      </span>
      <span
        className={`block text-xs truncate ${mono ? "font-mono" : ""} ${
          highlight ? "text-brand-200 font-semibold" : "text-text-primary"
        }`}
      >
        {value}
      </span>
    </div>
  );
}
