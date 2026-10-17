import { randomUUID } from "node:crypto";
import { NextResponse, type NextRequest } from "next/server";
import { createCustomerInquiryDraft, CustomerInquiryFailure } from "@/lib/inquiry-submissions-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
const bearer = (request: NextRequest) => request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
function errorResponse(error: unknown) {
  const reference = randomUUID().slice(0, 8).toUpperCase();
  const failure = error instanceof CustomerInquiryFailure ? error : new CustomerInquiryFailure("TEMPORARY_FAILURE", "The inquiry draft could not be created.");
  if (!(error instanceof CustomerInquiryFailure)) console.error(`[inquiry-draft:${reference}]`, error);
  const status = failure.code === "UNAUTHORIZED" ? 401 : failure.code === "FORBIDDEN" ? 403 : failure.code === "INVALID" ? 422 : failure.code === "NOT_FOUND" ? 404 : 503;
  return NextResponse.json({ ok: false, error: { code: failure.code, message: failure.message, reference } }, { status, headers: { "Cache-Control": "private, no-store" } });
}
export async function POST(request: NextRequest) {
  const site = request.headers.get("sec-fetch-site");
  if (site && site !== "same-origin" && site !== "none") return errorResponse(new CustomerInquiryFailure("FORBIDDEN", "Inquiry uploads must originate from this website."));
  try {
    if (!request.headers.get("content-type")?.toLowerCase().startsWith("multipart/form-data")) throw new CustomerInquiryFailure("INVALID", "Use multipart form data.");
    const data = await createCustomerInquiryDraft(bearer(request), await request.formData());
    return NextResponse.json({ ok: true, data }, { status: 201, headers: { "Cache-Control": "private, no-store" } });
  } catch (error) { return errorResponse(error); }
}
