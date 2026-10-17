export type CustomerInquiryStatus = "submitted" | "under_review" | "accepted" | "rejected" | "downloaded" | "closed";
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
  documents: CustomerInquiryDocument[];
};

export type CustomerInquiryListItem = Omit<CustomerInquirySubmission, "documents" | "description" | "phone"> & {
  document_count: number;
};

export type CustomerInquiryResponse =
  | { ok: true; data: CustomerInquirySubmission }
  | { ok: false; error: { code: string; message: string } };
