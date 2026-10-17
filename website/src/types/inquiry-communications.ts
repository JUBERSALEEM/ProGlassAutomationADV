export type InquiryCommunicationVisibility="customer"|"internal";
export type InquiryCommunicationKind="message"|"missing_information_request";
export type InquiryCommunication={id:string;submission_id:string;author_user_id:string;visibility:InquiryCommunicationVisibility;message_kind:InquiryCommunicationKind;request_status:"open"|"resolved"|null;body:string;created_at:string;resolved_at:string|null;resolved_by:string|null};
