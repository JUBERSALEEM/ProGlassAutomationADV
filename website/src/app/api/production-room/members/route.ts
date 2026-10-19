import type { NextRequest } from "next/server";

import { registerProductionRoomMember } from "@/lib/production-room-api";
import {
  assertSameOrigin,
  bearerToken,
  productionRoomError,
  productionRoomOk,
  readProductionRoomJson,
} from "../shared";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  try {
    assertSameOrigin(request);
    const body = await readProductionRoomJson<{
      email?: string;
      displayName?: string;
      department?: string;
      isRoomAdmin?: boolean;
      active?: boolean;
    }>(request);
    return productionRoomOk(
      await registerProductionRoomMember(bearerToken(request), {
        email: body.email || "",
        displayName: body.displayName || "",
        department: body.department || "",
        isRoomAdmin: body.isRoomAdmin === true,
        active: body.active !== false,
      }),
      201,
    );
  } catch (error) {
    return productionRoomError(error);
  }
}
