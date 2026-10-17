#!/usr/bin/env node
/** Disposable Phase 9A live matrix. Uses only normal Auth sessions and public API. */
import { createClient } from "@supabase/supabase-js";
const required=["NEXT_PUBLIC_SUPABASE_URL","NEXT_PUBLIC_SUPABASE_ANON_KEY","PHASE9_ADMIN_EMAIL","PHASE9_ADMIN_PASSWORD","PHASE9_PRIMARY_EMAIL","PHASE9_PRIMARY_PASSWORD","PHASE9_COLLABORATOR_EMAIL","PHASE9_COLLABORATOR_PASSWORD","PHASE9_SUBMISSION_REFERENCE"];
for(const key of required)if(!process.env[key])throw new Error(`${key} is required.`);
const url=process.env.NEXT_PUBLIC_SUPABASE_URL,key=process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
const client=()=>createClient(url,key,{auth:{persistSession:false,autoRefreshToken:false}});
async function signedIn(email,password,label){const c=client();const auth=await c.auth.signInWithPassword({email,password});if(auth.error||!auth.data.user)throw new Error(`${label} authentication failed: ${auth.error?.message||"no session"}`);return c;}
function check(value,message){if(!value)throw new Error(`FAILED: ${message}`);console.log(`PASS: ${message}`);}
async function rpc(c,name,args){const result=await c.rpc(name,args);if(result.error)throw new Error(`${name} failed: ${result.error.message}`);return result.data;}
async function visible(c){const result=await c.from("inquiry_customer_submissions").select("id,submission_reference").eq("upload_state","complete").order("submitted_at");if(result.error)throw new Error(`Submission visibility query failed: ${result.error.message}`);return result.data||[];}
const admin=await signedIn(process.env.PHASE9_ADMIN_EMAIL,process.env.PHASE9_ADMIN_PASSWORD,"Admin");
const targetResult=await admin.from("inquiry_customer_submissions").select("id,submission_reference").eq("submission_reference",process.env.PHASE9_SUBMISSION_REFERENCE).eq("upload_state","complete").maybeSingle();
if(targetResult.error||!targetResult.data)throw new Error(`Complete target submission not found: ${targetResult.error?.message||process.env.PHASE9_SUBMISSION_REFERENCE}`);
const target=targetResult.data;
const primaryId=await rpc(admin,"register_inquiry_estimator",{p_email:process.env.PHASE9_PRIMARY_EMAIL,p_estimator_code:"EST-PRIMARY-TEST",p_display_name:"Phase 9 Primary Estimator",p_active:true});
const collaboratorId=await rpc(admin,"register_inquiry_estimator",{p_email:process.env.PHASE9_COLLABORATOR_EMAIL,p_estimator_code:"EST-COLLAB-TEST",p_display_name:"Phase 9 Collaborator Estimator",p_active:true});
check(primaryId&&collaboratorId&&primaryId!==collaboratorId,"two distinct estimator identities registered");
const primary=await signedIn(process.env.PHASE9_PRIMARY_EMAIL,process.env.PHASE9_PRIMARY_PASSWORD,"Primary estimator");
const collaborator=await signedIn(process.env.PHASE9_COLLABORATOR_EMAIL,process.env.PHASE9_COLLABORATOR_PASSWORD,"Collaborator estimator");
check((await visible(primary)).length===0,"unassigned primary sees no complete submissions");
check((await visible(collaborator)).length===0,"unassigned collaborator sees no complete submissions");
await rpc(admin,"set_customer_inquiry_estimators",{p_submission_id:target.id,p_primary_estimator:primaryId,p_collaborators:[collaboratorId],p_reason:"Phase 9A primary and collaborator isolation test"});
let rows=await visible(primary);check(rows.length===1&&rows[0].id===target.id,"primary sees only the assigned submission");
rows=await visible(collaborator);check(rows.length===1&&rows[0].id===target.id,"collaborator sees only the assigned submission");
const adminDocs=await admin.from("inquiry_customer_documents").select("id,storage_bucket,storage_object_key").eq("submission_id",target.id).order("uploaded_at");if(adminDocs.error)throw new Error(adminDocs.error.message);
const primaryDocs=await primary.from("inquiry_customer_documents").select("id,storage_bucket,storage_object_key").eq("submission_id",target.id);if(primaryDocs.error)throw new Error(primaryDocs.error.message);
check(primaryDocs.data.length===adminDocs.data.length,"assigned primary sees all assigned document metadata");
if(adminDocs.data.length){const document=adminDocs.data[0];const download=await primary.storage.from(document.storage_bucket).download(document.storage_object_key);check(!download.error&&download.data.size>0,"assigned primary downloads an assigned private object");}
const other=await admin.from("inquiry_customer_submissions").select("id").neq("id",target.id).eq("upload_state","complete").limit(1).maybeSingle();
if(other.error)throw new Error(other.error.message);if(other.data){const denied=await primary.from("inquiry_customer_submissions").select("id").eq("id",other.data.id).maybeSingle();check(!denied.error&&!denied.data,"primary cannot read an unassigned submission");const deniedDocs=await primary.from("inquiry_customer_documents").select("id").eq("submission_id",other.data.id);check(!deniedDocs.error&&deniedDocs.data.length===0,"primary cannot read unassigned document metadata");}
await rpc(admin,"register_inquiry_estimator",{p_email:process.env.PHASE9_PRIMARY_EMAIL,p_estimator_code:"EST-PRIMARY-TEST",p_display_name:"Phase 9 Primary Estimator",p_active:false});
check((await visible(primary)).length===0,"disabled primary immediately loses assigned visibility");
await rpc(admin,"register_inquiry_estimator",{p_email:process.env.PHASE9_PRIMARY_EMAIL,p_estimator_code:"EST-PRIMARY-TEST",p_display_name:"Phase 9 Primary Estimator",p_active:true});
check((await visible(primary)).some(x=>x.id===target.id),"re-enabled primary regains assigned visibility");
await rpc(admin,"set_customer_inquiry_estimators",{p_submission_id:target.id,p_primary_estimator:collaboratorId,p_collaborators:[],p_reason:"Phase 9A reassignment access-loss test"});
check(!(await visible(primary)).some(x=>x.id===target.id),"former estimator loses access after reassignment");
check((await visible(collaborator)).some(x=>x.id===target.id),"new primary retains assigned access after reassignment");
const history=await admin.from("inquiry_estimator_assignments").select("id,active,assignment_role,ended_at").eq("submission_id",target.id);if(history.error)throw new Error(history.error.message);
check(history.data.filter(x=>x.active&&x.assignment_role==="primary").length===1,"exactly one active primary remains");
check(history.data.filter(x=>!x.active&&x.ended_at).length>=2,"prior primary and collaborator history is preserved");
const events=await admin.from("inquiry_estimator_events").select("id,event_type").eq("submission_id",target.id);if(events.error)throw new Error(events.error.message);
check(events.data.filter(x=>x.event_type==="assignment_set").length>=2,"assignment and reassignment events are audited");
console.log(JSON.stringify({ok:true,targetReference:target.submission_reference,assignedDocumentCount:adminDocs.data.length,historyRows:history.data.length,assignmentEvents:events.data.length,finalPrimary:"collaborator-test-account"},null,2));
