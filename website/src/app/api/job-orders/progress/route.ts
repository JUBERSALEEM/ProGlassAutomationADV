import { NextResponse, type NextRequest } from "next/server";
import { isSupabaseConfigured, supabaseServerWithToken } from "@/lib/supabase";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function jsonError(message: string, status: number, code: string) {
  return NextResponse.json(
    { ok: false, error: { message, code } },
    { status, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } },
  );
}

export async function PATCH(request: NextRequest) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim();
  if (!token) return jsonError("Sign in as an authorized operator before updating production progress.", 401, "UNAUTHORIZED");
  if (!isSupabaseConfigured()) return jsonError("Supabase is not configured for production tracking.", 503, "SUPABASE_NOT_CONFIGURED");

  let body: Record<string, unknown>;
  try {
    body = await request.json() as Record<string, unknown>;
  } catch {
    return jsonError("Request body must be valid JSON.", 400, "INVALID_BODY");
  }

  const jobOrderId = typeof body.job_order_id === "string" ? body.job_order_id : "";
  const operationId = typeof body.operation_id === "string" ? body.operation_id : "";
  const completedQty = typeof body.completed_qty === "number" ? body.completed_qty : Number.NaN;
  if (!UUID.test(jobOrderId) || !UUID.test(operationId) || !Number.isFinite(completedQty) || completedQty < 0 || completedQty > 10_000_000) {
    return jsonError("A valid Job Order, production stage and non-negative completed quantity are required.", 400, "INVALID_PROGRESS");
  }

  const { data, error } = await supabaseServerWithToken(token).rpc("update_production_job_order_progress", {
    p_job_order_id: jobOrderId,
    p_operation_id: operationId,
    p_completed_qty: Math.round(completedQty * 1000) / 1000,
  });

  if (error) {
    if (error.code === "PGRST202" || /update_production_job_order_progress.*schema cache/i.test(error.message)) {
      return jsonError("Production progress schema is not installed. Run website/supabase-job-orders.sql after website/supabase-dashboard.sql.", 503, "PRODUCTION_SCHEMA_NOT_INSTALLED");
    }
    if (/jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(`${error.code ?? ""} ${error.message}`)) {
      return jsonError("Your operator session is expired or not authorized to update production. Sign in again or check the authenticated Supabase role.", 401, "UNAUTHORIZED");
    }
    if (error.code === "P0002") return jsonError(error.message, 404, "NOT_FOUND");
    if (error.code === "22023") return jsonError(error.message, 400, "INVALID_PROGRESS");

    console.error("[job-order-progress] Update failed:", error);
    return jsonError("Unable to save production progress. Refresh the Job Order and try again.", 500, "PROGRESS_SAVE_FAILED");
  }

  return NextResponse.json(
    { ok: true, data },
    { status: 200, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } },
  );
}
