/**
 * Deep links into and out of the confirmed-only Job Order hand-off queue.
 * The API owns the Confirmed gate; no URL parameter can widen that source set.
 */
import type { RecordFilters } from "@/types/records";

function withQuery(path: string, values: Record<string, string | undefined>): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value) params.set(key, value);
  }
  const query = params.toString();
  return `${path}${query ? `?${query}` : ""}`;
}

/** Reports → Job Orders, preserving the active workbook and useful filters. */
export function jobOrdersHrefFromRecordFilters(filters: Partial<RecordFilters>): string {
  return withQuery("/job-orders", {
    search: filters.search,
    datePreset: filters.datePreset && filters.datePreset !== "all" ? filters.datePreset : undefined,
    dateFrom: filters.dateFrom,
    dateTo: filters.dateTo,
    company: filters.company,
    salesman: filters.salesman,
    preparedBy: filters.preparedBy,
    typeOfWork: filters.typeOfWork,
    sourceFile: filters.sourceFile && filters.sourceFile !== "current" ? filters.sourceFile : undefined,
  });
}

/** Job Orders → the confirmed proforma rows that supplied this queue entry. */
export function confirmedProformaHrefForJobOrder(piNumber: string, sourceFile: string | null): string {
  return withQuery("/reports", {
    status: "Confirmed",
    search: piNumber,
    // A missing source label cannot be disambiguated, so show all workbooks.
    sourceFile: sourceFile || "all",
  });
}

/** Job Orders → the same PI's delivery-note ledger in the selected workbook. */
export function deliveryNoteHrefForJobOrder(piNumber: string, sourceFile: string | null): string {
  return withQuery("/deliveries", {
    pi: piNumber,
    // Keep a historical/all-workbooks PI from silently landing on "current".
    sourceFile: sourceFile || "all",
  });
}
