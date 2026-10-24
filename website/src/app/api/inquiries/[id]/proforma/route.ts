import { NextResponse, type NextRequest } from "next/server";
import { fetchProformaWorkspace, ProformaApiFailure, saveProforma } from "@/lib/proforma-api";
import type { ProformaLineInput } from "@/types/proforma";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const HEADERS = { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" };

function errorResponse(error: unknown) {
  const failure = error instanceof ProformaApiFailure ? error : new ProformaApiFailure("TEMPORARY_FAILURE", "The proforma service could not complete this request.");
  if (!(error instanceof ProformaApiFailure)) console.error("[proforma-route] Unexpected failure:", error);
  const status =
    failure.code === "UNAUTHORIZED" ? 401
    : failure.code === "FORBIDDEN" ? 403
    : failure.code === "NOT_FOUND" ? 404
    : failure.code === "CONFLICT" ? 409
    : failure.code === "INVALID" ? 400
    : 503;
  return NextResponse.json({ ok: false, error: { message: failure.message, code: failure.code } }, { status, headers: HEADERS });
}

function tokenFrom(request: NextRequest): string {
  return request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
}

export async function GET(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const token = tokenFrom(request);
  if (!token) return errorResponse(new ProformaApiFailure("UNAUTHORIZED", "Sign in to open a proforma invoice."));
  try {
    const { id } = await context.params;
    return NextResponse.json({ ok: true, data: await fetchProformaWorkspace(token, id) }, { headers: HEADERS });
  } catch (error) {
    return errorResponse(error);
  }
}

export async function POST(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const token = tokenFrom(request);
  if (!token) return errorResponse(new ProformaApiFailure("UNAUTHORIZED", "Sign in to save a proforma invoice."));
  const idempotencyKey = request.headers.get("idempotency-key")?.trim() || "";
  if (!idempotencyKey) return errorResponse(new ProformaApiFailure("INVALID", "A save needs an idempotency key."));

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return errorResponse(new ProformaApiFailure("INVALID", "The request body is not valid JSON."));
  }
  const payload = (body || {}) as {
    expected_revision?: unknown;
    currency?: unknown;
    lines?: unknown;
    groups?: unknown;
  };
  const lines = Array.isArray(payload.lines) ? (payload.lines as ProformaLineInput[]) : [];
  const groups = Array.isArray(payload.groups)
    ? (payload.groups as Array<Record<string, unknown>>)
        .map((group) => ({
          group_no: Number(group.group_no),
          base_rate: Number(group.base_rate),
          surcharge_percent: Number(group.surcharge_percent),
        }))
        .filter(
          (group) =>
            Number.isInteger(group.group_no) &&
            group.group_no >= 1 &&
            group.group_no <= 50 &&
            Number.isFinite(group.base_rate) &&
            group.base_rate >= 0 &&
            group.base_rate <= 1_000_000 &&
            Number.isFinite(group.surcharge_percent) &&
            group.surcharge_percent >= 0 &&
            group.surcharge_percent <= 1000,
        )
    : [];

  try {
    const { id } = await context.params;
    const data = await saveProforma(token, id, {
      idempotency_key: idempotencyKey,
      expected_revision: Number(payload.expected_revision),
      currency: String(payload.currency || "AED"),
      lines,
      groups,
    });
    return NextResponse.json({ ok: true, data }, { headers: HEADERS });
  } catch (error) {
    return errorResponse(error);
  }
}
