export type InquiryEstimator = {
  user_id: string;
  estimator_code: string;
  display_name: string;
  active: boolean;
  created_at: string;
  updated_at: string;
};

export type InquiryEstimatorAssignmentRole = "primary" | "collaborator";

export type InquiryEstimatorAssignment = {
  id: string;
  assignment_group_id: string;
  submission_id: string;
  estimator_user_id: string;
  assignment_role: InquiryEstimatorAssignmentRole;
  active: boolean;
  reason: string;
  assigned_by: string;
  assigned_at: string;
  ended_by: string | null;
  ended_at: string | null;
};

export type InquiryEstimatorQueueItem = {
  id: string;
  submission_reference: string;
  company_name: string;
  contact_name: string;
  customer_reference: string;
  project_name: string;
  project_location: string;
  inquiry_type: string;
  status: string;
  submitted_at: string;
  status_updated_at: string;
  estimated_response_at: string | null;
  document_count: number;
  assignments: Array<Pick<InquiryEstimatorAssignment,"id"|"estimator_user_id"|"assignment_role"|"assigned_at">>;
};

export type InquiryEstimatorEventType =
  | "estimator_registered"
  | "estimator_updated"
  | "assignment_set"
  | "assignment_ended";
