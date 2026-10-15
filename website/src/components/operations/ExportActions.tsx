"use client";

import { useState } from "react";
import type { OperationsExportReport } from "@/lib/operations-exports";

type ExportValue = string | number | boolean | null | undefined;
type ExportSort = { field: string; direction: string };

type ExportActionsProps = {
  report: OperationsExportReport;
  filters: object;
  sort: ExportSort;
  authToken: string | null;
  disabled?: boolean;
};

type ExportFormat = "csv" | "xlsx" | "pdf";

const FORMATS: { value: ExportFormat; label: string }[] = [
  { value: "csv", label: "CSV" },
  { value: "xlsx", label: "Excel" },
  { value: "pdf", label: "PDF" },
];

function downloadName(response: Response, fallback: string): string {
  const header = response.headers.get("content-disposition") || "";
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header)?.[1];
  if (encoded) {
    try {
      return decodeURIComponent(encoded);
    } catch {
      return fallback;
    }
  }
  return /filename="?([^";]+)"?/i.exec(header)?.[1] || fallback;
}

export function ExportActions({ report, filters, sort, authToken, disabled = false }: ExportActionsProps) {
  const [activeFormat, setActiveFormat] = useState<ExportFormat | null>(null);
  const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null);

  const download = async (format: ExportFormat) => {
    setActiveFormat(format);
    setMessage(null);
    try {
      const params = new URLSearchParams({ format });
      for (const [key, rawValue] of Object.entries(filters as Record<string, ExportValue>)) {
        if (rawValue === null || rawValue === undefined || rawValue === "" || rawValue === false) continue;
        if (key === "datePreset" && rawValue === "all") continue;
        params.set(key, rawValue === true ? "1" : String(rawValue));
      }
      params.set("sortBy", sort.field);
      params.set("sortOrder", sort.direction);

      const headers: Record<string, string> = {};
      if (authToken) headers.Authorization = `Bearer ${authToken}`;
      const response = await fetch(`/api/operations-export/${report}?${params.toString()}`, {
        method: "GET",
        headers,
        cache: "no-store",
      });

      if (!response.ok) {
        let errorMessage = `Unable to export ${report}.`;
        try {
          const payload = await response.json() as { error?: { message?: string } };
          errorMessage = payload.error?.message || errorMessage;
        } catch {
          // Use the readable default when the server response is not JSON.
        }
        throw new Error(errorMessage);
      }

      const blob = await response.blob();
      const fallback = `${report}-${new Date().toISOString().slice(0, 10)}.${format}`;
      const filename = downloadName(response, fallback);
      const objectUrl = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = objectUrl;
      anchor.download = filename;
      anchor.style.display = "none";
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1_000);
      setMessage({ text: `${filename} downloaded.`, error: false });
    } catch (error) {
      setMessage({
        text: error instanceof Error ? error.message : "The export could not be created.",
        error: true,
      });
    } finally {
      setActiveFormat(null);
    }
  };

  return (
    <section
      aria-label="Export filtered records"
      aria-busy={activeFormat !== null}
      className="flex flex-col gap-2 rounded-2xl border border-line-strong/60 bg-ink-900/70 px-4 py-2.5 sm:flex-row sm:items-center sm:justify-between"
    >
      <div className="min-w-0">
        <p className="text-xs font-semibold text-text-primary">Export filtered results</p>
        <p className="mt-0.5 text-2xs text-text-faint">Exports all rows matching the current filters, not just this page.</p>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {FORMATS.map(({ value, label }) => (
          <button
            key={value}
            type="button"
            onClick={() => void download(value)}
            disabled={disabled || activeFormat !== null}
            className="inline-flex h-9 min-w-16 items-center justify-center rounded-lg border border-line-strong/80 bg-ink-950/70 px-3 text-xs font-semibold text-text-secondary transition-colors hover:border-brand-400 hover:text-brand-200 disabled:cursor-not-allowed disabled:opacity-50"
            aria-label={`Export ${report} as ${value === "xlsx" ? "Excel workbook" : value.toUpperCase()}`}
          >
            {activeFormat === value ? "Preparing…" : label}
          </button>
        ))}
      </div>
      {message && (
        <p role={message.error ? "alert" : "status"} className={`text-2xs leading-relaxed sm:max-w-80 ${message.error ? "text-state-danger" : "text-state-success"}`}>
          {message.text}
        </p>
      )}
    </section>
  );
}
