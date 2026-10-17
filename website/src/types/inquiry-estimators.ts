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

export type InquiryEstimatorEventType =
  | "estimator_registered"
  | "estimator_updated"
  | "assignment_set"
  | "assignment_ended";
