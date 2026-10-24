import type { InquiryListRow } from "@/types/inquiries";

/**
 * The inbox's URL state and the two decisions a card has to make.
 *
 * Filters live in the query string, so a reload, a back button and a pasted
 * link all show the same register. Nothing here filters rows: the API filters
 * on the server, and this module only describes what was asked for.
 */

export type InboxFilters = {
  search: string;
  status: string;
  priority: string;
  hasDocuments: boolean;
  page: number;
};

export const EMPTY_FILTERS: InboxFilters = { search: "", status: "", priority: "", hasDocuments: false, page: 1 };

export function parseInboxFilters(params: URLSearchParams | { get(key: string): string | null }): InboxFilters {
  const page = Number(params.get("page") || "1");
  return {
    search: (params.get("search") || "").trim(),
    status: params.get("status") || "",
    priority: params.get("priority") || "",
    hasDocuments: params.get("hasDocuments") === "true",
    page: Number.isFinite(page) && page >= 1 ? Math.floor(page) : 1,
  };
}

export function inboxQuery(filters: InboxFilters): string {
  const params = new URLSearchParams();
  if (filters.search) params.set("search", filters.search);
  if (filters.status) params.set("status", filters.status);
  if (filters.priority) params.set("priority", filters.priority);
  if (filters.hasDocuments) params.set("hasDocuments", "true");
  if (filters.page > 1) params.set("page", String(filters.page));
  const query = params.toString();
  return query ? `?${query}` : "";
}

/** How many filter controls are narrowing the register right now. */
export function activeFilterCount(filters: InboxFilters): number {
  return [Boolean(filters.search), Boolean(filters.status), Boolean(filters.priority), filters.hasDocuments].filter(Boolean).length;
}

export type ProformaAction = {
  kind: "resume" | "desktop" | "create";
  label: string;
  detail: string;
};

/**
 * What the proforma button can honestly say.
 *
 * A website draft is resumable. A desktop proforma number is a reference to a
 * document this site does not hold, so it opens the workspace, which states
 * that plainly. With neither, the workspace starts a new one.
 */
export function proformaAction(row: Pick<InquiryListRow, "proforma_number" | "proforma_draft">): ProformaAction {
  if (row.proforma_draft) {
    return { kind: "resume", label: "Resume proforma", detail: `Draft revision ${row.proforma_draft.revision}` };
  }
  if (row.proforma_number) {
    return { kind: "desktop", label: "Open proforma", detail: `Desktop ${row.proforma_number}` };
  }
  return { kind: "create", label: "Create proforma", detail: "No proforma yet" };
}

/** Cards show the same fields in the same order at every width. */
export const CARD_FIELD_ORDER = [
  "inquiry_number",
  "status",
  "customer_name",
  "project_name",
  "priority",
  "attachment_count",
  "proforma_number",
  "job_order_number",
  "source_updated_at",
] as const;

export type BadgeTone = "neutral" | "accent" | "success" | "warning" | "danger";

export function statusTone(status: InquiryListRow["status"]): BadgeTone {
  if (status === "Confirmed") return "success";
  if (status === "Cancelled") return "danger";
  if (status === "Hold") return "warning";
  if (status === "Received") return "neutral";
  return "accent";
}

export function priorityTone(priority: InquiryListRow["priority"]): BadgeTone {
  if (priority === "Urgent") return "danger";
  if (priority === "High") return "warning";
  if (priority === "Normal") return "neutral";
  return "neutral";
}

/** A missing date says so in words, not with a dash. */
export function formatDate(value: string): string {
  const date = new Date(value);
  if (!value || Number.isNaN(date.getTime())) return "Not reported";
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(date);
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(Number.isFinite(value) ? value : 0);
}

export function documentLabel(count: number): string {
  return `${count} document${count === 1 ? "" : "s"}`;
}
