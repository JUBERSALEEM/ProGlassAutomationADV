import { NextResponse, type NextRequest } from "next/server";
import { ProformaApiFailure, readProformaShare, revokeProformaShare, shareProforma } from "@/lib/proforma-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const HEADERS = { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" };

function errorResponse(error: unknown) {
  if (!(error instanceof ProformaApiFailure)) {
    console.error("[proforma-share-route] Unexpected failure:", error);
    return NextResponse.json(
      { ok: false, error: { message: "The share state could not be read.", code: "INTERNAL_ERROR" } },
      { status: 500, headers: HEADERS },
    );
  }
  const failure = error;
  const status =
    failure.code === "UNAUTHORIZED" ? 401
    : failure.code === "FORBIDDEN" ? 403
    : failure.code === "NOT_FOUND" ? 404
    : failure.code === "NOT_CONFIGURED" || failure.code === "TEMPORARY_FAILURE" ? 503
    : failure.code === "INVALID" ? 400
    : 500;
  return NextResponse.json({ ok: false, error: { message: failure.message, code: failure.code } }, { status, headers: HEADERS });
}

function tokenFrom(request: NextRequest): string {
  return request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
}

export async function GET(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const token = tokenFrom(request);
  if (!token) return errorResponse(new ProformaApiFailure("UNAUTHORIZED", "Sign in to see who holds this proforma."));
  try {
    const { id } = await context.params;
    return NextResponse.json({ ok: true, data: await readProformaShare(token, id) }, { headers: HEADERS });
  } catch (error) {
    return errorResponse(error);
  }
}

export async function POST(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const token = tokenFrom(request);
  if (!token) return errorResponse(new ProformaApiFailure("UNAUTHORIZED", "Sign in to share a proforma."));
  try {
    const { id } = await context.params;
    const revision = await shareProforma(token, id);
    return NextResponse.json({ ok: true, data: { revision, state: await readProformaShare(token, id) } }, { headers: HEADERS });
  } catch (error) {
    return errorResponse(error);
  }
}

export async function DELETE(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const token = tokenFrom(request);
  if (!token) return errorResponse(new ProformaApiFailure("UNAUTHORIZED", "Sign in to withdraw a shared proforma."));
  try {
    const { id } = await context.params;
    await revokeProformaShare(token, id);
    return NextResponse.json({ ok: true, data: { state: await readProformaShare(token, id) } }, { headers: HEADERS });
  } catch (error) {
    return errorResponse(error);
  }
}
