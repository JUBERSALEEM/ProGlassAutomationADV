import { NextResponse, type NextRequest } from "next/server";
import { CustomerProformaFailure, fetchCustomerProforma } from "@/lib/customer-proforma-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const HEADERS = { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" };

/** One shared proforma, for the account it was shared with. */
export async function GET(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
  try {
    const { id } = await context.params;
    return NextResponse.json({ ok: true, data: await fetchCustomerProforma(token, id) }, { headers: HEADERS });
  } catch (error) {
    const failure =
      error instanceof CustomerProformaFailure ? error : new CustomerProformaFailure("INTERNAL_ERROR", "That document could not be opened.");
    if (!(error instanceof CustomerProformaFailure)) console.error("[customer-proforma-route] Unexpected failure:", error);
    const status =
      failure.code === "UNAUTHORIZED" ? 401 : failure.code === "NOT_FOUND" ? 404 : failure.code === "NOT_CONFIGURED" || failure.code === "TEMPORARY_FAILURE" ? 503 : 500;
    return NextResponse.json({ ok: false, error: { message: failure.message, code: failure.code } }, { status, headers: HEADERS });
  }
}
