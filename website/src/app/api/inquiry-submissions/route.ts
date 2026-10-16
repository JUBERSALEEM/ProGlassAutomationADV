import { NextResponse, type NextRequest } from "next/server";
import { createCustomerInquiry, CUSTOMER_INQUIRY_MAX_REQUEST_BYTES, CustomerInquiryFailure, listCustomerInquiries } from "@/lib/inquiry-submissions-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function bearer(request: NextRequest) { return request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || ""; }
function responseError(error: unknown) {
  const failure = error instanceof CustomerInquiryFailure ? error : new CustomerInquiryFailure("TEMPORARY_FAILURE", "The inquiry request could not be completed.");
  if (!(error instanceof CustomerInquiryFailure)) console.error("[inquiry-submissions-route] Unexpected failure:", error);
  const status = failure.code === "UNAUTHORIZED" ? 401 : failure.code === "FORBIDDEN" ? 403 : failure.code === "INVALID" ? 422 : failure.code === "NOT_FOUND" ? 404 : 503;
  return NextResponse.json({ ok: false, error: { code: failure.code, message: failure.message } }, { status, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow", ...(status === 401 ? { "WWW-Authenticate": "Bearer" } : {}) } });
}
function crossSite(request: NextRequest) {
  const site = request.headers.get("sec-fetch-site");
  if (site) return site !== "same-origin" && site !== "none";
  const origin = request.headers.get("origin");
  if (!origin) return false;
  const forwardedHost = request.headers.get("x-forwarded-host")?.split(",", 1)[0]?.trim();
  const requestHost = forwardedHost || request.headers.get("host")?.trim();
  if (!requestHost) return true;
  try { return new URL(origin).host !== requestHost; } catch { return true; }
}

export async function GET(request: NextRequest) {
  try { return NextResponse.json({ ok: true, data: await listCustomerInquiries(bearer(request)) }, { headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } }); }
  catch (error) { return responseError(error); }
}

export async function POST(request: NextRequest) {
  if (crossSite(request)) return responseError(new CustomerInquiryFailure("FORBIDDEN", "Inquiry submissions must originate from this website."));
  const length = Number(request.headers.get("content-length") || 0);
  if (length > CUSTOMER_INQUIRY_MAX_REQUEST_BYTES) return NextResponse.json({ ok: false, error: { code: "INVALID", message: "The submission exceeds the upload limit." } }, { status: 413 });
  if (!request.headers.get("content-type")?.toLowerCase().startsWith("multipart/form-data")) return responseError(new CustomerInquiryFailure("INVALID", "Use multipart form data for inquiry submissions."));
  try {
    const form = await request.formData();
    const data = await createCustomerInquiry(bearer(request), form);
    return NextResponse.json({ ok: true, data }, { status: 201, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } });
  } catch (error) { return responseError(error); }
}
