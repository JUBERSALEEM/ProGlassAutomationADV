import { NextResponse, type NextRequest } from "next/server";

import { ProductionRoomFailure } from "@/lib/production-room-api";

export function bearerToken(request: NextRequest): string {
  return request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
}

export function assertSameOrigin(request: NextRequest): void {
  const site = request.headers.get("sec-fetch-site");
  if (site && site !== "same-origin" && site !== "none") {
    throw new ProductionRoomFailure("FORBIDDEN", "Updates must originate from this website.");
  }
}

export function productionRoomOk(data: unknown, status = 200): NextResponse {
  return NextResponse.json(
    { ok: true, data },
    { status, headers: { "Cache-Control": "private, no-store" } },
  );
}

export function productionRoomError(error: unknown): NextResponse {
  const failure =
    error instanceof ProductionRoomFailure
      ? error
      : new ProductionRoomFailure("TEMPORARY_FAILURE", "Production room request failed.");
  const status =
    failure.code === "UNAUTHORIZED"
      ? 401
      : failure.code === "FORBIDDEN"
        ? 403
        : failure.code === "NOT_FOUND"
          ? 404
          : failure.code === "INVALID"
            ? 422
            : 503;
  return NextResponse.json(
    { ok: false, error: { code: failure.code, message: failure.message } },
    { status, headers: { "Cache-Control": "private, no-store" } },
  );
}

export async function readProductionRoomJson<T>(request: NextRequest): Promise<T> {
  try {
    return (await request.json()) as T;
  } catch {
    throw new ProductionRoomFailure("INVALID", "Request body must be valid JSON.");
  }
}

export async function readOptionalProductionRoomJson<T>(request: NextRequest): Promise<T> {
  const text = await request.text();
  if (!text.trim()) return {} as T;
  try {
    return JSON.parse(text) as T;
  } catch {
    throw new ProductionRoomFailure("INVALID", "Request body must be valid JSON.");
  }
}
