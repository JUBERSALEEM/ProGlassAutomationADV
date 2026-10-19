import type { NextRequest } from "next/server";

import { setProductionRoomMemberActive } from "@/lib/production-room-api";
import {
  assertSameOrigin,
  bearerToken,
  productionRoomError,
  productionRoomOk,
  readProductionRoomJson,
} from "../../shared";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

type Context = { params: Promise<{ userId: string }> };

export async function PATCH(request: NextRequest, context: Context) {
  try {
    assertSameOrigin(request);
    const { userId } = await context.params;
    const body = await readProductionRoomJson<{ active?: boolean }>(request);
    return productionRoomOk(
      await setProductionRoomMemberActive(
        bearerToken(request),
        userId,
        body.active as boolean,
      ),
    );
  } catch (error) {
    return productionRoomError(error);
  }
}
