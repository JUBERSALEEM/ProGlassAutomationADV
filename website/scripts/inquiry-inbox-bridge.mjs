#!/usr/bin/env node
/** Pull accepted website inquiries into an isolated Windows inbox. Dry-run is
 * default and performs a genuine authenticated remote plan without downloads,
 * acknowledgements, directory creation, or checkpoint mutation. */
import { createHash,randomUUID } from "node:crypto";
import { existsSync,lstatSync } from "node:fs";
import { mkdir,readFile,readdir,rename,rm,writeFile } from "node:fs/promises";
import { basename,dirname,join,resolve } from "node:path";
import { createClient } from "@supabase/supabase-js";
const apply=process.argv.includes("--apply");
const required=["NEXT_PUBLIC_SUPABASE_URL","NEXT_PUBLIC_SUPABASE_ANON_KEY","INQUIRY_INBOX_EMAIL","INQUIRY_INBOX_PASSWORD","INQUIRY_INBOX_ROOT"];
for(const name of required)if(!process.env[name])throw new Error(`${name} is required.`);
const root=resolve(process.env.INQUIRY_INBOX_ROOT);const stageRoot=join(root,".staging");
if(resolve(process.env.PROGLASS_DATA_ROOT||"__not_set__")===root)throw new Error("Inbox must be isolated from the authoritative ProGlass data root.");
if(existsSync(root)&&lstatSync(root).isSymbolicLink())throw new Error("Inbox root must not be a symbolic link.");
const limit=Math.min(100,Math.max(1,Number(process.env.INQUIRY_INBOX_MAX_ITEMS)||25));
const hash=b=>createHash("sha256").update(b).digest("hex");
const canonical=v=>Array.isArray(v)?v.map(canonical):v&&typeof v==="object"?Object.fromEntries(Object.keys(v).sort().map(k=>[k,canonical(v[k])])):v;
const safe=v=>(String(v).normalize("NFKC").replace(/[<>:"/\\|?*\u0000-\u001f]/g,"_").replace(/[. ]+$/g,"").slice(0,100)||"item");
const client=createClient(process.env.NEXT_PUBLIC_SUPABASE_URL,process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY,{auth:{persistSession:false,autoRefreshToken:false}});
const auth=await client.auth.signInWithPassword({email:process.env.INQUIRY_INBOX_EMAIL,password:process.env.INQUIRY_INBOX_PASSWORD});
if(auth.error||!auth.data.user)throw new Error(`Inbox bridge authentication failed: ${auth.error?.message||"no session"}`);
const planned=await client.rpc("plan_customer_inquiry_deliveries_v2",{p_limit:limit});
if(planned.error)throw new Error(`Inbox plan failed: ${planned.error.message}`);
const items=planned.data||[];const plan=[];
for(const submission of items){const docs=await client.rpc("plan_customer_inquiry_delivery_documents",{p_submission_id:submission.submission_id});if(docs.error)throw new Error(`Document plan failed for ${submission.submission_reference}: ${docs.error.message}`);if(Number(submission.document_count)!==(docs.data||[]).length)throw new Error(`Remote document count changed for ${submission.submission_reference}.`);plan.push({submission,documents:docs.data||[]});}
console.log(JSON.stringify({mode:apply?"apply":"remote-plan-dry-run",items:plan.length,documents:plan.reduce((n,x)=>n+x.documents.length,0),deliveries:plan.map(x=>({reference:x.submission.submission_reference,routingKey:x.submission.routing_key,primaryEstimatorCode:x.submission.primary_estimator_code||null,assignmentCount:Array.isArray(x.submission.assignment_snapshot)?x.submission.assignment_snapshot.length:0,documentCount:x.documents.length}))},null,2));
if(!apply)process.exit(0);
await mkdir(stageRoot,{recursive:true,mode:0o700});
for(const entry of await readdir(stageRoot,{withFileTypes:true})){
  if(entry.isDirectory()&&!entry.isSymbolicLink()&&entry.name.endsWith(".partial")){
    await rm(join(stageRoot,entry.name),{recursive:true,force:true});
    console.warn(`[inquiry-inbox] Removed stale interrupted staging folder ${entry.name}.`);
  }
}
for(const item of plan){const id=item.submission.submission_id;const route=String(item.submission.routing_key||"");if(route!=="unassigned"&&!/^assigned\/[A-Z0-9][A-Z0-9_-]{1,29}$/.test(route))throw new Error(`Invalid inbox routing key for ${item.submission.submission_reference}.`);const folder=`${safe(item.submission.submission_reference)}_${id}`;const finalPath=join(root,...route.split("/"),folder);if(!resolve(finalPath).startsWith(`${root}${process.platform==="win32"?"\\":"/"}`))throw new Error("Inbox route escaped the isolated root.");const stage=join(stageRoot,`${id}.${randomUUID()}.partial`);await mkdir(join(stage,"documents"),{recursive:true,mode:0o700});try{const manifest={version:2,routing:{key:route,primaryEstimatorCode:item.submission.primary_estimator_code||null,assignmentSnapshot:item.submission.assignment_snapshot||[]},submission:item.submission,documents:item.documents.map(d=>({id:d.document_id,filename:d.original_filename,mime:d.detected_mime,byteSize:Number(d.byte_size),sha256:d.content_sha256,localFile:`documents/${d.document_id}_${safe(basename(d.original_filename))}`}))};const manifestBytes=Buffer.from(JSON.stringify(canonical(manifest),null,2)+"\n");const manifestHash=hash(manifestBytes);if(existsSync(finalPath)){const current=await readFile(join(finalPath,"manifest.json"));if(hash(current)!==manifestHash)throw new Error(`Existing inbox item conflicts for ${item.submission.submission_reference}.`);for(const document of manifest.documents){const bytes=await readFile(join(finalPath,document.localFile));if(bytes.length!==document.byteSize||hash(bytes)!==document.sha256)throw new Error(`Existing inbox integrity check failed for ${document.filename}.`);}await rm(stage,{recursive:true,force:true});}else{for(const document of item.documents){const object=await client.storage.from(document.storage_bucket).download(document.storage_object_key);if(object.error)throw new Error(`Download failed for ${document.original_filename}: ${object.error.message}`);const bytes=Buffer.from(await object.data.arrayBuffer());if(bytes.length!==Number(document.byte_size)||hash(bytes)!==document.content_sha256)throw new Error(`Integrity check failed for ${document.original_filename}.`);await writeFile(join(stage,"documents",`${document.document_id}_${safe(basename(document.original_filename))}`),bytes,{mode:0o600});}await writeFile(join(stage,"manifest.json"),manifestBytes,{mode:0o600});await writeFile(join(stage,"READY"),`${manifestHash}\n`,{mode:0o600});await mkdir(dirname(finalPath),{recursive:true,mode:0o700});await rename(stage,finalPath);}const ack=await client.rpc("acknowledge_customer_inquiry_delivery_v2",{p_submission_id:id,p_manifest_sha256:manifestHash,p_document_count:item.documents.length,p_routing_key:route,p_assignment_snapshot:item.submission.assignment_snapshot||[]});if(ack.error)throw new Error(`Acknowledgement failed for ${item.submission.submission_reference}: ${ack.error.message}`);await mkdir(dirname(join(root,"checkpoint.json")),{recursive:true});await writeFile(join(root,"checkpoint.json"),JSON.stringify({lastAcknowledgedSubmissionId:id,lastAcknowledgedAt:new Date().toISOString()},null,2),{mode:0o600});console.log(`[inquiry-inbox] Delivered ${item.submission.submission_reference} (${item.documents.length} documents).`);}catch(error){await rm(stage,{recursive:true,force:true});throw error;}}
