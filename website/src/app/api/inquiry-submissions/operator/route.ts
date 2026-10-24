import { NextResponse,type NextRequest } from "next/server";
import { CustomerInquiryFailure,listOperatorCustomerInquiries } from "@/lib/inquiry-submissions-api";
export const runtime="nodejs"; export const dynamic="force-dynamic";
const token=(r:NextRequest)=>r.headers.get("authorization")?.replace(/^Bearer\s+/i,"").trim()||"";
export async function GET(request:NextRequest){
  try{
    const params=request.nextUrl.searchParams;
    // Status, search and paging are answered by the database under the caller's
    // own token. The browser no longer receives two hundred rows and narrows
    // them itself, so the counts and the page it shows come from the same query.
    const data=await listOperatorCustomerInquiries(token(request),{
      status:params.get("status"),
      q:params.get("q"),
      page:params.get("page"),
      pageSize:params.get("pageSize"),
    });
    return NextResponse.json({ok:true,data},{headers:{"Cache-Control":"private, no-store","X-Robots-Tag":"noindex, nofollow"}});
  }catch(e){
    const f=e instanceof CustomerInquiryFailure?e:new CustomerInquiryFailure("TEMPORARY_FAILURE","Unable to load website submissions.");
    return NextResponse.json({ok:false,error:{code:f.code,message:f.message}},{status:f.code==="UNAUTHORIZED"?401:f.code==="FORBIDDEN"?403:503,headers:{"Cache-Control":"private, no-store","X-Robots-Tag":"noindex, nofollow"}});
  }
}
