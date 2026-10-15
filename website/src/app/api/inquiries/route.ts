import { NextResponse, type NextRequest } from "next/server";
import { fetchInquiries, InquiryApiFailure } from "@/lib/inquiries-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

function errorResponse(error: unknown) {
  const failure = error instanceof InquiryApiFailure ? error : new InquiryApiFailure("INTERNAL_ERROR", "Unable to load inquiries.");
  if (!(error instanceof InquiryApiFailure)) console.error("[inquiries-route] Unexpected failure:", error);
  const status = failure.code === "UNAUTHORIZED" ? 401 : failure.code === "FORBIDDEN" ? 403 : failure.code === "NOT_FOUND" ? 404 : failure.code === "NOT_CONFIGURED" || failure.code === "TEMPORARY_FAILURE" ? 503 : 500;
  return NextResponse.json({ ok: false, error: { message: failure.message, code: failure.code } }, { status, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow", ...(status === 401 ? { "WWW-Authenticate": "Bearer" } : {}) } });
}

export async function GET(request: NextRequest) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
  if (!token) return errorResponse(new InquiryApiFailure("UNAUTHORIZED", "Sign in to access private inquiries."));
  const params = request.nextUrl.searchParams;
  try {
    const data = await fetchInquiries(token, {
      page: Math.max(1, Number(params.get("page")) || 1),
      pageSize: Math.min(100, Math.max(10, Number(params.get("pageSize")) || 50)),
      search: params.get("search") || "", status: params.get("status") || "", priority: params.get("priority") || "",
      hasDocuments: params.get("hasDocuments") === "true",
    });
    return NextResponse.json({ ok: true, data }, { headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } });
  } catch (error) { return errorResponse(error); }
}
