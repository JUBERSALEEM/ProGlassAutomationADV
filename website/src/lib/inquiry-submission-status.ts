import {
  CUSTOMER_INQUIRY_REVIEW_STATUSES,
  CUSTOMER_INQUIRY_STATUSES,
  type CustomerInquiryReviewStatus,
  type CustomerInquiryStatus,
} from "@/types/inquiry-submissions";

/**
 * The one place a submission status becomes words and a colour.
 *
 * The register, the review screen and the inbox strip all read from here, so
 * "Under review" is spelled and coloured the same way in all three and no
 * screen can invent a seventh status.
 */
const LABELS: Record<CustomerInquiryStatus, string> = {
  submitted: "Submitted",
  under_review: "Under review",
  accepted: "Accepted",
  downloaded: "Downloaded",
  rejected: "Rejected",
  closed: "Closed",
};

export type StatusTone = "neutral" | "accent" | "success" | "warning" | "danger";

const TONES: Record<CustomerInquiryStatus, StatusTone> = {
  submitted: "accent",
  under_review: "warning",
  accepted: "success",
  downloaded: "neutral",
  rejected: "danger",
  closed: "neutral",
};

/** What the status means for the operator, in one short sentence. */
const SUMMARIES: Record<CustomerInquiryStatus, string> = {
  submitted: "Waiting for a first review.",
  under_review: "An operator has started reviewing it.",
  accepted: "Accepted, and eligible for the desktop handoff.",
  downloaded: "Delivered to the desktop and downloaded there.",
  rejected: "Rejected; the review is closed.",
  closed: "Closed with no further action.",
};

export function isCustomerInquiryStatus(value: unknown): value is CustomerInquiryStatus {
  return typeof value === "string" && (CUSTOMER_INQUIRY_STATUSES as readonly string[]).includes(value);
}

export function isReviewStatus(value: unknown): value is CustomerInquiryReviewStatus {
  return typeof value === "string" && (CUSTOMER_INQUIRY_REVIEW_STATUSES as readonly string[]).includes(value);
}

export function statusLabel(status: string): string {
  return isCustomerInquiryStatus(status) ? LABELS[status] : status.replaceAll("_", " ");
}

export function statusTone(status: string): StatusTone {
  return isCustomerInquiryStatus(status) ? TONES[status] : "neutral";
}

export function statusSummary(status: string): string {
  return isCustomerInquiryStatus(status) ? SUMMARIES[status] : "";
}

/**
 * The value the review select opens on.
 *
 * A submitted row and a downloaded row cannot be written back as they are, so
 * neither is offered as if it were a review outcome. A submitted row starts on
 * Under review because that is the review this screen exists to start; a
 * downloaded row starts empty and the operator has to say what happens next.
 * A rejected or closed row can only keep its own status.
 */
export function reviewSelection(status: CustomerInquiryStatus): CustomerInquiryReviewStatus | "" {
  if (status === "rejected" || status === "closed") return status;
  if (status === "downloaded") return "";
  if (status === "submitted") return "under_review";
  return isReviewStatus(status) ? status : "";
}

/** True when the review screen must not offer any other status. */
export function isReviewLocked(status: CustomerInquiryStatus): boolean {
  return status === "rejected" || status === "closed";
}

/**
 * A proforma invoice request that no coordinator has accepted yet.
 *
 * The desk receives a proforma request like any other submission — the files
 * are there and they can be downloaded — so "awaiting approval" is not a
 * delivery state. It is a decision someone still owes, and it outlives the
 * download: a request pulled by the shop PC and never accepted is still awaiting
 * approval. Derived from the type and the status on purpose, so there is one
 * fact to read and none to keep in step.
 */
export function isAwaitingProformaApproval(row: { inquiry_type: string; status: string }): boolean {
  if (row.inquiry_type !== "proforma_invoice") return false;
  return row.status === "submitted" || row.status === "under_review" || row.status === "downloaded";
}
