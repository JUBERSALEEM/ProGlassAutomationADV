import { NextResponse, type NextRequest } from "next/server";
import { CustomerInquiryFailure, getCustomerInquiry } from "@/lib/inquiry-submissions-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
  try {
    const { id } = await context.params;
    return NextResponse.json({ ok: true, data: await getCustomerInquiry(token, id) }, { headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } });
  } catch (error) {
    const failure = error instanceof CustomerInquiryFailure ? error : new CustomerInquiryFailure("TEMPORARY_FAILURE", "Unable to load this inquiry submission.");
    const status = failure.code === "UNAUTHORIZED" ? 401 : failure.code === "FORBIDDEN" ? 403 : failure.code === "NOT_FOUND" ? 404 : failure.code === "INVALID" ? 422 : 503;
    return NextResponse.json({ ok: false, error: { code: failure.code, message: failure.message } }, { status, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } });
  }
}
