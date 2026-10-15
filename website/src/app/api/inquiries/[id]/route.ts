import { NextResponse, type NextRequest } from "next/server";
import { fetchInquiryDetail, InquiryApiFailure } from "@/lib/inquiries-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function errorResponse(error: unknown) {
  const failure = error instanceof InquiryApiFailure ? error : new InquiryApiFailure("INTERNAL_ERROR", "Unable to load this inquiry.");
  if (!(error instanceof InquiryApiFailure)) console.error("[inquiry-detail-route] Unexpected failure:", error);
  const status = failure.code === "UNAUTHORIZED" ? 401 : failure.code === "FORBIDDEN" ? 403 : failure.code === "NOT_FOUND" ? 404 : failure.code === "NOT_CONFIGURED" || failure.code === "TEMPORARY_FAILURE" ? 503 : 500;
  return NextResponse.json({ ok: false, error: { message: failure.message, code: failure.code } }, { status, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } });
}

export async function GET(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
  if (!token) return errorResponse(new InquiryApiFailure("UNAUTHORIZED", "Sign in to access private inquiries."));
  try { return NextResponse.json({ ok: true, data: await fetchInquiryDetail(token, (await context.params).id) }, { headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } }); }
  catch (error) { return errorResponse(error); }
}
