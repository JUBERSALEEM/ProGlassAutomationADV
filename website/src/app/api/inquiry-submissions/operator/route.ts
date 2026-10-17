import { NextResponse,type NextRequest } from "next/server";
import { CustomerInquiryFailure,listOperatorCustomerInquiries } from "@/lib/inquiry-submissions-api";
export const runtime="nodejs"; export const dynamic="force-dynamic";
const token=(r:NextRequest)=>r.headers.get("authorization")?.replace(/^Bearer\s+/i,"").trim()||"";
export async function GET(request:NextRequest){try{return NextResponse.json({ok:true,data:await listOperatorCustomerInquiries(token(request))},{headers:{"Cache-Control":"private, no-store","X-Robots-Tag":"noindex, nofollow"}});}catch(e){const f=e instanceof CustomerInquiryFailure?e:new CustomerInquiryFailure("TEMPORARY_FAILURE","Unable to load website submissions.");return NextResponse.json({ok:false,error:{code:f.code,message:f.message}},{status:f.code==="UNAUTHORIZED"?401:f.code==="FORBIDDEN"?403:503});}}
