export type InquiryStatus = "Received" | "Prepared" | "Sent" | "Confirmed" | "Hold" | "Cancelled";
export type InquiryPriority = "Low" | "Normal" | "High" | "Urgent";
export type InquiryAvailability = "available" | "source_missing" | "tombstoned";
export type InquiryDocumentAvailability = "available" | "source_missing" | "quarantined";

export type InquiryListRow = {
  id: string;
  inquiry_number: string;
  inquiry_reference: string;
  customer_name: string;
  project_name: string;
  salesman_name: string;
  prepared_by_name: string;
  status: InquiryStatus;
  priority: InquiryPriority;
  inquiry_value: number;
  received_at: string;
  source_updated_at: string;
  proforma_number: string;
  job_order_number: string;
  availability: InquiryAvailability;
  attachment_count: number;
  published_at: string;
};

export type InquiryDocumentRow = {
  id: string;
  original_filename: string;
  extension: string;
  availability: InquiryDocumentAvailability;
  current_version: null | {
    id: string;
    byte_size: number;
    detected_mime: string;
    content_sha256: string;
    source_uploaded_at: string | null;
    source_modified_at: string | null;
    published_at: string;
  };
};

export type InquiryDetail = InquiryListRow & {
  customer_location: string;
  project_description: string;
  prepared_at: string | null;
  sent_at: string | null;
  confirmed_at: string | null;
  last_observed_at: string;
  documents: InquiryDocumentRow[];
};

export type InquiryListData = {
  rows: InquiryListRow[];
  pagination: { page: number; pageSize: number; totalEntries: number; totalPages: number };
  summary: { total: number; active: number; confirmed: number; withDocuments: number };
  lastSuccessfulSyncAt: string | null;
};

export type InquiryApiError = {
  message: string;
  code: "UNAUTHORIZED" | "FORBIDDEN" | "NOT_FOUND" | "NOT_CONFIGURED" | "TEMPORARY_FAILURE" | "INTERNAL_ERROR";
};
export type InquiryListResponse = { ok: true; data: InquiryListData } | { ok: false; error: InquiryApiError };
export type InquiryDetailResponse = { ok: true; data: InquiryDetail } | { ok: false; error: InquiryApiError };
