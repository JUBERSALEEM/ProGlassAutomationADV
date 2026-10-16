export type CustomerInquiryStatus = "submitted" | "under_review" | "accepted" | "rejected" | "downloaded" | "closed";

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
  project_name: string;
  project_location: string;
  description: string;
  status: CustomerInquiryStatus;
  submitted_at: string;
  status_updated_at: string;
  accepted_at: string | null;
  downloaded_at: string | null;
  closed_at: string | null;
  documents: CustomerInquiryDocument[];
};

export type CustomerInquiryListItem = Omit<CustomerInquirySubmission, "documents" | "description" | "phone"> & {
  document_count: number;
};

export type CustomerInquiryResponse =
  | { ok: true; data: CustomerInquirySubmission }
  | { ok: false; error: { code: string; message: string } };
