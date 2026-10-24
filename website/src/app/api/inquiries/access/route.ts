import { NextResponse, type NextRequest } from "next/server";
import { InquiryApiFailure, readOperatorAccess } from "@/lib/inquiries-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const HEADERS = { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" };

/**
 * The signed-in operator's role, so a refused page can say which role is
 * missing instead of leaving the reader to guess. It grants nothing: every
 * route and every database write function checks the role itself.
 */
export async function GET(request: NextRequest) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
  if (!token) {
    return NextResponse.json(
      { ok: false, error: { message: "Sign in to see your access.", code: "UNAUTHORIZED" } },
      { status: 401, headers: { ...HEADERS, "WWW-Authenticate": "Bearer" } },
    );
  }
  try {
    return NextResponse.json({ ok: true, data: await readOperatorAccess(token) }, { headers: HEADERS });
  } catch (error) {
    const failure = error instanceof InquiryApiFailure ? error : new InquiryApiFailure("INTERNAL_ERROR", "Unable to read your access.");
    if (!(error instanceof InquiryApiFailure)) console.error("[inquiry-access-route] Unexpected failure:", error);
    const status =
      failure.code === "UNAUTHORIZED" ? 401
      : failure.code === "FORBIDDEN" ? 403
      : failure.code === "NOT_CONFIGURED" || failure.code === "TEMPORARY_FAILURE" ? 503
      : 500;
    return NextResponse.json({ ok: false, error: { message: failure.message, code: failure.code } }, { status, headers: HEADERS });
  }
}
