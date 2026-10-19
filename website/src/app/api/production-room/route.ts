import type { NextRequest } from "next/server";

import { getProductionRoom, postProductionRoomMessage } from "@/lib/production-room-api";
import {
  assertSameOrigin,
  bearerToken,
  productionRoomError,
  productionRoomOk,
  readProductionRoomJson,
} from "./shared";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  try {
    const params = request.nextUrl.searchParams;
    const pinned = params.get("pinned");
    return productionRoomOk(
      await getProductionRoom(bearerToken(request), {
        category: params.get("category") || undefined,
        query: params.get("q") || undefined,
        from: params.get("from") || undefined,
        to: params.get("to") || undefined,
        pinnedOnly: pinned === "1" || pinned === "true",
      }),
    );
  } catch (error) {
    return productionRoomError(error);
  }
}

export async function POST(request: NextRequest) {
  try {
    assertSameOrigin(request);
    const body = await readProductionRoomJson<{
      body?: string;
      category?: string;
      parentId?: string | null;
    }>(request);
    return productionRoomOk(
      await postProductionRoomMessage(bearerToken(request), {
        body: body.body || "",
        category: body.category,
        parentId: body.parentId ?? null,
      }),
      201,
    );
  } catch (error) {
    return productionRoomError(error);
  }
}
