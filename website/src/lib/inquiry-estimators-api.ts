import type { SupabaseClient } from "@supabase/supabase-js";
import { isSupabaseConfigured,supabaseServerWithToken } from "@/lib/supabase";
import type { InquiryEstimator,InquiryEstimatorAssignment,InquiryEstimatorQueueItem } from "@/types/inquiry-estimators";
const ROLES=["inquiry_admin","inquiry_auditor","inquiry_estimator"] as const;
const UUID=/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
export class EstimatorFailure extends Error{constructor(public code:"UNAUTHORIZED"|"FORBIDDEN"|"INVALID"|"NOT_FOUND"|"TEMPORARY_FAILURE",message:string){super(message);}}
function db(error:{code?:string;message?:string}):never{if(/jwt|42501|permission|unauthorized/i.test(`${error.code} ${error.message}`))throw new EstimatorFailure("FORBIDDEN","This account cannot perform that estimator action.");if(error.code==="P0002")throw new EstimatorFailure("NOT_FOUND",error.message||"Record not found.");if(/22023|23505|21000/i.test(error.code||""))throw new EstimatorFailure("INVALID",error.message||"Invalid estimator action.");console.error("[inquiry-estimators]",{code:error.code,message:error.message});throw new EstimatorFailure("TEMPORARY_FAILURE","Estimator service could not complete the request.");}
async function access(token:string):Promise<{client:SupabaseClient;userId:string;role:string}>{if(!token)throw new EstimatorFailure("UNAUTHORIZED","Sign in to continue.");if(!isSupabaseConfigured())throw new EstimatorFailure("TEMPORARY_FAILURE","Supabase is not configured.");const client=supabaseServerWithToken(token);const auth=await client.auth.getUser(token);if(auth.error||!auth.data.user)throw new EstimatorFailure("UNAUTHORIZED","Session expired or invalid.");const result=await client.from("inquiry_operator_roles").select("role,enabled").eq("user_id",auth.data.user.id).maybeSingle();if(result.error)db(result.error);if(!result.data?.enabled||!ROLES.includes(result.data.role as typeof ROLES[number]))throw new EstimatorFailure("FORBIDDEN","An enabled estimator, auditor, or admin role is required.");return{client,userId:auth.data.user.id,role:result.data.role};}
export async function estimatorSetup(token:string){const {client,role}=await access(token);const estimators=await client.from("inquiry_estimators").select("user_id,estimator_code,display_name,active,created_at,updated_at").order("display_name");if(estimators.error)db(estimators.error);return{role,estimators:(estimators.data||[]) as InquiryEstimator[]};}
export async function estimatorQueue(token:string):Promise<{role:string;rows:InquiryEstimatorQueueItem[]}> {
  const {client,role}=await access(token);
  const submissions: Record<string,unknown>[]=[];
  for(let from=0;from<10_000;from+=1000){
    const page=await client.from("inquiry_customer_submissions")
      .select("id,submission_reference,company_name,contact_name,customer_reference,project_name,project_location,inquiry_type,status,submitted_at,status_updated_at,estimated_response_at,inquiry_customer_documents(id)")
      .eq("upload_state","complete").order("submitted_at",{ascending:false}).range(from,from+999);
    if(page.error)db(page.error);
    submissions.push(...(page.data||[]));
    if((page.data||[]).length<1000)break;
    if(from===9000)throw new EstimatorFailure("TEMPORARY_FAILURE","Estimator report safety limit exceeded; add server pagination before continuing.");
  }
  const ids=submissions.map(row=>String(row.id));
  const assignments:InquiryEstimatorAssignment[]=[];
  for(let i=0;i<ids.length;i+=200){
    const result=await client.from("inquiry_estimator_assignments")
      .select("id,assignment_group_id,submission_id,estimator_user_id,assignment_role,active,reason,assigned_by,assigned_at,ended_by,ended_at")
      .in("submission_id",ids.slice(i,i+200)).eq("active",true);
    if(result.error)db(result.error);
    assignments.push(...((result.data||[]) as InquiryEstimatorAssignment[]));
  }
  return{role,rows:submissions.map(row=>{
    const documents=row.inquiry_customer_documents;
    const rest={...row};delete rest.inquiry_customer_documents;
    return{...rest,document_count:Array.isArray(documents)?documents.length:0,
      assignments:assignments.filter(a=>a.submission_id===row.id).map(({id,estimator_user_id,assignment_role,assigned_at})=>({id,estimator_user_id,assignment_role,assigned_at}))} as InquiryEstimatorQueueItem;
  })};
}
export async function estimatorDetail(token:string,id:string){if(!UUID.test(id))throw new EstimatorFailure("NOT_FOUND","Inquiry not found.");const {client,role}=await access(token);const submission=await client.from("inquiry_customer_submissions").select("*").eq("id",id).eq("upload_state","complete").maybeSingle();if(submission.error)db(submission.error);if(!submission.data)throw new EstimatorFailure("NOT_FOUND","Inquiry not found.");const [documents,assignments]=await Promise.all([client.from("inquiry_customer_documents").select("id,original_filename,detected_mime,byte_size,content_sha256,uploaded_at").eq("submission_id",id).order("uploaded_at"),client.from("inquiry_estimator_assignments").select("id,estimator_user_id,assignment_role,assigned_at").eq("submission_id",id).eq("active",true)]);if(documents.error)db(documents.error);if(assignments.error)db(assignments.error);return{role,submission:submission.data,documents:documents.data||[],assignments:assignments.data||[]};}
function admin(role:string){if(role!=="inquiry_admin")throw new EstimatorFailure("FORBIDDEN","Inquiry admin role required.");}
export async function saveEstimator(token:string,input:{email:string;code:string;displayName:string;active:boolean}){const {client,role}=await access(token);admin(role);const result=await client.rpc("register_inquiry_estimator",{p_email:input.email.trim(),p_estimator_code:input.code.trim(),p_display_name:input.displayName.trim(),p_active:input.active});if(result.error)db(result.error);return estimatorSetup(token);}
export async function setAssignment(token:string,input:{submissionId:string;primaryId:string;collaboratorIds:string[];reason:string}){const {client,role}=await access(token);admin(role);if(!UUID.test(input.submissionId)||!UUID.test(input.primaryId)||input.collaboratorIds.some(id=>!UUID.test(id)))throw new EstimatorFailure("INVALID","Invalid estimator assignment identifiers.");const result=await client.rpc("set_customer_inquiry_estimators",{p_submission_id:input.submissionId,p_primary_estimator:input.primaryId,p_collaborators:input.collaboratorIds,p_reason:input.reason});if(result.error)db(result.error);return estimatorQueue(token);}
export async function endAssignment(token:string,input:{submissionId:string;reason:string}){const {client,role}=await access(token);admin(role);if(!UUID.test(input.submissionId))throw new EstimatorFailure("INVALID","Invalid submission identifier.");const result=await client.rpc("end_customer_inquiry_estimator_assignment",{p_submission_id:input.submissionId,p_reason:input.reason});if(result.error)db(result.error);return estimatorQueue(token);}
