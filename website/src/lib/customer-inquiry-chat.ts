import type { InquiryCommunication } from "@/types/inquiry-communications";
import type {
  CustomerInquiryListItem,
  CustomerInquiryStatus,
  CustomerInquiryType,
} from "@/types/inquiry-submissions";

export type CustomerInquiryStatusCounts = Record<CustomerInquiryStatus, number> & {
  total: number;
  active: number;
  acceptedOrDownloaded: number;
  closedOrRejected: number;
};

export type InquiryCommunicationStats = {
  messageCount: number;
  openInformationRequests: number;
  lastActivityAt: string | null;
};

const STATUS_LABELS: Record<CustomerInquiryStatus, string> = {
  submitted: "Submitted",
  under_review: "Under review",
  accepted: "Accepted",
  rejected: "Rejected",
  downloaded: "Downloaded",
  closed: "Closed",
};

const TYPE_LABELS: Record<CustomerInquiryType, string> = {
  quotation: "Quotation",
  drawing_review: "Drawing review",
  technical: "Technical",
  order_handover: "Order handover",
  other: "Other",
};

export function customerInquiryStatusLabel(status: CustomerInquiryStatus): string {
  return STATUS_LABELS[status] ?? status.replaceAll("_", " ");
}

export function customerInquiryTypeLabel(type: CustomerInquiryType): string {
  return TYPE_LABELS[type] ?? type.replaceAll("_", " ");
}

export function countCustomerInquiriesByStatus(
  inquiries: readonly CustomerInquiryListItem[],
): CustomerInquiryStatusCounts {
  const counts: CustomerInquiryStatusCounts = {
    submitted: 0,
    under_review: 0,
    accepted: 0,
    rejected: 0,
    downloaded: 0,
    closed: 0,
    total: inquiries.length,
    active: 0,
    acceptedOrDownloaded: 0,
    closedOrRejected: 0,
  };

  for (const inquiry of inquiries) counts[inquiry.status] += 1;
  counts.acceptedOrDownloaded = counts.accepted + counts.downloaded;
  counts.closedOrRejected = counts.closed + counts.rejected;
  counts.active = counts.total - counts.closedOrRejected;
  return counts;
}

export function inquiryCommunicationStats(
  rows: readonly InquiryCommunication[],
): InquiryCommunicationStats {
  const customerRows = rows.filter((row) => row.visibility === "customer");
  const activityTimes = customerRows
    .flatMap((row) => [row.created_at, row.resolved_at])
    .filter((value): value is string => Boolean(value))
    .map((value) => new Date(value).getTime())
    .filter((value) => Number.isFinite(value));

  return {
    messageCount: customerRows.filter((row) => row.message_kind === "message").length,
    openInformationRequests: customerRows.filter(
      (row) => row.message_kind === "missing_information_request" && row.request_status === "open",
    ).length,
    lastActivityAt: activityTimes.length ? new Date(Math.max(...activityTimes)).toISOString() : null,
  };
}

export function formatCustomerInquiryDate(value: string | null | undefined, locale?: string): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export function customerInquiryChatPath(inquiryId?: string): string {
  return inquiryId ? `/inquiries/chat?inquiry=${encodeURIComponent(inquiryId)}` : "/inquiries/chat";
}

export function customerInquiryDetailPath(inquiryId: string): string {
  return `/inquiries/submissions/${encodeURIComponent(inquiryId)}`;
}

export function accountPathForRedirect(redirectPath: string, mode?: "signup"): string {
  const params = new URLSearchParams({ redirect: redirectPath });
  if (mode === "signup") params.set("mode", "signup");
  return `/account?${params.toString()}`;
}
