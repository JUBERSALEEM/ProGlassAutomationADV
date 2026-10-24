import { NextResponse, type NextRequest } from "next/server";
import { CustomerProformaFailure, listCustomerProformas } from "@/lib/customer-proforma-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const HEADERS = { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" };

/** The signed-in customer's own shared proforma invoices. Policies decide what is returned. */
export async function GET(request: NextRequest) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
  try {
    return NextResponse.json({ ok: true, data: { proformas: await listCustomerProformas(token) } }, { headers: HEADERS });
  } catch (error) {
    const failure =
      error instanceof CustomerProformaFailure ? error : new CustomerProformaFailure("INTERNAL_ERROR", "Your documents could not be loaded.");
    if (!(error instanceof CustomerProformaFailure)) console.error("[customer-proformas-route] Unexpected failure:", error);
    const status =
      failure.code === "UNAUTHORIZED" ? 401 : failure.code === "NOT_FOUND" ? 404 : failure.code === "NOT_CONFIGURED" || failure.code === "TEMPORARY_FAILURE" ? 503 : 500;
    return NextResponse.json({ ok: false, error: { message: failure.message, code: failure.code } }, { status, headers: HEADERS });
  }
}
