import { randomUUID } from "node:crypto";
import { NextResponse, type NextRequest } from "next/server";
import { CustomerInquiryFailure, finalizeCustomerInquiry } from "@/lib/inquiry-submissions-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
const bearer = (request: NextRequest) => request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
function errorResponse(error: unknown) {
  const reference = randomUUID().slice(0, 8).toUpperCase();
  const failure = error instanceof CustomerInquiryFailure ? error : new CustomerInquiryFailure("TEMPORARY_FAILURE", "The inquiry could not be finalized.");
  if (!(error instanceof CustomerInquiryFailure)) console.error(`[inquiry-finalize:${reference}]`, error);
  const status = failure.code === "UNAUTHORIZED" ? 401 : failure.code === "FORBIDDEN" ? 403 : failure.code === "INVALID" ? 422 : failure.code === "NOT_FOUND" ? 404 : 503;
  return NextResponse.json({ ok: false, error: { code: failure.code, message: failure.message, reference } }, { status, headers: { "Cache-Control": "private, no-store" } });
}
export async function POST(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const site = request.headers.get("sec-fetch-site");
  if (site && site !== "same-origin" && site !== "none") return errorResponse(new CustomerInquiryFailure("FORBIDDEN", "Inquiry uploads must originate from this website."));
  try {
    const { id } = await context.params;
    const data = await finalizeCustomerInquiry(bearer(request), id);
    return NextResponse.json({ ok: true, data }, { headers: { "Cache-Control": "private, no-store" } });
  } catch (error) { return errorResponse(error); }
}
