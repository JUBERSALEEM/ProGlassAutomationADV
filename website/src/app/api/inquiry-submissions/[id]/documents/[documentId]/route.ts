import { NextResponse, type NextRequest } from "next/server";
import { CustomerInquiryFailure, downloadCustomerInquiryDocument } from "@/lib/inquiry-submissions-api";

export const runtime="nodejs"; export const dynamic="force-dynamic";
function filename(value:string){const safe=value.normalize("NFKC").replace(/[\u0000-\u001f\u007f"\\/]/g,"_").slice(0,180)||"inquiry-document";return {ascii:safe.replace(/[^\x20-\x7e]/g,"_"),encoded:encodeURIComponent(safe).replace(/[!'()*]/g,c=>`%${c.charCodeAt(0).toString(16).toUpperCase()}`)};}
export async function GET(request:NextRequest,context:{params:Promise<{id:string;documentId:string}>}){
 const token=request.headers.get("authorization")?.replace(/^Bearer\s+/i,"").trim()||"";
 try{const {id,documentId}=await context.params;const file=await downloadCustomerInquiryDocument(token,id,documentId);const name=filename(file.filename);return new NextResponse(file.bytes,{headers:{"Content-Type":file.mime,"Content-Length":String(file.bytes.byteLength),"Content-Disposition":`attachment; filename="${name.ascii}"; filename*=UTF-8''${name.encoded}`,"Cache-Control":"private, no-store","X-Content-Type-Options":"nosniff","X-Robots-Tag":"noindex, nofollow","Content-Security-Policy":"sandbox"}});}
 catch(error){const failure=error instanceof CustomerInquiryFailure?error:new CustomerInquiryFailure("TEMPORARY_FAILURE","Unable to download this document.");const status=failure.code==="UNAUTHORIZED"?401:failure.code==="FORBIDDEN"?403:failure.code==="NOT_FOUND"?404:503;return NextResponse.json({ok:false,error:{code:failure.code,message:failure.message}},{status,headers:{"Cache-Control":"private, no-store","X-Robots-Tag":"noindex, nofollow"}});}
}
