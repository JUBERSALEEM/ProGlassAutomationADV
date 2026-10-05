import { NextResponse, type NextRequest } from "next/server";
import { isSupabaseConfigured, supabaseServerWithToken } from "@/lib/supabase";
import { isProductionStageCode, PRODUCTION_STAGE_DEFINITIONS } from "@/lib/job-order-production";
import type { PlannedProductionStage } from "@/lib/job-order-production";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const jsonError = (message: string, status: number, code: string) =>
  NextResponse.json(
    { ok: false, error: { message, code } },
    { status, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } },
  );

function plannedStages(value: unknown): PlannedProductionStage[] | null {
  if (!Array.isArray(value) || value.length < 1 || value.length > PRODUCTION_STAGE_DEFINITIONS.length) return null;
  const seen = new Set<string>();
  const output: PlannedProductionStage[] = [];
  for (const item of value) {
    if (!item || typeof item !== "object") return null;
    const stage = item as Partial<PlannedProductionStage>;
    if (!stage.stage_code || !isProductionStageCode(stage.stage_code) || seen.has(stage.stage_code)) return null;
    if (typeof stage.planned_qty !== "number" || !Number.isFinite(stage.planned_qty) || stage.planned_qty <= 0 || stage.planned_qty > 10_000_000) return null;
    const definition = PRODUCTION_STAGE_DEFINITIONS.find((candidate) => candidate.code === stage.stage_code)!;
    if (stage.quantity_unit && stage.quantity_unit !== definition.quantityUnit) return null;
    if (definition.quantityUnit !== "linear metres" && !Number.isInteger(stage.planned_qty)) return null;
    const plannedQty = Math.round(stage.planned_qty * 1000) / 1000;
    if (plannedQty <= 0) return null;
    seen.add(stage.stage_code);
    output.push({
      stage_code: definition.code,
      stage_label: definition.label,
      stage_order: definition.order,
      planned_qty: plannedQty,
      quantity_unit: definition.quantityUnit,
    });
  }
  if (!seen.has("cutting")) return null;
  return output.sort((a, b) => a.stage_order - b.stage_order);
}

export async function POST(request: NextRequest) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim();
  if (!token) return jsonError("Sign in as an authorized operator before releasing a Job Order.", 401, "UNAUTHORIZED");
  if (!isSupabaseConfigured()) return jsonError("Supabase is not configured for production release tracking.", 503, "SUPABASE_NOT_CONFIGURED");

  let body: Record<string, unknown>;
  try {
    body = await request.json() as Record<string, unknown>;
  } catch {
    return jsonError("Request body must be valid JSON.", 400, "INVALID_BODY");
  }

  const piNumber = typeof body.pi_number === "string" ? body.pi_number.trim().slice(0, 160) : "";
  const sourceFile = body.source_file === null || body.source_file === undefined
    ? null
    : typeof body.source_file === "string" ? body.source_file.trim().slice(0, 500) : null;
  const recordIds = Array.isArray(body.record_ids)
    ? body.record_ids.map((id) => String(id).trim()).filter(Boolean)
    : [];
  const stages = plannedStages(body.operations);

  if (!piNumber || recordIds.length === 0 || recordIds.length > 25_000 || !stages || body.route_confirmed !== true) {
    return jsonError("PI source lines and a reviewed route with Cutting are required before release.", 400, "INVALID_RELEASE");
  }

  const { data, error } = await supabaseServerWithToken(token).rpc("release_confirmed_pi_to_production", {
    p_pi_number: piNumber,
    p_source_file: sourceFile,
    p_expected_record_ids: recordIds,
    p_operations: stages,
    p_route_confirmed: true,
  });

  if (error) {
    if (error.code === "PGRST202" || /release_confirmed_pi_to_production.*schema cache/i.test(error.message)) {
      return jsonError("Production release schema is not installed. Run website/supabase-job-orders.sql after website/supabase-dashboard.sql.", 503, "PRODUCTION_SCHEMA_NOT_INSTALLED");
    }
    if (/jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(`${error.code ?? ""} ${error.message}`)) {
      return jsonError("Your operator session is expired or not authorized to release production work. Sign in again or check the authenticated Supabase role.", 401, "UNAUTHORIZED");
    }
    if (error.code === "40001") return jsonError(error.message, 409, "SOURCE_CHANGED");
    if (error.code === "P0002") return jsonError(error.message, 409, "PI_NOT_CONFIRMED");
    if (error.code === "22023") return jsonError(error.message, 400, "INVALID_RELEASE");

    console.error("[job-order-release] Release failed:", error);
    return jsonError("Unable to release this PI. Refresh the queue and try again.", 500, "RELEASE_FAILED");
  }

  return NextResponse.json(
    { ok: true, data },
    { status: 200, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } },
  );
}
