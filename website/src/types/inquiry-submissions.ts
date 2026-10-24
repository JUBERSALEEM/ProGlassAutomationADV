/**
 * The six statuses the database constraint allows, in the order an operator
 * meets them. The register, the review screen and the tests all read this one
 * list, so a status can never be missing from a filter again: `downloaded` was
 * the one that went missing when the list was typed out by hand in the client.
 */
export const CUSTOMER_INQUIRY_STATUSES = ["submitted", "under_review", "accepted", "downloaded", "rejected", "closed"] as const;

export type CustomerInquiryStatus = (typeof CUSTOMER_INQUIRY_STATUSES)[number];

/**
 * The four values `review_customer_inquiry_submission` accepts. `submitted`
 * and `downloaded` are states the operator reads, not values it may write.
 */
export const CUSTOMER_INQUIRY_REVIEW_STATUSES = ["under_review", "accepted", "rejected", "closed"] as const;

export type CustomerInquiryReviewStatus = (typeof CUSTOMER_INQUIRY_REVIEW_STATUSES)[number];

export type CustomerInquiryType = "quotation" | "drawing_review" | "technical" | "order_handover" | "other";
export type CustomerInquiryProfile = { contact_name: string; company_name: string; phone: string; vat_trn_number: string; attention_name: string };
export type CustomerInquiryProject = { id: string; project_name: string; project_location: string; default_inquiry_type: CustomerInquiryType; active: boolean; created_at: string; updated_at: string };

export type CustomerInquiryDocument = {
  id: string;
  original_filename: string;
  extension: string;
  detected_mime: string;
  byte_size: number;
  content_sha256: string;
  uploaded_at: string;
};

export type CustomerInquirySubmission = {
  id: string;
  submission_reference: string;
  contact_name: string;
  company_name: string;
  phone: string;
  customer_reference: string;
  attention_name: string;
  vat_trn_number: string;
  salesman_reference: string;
  handover_estimator_name: string;
  project_id: string | null;
  project_name: string;
  project_location: string;
  inquiry_type: CustomerInquiryType;
  description: string;
  status: CustomerInquiryStatus;
  submitted_at: string;
  status_updated_at: string;
  accepted_at: string | null;
  downloaded_at: string | null;
  closed_at: string | null;
  estimated_response_at: string | null;
  operator_note: string;
  // Written only by the desktop bridge through update_customer_inquiry_status_v1.
  // Never sent to a customer-facing response; see withoutPipelineFields.
  pipeline_status?: string | null;
  pipeline_note?: string | null;
  pipeline_updated_at?: string | null;
  pipeline_updated_by?: string | null;
  documents: CustomerInquiryDocument[];
};

export type CustomerInquiryListItem = Omit<CustomerInquirySubmission, "documents" | "description" | "phone"> & {
  document_count: number;
};

export type CustomerInquiryStatusCounts = Record<CustomerInquiryStatus, number>;

/**
 * One page of the operator queue.
 *
 * `total` is the count of rows matching the active status and search, so the
 * header can say "showing 25 of 78" without a second request. `counts` is the
 * same search with the status filter lifted, which is what makes the tabs
 * honest: clicking Submitted cannot change the number printed on it.
 */
export type CustomerInquiryPage = {
  rows: CustomerInquiryListItem[];
  total: number;
  page: number;
  pageSize: number;
  pageCount: number;
  counts: CustomerInquiryStatusCounts;
  allTotal: number;
};

export type CustomerInquiryResponse =
  | { ok: true; data: CustomerInquirySubmission }
  | { ok: false; error: { code: string; message: string } };
