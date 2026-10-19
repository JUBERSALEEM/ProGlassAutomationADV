import type { NextRequest } from "next/server";

import { markProductionRoomRead } from "@/lib/production-room-api";
import {
  assertSameOrigin,
  bearerToken,
  productionRoomError,
  productionRoomOk,
  readOptionalProductionRoomJson,
} from "../shared";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  try {
    assertSameOrigin(request);
    const body = await readOptionalProductionRoomJson<{ through?: string }>(request);
    return productionRoomOk(await markProductionRoomRead(bearerToken(request), body.through));
  } catch (error) {
    return productionRoomError(error);
  }
}
